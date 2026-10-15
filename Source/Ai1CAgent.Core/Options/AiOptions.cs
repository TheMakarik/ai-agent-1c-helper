namespace Ai1CAgent.Core.Options;

public sealed class AiOptions
{
    public string Title { get; set; } = "1С:АгентПомощник";

    public int MaxOutputTokens { get; set; } = 16000;

    public IReadOnlyList<AiProviderSettings> Providers { get; set; } = [];
}
