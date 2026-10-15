using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Ai1CAgent.Modules.Mcp;

public sealed class McpToolProvider(
    IOptions<McpOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<McpToolProvider> logger) : IMcpToolProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<McpClient> _clients = [];
    private IReadOnlyList<AITool>? _tools;
    private volatile bool _disposed;

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
            return _tools ?? [];

        if (_tools is not null)
            return _tools;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
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
            logger.LogWarning(exception, "Инструменты 1С MCP недоступны, запрос продолжится без них");
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
            if (_clients.Count > 0)
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            else
                await ConnectAsync(cancellationToken).ConfigureAwait(false);

            var isAvailable = _clients.Count > 0;

            if (isAvailable)
                logger.LogInformation("Проверка 1С MCP пройдена, инструментов: {ToolCount}", _tools?.Count ?? 0);
            else
                logger.LogInformation("Проверка 1С MCP пройдена: серверы не настроены");

            return isAvailable;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Проверка подключения к 1С MCP не пройдена");
            await ResetAsync().ConfigureAwait(false);
            _tools = [];
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        await ResetAsync().ConfigureAwait(false);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var servers = options.Value.Servers;

        if (servers.Count == 0)
        {
            await ResetAsync().ConfigureAwait(false);
            _tools = [];
            return;
        }

        var clients = await Task.WhenAll(servers.Select(server => CreateClientAsync(server, cancellationToken)))
            .ConfigureAwait(false);

        var toolLists = await Task.WhenAll(
                clients.Select(client => client.ListToolsAsync(cancellationToken: cancellationToken).AsTask()))
            .ConfigureAwait(false);

        await ResetAsync().ConfigureAwait(false);

        _clients = clients;
        _tools = toolLists.SelectMany(tools => tools).ToArray();

        logger.LogInformation(
            "Подключено MCP-серверов: {ServerCount}, инструментов: {ToolCount}",
            _clients.Count,
            _tools.Count);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var toolLists = await Task.WhenAll(
                _clients.Select(client => client.ListToolsAsync(cancellationToken: cancellationToken).AsTask()))
            .ConfigureAwait(false);

        _tools = toolLists.SelectMany(tools => tools).ToArray();
    }

    private async Task ResetAsync()
    {
        var clients = _clients;
        _clients = [];
        _tools = null;

        foreach (var client in clients)
        {
            try
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Не удалось освободить клиент MCP");
            }
        }
    }

    private async Task<McpClient> CreateClientAsync(McpServerOptions server, CancellationToken cancellationToken)
    {
        logger.LogInformation("Подключение к MCP-серверу {Server} через {Transport}", server.Name, server.Transport);

        return await McpClient.CreateAsync(CreateTransport(server), loggerFactory: loggerFactory, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static IClientTransport CreateTransport(McpServerOptions server) => server.Transport switch
    {
        McpTransportType.Stdio => new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = server.Name,
            Command = server.Command,
            Arguments = [.. server.Arguments],
        }),
        McpTransportType.Http => new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = server.Name,
            Endpoint = new Uri(server.Endpoint),
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(server), server.Transport, "Неизвестный транспорт MCP"),
    };
}
