namespace Ai1CAgent.Core.Options;

public sealed class DeepSeekOptions
{
    public string Endpoint { get; set; } = "https://api.deepseek.com";

    public string ApiKeyEnvironmentVariable { get; set; } = "DEEPSEEK_API_KEY";

    public string Model { get; set; } = string.Empty;
}
