using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Ai1CAgent.Avalonia.Services;

public sealed class EmbeddedSkills : IEmbeddedSkills
{
    private const string Suffix = "/SKILL.md";

    private readonly Assembly _assembly = typeof(EmbeddedSkills).Assembly;

    public IReadOnlyList<string> List(string category)
    {
        if (category == "System")
            return ["Системная документация"];

        var prefix = $"Skills/{category}/";

        return _assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(Suffix, StringComparison.Ordinal))
            .Select(name => name[prefix.Length..^Suffix.Length])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string? Read(string category, string name)
    {
        var resource = category == "System"
            ? "Skills/System/SKILL.md"
            : $"Skills/{category}/{name}/SKILL.md";

        using var stream = _assembly.GetManifestResourceStream(resource);

        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
