using System.Text.Json.Serialization;

namespace Ai1CAgent.Modules.Configuration;

internal sealed class PersistedConfigurationPath
{
    public string? LastPath { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PersistedConfigurationPath))]
internal sealed partial class ConfigurationPathJsonContext : JsonSerializerContext;
