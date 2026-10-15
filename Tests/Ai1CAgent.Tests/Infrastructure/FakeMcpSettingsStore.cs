using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp.Interfaces;

namespace Ai1CAgent.Tests.Infrastructure;

public sealed class FakeMcpSettingsStore(McpServerSettings settings) : IMcpSettingsStore
{
    private McpServerSettings _settings = settings;

    public event EventHandler? SettingsChanged;

    public McpServerSettings Get() => _settings.Clone();

    public Task SaveAsync(McpServerSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings.Clone();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
