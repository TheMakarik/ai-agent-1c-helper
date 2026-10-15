using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Options;

public sealed class McpServerSettings
{
    public McpTransportType Transport { get; set; } = McpTransportType.Http;

    public string Endpoint { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public bool IsConfigured => Transport switch
    {
        McpTransportType.Http => !string.IsNullOrWhiteSpace(Endpoint),
        McpTransportType.Stdio => !string.IsNullOrWhiteSpace(Command),
        _ => false,
    };

    public McpServerSettings Clone() => new()
    {
        Transport = Transport,
        Endpoint = Endpoint,
        Command = Command,
        Arguments = Arguments,
    };
}
