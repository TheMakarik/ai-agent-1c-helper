using System.Text;

namespace Ai1CAgent.Modules.Configuration;

/// <summary>
/// Доступ только на чтение к папке выгрузки конфигурации 1С
/// (Конфигуратор → «Выгрузить конфигурацию в файлы»). Пути вне папки отклоняются.
/// </summary>
internal sealed class ConfigurationFolder
{
    public const string RootFileName = "Configuration.xml";

    private const int MaxListEntries = 500;
    private const int DefaultReadLimit = 400;
    private const int MaxReadLimit = 2000;
    private const int MaxSearchMatches = 100;
    private const int MaxMatchLineLength = 300;

    private readonly string _root;

    public ConfigurationFolder(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    public static bool IsConfigurationDump(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Join(path, RootFileName));

    public string List(string? relativePath)
    {
        var directory = Resolve(relativePath);

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Папка не найдена: {ToRelative(directory)}");

        var builder = new StringBuilder();
        var count = 0;

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (count == MaxListEntries)
            {
                builder.AppendLine($"… показаны первые {MaxListEntries} элементов, уточните путь");
                break;
            }

            if (Directory.Exists(entry))
                builder.Append(ToRelative(entry)).AppendLine("/");
            else
                builder.Append(ToRelative(entry)).Append("  (").Append(new FileInfo(entry).Length).AppendLine(" байт)");

            count++;
        }

        return count == 0 ? "Папка пуста" : builder.ToString();
    }

    public string Read(string relativePath, int? offset, int? limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var file = Resolve(relativePath);

        if (!File.Exists(file))
            throw new FileNotFoundException($"Файл не найден: {ToRelative(file)}");

        var lines = File.ReadAllLines(file);
        var start = Math.Max(1, offset ?? 1);
        var count = Math.Clamp(limit ?? DefaultReadLimit, 1, MaxReadLimit);

        if (lines.Length == 0)
            return $"Файл {ToRelative(file)} пуст";

        if (start > lines.Length)
            return $"В файле {ToRelative(file)} всего {lines.Length} строк";

        var end = Math.Min(lines.Length, start + count - 1);
        var builder = new StringBuilder();
        builder.AppendLine($"Файл {ToRelative(file)}, строки {start}–{end} из {lines.Length}");

        for (var index = start; index <= end; index++)
            builder.Append(index).Append(": ").AppendLine(lines[index - 1]);

        if (end < lines.Length)
            builder.AppendLine($"… продолжение: offset = {end + 1}");

        return builder.ToString();
    }

    public string Search(string query, string? filePattern, string? relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var directory = Resolve(relativePath);

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Папка не найдена: {ToRelative(directory)}");

        var pattern = string.IsNullOrWhiteSpace(filePattern) ? "*" : filePattern.Trim();
        var builder = new StringBuilder();
        var matches = 0;

        foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            var lineNumber = 0;

            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;

                if (!line.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (matches == MaxSearchMatches)
                {
                    builder.AppendLine($"… показаны первые {MaxSearchMatches} совпадений, уточните запрос или папку");
                    return builder.ToString();
                }

                var text = line.Trim();

                if (text.Length > MaxMatchLineLength)
                    text = text[..MaxMatchLineLength] + "…";

                builder.Append(ToRelative(file)).Append(':').Append(lineNumber).Append(": ").AppendLine(text);
                matches++;
            }
        }

        return matches == 0 ? "Совпадений не найдено" : builder.ToString();
    }

    private string Resolve(string? relativePath)
    {
        var relative = (relativePath ?? string.Empty).Trim().Replace('/', Path.DirectorySeparatorChar);

        if (relative is "." or "")
            return _root;

        if (Path.IsPathRooted(relative))
            throw new ArgumentException("Путь должен быть относительным к папке выгрузки конфигурации");

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Join(_root, relative)));

        if (!string.Equals(full, _root, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Путь выходит за пределы папки выгрузки конфигурации");
        }

        return full;
    }

    private string ToRelative(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        return relative == "." ? "/" : relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
