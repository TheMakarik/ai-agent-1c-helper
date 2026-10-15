namespace Ai1CAgent.Core.Models;

public sealed record ProjectFile(
    long Id,
    long ProjectId,
    string RelativePath,
    DateTimeOffset LastWriteTimeUtc,
    long Size,
    DateTimeOffset IndexedAt);
