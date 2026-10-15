using System.Collections.Generic;

namespace Ai1CAgent.Avalonia.Services;

public interface IEmbeddedSkills
{
    IReadOnlyList<string> List(string category);

    string? Read(string category, string name);
}
