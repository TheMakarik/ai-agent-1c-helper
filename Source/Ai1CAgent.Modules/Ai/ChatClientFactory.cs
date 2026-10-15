using System.ClientModel;
using System.Collections.Concurrent;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Ai1CAgent.Modules.Ai;

public sealed class ChatClientFactory : IChatClientFactory, IDisposable
{
    private static readonly TimeSpan StaleClientLifetime = TimeSpan.FromSeconds(30);

    private readonly IOptions<OllamaOptions> _ollamaOptions;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly IAiSettingsStore _settingsStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ChatClientFactory> _logger;
    private readonly ConcurrentDictionary<AiModel, IChatClient> _clients = new();

    public ChatClientFactory(
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<AiOptions> aiOptions,
        IAiSettingsStore settingsStore,
        ILoggerFactory loggerFactory,
        ILogger<ChatClientFactory> logger)
    {
        _ollamaOptions = ollamaOptions;
        _aiOptions = aiOptions;
        _settingsStore = settingsStore;
        _loggerFactory = loggerFactory;
        _logger = logger;

        settingsStore.SettingsChanged += OnSettingsChanged;
    }

    public IChatClient GetClient(AiModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _clients.GetOrAdd(model, CreateClient);
    }

    public void Dispose()
    {
        _settingsStore.SettingsChanged -= OnSettingsChanged;
        DisposeClients();
    }

    // После смены настроек старые клиенты могут обслуживать ещё не завершённые запросы,
    // поэтому их нельзя диспозить синхронно. Сразу убираем их из кэша (новые запросы
    // создадут свежие клиенты), а освобождение откладываем на небольшой grace-период —
    // in-flight запросы успевают завершиться, и не остаётся неосвобождённых сокетов.
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_clients.IsEmpty)
            return;

        IChatClient[] stale = [.. _clients.Values];
        _clients.Clear();

        ScheduleDisposal(stale);
    }

    private void ScheduleDisposal(IChatClient[] clients)
    {
        if (clients.Length == 0)
            return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(StaleClientLifetime).ConfigureAwait(false);

            foreach (var client in clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Не удалось освободить устаревший клиент чата");
                }
            }
        });
    }

    private void DisposeClients()
    {
        foreach (var client in _clients.Values)
            client.Dispose();

        _clients.Clear();
    }

    private IChatClient CreateClient(AiModel model)
    {
        _logger.LogInformation("Создаётся клиент {Provider} для модели {Model}", model.ProviderKey, model.Name);

        var client = string.Equals(model.ProviderKey, nameof(AiProvider.Ollama), StringComparison.Ordinal)
            ? CreateOllamaClient(model)
            : CreateCompatibleClient(model);

        return client
            .AsBuilder()
            .UseFunctionInvocation(_loggerFactory)
            .Build();
    }

    private IChatClient CreateOllamaClient(AiModel model)
    {
        var options = _ollamaOptions.Value;
        var settings = _settingsStore.GetProviders()
            .FirstOrDefault(provider => provider.Provider == AiProvider.Ollama);

        var apiKey = settings?.ResolvedApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = "ollama";

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = ResolveOllamaEndpoint(settings, options),
            NetworkTimeout = options.RequestTimeout,
        };

        return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(model.Name)
            .AsIChatClient();
    }

    private IChatClient CreateCompatibleClient(AiModel model)
    {
        var resolved = Resolve(model);

        if (string.IsNullOrWhiteSpace(resolved.Endpoint))
            throw new InvalidOperationException($"Не задан адрес API для провайдера «{resolved.Settings.DisplayName}»");

        if (string.IsNullOrWhiteSpace(resolved.ApiKey))
            throw new InvalidOperationException($"Не задан API-ключ для провайдера «{resolved.Settings.DisplayName}»");

        return resolved.Format == AiApiFormat.Anthropic
            ? CreateAnthropicClient(model.Name, resolved.Endpoint, resolved.ApiKey)
            : CreateOpenAiCompatibleClient(model.Name, resolved.Endpoint, resolved.ApiKey);
    }

    /// <summary>
    /// Разрешает настройки, формат API, адрес и ключ для модели без создания сетевого клиента.
    /// </summary>
    internal ResolvedClientSettings Resolve(AiModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var settings = _settingsStore.GetProviders()
            .FirstOrDefault(provider =>
                string.Equals(provider.Provider?.ToString(), model.ProviderKey, StringComparison.Ordinal) ||
                string.Equals(provider.Name, model.ProviderKey, StringComparison.Ordinal))
            ?? CreateFallbackSettings(model.ProviderKey);

        var customModel = settings.CustomModels
            .FirstOrDefault(custom => string.Equals(custom.Name.Trim(), model.Name, StringComparison.Ordinal));

        var format = customModel?.Format ?? settings.ResolvedFormat();

        return new ResolvedClientSettings(settings, format, settings.ResolvedEndpoint(), settings.ResolvedApiKey());
    }

    internal static Uri ResolveOllamaEndpoint(AiProviderSettings? settings, OllamaOptions options)
    {
        var resolved = settings?.ResolvedEndpoint();
        var defaultEndpoint = AiProviderDescriptors.DefaultEndpoint(AiProvider.Ollama);

        if (!string.IsNullOrWhiteSpace(resolved) &&
            !string.Equals(resolved, defaultEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(resolved);
        }

        return new Uri(new Uri(options.Endpoint), "v1");
    }

    private static AiProviderSettings CreateFallbackSettings(string providerKey) =>
        Enum.TryParse<AiProvider>(providerKey, out var provider) && Enum.IsDefined(provider)
            ? new AiProviderSettings { Provider = provider }
            : new AiProviderSettings { Name = providerKey };

    private static IChatClient CreateOpenAiCompatibleClient(string modelName, string endpoint, string apiKey)
    {
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };

        return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelName)
            .AsIChatClient();
    }

    private IChatClient CreateAnthropicClient(string modelName, string endpoint, string apiKey)
    {
        var maxOutputTokens = _aiOptions.Value.MaxOutputTokens;
        var client = new AnthropicClient { ApiKey = apiKey, BaseUrl = endpoint };

        return client
            .AsIChatClient(modelName)
            .AsBuilder()
            .ConfigureOptions(chatOptions => chatOptions.MaxOutputTokens ??= maxOutputTokens)
            .Build();
    }
}
