using Ai1CAgent.Core.Models;
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
    public async Task GetToolsAsync_WhenServerNotConfigured_ReturnsEmpty()
    {
        // Arrange
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(new McpServerSettings()));

        // Act
        var tools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Assert
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WhenServerNotConfigured_ReturnsFalse()
    {
        // Arrange
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(new McpServerSettings()));

        // Act
        var isAvailable = await _systemUnderTests.CheckAsync(_timeout.Token);

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_WhenStdioCommandEmpty_ReturnsUnavailable()
    {
        // Arrange
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(new McpServerSettings()));
        var settings = new McpServerSettings { Transport = McpTransportType.Stdio };

        // Act
        var result = await _systemUnderTests.ProbeAsync(settings, _timeout.Token);

        // Assert
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_WhenHttpServerUnreachable_ReturnsUnavailableWithError()
    {
        // Arrange
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(new McpServerSettings()));
        var settings = new McpServerSettings
        {
            Transport = McpTransportType.Http,
            Endpoint = "http://127.0.0.1:1/mcp",
        };

        // Act
        var result = await _systemUnderTests.ProbeAsync(settings, _timeout.Token);

        // Assert
        result.Error.Should().NotBeEmpty();
    }

    [SkippableFact]
    public async Task GetToolsAsync_WhenEverythingServerConfigured_ReturnsEchoTool()
    {
        // Arrange
        Skip.IfNot(CommandProbe.Exists(EverythingMcpServer.Command), "Не найден npx");
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(EverythingMcpServer.CreateSettings()));

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
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(EverythingMcpServer.CreateSettings()));
        var expectedTools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Act
        var actualTools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Assert
        actualTools.Should().BeSameAs(expectedTools);
    }

    [SkippableFact]
    public async Task GetToolsAsync_WhenSettingsChangedToNotConfigured_ReturnsEmpty()
    {
        // Arrange
        Skip.IfNot(CommandProbe.Exists(EverythingMcpServer.Command), "Не найден npx");
        var store = new FakeMcpSettingsStore(EverythingMcpServer.CreateSettings());
        _systemUnderTests = CreateProvider(store);
        await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Act
        await store.SaveAsync(new McpServerSettings(), _timeout.Token);
        var tools = await _systemUnderTests.GetToolsAsync(_timeout.Token);

        // Assert
        tools.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ProbeAsync_WhenEverythingServerConfigured_ReturnsAvailable()
    {
        // Arrange
        Skip.IfNot(CommandProbe.Exists(EverythingMcpServer.Command), "Не найден npx");
        _systemUnderTests = CreateProvider(new FakeMcpSettingsStore(new McpServerSettings()));

        // Act
        var result = await _systemUnderTests.ProbeAsync(EverythingMcpServer.CreateSettings(), _timeout.Token);

        // Assert
        result.IsAvailable.Should().BeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        if (_systemUnderTests is not null)
            await _systemUnderTests.DisposeAsync();

        _timeout.Dispose();
    }

    private static McpToolProvider CreateProvider(FakeMcpSettingsStore store)
        => new(store, NullLoggerFactory.Instance, A.Dummy<ILogger<McpToolProvider>>());
}
