using System.Text.Json.Serialization;

namespace Ai1CAgent.Core.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<MessageBlock>))]
internal sealed partial class MessageBlocksJsonContext : JsonSerializerContext;
