using System;
using System.IO;
using Ai1CAgent.Core.Common;

namespace Ai1CAgent.Avalonia.Services;

public sealed class AvaloniaSystemDocumentation : ISystemDocumentation
{
    private const string ResourceName = "Skills/System/SKILL.md";

    public string Read()
    {
        using var stream = typeof(AvaloniaSystemDocumentation).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Не найден встроенный ресурс «{ResourceName}»");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
