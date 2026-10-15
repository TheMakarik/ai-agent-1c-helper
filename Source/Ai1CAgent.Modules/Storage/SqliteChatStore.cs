using System.Globalization;
using System.Runtime.CompilerServices;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Dapper;

namespace Ai1CAgent.Modules.Storage;

public sealed class SqliteChatStore(Database database) : IChatStore
{
    private const string SelectSession =
        """
        SELECT id AS Id, project_id AS ProjectId, title AS Title,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM chat_session
        """;

    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(long? projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<ChatSession>(new CommandDefinition(
            $"{SelectSession} WHERE ($projectId IS NULL AND project_id IS NULL) OR project_id = $projectId ORDER BY updated_at DESC",
            new { projectId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.AsList();
    }

    public async Task<ChatSession?> GetSessionAsync(long sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.QuerySingleOrDefaultAsync<ChatSession>(new CommandDefinition(
            $"{SelectSession} WHERE id = $sessionId",
            new { sessionId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ChatSession> CreateSessionAsync(long? projectId, string title, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO chat_session (project_id, title, created_at, updated_at)
            VALUES ($projectId, $title, $createdAt, $updatedAt)
            RETURNING id;
            """,
            new { projectId, title, createdAt = now, updatedAt = now },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return new ChatSession(id, projectId, title, now, now);
    }

    public async Task RenameSessionAsync(long sessionId, string title, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_session SET title = $title, updated_at = $updatedAt WHERE id = $sessionId",
            new { sessionId, title, updatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteSessionAsync(long sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM chat_session WHERE id = $sessionId",
            new { sessionId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ChatMessageRecord> GetMessagesAsync(
        long sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, session_id, role, text, reasoning, input_tokens, output_tokens, total_tokens, created_at, content
            FROM chat_message
            WHERE session_id = $sessionId
            ORDER BY id
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new ChatMessageRecord(
                reader.GetInt64(0),
                reader.GetInt64(1),
                Enum.Parse<AiChatRole>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetInt64(7),
                DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(9) ? string.Empty : reader.GetString(9));
        }
    }

    public async Task<ChatMessageRecord> AppendMessageAsync(ChatMessageRecord message, CancellationToken cancellationToken)
    {
        var createdAt = message.CreatedAt == default ? DateTimeOffset.UtcNow : message.CreatedAt;
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO chat_message (session_id, role, text, reasoning, input_tokens, output_tokens, total_tokens, created_at, content)
            VALUES ($sessionId, $role, $text, $reasoning, $inputTokens, $outputTokens, $totalTokens, $createdAt, $content)
            RETURNING id;
            """,
            new
            {
                sessionId = message.SessionId,
                role = message.Role,
                text = message.Text,
                reasoning = message.Reasoning,
                inputTokens = message.InputTokens,
                outputTokens = message.OutputTokens,
                totalTokens = message.TotalTokens,
                createdAt,
                content = message.Content,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_session SET updated_at = $updatedAt WHERE id = $sessionId",
            new { sessionId = message.SessionId, updatedAt = createdAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return message with { Id = id, CreatedAt = createdAt };
    }

    public async Task DeleteMessagesAsync(long sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM chat_message WHERE session_id = $sessionId",
            new { sessionId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> ReassignOrphanSessionsAsync(long projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_session SET project_id = $projectId, updated_at = $updatedAt WHERE project_id IS NULL",
            new { projectId, updatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
