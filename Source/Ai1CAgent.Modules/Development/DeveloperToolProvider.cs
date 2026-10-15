using System.Diagnostics;
using System.Text;
using Ai1CAgent.Modules.Configuration;
using Ai1CAgent.Modules.Context;
using Ai1CAgent.Modules.Development.Interfaces;
using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Development;

public sealed class DeveloperToolProvider(ILogger<DeveloperToolProvider> logger) : IDeveloperToolProvider
{
    private const int MaxOutputLength = 20000;
    private const int CommandTimeoutSeconds = 60;
    private const string GitHint = "Операции выполняются в папке проекта (Git-репозиторий).";

    private const string NoArgs = """{ "type": "object", "properties": {} }""";
    private const string LimitSchema = """
        { "type": "object", "properties": { "limit": { "type": "integer", "description": "Сколько записей показать (по умолчанию 20)." } } }
        """;
    private const string PathSchema = """
        { "type": "object", "properties": { "path": { "type": "string", "description": "Путь относительно корня проекта (необязательно)." } } }
        """;
    private const string RefSchema = """
        {
          "type": "object",
          "properties": { "ref": { "type": "string", "description": "Ветка, тег или хеш коммита." } },
          "required": ["ref"]
        }
        """;
    private const string CommitSchema = """
        {
          "type": "object",
          "properties": { "commit": { "type": "string", "description": "Хеш коммита." } },
          "required": ["commit"]
        }
        """;
    private const string MessageSchema = """
        {
          "type": "object",
          "properties": { "message": { "type": "string", "description": "Сообщение коммита или stash." } },
          "required": ["message"]
        }
        """;
    private const string RemoteSchema = """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "Имя remote, например origin." },
            "url": { "type": "string", "description": "URL репозитория." }
          },
          "required": ["name", "url"]
        }
        """;
    private const string CommandSchema = """
        {
          "type": "object",
          "properties": { "command": { "type": "string", "description": "Команда оболочки для выполнения в папке проекта." } },
          "required": ["command"]
        }
        """;
    private const string WriteSchema = """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "Путь к файлу относительно корня проекта." },
            "content": { "type": "string", "description": "Полное содержимое файла." }
          },
          "required": ["path", "content"]
        }
        """;
    private const string TouchSchema = """
        {
          "type": "object",
          "properties": { "path": { "type": "string", "description": "Путь к файлу относительно корня проекта." } },
          "required": ["path"]
        }
        """;

