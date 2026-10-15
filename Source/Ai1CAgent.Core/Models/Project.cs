namespace Ai1CAgent.Core.Models;

public sealed record Project(
    long Id,
    string Name,
    string Path,
    string GlobalContext,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool FolderExists => System.IO.Directory.Exists(Path);
}
