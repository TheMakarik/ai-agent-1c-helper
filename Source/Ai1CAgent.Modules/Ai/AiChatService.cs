using System.Runtime.CompilerServices;
using System.Text;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Ai;

public sealed class AiChatService(
    IChatClientFactory chatClientFactory,
    IMcpToolProvider mcpToolProvider,
    ILogger<AiChatService> logger) : IAiChatService
{
    public async Task<string> SendAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Запрос к {Provider}/{Model}, сообщений: {Count}", model.ProviderKey, model.Name, messages.Count);

        var client = chatClientFactory.GetClient(model);
        var chatOptions = await CreateChatOptionsAsync(cancellationToken).ConfigureAwait(false);
        var response = await client
            .GetResponseAsync(ToChatMessages(messages), chatOptions, cancellationToken)
            .ConfigureAwait(false);

        return response.Text;
    }

    public async IAsyncEnumerable<AiChatChunk> StreamAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Потоковый запрос к {Provider}/{Model}, сообщений: {Count}", model.ProviderKey, model.Name, messages.Count);

        var client = chatClientFactory.GetClient(model);
        var chatOptions = await CreateChatOptionsAsync(cancellationToken).ConfigureAwait(false);
        var updates = client.GetStreamingResponseAsync(ToChatMessages(messages), chatOptions, cancellationToken);

        await foreach (var update in updates.ConfigureAwait(false))
        {
            var text = update.Text;
            var reasoning = ExtractReasoning(update.Contents);
            var usage = ExtractUsage(update.Contents);

            if (text.Length == 0 && reasoning.Length == 0 && usage is null)
                continue;

            yield return new AiChatChunk(text, reasoning, usage);
        }
    }

    private async Task<ChatOptions?> CreateChatOptionsAsync(CancellationToken cancellationToken)
    {
        var tools = await mcpToolProvider.GetToolsAsync(cancellationToken).ConfigureAwait(false);

        if (tools.Count == 0)
            return null;

        return new ChatOptions { Tools = [.. tools] };
    }

    private static string ExtractReasoning(IEnumerable<AIContent> contents)
    {
        StringBuilder? builder = null;

        foreach (var content in contents)
        {
            if (content is not TextReasoningContent { Text: { Length: > 0 } text })
                continue;

            builder ??= new StringBuilder();
            builder.Append(text);
        }

        return builder?.ToString() ?? string.Empty;
    }

    private static AiTokenUsage? ExtractUsage(IEnumerable<AIContent> contents)
    {
        foreach (var content in contents)
        {
            if (content is UsageContent { Details: { } details })
                return new AiTokenUsage(details.InputTokenCount, details.OutputTokenCount, details.TotalTokenCount);
        }

        return null;
    }

    private static List<ChatMessage> ToChatMessages(IReadOnlyCollection<AiChatMessage> messages)
        => messages.Select(message => new ChatMessage(ToChatRole(message.Role), message.Text)).ToList();

    private static ChatRole ToChatRole(AiChatRole role) => role switch
    {
        AiChatRole.System => ChatRole.System,
        AiChatRole.User => ChatRole.User,
        AiChatRole.Assistant => ChatRole.Assistant,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Неизвестная роль"),
    };
}
