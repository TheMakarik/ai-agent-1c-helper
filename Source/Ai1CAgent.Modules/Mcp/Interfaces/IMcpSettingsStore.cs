using Ai1CAgent.Core.Options;

namespace Ai1CAgent.Modules.Mcp.Interfaces;

public interface IMcpSettingsStore
{
    event EventHandler? SettingsChanged;

    McpServerSettings Get();

    Task SaveAsync(McpServerSettings settings, CancellationToken cancellationToken = default);
}
