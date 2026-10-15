using System.ClientModel;
using System.Collections.Concurrent;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace Ai1CAgent.Modules.Ai;

public sealed class ChatClientFactory(
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<DeepSeekOptions> deepSeekOptions,
    IOptions<ClaudeOptions> claudeOptions,
    ILoggerFactory loggerFactory,
    ILogger<ChatClientFactory> logger) : IChatClientFactory, IDisposable
{
    private readonly ConcurrentDictionary<AiModel, IChatClient> _clients = new();

    public IChatClient GetClient(AiModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _clients.GetOrAdd(model, CreateClient);
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
            client.Dispose();

        _clients.Clear();
    }

    private IChatClient CreateClient(AiModel model)
    {
        logger.LogInformation("Создаётся клиент {Provider} для модели {Model}", model.Provider, model.Name);

        var client = model.Provider switch
        {
            AiProvider.Ollama => CreateOllamaClient(model.Name),
            AiProvider.DeepSeek => CreateDeepSeekClient(model.Name),
            AiProvider.Claude => CreateClaudeClient(model.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.Provider, "Неизвестный провайдер"),
        };

        return client
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory)
            .Build();
    }

    private IChatClient CreateOllamaClient(string modelName)
        => new OllamaApiClient(new Uri(ollamaOptions.Value.Endpoint), modelName);

    private IChatClient CreateDeepSeekClient(string modelName)
    {
        var options = deepSeekOptions.Value;
        var apiKey = GetRequiredApiKey(options.ApiKeyEnvironmentVariable);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) };

        return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelName)
            .AsIChatClient();
    }

    private IChatClient CreateClaudeClient(string modelName)
    {
        var options = claudeOptions.Value;
        var apiKey = GetRequiredApiKey(options.ApiKeyEnvironmentVariable);
        var client = new AnthropicClient { ApiKey = apiKey };

        return client.AsIChatClient(modelName)
            .AsBuilder()
            .ConfigureOptions(chatOptions => chatOptions.MaxOutputTokens ??= options.MaxOutputTokens)
            .Build();
    }

    private static string GetRequiredApiKey(string environmentVariable)
    {
        var apiKey = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"Не задан API-ключ: переменная окружения {environmentVariable} пуста");

        return apiKey;
    }
}
