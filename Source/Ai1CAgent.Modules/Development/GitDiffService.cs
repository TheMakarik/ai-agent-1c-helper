using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using LibGit2Sharp;

namespace Ai1CAgent.Modules.Development;

public sealed class GitDiffService : IGitDiffService
{
    public bool IsRepository(string rootPath) =>
        !string.IsNullOrWhiteSpace(rootPath) && Repository.Discover(rootPath) is not null;

    public void Initialize(string rootPath) => Repository.Init(rootPath);

    public IReadOnlyList<GitCommitInfo> GetCommits(string rootPath, int limit)
    {
        using var repository = Open(rootPath);
        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };

        return repository.Commits.QueryBy(filter)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(commit => new GitCommitInfo(commit.Sha, commit.MessageShort, commit.Author.Name, commit.Author.When))
            .ToArray();
    }

    public IReadOnlyList<GitFileChange> GetChangedFiles(string rootPath, string sha)
    {
        using var repository = Open(rootPath);
        var commit = ResolveCommit(repository, sha);
        var changes = repository.Diff.Compare<TreeChanges>(commit.Parents.FirstOrDefault()?.Tree, commit.Tree);

        return changes
            .Select(change => new GitFileChange(change.Path, change.Status.ToString()))
            .ToArray();
    }

    public string GetDiff(string rootPath, string sha, string path)
    {
        using var repository = Open(rootPath);
        var commit = ResolveCommit(repository, sha);
        var patch = repository.Diff.Compare<Patch>(commit.Parents.FirstOrDefault()?.Tree, commit.Tree);

        foreach (var change in patch)
        {
            if (string.Equals(change.Path, path, StringComparison.Ordinal))
                return change.Patch;
        }

        return string.Empty;
    }

    public (string Old, string New) GetVersions(string rootPath, string sha, string path)
    {
        using var repository = Open(rootPath);
        var commit = ResolveCommit(repository, sha);

        return (ReadBlob(repository, commit.Parents.FirstOrDefault()?.Tree, path), ReadBlob(repository, commit.Tree, path));
    }

    private static string ReadBlob(Repository repository, Tree? tree, string path)
    {
        if (tree is null)
            return string.Empty;

        var entry = tree[path];

        if (entry?.Target is not Blob blob)
            return string.Empty;

        using var content = blob.GetContentStream();
        using var reader = new StreamReader(content);

        return reader.ReadToEnd();
    }

    private static Repository Open(string rootPath)
    {
        var discovered = Repository.Discover(rootPath)
            ?? throw new InvalidOperationException("Папка проекта не является Git-репозиторием.");

        return new Repository(discovered);
    }

    private static Commit ResolveCommit(Repository repository, string sha) =>
        repository.Lookup<Commit>(sha) ?? throw new InvalidOperationException($"Коммит «{sha}» не найден.");
}
