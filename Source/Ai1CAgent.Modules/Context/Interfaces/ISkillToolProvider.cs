using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Context.Interfaces;

public interface ISkillToolProvider
{
    IReadOnlyList<AITool> GetTools(long? projectId);
}
