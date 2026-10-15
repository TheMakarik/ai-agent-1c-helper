namespace Ai1CAgent.Core.Models;

public sealed record McpProbeResult(bool IsAvailable, int ToolCount, string Error)
{
    public static McpProbeResult Available(int toolCount) => new(true, toolCount, string.Empty);

    public static McpProbeResult Unavailable(string error) => new(false, 0, error);
}
