using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public interface IChatStore
{
    Task<IReadOnlyList<ChatSession>> GetSessionsAsync(long? projectId, CancellationToken cancellationToken);

    Task<ChatSession?> GetSessionAsync(long sessionId, CancellationToken cancellationToken);

    Task<ChatSession> CreateSessionAsync(long? projectId, string title, CancellationToken cancellationToken);

    Task RenameSessionAsync(long sessionId, string title, CancellationToken cancellationToken);

    Task DeleteSessionAsync(long sessionId, CancellationToken cancellationToken);

    IAsyncEnumerable<ChatMessageRecord> GetMessagesAsync(long sessionId, CancellationToken cancellationToken);

    Task<ChatMessageRecord> AppendMessageAsync(ChatMessageRecord message, CancellationToken cancellationToken);

    Task DeleteMessagesAsync(long sessionId, CancellationToken cancellationToken);

    Task<int> ReassignOrphanSessionsAsync(long projectId, CancellationToken cancellationToken);
}
