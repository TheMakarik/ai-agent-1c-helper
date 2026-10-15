using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Modules.Ai.Interfaces;

public interface IAiChatService
{
    Task<string> SendAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        string? configurationPath,
        CancellationToken cancellationToken);

    IAsyncEnumerable<AiChatChunk> StreamAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        string? configurationPath,
        CancellationToken cancellationToken);
}
