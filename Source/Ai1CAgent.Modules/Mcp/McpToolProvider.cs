using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Ai1CAgent.Modules.Mcp;

public sealed class McpToolProvider : IMcpToolProvider, IAsyncDisposable
{
    private const string ServerName = "1C MCP";

    private readonly IMcpSettingsStore _settingsStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpToolProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;
    private IReadOnlyList<AITool>? _tools;
    private volatile bool _settingsChanged;
    private volatile bool _disposed;

    public McpToolProvider(
        IMcpSettingsStore settingsStore,
        ILoggerFactory loggerFactory,
        ILogger<McpToolProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _loggerFactory = loggerFactory;
        _logger = logger;

        _settingsStore.SettingsChanged += OnSettingsChanged;
    }

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
            return _tools ?? [];

        if (_tools is not null && !_settingsChanged)
            return _tools;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ApplySettingsChangeAsync().ConfigureAwait(false);

            if (_tools is not null)
                return _tools;

            await ConnectAsync(cancellationToken).ConfigureAwait(false);
            return _tools ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Инструменты 1С MCP недоступны, запрос продолжится без них");
            await ResetAsync().ConfigureAwait(false);
            _tools = [];
            return _tools;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
            return false;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ApplySettingsChangeAsync().ConfigureAwait(false);

            if (_client is not null)
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            else
                await ConnectAsync(cancellationToken).ConfigureAwait(false);

            var isAvailable = _client is not null;

            if (isAvailable)
                _logger.LogInformation("Проверка 1С MCP пройдена, инструментов: {ToolCount}", _tools?.Count ?? 0);
            else
                _logger.LogInformation("Проверка 1С MCP: сервер не настроен");

            return isAvailable;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Проверка подключения к 1С MCP не пройдена");
            await ResetAsync().ConfigureAwait(false);
            _tools = [];
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<McpProbeResult> ProbeAsync(McpServerSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsConfigured)
        {
            return McpProbeResult.Unavailable(settings.Transport is McpTransportType.Http
                ? "не указан адрес сервера"
                : "не указана команда запуска сервера");
        }

        try
        {
            var client = await CreateClientAsync(settings, cancellationToken).ConfigureAwait(false);

            await using (client.ConfigureAwait(false))
            {
                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Проверка настроек MCP пройдена, инструментов: {ToolCount}", tools.Count);
                return McpProbeResult.Available(tools.Count);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Проверка настроек MCP не пройдена");
            return McpProbeResult.Unavailable(exception.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _settingsStore.SettingsChanged -= OnSettingsChanged;

        await ResetAsync().ConfigureAwait(false);
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => _settingsChanged = true;

    private async Task ApplySettingsChangeAsync()
    {
        if (!_settingsChanged)
            return;

        _settingsChanged = false;
        _logger.LogInformation("Настройки MCP изменились, подключение будет установлено заново");
        await ResetAsync().ConfigureAwait(false);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Get();

        if (!settings.IsConfigured)
        {
            await ResetAsync().ConfigureAwait(false);
            _tools = [];
            return;
        }

        var client = await CreateClientAsync(settings, cancellationToken).ConfigureAwait(false);
        IList<McpClientTool> tools;

        try
        {
            tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await ResetAsync().ConfigureAwait(false);

        _client = client;
        _tools = [.. tools];

        _logger.LogInformation(
            "Подключено к MCP-серверу через {Transport}, инструментов: {ToolCount}",
            settings.Transport,
            _tools.Count);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var tools = await _client!.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _tools = [.. tools];
    }

    private async Task ResetAsync()
    {
        var client = _client;
        _client = null;
        _tools = null;

        if (client is null)
            return;

        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Не удалось освободить клиент MCP");
        }
    }

    private async Task<McpClient> CreateClientAsync(McpServerSettings settings, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Подключение к MCP-серверу через {Transport}", settings.Transport);

        return await McpClient.CreateAsync(CreateTransport(settings), loggerFactory: _loggerFactory, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static IClientTransport CreateTransport(McpServerSettings settings) => settings.Transport switch
    {
        McpTransportType.Stdio => new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = ServerName,
            Command = Environment.ExpandEnvironmentVariables(settings.Command.Trim()),
            Arguments = [.. CommandLineArguments.Split(Environment.ExpandEnvironmentVariables(settings.Arguments))],
        }),
        McpTransportType.Http => new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = ServerName,
            Endpoint = new Uri(settings.Endpoint.Trim()),
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Transport, "Неизвестный транспорт MCP"),
    };
}
