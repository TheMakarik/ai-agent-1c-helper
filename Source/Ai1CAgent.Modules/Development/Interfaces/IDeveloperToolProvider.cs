using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Development.Interfaces;

public interface IDeveloperToolProvider
{
    IReadOnlyList<AITool> GetTools(string rootPath, bool allowWrite);
}