    public IReadOnlyList<AITool> GetTools(string rootPath, bool allowWrite, bool allowTerminal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var tools = new List<AITool>
        {
            Read("GitStatus", "Показать ветку и изменённые файлы Git-репозитория проекта.", NoArgs, rootPath),
            Read("GitLog", "Показать последние коммиты (хеш, дата, автор, сообщение).", LimitSchema, rootPath),
            Read("GitDiff", "Показать незакоммиченные изменения (diff рабочей директории против HEAD).", PathSchema, rootPath),
            Read("GitBranches", "Показать ветки Git-репозитория.", NoArgs, rootPath),
            Read("GitShow", "Показать подробности коммита, включая его изменения.", CommitSchema, rootPath),
            Read("GitBlame", "Показать, кто и каким коммитом менял строки файла.", PathSchema, rootPath),
            Read("GitRemotes", "Показать настроенные удалённые репозитории (remote).", NoArgs, rootPath),
            Read("GitTags", "Показать теги репозитория.", NoArgs, rootPath),
            Read("GitStashList", "Показать сохранённые stash-записи.", NoArgs, rootPath),
            Read("CSharpOutline", "Показать структуру файла C# (пространства имён, типы, методы, свойства).", PathSchema, rootPath),
            new ContextFunction("CanWrite", "Проверить, разрешена ли запись файлов (режим записи в настройках).", NoArgs,
                (_, _) => ValueTask.FromResult(allowWrite ? "Запись разрешена." : "Запись запрещена.")),
        };

        if (allowTerminal)
        {
            tools.Add(new ContextFunction("RunCommand", "Выполнить команду оболочки в папке проекта и вернуть вывод.", CommandSchema,
                (arguments, cancellationToken) => Run(arguments, rootPath, cancellationToken)));
        }

        if (!allowWrite)
            return tools;

        tools.AddRange(
        [
            Write("GitInit", "Инициализировать Git-репозиторий в папке проекта.", NoArgs, rootPath),
            Write("GitAdd", "Подготовить файл (или все изменения) к коммиту.", PathSchema, rootPath),
            Write("GitCommit", "Создать коммит с сообщением.", MessageSchema, rootPath),
            Write("GitCheckout", "Переключиться на ветку/тег/коммит.", RefSchema, rootPath),
            Write("GitCreateBranch", "Создать ветку.", RefSchema, rootPath),
            Write("GitDeleteBranch", "Удалить ветку.", RefSchema, rootPath),
            Write("GitMerge", "Слить указанную ветку в текущую.", RefSchema, rootPath),
            Write("GitRebase", "Перебазировать текущую ветку на указанную.", RefSchema, rootPath),
            Write("GitCherryPick", "Применить изменения указанного коммита (cherry-pick).", CommitSchema, rootPath),
            Write("GitRevert", "Отменить указанный коммит (revert).", CommitSchema, rootPath),
            Write("GitReset", "Сбросить индекс (mixed) или снять файл из индекса.", PathSchema, rootPath),
            Write("GitPull", "Забрать и слить изменения из upstream.", NoArgs, rootPath),
            Write("GitPush", "Отправить текущую ветку в origin.", NoArgs, rootPath),
            Write("GitFetch", "Получить изменения из origin без слияния.", NoArgs, rootPath),
            Write("GitStash", "Сохранить незакоммиченные изменения в stash.", MessageSchema, rootPath),
            Write("GitStashPop", "Применить последний stash и удалить его.", NoArgs, rootPath),
            Write("GitCreateTag", "Создать тег на текущем коммите.", RefSchema, rootPath),
            Write("GitAddRemote", "Добавить удалённый репозиторий (remote).", RemoteSchema, rootPath),
            new ContextFunction("Write", "Записать файл в папке проекта: создать или полностью перезаписать.", WriteSchema,
                (arguments, _) => ValueTask.FromResult(WriteFile(rootPath, arguments))),
            new ContextFunction("Touch", "Создать пустой файл в папке проекта.", TouchSchema,
                (arguments, _) => ValueTask.FromResult(TouchFile(rootPath, arguments))),
        ]);

        return tools;
    }

    private ContextFunction Read(string name, string description, string schema, string rootPath) =>
        new(name, description + " " + GitHint, schema,
            (arguments, _) => ValueTask.FromResult(Logged(() => GitInvoke(name, rootPath, arguments), name)));

    private ContextFunction Write(string name, string description, string schema, string rootPath) =>
        new(name, description + " " + GitHint, schema,
            (arguments, _) => ValueTask.FromResult(Logged(() => GitInvoke(name, rootPath, arguments), name)));

    private string Logged(Func<string> action, string operation)
    {
        logger.LogInformation("Модель вызвала {Operation}", operation);
        return action();
    }

