using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public interface IGitDiffService
{
    bool IsRepository(string rootPath);

    IReadOnlyList<GitCommitInfo> GetCommits(string rootPath, int limit);

    IReadOnlyList<GitFileChange> GetChangedFiles(string rootPath, string sha);

    string GetDiff(string rootPath, string sha, string path);

    (string Old, string New) GetVersions(string rootPath, string sha, string path);
}
