namespace Ai1CAgent.Core.Models;

public sealed record GitCommitInfo(string Sha, string Message, string Author, DateTimeOffset When)
{
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;

    public string Display => $"{ShortSha}  {Message}";
}