    private static string GitInvoke(string name, string rootPath, AIFunctionArguments arguments) => name switch
    {
        "GitStatus" => Status(rootPath),
        "GitLog" => Log(rootPath, ConfigurationFunction.GetInt(arguments, "limit") ?? 20),
        "GitDiff" => Diff(rootPath, ConfigurationFunction.GetString(arguments, "path")),
        "GitBranches" => Branches(rootPath),
        "GitShow" => Show(rootPath, ConfigurationFunction.GetString(arguments, "commit")),
        "GitBlame" => Blame(rootPath, ConfigurationFunction.GetString(arguments, "path")),
        "GitRemotes" => Remotes(rootPath),
        "GitTags" => Tags(rootPath),
        "GitStashList" => StashList(rootPath),
        "CSharpOutline" => CSharpOutline(rootPath, ConfigurationFunction.GetString(arguments, "path")),
        "GitInit" => Init(rootPath),
        "GitAdd" => Add(rootPath, ConfigurationFunction.GetString(arguments, "path")),
        "GitCommit" => Commit(rootPath, ConfigurationFunction.GetString(arguments, "message")),
        "GitCheckout" => Checkout(rootPath, ConfigurationFunction.GetString(arguments, "ref")),
        "GitCreateBranch" => CreateBranch(rootPath, ConfigurationFunction.GetString(arguments, "ref")),
        "GitDeleteBranch" => DeleteBranch(rootPath, ConfigurationFunction.GetString(arguments, "ref")),
        "GitMerge" => Merge(rootPath, ConfigurationFunction.GetString(arguments, "ref")),
        "GitRebase" => Rebase(rootPath, ConfigurationFunction.GetString(arguments, "ref")),
        "GitCherryPick" => CherryPick(rootPath, ConfigurationFunction.GetString(arguments, "commit"), revert: false),
        "GitRevert" => CherryPick(rootPath, ConfigurationFunction.GetString(arguments, "commit"), revert: true),
        "GitReset" => Reset(rootPath, ConfigurationFunction.GetString(arguments, "path")),
        "GitPull" => Pull(rootPath),
        "GitPush" => Push(rootPath),
        "GitFetch" => Fetch(rootPath),
        "GitStash" => Stash(rootPath, ConfigurationFunction.GetString(arguments, "message")),
        "GitStashPop" => StashPop(rootPath),
        "GitCreateTag" => CreateTag(rootPath, ConfigurationFunction.GetString(arguments, "ref")),
        "GitAddRemote" => AddRemote(rootPath, ConfigurationFunction.GetString(arguments, "name"), ConfigurationFunction.GetString(arguments, "url")),
        _ => throw new InvalidOperationException($"Неизвестная Git-функция: {name}"),
    };

