using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Options;

public sealed class McpServerOptions
{
    public string Name { get; set; } = string.Empty;

    public McpTransportType Transport { get; set; } = McpTransportType.Stdio;

    public string Command
    {
        get;
        set => field = Environment.ExpandEnvironmentVariables(value);
    } = string.Empty;

    public IReadOnlyList<string> Arguments { get; set; } = [];

    public string Endpoint { get; set; } = string.Empty;
}
