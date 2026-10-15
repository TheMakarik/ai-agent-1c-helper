using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ai1CAgent.Modules.Storage;

public sealed class SqliteProjectStore(Database database) : IProjectStore
{
    private const string SelectProject =
        """
        SELECT id AS Id, name AS Name, path AS Path, global_context AS GlobalContext,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM project
        """;

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<Project>(new CommandDefinition(
            $"{SelectProject} ORDER BY name COLLATE NOCASE",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.AsList();
    }

    public async Task<Project?> GetProjectAsync(long projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.QuerySingleOrDefaultAsync<Project>(new CommandDefinition(
            $"{SelectProject} WHERE id = $projectId",
            new { projectId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Project> AddProjectAsync(string name, string path, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO project (name, path, global_context, created_at, updated_at)
            VALUES ($name, $path, '', $createdAt, $updatedAt)
            RETURNING id;
            """,
            new { name, path, createdAt = now, updatedAt = now },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var project = new Project(id, name, path, string.Empty, now, now);
        await IndexFilesAsync(project, connection, cancellationToken).ConfigureAwait(false);

        return project;
    }

    public async Task RenameProjectAsync(long projectId, string name, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project SET name = $name, updated_at = $updatedAt WHERE id = $projectId",
            new { projectId, name, updatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteProjectAsync(long projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM project WHERE id = $projectId",
            new { projectId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string> GetGlobalContextAsync(long projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var context = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT global_context FROM project WHERE id = $projectId",
            new { projectId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return context ?? string.Empty;
    }

    public async Task SetGlobalContextAsync(long projectId, string context, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project SET global_context = $context, updated_at = $updatedAt WHERE id = $projectId",
            new { projectId, context, updatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProjectFile>> GetFilesAsync(long projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<ProjectFile>(new CommandDefinition(
            """
            SELECT id AS Id, project_id AS ProjectId, relative_path AS RelativePath,
                   last_write_time_utc AS LastWriteTimeUtc, size AS Size, indexed_at AS IndexedAt
            FROM project_file
            WHERE project_id = $projectId
            ORDER BY relative_path
            """,
            new { projectId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.AsList();
    }

    public async Task<IReadOnlyList<ProjectFile>> IndexFilesAsync(long projectId, CancellationToken cancellationToken)
    {
        var project = await GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Проект {projectId} не найден.");

        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await IndexFilesAsync(project, connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ProjectFile>> IndexFilesAsync(
        Project project,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!project.FolderExists)
            throw new InvalidOperationException($"Папка проекта не найдена: {project.Path}");

        var indexedAt = DateTimeOffset.UtcNow;
        var files = new List<ProjectFile>();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        foreach (var file in Directory.EnumerateFiles(project.Path, "*", options))
        {
            var info = new FileInfo(file);
            files.Add(new ProjectFile(
                0,
                project.Id,
                Path.GetRelativePath(project.Path, file),
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                info.Length,
                indexedAt));
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM project_file WHERE project_id = $projectId",
            new { projectId = project.Id },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (files.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO project_file (project_id, relative_path, last_write_time_utc, size, indexed_at)
                VALUES ($ProjectId, $RelativePath, $LastWriteTimeUtc, $Size, $IndexedAt)
                """,
                files,
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        transaction.Commit();

        return files;
    }
}
