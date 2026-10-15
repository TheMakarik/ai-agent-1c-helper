namespace Ai1CAgent.Core.Models;

public sealed record AiModel(string ProviderKey, string Name)
{
    public AiModel(AiProvider provider, string name) : this(provider.ToString(), name)
    {
    }
}
