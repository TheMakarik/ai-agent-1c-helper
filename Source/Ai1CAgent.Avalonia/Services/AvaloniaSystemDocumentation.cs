using System;
using System.IO;
using Ai1CAgent.Core.Common;
using Avalonia.Platform;

namespace Ai1CAgent.Avalonia.Services;

public sealed class AvaloniaSystemDocumentation : ISystemDocumentation
{
    private const string ResourceUri = "avares://Ai1CAgent.Avalonia/Assets/Skills/System/SKILL.md";

    public string Read()
    {
        using var stream = AssetLoader.Open(new Uri(ResourceUri));
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
