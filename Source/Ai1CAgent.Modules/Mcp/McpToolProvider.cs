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

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken)
    {
        if (_tools is not null)
            return _tools;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_tools is not null)
                return _tools;

            var servers = options.Value.Servers;
            var clients = await Task.WhenAll(servers.Select(server => ConnectAsync(server, cancellationToken)))
                .ConfigureAwait(false);
            _clients = clients;

            var toolLists = await Task.WhenAll(clients.Select(client => client.ListToolsAsync(cancellationToken: cancellationToken).AsTask()))
                .ConfigureAwait(false);
            _tools = toolLists.SelectMany(tools => tools).ToArray<AITool>();

            logger.LogInformation("Подключено MCP-серверов: {ServerCount}, инструментов: {ToolCount}", clients.Length, _tools.Count);

            return _tools;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
            await client.DisposeAsync().ConfigureAwait(false);

        _clients = [];
        _gate.Dispose();
    }

    private async Task<McpClient> ConnectAsync(McpServerOptions server, CancellationToken cancellationToken)
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
