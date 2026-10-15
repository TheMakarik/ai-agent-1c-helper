using Ai1CAgent.Core.Models;
using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Ai.Interfaces;

public interface IChatClientFactory
{
    IChatClient GetClient(AiModel model);
}
