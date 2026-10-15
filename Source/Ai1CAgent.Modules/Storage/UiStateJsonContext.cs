using System.Text.Json.Serialization;
using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Modules.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UiState))]
internal sealed partial class UiStateJsonContext : JsonSerializerContext;
