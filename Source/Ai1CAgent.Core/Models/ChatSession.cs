namespace Ai1CAgent.Core.Models;

public sealed record ChatSession(
    long Id,
    long? ProjectId,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
