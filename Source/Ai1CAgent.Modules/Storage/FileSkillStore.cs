using System.Globalization;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Modules.Storage;

public sealed class FileSkillStore : ISkillStore
{
    private const string FileName = "SKILL.md";

    public IReadOnlyList<string> List(SkillScope scope, long? projectId)
    {
        var root = ResolveRoot(scope, projectId);

        if (!Directory.Exists(root))
            return [];

        return Directory.EnumerateDirectories(root)
            .Where(directory => File.Exists(Path.Join(directory, FileName)))
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    public string? Read(SkillScope scope, long? projectId, string name)
    {
        var file = ResolveFile(scope, projectId, name);

        return File.Exists(file) ? File.ReadAllText(file) : null;
    }

    public void Write(SkillScope scope, long? projectId, string name, string content)
    {
        var file = ResolveFile(scope, projectId, name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public void Delete(SkillScope scope, long? projectId, string name)
    {
        var directory = Path.GetDirectoryName(ResolveFile(scope, projectId, name))!;

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static string ResolveRoot(SkillScope scope, long? projectId) => scope switch
    {
        SkillScope.Global => AppDataPaths.SkillsGlobal,
        SkillScope.Local when projectId is { } id =>
            Path.Join(AppDataPaths.SkillsLocal, id.ToString(CultureInfo.InvariantCulture)),
        _ => throw new InvalidOperationException("Локальные скиллы доступны только внутри проекта."),
    };

    private static string ResolveFile(SkillScope scope, long? projectId, string name) =>
        Path.Join(ResolveRoot(scope, projectId), SanitizeName(name), FileName);

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());

        return cleaned.Length == 0 ? "Skill" : cleaned;
    }
}