    private async ValueTask<string> Run(AIFunctionArguments arguments, string rootPath, CancellationToken cancellationToken)
    {
        var command = ConfigurationFunction.GetString(arguments, "command");

        if (string.IsNullOrWhiteSpace(command))
            return "Ошибка: не задана команда.";

        logger.LogInformation("Модель выполняет команду в терминале");

        return await ProcessRunner.RunAsync(rootPath, command, CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
    }

    // ----- Git read -----

    private static Repository Open(string rootPath) => new(ResolveRepo(rootPath));

    private static string ResolveRepo(string rootPath) =>
        Repository.Discover(rootPath) ?? throw new InvalidOperationException("Папка проекта не является Git-репозиторием.");

    private static Signature Signature(Repository repository) =>
        repository.Config.BuildSignature(DateTimeOffset.Now) ?? new Signature("Ai1CAgent", "ai1cagent@local", DateTimeOffset.Now);

    private static Commit ResolveCommit(Repository repository, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Не задан коммит.");

        var direct = repository.Lookup<Commit>(reference);

        if (direct is not null)
            return direct;

        return repository.Commits.FirstOrDefault(commit => commit.Sha.StartsWith(reference, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Коммит «{reference}» не найден.");
    }

    private static string Status(string rootPath)
    {
        using var repository = Open(rootPath);

        var builder = new StringBuilder();
        builder.Append("Ветка: ").AppendLine(repository.Head.FriendlyName);

        var changed = false;

        foreach (var entry in repository.RetrieveStatus(new StatusOptions()))
        {
            changed = true;
            builder.Append(entry.State).Append(": ").AppendLine(entry.FilePath);
        }

        if (!changed)
            builder.AppendLine("Изменений нет.");

        return Truncate(builder.ToString());
    }

    private static string Log(string rootPath, int limit)
    {
        using var repository = Open(rootPath);

        var builder = new StringBuilder();
        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };

        foreach (var commit in repository.Commits.QueryBy(filter).Take(Math.Clamp(limit, 1, 500)))
        {
            builder.Append(commit.Sha[..7]).Append(' ')
                .Append(commit.Author.When.ToString("yyyy-MM-dd HH:mm")).Append(' ')
                .Append(commit.Author.Name).Append(": ")
                .AppendLine(commit.MessageShort);
        }

        return builder.Length == 0 ? "Коммитов нет." : Truncate(builder.ToString());
    }

    private static string Diff(string rootPath, string? relativePath)
    {
        using var repository = Open(rootPath);

        var patch = repository.Diff.Compare<Patch>(repository.Head.Tip?.Tree, DiffTargets.WorkingDirectory);

        if (string.IsNullOrWhiteSpace(relativePath))
            return patch.Content.Length == 0 ? "Изменений нет." : Truncate(patch.Content);

        var builder = new StringBuilder();

        foreach (var change in patch)
        {
            if (change.Path.Contains(relativePath, StringComparison.OrdinalIgnoreCase))
                builder.Append(change.Patch);
        }

        return builder.Length == 0 ? "Изменений по указанному пути нет." : Truncate(builder.ToString());
    }

    private static string Branches(string rootPath)
    {
        using var repository = Open(rootPath);

        var builder = new StringBuilder();

        foreach (var branch in repository.Branches.OrderBy(b => b.FriendlyName, StringComparer.OrdinalIgnoreCase))
            builder.Append(branch.IsCurrentRepositoryHead ? "* " : "  ").AppendLine(branch.FriendlyName);

        return builder.ToString();
    }

    private static string Show(string rootPath, string? reference)
    {
        using var repository = Open(rootPath);

        var commit = string.IsNullOrWhiteSpace(reference) ? repository.Head.Tip : ResolveCommit(repository, reference);

        if (commit is null)
            return "Коммит не найден.";

        var builder = new StringBuilder();
        builder.Append(commit.Sha).Append(' ').Append(commit.Author.Name).Append(' ')
            .AppendLine(commit.Author.When.ToString("yyyy-MM-dd HH:mm"));
        builder.AppendLine(commit.Message);
        builder.AppendLine(repository.Diff.Compare<Patch>(commit.Parents.FirstOrDefault()?.Tree, commit.Tree).Content);

        return Truncate(builder.ToString());
    }

    private static string Blame(string rootPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Не задан путь к файлу.");

        using var repository = Open(rootPath);

        var builder = new StringBuilder();

        foreach (var hunk in repository.Blame(relativePath))
            builder.Append(hunk.FinalCommit.Sha[..7]).Append(' ')
                .Append(hunk.FinalSignature?.Name ?? "?").Append(" строка ")
                .AppendLine(hunk.FinalStartLineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return Truncate(builder.ToString());
    }

    private static string Remotes(string rootPath)
    {
        using var repository = Open(rootPath);

        var builder = new StringBuilder();

        foreach (var remote in repository.Network.Remotes)
            builder.Append(remote.Name).Append("  ").AppendLine(remote.Url);

        return builder.Length == 0 ? "Remote нет." : builder.ToString();
    }

    private static string Tags(string rootPath)
    {
        using var repository = Open(rootPath);

        var builder = new StringBuilder();

        foreach (var tag in repository.Tags)
            builder.Append(tag.FriendlyName).Append(" -> ").AppendLine(tag.Target.Sha[..7]);

        return builder.Length == 0 ? "Тегов нет." : builder.ToString();
    }

    private static string StashList(string rootPath)
    {
        using var repository = Open(rootPath);

        var builder = new StringBuilder();

        foreach (var stash in repository.Stashes)
            builder.AppendLine(stash.Message);

        return builder.Length == 0 ? "Stash пуст." : builder.ToString();
    }

    // ----- Git write -----

    private static string Init(string rootPath)
    {
        Repository.Init(rootPath);
        return "Git-репозиторий инициализирован.";
    }

    private static string Add(string rootPath, string? relativePath)
    {
        using var repository = Open(rootPath);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            var files = repository.RetrieveStatus(new StatusOptions())
                .Where(entry => entry.State != FileStatus.Ignored)
                .Select(entry => entry.FilePath)
                .ToArray();

            Commands.Stage(repository, files);
            return $"Подготовлено файлов: {files.Length}.";
        }

        Commands.Stage(repository, relativePath);
        return $"Подготовлено: {relativePath}.";
    }

    private static string Commit(string rootPath, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Не задано сообщение коммита.");

        using var repository = Open(rootPath);
        var signature = Signature(repository);
        var commit = repository.Commit(message, signature, signature);

        return $"Создан коммит {commit.Sha[..7]}.";
    }

    private static string Checkout(string rootPath, string? reference)
    {
        using var repository = Open(rootPath);

        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Не задана ветка или коммит.");

        Commands.Checkout(repository, reference);

        return $"Переключено на {reference}.";
    }

    private static string CreateBranch(string rootPath, string? name)
    {
        using var repository = Open(rootPath);
        var branch = repository.CreateBranch(Require(name, "Не задано имя ветки."));

        return $"Создана ветка {branch.FriendlyName}.";
    }

    private static string DeleteBranch(string rootPath, string? name)
    {
        using var repository = Open(rootPath);
        repository.Branches.Remove(Require(name, "Не задано имя ветки."));

        return $"Ветка {name} удалена.";
    }

    private static string Merge(string rootPath, string? name)
    {
        using var repository = Open(rootPath);
        var branch = repository.Branches[Require(name, "Не задано имя ветки.")]
            ?? throw new InvalidOperationException($"Ветка «{name}» не найдена.");

        var result = repository.Merge(branch, Signature(repository));

        return $"Merge: {result.Status}.";
    }

    private static string Rebase(string rootPath, string? name)
    {
        using var repository = Open(rootPath);
        var upstream = repository.Branches[Require(name, "Не задана ветка для rebase.")]
            ?? throw new InvalidOperationException($"Ветка «{name}» не найдена.");

        var signature = Signature(repository);
        var result = repository.Rebase.Start(repository.Head, upstream, null, new Identity(signature.Name, signature.Email), new RebaseOptions());

        return $"Rebase: {result.Status}.";
    }

    private static string CherryPick(string rootPath, string? reference, bool revert)
    {
        using var repository = Open(rootPath);
        var commit = ResolveCommit(repository, Require(reference, "Не задан коммит."));
        var signature = Signature(repository);

        return revert
            ? $"Revert: {repository.Revert(commit, signature).Status}."
            : $"Cherry-pick: {repository.CherryPick(commit, signature).Status}.";
    }

    private static string Reset(string rootPath, string? relativePath)
    {
        using var repository = Open(rootPath);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            repository.Reset(ResetMode.Mixed, repository.Head.Tip);
            return "Индекс сброшен к HEAD (mixed).";
        }

        Commands.Unstage(repository, [relativePath]);
        return $"Снято из индекса: {relativePath}.";
    }

    private static string Pull(string rootPath)
    {
        using var repository = Open(rootPath);
        var result = Commands.Pull(repository, Signature(repository), new PullOptions());

        return $"Pull: {result.Status}.";
    }

    private static string Push(string rootPath)
    {
        using var repository = Open(rootPath);
        var remote = repository.Network.Remotes["origin"]
            ?? repository.Network.Remotes.FirstOrDefault()
            ?? throw new InvalidOperationException("Не настроен remote.");
        var branch = repository.Head.FriendlyName;

        repository.Network.Push(remote, $"refs/heads/{branch}:refs/heads/{branch}", new PushOptions());

        return $"Ветка {branch} отправлена в {remote.Name}.";
    }

    private static string Fetch(string rootPath)
    {
        using var repository = Open(rootPath);
        var remote = repository.Network.Remotes["origin"]
            ?? repository.Network.Remotes.FirstOrDefault()
            ?? throw new InvalidOperationException("Не настроен remote.");

        Commands.Fetch(repository, remote.Name, [], new FetchOptions(), null);

        return $"Получены изменения из {remote.Name}.";
    }

    private static string Stash(string rootPath, string? message)
    {
        using var repository = Open(rootPath);
        repository.Stashes.Add(Signature(repository), string.IsNullOrWhiteSpace(message) ? null : message, StashModifiers.Default);

        return "Изменения сохранены в stash.";
    }

    private static string StashPop(string rootPath)
    {
        using var repository = Open(rootPath);
        repository.Stashes.Pop(0);

        return "Stash применён и удалён.";
    }

    private static string CreateTag(string rootPath, string? name)
    {
        using var repository = Open(rootPath);
        repository.ApplyTag(Require(name, "Не задано имя тега."));

        return $"Создан тег {name}.";
    }

    private static string AddRemote(string rootPath, string? name, string? url)
    {
        using var repository = Open(rootPath);
        repository.Network.Remotes.Add(Require(name, "Не задано имя remote."), Require(url, "Не задан URL remote."));

        return $"Добавлен remote {name}.";
    }

    private static string Require(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    // ----- C# / files / process -----

    private static string CSharpOutline(string rootPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Не задан путь к файлу C#.");

        var file = Path.Join(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(file))
            throw new FileNotFoundException($"Файл не найден: {relativePath}");

        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
        var builder = new StringBuilder();

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseNamespaceDeclarationSyntax ns:
                    builder.Append("namespace ").AppendLine(ns.Name.ToString());
                    break;
                case TypeDeclarationSyntax type:
                    builder.Append(type.Keyword.Text).Append(' ').AppendLine(type.Identifier.Text);
                    break;
                case MethodDeclarationSyntax method:
                    builder.Append("  метод ").Append(method.ReturnType).Append(' ').AppendLine(method.Identifier.Text);
                    break;
                case PropertyDeclarationSyntax property:
                    builder.Append("  свойство ").Append(property.Type).Append(' ').AppendLine(property.Identifier.Text);
                    break;
            }
        }

        return builder.Length == 0 ? "Объявления не найдены." : Truncate(builder.ToString());
    }

