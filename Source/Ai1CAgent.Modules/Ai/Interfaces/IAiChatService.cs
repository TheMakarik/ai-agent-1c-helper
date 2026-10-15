using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Modules.Ai.Interfaces;

public interface IAiChatService
{
    Task<string> SendAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        CancellationToken cancellationToken);

    IAsyncEnumerable<AiChatChunk> StreamAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        CancellationToken cancellationToken);
}
