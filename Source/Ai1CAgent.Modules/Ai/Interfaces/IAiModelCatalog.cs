using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Modules.Ai.Interfaces;

public interface IAiModelCatalog
{
    IReadOnlyList<AiModel> GetModels();
}
