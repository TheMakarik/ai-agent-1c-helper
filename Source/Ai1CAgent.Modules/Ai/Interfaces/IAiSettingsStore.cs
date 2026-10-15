using Ai1CAgent.Core.Options;

namespace Ai1CAgent.Modules.Ai.Interfaces;

public interface IAiSettingsStore
{
    event EventHandler? SettingsChanged;

    IReadOnlyList<AiProviderSettings> GetProviders();

    Task SaveProvidersAsync(IReadOnlyList<AiProviderSettings> providers, CancellationToken cancellationToken = default);
}
