using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Configuration.Interfaces;
using Ai1CAgent.Modules.Context.Interfaces;
using Ai1CAgent.Modules.Development.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Ai;

public sealed class AiChatService(
    IChatClientFactory chatClientFactory,
    IConfigurationToolProvider configurationToolProvider,
    IProjectContextToolProvider projectContextToolProvider,
    ISkillToolProvider skillToolProvider,
    IDeveloperToolProvider developerToolProvider,
    ISystemDocumentationToolProvider systemDocumentationToolProvider,
    IAppSettingsStore appSettingsStore,
    ILogger<AiChatService> logger) : IAiChatService
{
    public async Task<string> SendAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        string? configurationPath,
        long? projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Запрос к {Provider}/{Model}, сообщений: {Count}", model.ProviderKey, model.Name, messages.Count);

        var client = chatClientFactory.GetClient(model);
        var chatOptions = CreateChatOptions(configurationPath, projectId);
        var response = await client
            .GetResponseAsync(ToChatMessages(messages), chatOptions, cancellationToken)
            .ConfigureAwait(false);

        return response.Text;
    }

    public async IAsyncEnumerable<AiChatChunk> StreamAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        string? configurationPath,
        long? projectId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Потоковый запрос к {Provider}/{Model}, сообщений: {Count}", model.ProviderKey, model.Name, messages.Count);

        var client = chatClientFactory.GetClient(model);
        var chatOptions = CreateChatOptions(configurationPath, projectId);
        var updates = client.GetStreamingResponseAsync(ToChatMessages(messages), chatOptions, cancellationToken);

        await foreach (var update in updates.ConfigureAwait(false))
        {
            var text = update.Text;
            var reasoning = ExtractReasoning(update.Contents);
            var usage = ExtractUsage(update.Contents);

            if (text.Length > 0 || reasoning.Length > 0 || usage is not null)
                yield return new AiChatChunk(text, reasoning, usage);

            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent { Name: { Length: > 0 } name } call)
                    yield return new AiChatChunk(string.Empty, string.Empty, null, name, BuildToolDetail(call.Arguments));
            }
        }
    }

    public async IAsyncEnumerable<AiChatChunk> CompressAsync(
        AiModel model,
        IReadOnlyCollection<AiChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(messages);

        logger.LogInformation("Сжатие контекста для {Provider}/{Model}", model.ProviderKey, model.Name);

        var client = chatClientFactory.GetClient(model);
        var chatMessages = new List<ChatMessage> { new(ChatRole.System, CompressionPrompt) };
        chatMessages.AddRange(ToChatMessages(messages));

        var updates = client.GetStreamingResponseAsync(chatMessages, options: null, cancellationToken);

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

    private ChatOptions? CreateChatOptions(string? configurationPath, long? projectId)
    {
        List<AITool> tools = [.. systemDocumentationToolProvider.GetTools()];
        tools.AddRange(skillToolProvider.GetTools(projectId));

        if (!string.IsNullOrWhiteSpace(configurationPath) && Directory.Exists(configurationPath))
        {
            var allowWrite = appSettingsStore.Get()?.AllowWrite ?? false;
            tools.AddRange(configurationToolProvider.GetTools(configurationPath));
            tools.AddRange(developerToolProvider.GetTools(configurationPath, allowWrite));
        }
        else if (!string.IsNullOrWhiteSpace(configurationPath))
        {
            logger.LogWarning("Папка проекта не найдена, функции чтения не переданы: {Path}", configurationPath);
        }

        if (projectId is { } id)
            tools.AddRange(projectContextToolProvider.GetTools(id));

        return tools.Count == 0 ? null : new ChatOptions { Tools = tools };
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

    private static readonly string[] DetailKeys =
    [
        "command", "path", "ref", "commit", "query", "name", "message", "pattern", "url",
    ];

    private const string CompressionPrompt =
        "Ты — модуль сжатия контекста диалога. Сократи переписку: убери воду, повторы и несущественное, "
        + "сохрани факты, принятые решения, договорённости, имена, пути, фрагменты кода и незавершённые задачи. "
        + "Верни связный краткий пересказ, который полностью заменит историю. Не задавай вопросов и не комментируй процесс.";

    private static string? BuildToolDetail(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return null;

        foreach (var key in DetailKeys)
        {
            if (arguments.TryGetValue(key, out var value) && FormatValue(value) is { Length: > 0 } text)
                return text.Length <= 120 ? text : text[..120] + "…";
        }

        return null;
    }

    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonElement element => element.GetRawText(),
        _ => value.ToString(),
    };

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
