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

    private const string GitRootHint = "Операции выполняются в папке проекта (Git-репозиторий).";

    private const string NoArgsSchema = """
        { "type": "object", "properties": {} }
        """;

    private const string LimitSchema = """
        {
          "type": "object",
          "properties": { "limit": { "type": "integer", "description": "Сколько последних коммитов показать (по умолчанию 20)." } }
        }
        """;

    private const string PathSchema = """
        {
          "type": "object",
          "properties": { "path": { "type": "string", "description": "Путь к файлу относительно корня проекта (необязательно)." } }
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

    public IReadOnlyList<AITool> GetTools(string rootPath, bool allowWrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var tools = new List<AITool>
        {
            new ContextFunction("GitStatus", "Показать ветку и изменённые файлы Git-репозитория проекта. " + GitRootHint, NoArgsSchema,
                (_, _) => ValueTask.FromResult(Git(() => Status(rootPath), "GitStatus"))),
            new ContextFunction("GitLog", "Показать последние коммиты Git-репозитория проекта (хеш, дата, автор, сообщение). " + GitRootHint, LimitSchema,
                (arguments, _) => ValueTask.FromResult(Git(() => Log(rootPath, ConfigurationFunction.GetInt(arguments, "limit") ?? 20), "GitLog"))),
            new ContextFunction("GitDiff", "Показать незакоммиченные изменения (diff рабочей директории против HEAD). " + GitRootHint, PathSchema,
                (arguments, _) => ValueTask.FromResult(Git(() => Diff(rootPath, ConfigurationFunction.GetString(arguments, "path")), "GitDiff"))),
            new ContextFunction("GitBranches", "Показать ветки Git-репозитория проекта. " + GitRootHint, NoArgsSchema,
                (_, _) => ValueTask.FromResult(Git(() => Branches(rootPath), "GitBranches"))),
            new ContextFunction("CSharpOutline", "Показать структуру файла C# (пространства имён, типы, методы, свойства) по пути относительно корня проекта.", PathSchema,
                (arguments, _) => ValueTask.FromResult(Git(() => CSharpOutline(rootPath, ConfigurationFunction.GetString(arguments, "path")), "CSharpOutline"))),
            new ContextFunction("RunCommand", "Выполнить команду оболочки в папке проекта и вернуть вывод. Используй для сборки, тестов и служебных команд.", CommandSchema,
                (arguments, cancellationToken) => Run(arguments, rootPath, cancellationToken)),
            new ContextFunction("CanWrite", "Проверить, разрешена ли запись файлов (режим записи в настройках).", NoArgsSchema,
                (_, _) => ValueTask.FromResult(allowWrite ? "Запись разрешена." : "Запись запрещена.")),
        };

        if (allowWrite)
        {
            tools.Add(new ContextFunction("Write", "Записать файл в папке проекта: создать или полностью перезаписать.", WriteSchema,
                (arguments, _) => ValueTask.FromResult(WriteFile(rootPath, arguments))));
            tools.Add(new ContextFunction("Touch", "Создать пустой файл в папке проекта по относительному пути.", TouchSchema,
                (arguments, _) => ValueTask.FromResult(TouchFile(rootPath, arguments))));
        }

        return tools;
    }

    private string Git(Func<string> action, string operation)
    {
        logger.LogInformation("Модель вызвала {Operation}", operation);
        return action();
    }

    private async ValueTask<string> Run(AIFunctionArguments arguments, string rootPath, CancellationToken cancellationToken)
    {
        var command = ConfigurationFunction.GetString(arguments, "command");

        if (string.IsNullOrWhiteSpace(command))
            return "Ошибка: не задана команда.";

        logger.LogInformation("Модель выполняет команду в терминале");

        return await ProcessRunner.RunAsync(rootPath, command, CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveRepo(string rootPath)
    {
        var discovered = Repository.Discover(rootPath)
            ?? throw new InvalidOperationException("Папка проекта не является Git-репозиторием.");

        return discovered;
    }

    private static string Status(string rootPath)
    {
        using var repository = new Repository(ResolveRepo(rootPath));

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
        using var repository = new Repository(ResolveRepo(rootPath));

        var builder = new StringBuilder();
        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };

        foreach (var commit in repository.Commits.QueryBy(filter).Take(Math.Clamp(limit, 1, 200)))
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
        using var repository = new Repository(ResolveRepo(rootPath));

        var tree = repository.Head.Tip?.Tree;
        var patch = repository.Diff.Compare<Patch>(tree, DiffTargets.WorkingDirectory);

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
        using var repository = new Repository(ResolveRepo(rootPath));

        var builder = new StringBuilder();

        foreach (var branch in repository.Branches.OrderBy(branch => branch.FriendlyName, StringComparer.OrdinalIgnoreCase))
            builder.Append(branch.IsCurrentRepositoryHead ? "* " : "  ").AppendLine(branch.FriendlyName);

        return builder.ToString();
    }

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

    private static string Truncate(string value) =>
        value.Length <= MaxOutputLength ? value : value[..MaxOutputLength] + "… (вывод обрезан)";

    private string WriteFile(string rootPath, AIFunctionArguments arguments)
    {
        var relative = ConfigurationFunction.GetString(arguments, "path");
        var content = ConfigurationFunction.GetString(arguments, "content") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(relative))
            throw new ArgumentException("Не задан путь к файлу.");

        var file = ResolvePath(rootPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);

        logger.LogInformation("Модель записала файл {Path}", relative);

        return $"Файл записан: {relative}";
    }

    private string TouchFile(string rootPath, AIFunctionArguments arguments)
    {
        var relative = ConfigurationFunction.GetString(arguments, "path");

        if (string.IsNullOrWhiteSpace(relative))
            throw new ArgumentException("Не задан путь к файлу.");

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
