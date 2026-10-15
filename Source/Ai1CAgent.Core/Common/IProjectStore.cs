using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public interface IProjectStore
{
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken);

    Task<Project?> GetProjectAsync(long projectId, CancellationToken cancellationToken);

    Task<Project> AddProjectAsync(string name, string path, CancellationToken cancellationToken);

    Task RenameProjectAsync(long projectId, string name, CancellationToken cancellationToken);

    Task DeleteProjectAsync(long projectId, CancellationToken cancellationToken);

    Task<string> GetGlobalContextAsync(long projectId, CancellationToken cancellationToken);

    Task SetGlobalContextAsync(long projectId, string context, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectFile>> GetFilesAsync(long projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectFile>> IndexFilesAsync(long projectId, CancellationToken cancellationToken);
}
