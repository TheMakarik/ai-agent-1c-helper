using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;

namespace Ai1CAgent.Tests.Infrastructure;

public static class EverythingMcpServer
{
    public const string Command = "npx";
    public const string EchoToolName = "echo";

    public static McpServerSettings CreateSettings() => new()
    {
        Transport = McpTransportType.Stdio,
        Command = Command,
        Arguments = "-y @modelcontextprotocol/server-everything",
    };
}
