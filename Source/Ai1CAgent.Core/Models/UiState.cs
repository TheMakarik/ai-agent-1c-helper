namespace Ai1CAgent.Core.Models;

public sealed record UiState(string? ProviderKey, string? ModelName, long? ProjectId, long? SessionId);
