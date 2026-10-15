using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Context.Interfaces;

public interface ISystemDocumentationToolProvider
{
    IReadOnlyList<AITool> GetTools();
}
