using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Mcp.Interfaces;

public interface IMcpToolProvider
{
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken);

    Task<bool> CheckAsync(CancellationToken cancellationToken);

    Task<McpProbeResult> ProbeAsync(McpServerSettings settings, CancellationToken cancellationToken);
}
