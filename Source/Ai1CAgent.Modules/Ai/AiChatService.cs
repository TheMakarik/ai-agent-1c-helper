using System.Runtime.CompilerServices;
using System.Text;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Configuration.Interfaces;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Ai;

public sealed class AiChatService(
    IChatClientFactory chatClientFactory,
    IMcpToolProvider mcpToolProvider,
    IConfigurationToolProvider configurationToolProvider,
    ILogger<AiChatService> logger) : IAiChatService
{
    public async Task<string> SendAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        string? configurationPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Запрос к {Provider}/{Model}, сообщений: {Count}", model.ProviderKey, model.Name, messages.Count);

        var client = chatClientFactory.GetClient(model);
        var chatOptions = await CreateChatOptionsAsync(configurationPath, cancellationToken).ConfigureAwait(false);
        var response = await client
            .GetResponseAsync(ToChatMessages(messages), chatOptions, cancellationToken)
            .ConfigureAwait(false);

        return response.Text;
    }

    public async IAsyncEnumerable<AiChatChunk> StreamAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        string? configurationPath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Потоковый запрос к {Provider}/{Model}, сообщений: {Count}", model.ProviderKey, model.Name, messages.Count);

        var client = chatClientFactory.GetClient(model);
        var chatOptions = await CreateChatOptionsAsync(configurationPath, cancellationToken).ConfigureAwait(false);
        var updates = client.GetStreamingResponseAsync(ToChatMessages(messages), chatOptions, cancellationToken);

        await foreach (var update in updates.ConfigureAwait(false))
        {
            var text = update.Text;
            var reasoning = ExtractReasoning(update.Contents);

            if (text.Length == 0 && reasoning.Length == 0)
                continue;

            yield return new AiChatChunk(text, reasoning);
        }
    }

    private async Task<ChatOptions?> CreateChatOptionsAsync(string? configurationPath, CancellationToken cancellationToken)
    {
        List<AITool> tools = [.. await mcpToolProvider.GetToolsAsync(cancellationToken).ConfigureAwait(false)];

        if (!string.IsNullOrWhiteSpace(configurationPath))
        {
            if (Directory.Exists(configurationPath))
                tools.AddRange(configurationToolProvider.GetTools(configurationPath));
            else
                logger.LogWarning("Папка выгрузки конфигурации не найдена, функции чтения не переданы: {Path}", configurationPath);
        }

        if (tools.Count == 0)
            return null;

        return new ChatOptions { Tools = tools };
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
