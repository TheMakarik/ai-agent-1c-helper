using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp;
using Ai1CAgent.Tests.Infrastructure;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ai1CAgent.Tests.Mcp;

public sealed class McpToolProviderTests : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromMinutes(3));
    private McpToolProvider? _systemUnderTests;

    [Fact]
    public async Task GetToolsAsync_WhenNoServersConfigured_ReturnsEmpty()
    {
        // Arrange
        _systemUnderTests = CreateProvider(new McpOptions());

        // Act
        var tools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Assert
        tools.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task GetToolsAsync_WhenEverythingServerConfigured_ReturnsEchoTool()
    {
        // Arrange
        Skip.IfNot(CommandProbe.Exists(EverythingMcpServer.Command), "Не найден npx");
        _systemUnderTests = CreateProvider(EverythingMcpServer.CreateOptions());

        // Act
        var tools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Assert
        tools.Select(tool => tool.Name).Should().Contain(EverythingMcpServer.EchoToolName);
    }

    [SkippableFact]
    public async Task GetToolsAsync_WhenCalledTwice_ReturnsCachedTools()
    {
        // Arrange
        Skip.IfNot(CommandProbe.Exists(EverythingMcpServer.Command), "Не найден npx");
        _systemUnderTests = CreateProvider(EverythingMcpServer.CreateOptions());
        var expectedTools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Act
        var actualTools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Assert
        actualTools.Should().BeSameAs(expectedTools);
    }

    public async ValueTask DisposeAsync()
    {
        if (_systemUnderTests is not null)
            await _systemUnderTests.DisposeAsync();

        _timeout.Dispose();
    }

    private static McpToolProvider CreateProvider(McpOptions options)
        => new(FakeOptions.Create(options), NullLoggerFactory.Instance, A.Dummy<ILogger<McpToolProvider>>());
}
