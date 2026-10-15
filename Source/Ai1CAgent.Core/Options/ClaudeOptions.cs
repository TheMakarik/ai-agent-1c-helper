namespace Ai1CAgent.Core.Options;

public sealed class ClaudeOptions
{
    public string ApiKeyEnvironmentVariable { get; set; } = "ANTHROPIC_API_KEY";

    public string Model { get; set; } = string.Empty;

    public int MaxOutputTokens { get; set; } = 16000;
}
