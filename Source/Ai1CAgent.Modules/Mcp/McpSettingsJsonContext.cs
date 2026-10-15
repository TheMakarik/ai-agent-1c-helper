using System.Text.Json.Serialization;

namespace Ai1CAgent.Modules.Mcp;

internal sealed class PersistedMcpServer
{
    public string? Transport { get; set; }

    public string? Endpoint { get; set; }

    public string? Command { get; set; }

    public string? Arguments { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PersistedMcpServer))]
internal sealed partial class McpSettingsJsonContext : JsonSerializerContext;
