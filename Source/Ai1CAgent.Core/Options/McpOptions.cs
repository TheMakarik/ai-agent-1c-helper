namespace Ai1CAgent.Core.Options;

public sealed class McpOptions
{
    public IReadOnlyList<McpServerOptions> Servers { get; set; } = [];
}