    private string WriteFile(string rootPath, AIFunctionArguments arguments)
    {
        var relative = Require(ConfigurationFunction.GetString(arguments, "path"), "Не задан путь к файлу.");
        var content = ConfigurationFunction.GetString(arguments, "content") ?? string.Empty;
        var file = ResolvePath(rootPath, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);

        logger.LogInformation("Модель записала файл {Path}", relative);

        return $"Файл записан: {relative}";
    }

    private string TouchFile(string rootPath, AIFunctionArguments arguments)
    {
        var relative = Require(ConfigurationFunction.GetString(arguments, "path"), "Не задан путь к файлу.");
        var file = ResolvePath(rootPath, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        if (!File.Exists(file))
            File.WriteAllText(file, string.Empty);

        logger.LogInformation("Модель создала файл {Path}", relative);

        return $"Файл создан: {relative}";
    }

    private static string ResolvePath(string rootPath, string relative)
    {
        if (Path.IsPathRooted(relative))
            throw new ArgumentException("Путь должен быть относительным к корню проекта.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var full = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Join(root, relative.Replace('/', Path.DirectorySeparatorChar))));

        if (!string.Equals(full, root, StringComparison.Ordinal) &&
            !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Путь выходит за пределы папки проекта.");
        }

        return full;
    }

    private static string Truncate(string value) =>
        value.Length <= MaxOutputLength ? value : value[..MaxOutputLength] + "… (вывод обрезан)";

    private static class ProcessRunner
    {
        public static async Task<string> RunAsync(string workingDirectory, string command, int timeoutSeconds, CancellationToken cancellationToken)
        {
            var isWindows = OperatingSystem.IsWindows();

            var startInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add(isWindows ? "/c" : "-c");
            startInfo.ArgumentList.Add(command);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Не удалось запустить процесс.");

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return $"Команда превысила лимит {timeoutSeconds} с и была остановлена.";
            }

            var output = $"{(await outputTask.ConfigureAwait(false)).TrimEnd()}\n{(await errorTask.ConfigureAwait(false)).TrimEnd()}".Trim();

            return output.Length == 0 ? "Команда выполнена без вывода." : Truncate(output);
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
