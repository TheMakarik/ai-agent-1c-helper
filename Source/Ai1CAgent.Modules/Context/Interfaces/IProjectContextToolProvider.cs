using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Context.Interfaces;

public interface IProjectContextToolProvider
{
    IReadOnlyList<AITool> GetTools(long projectId);
}
