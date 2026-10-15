namespace Ai1CAgent.Core.Options;

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";

    public IReadOnlyList<string> Models { get; set; } = [];
}
