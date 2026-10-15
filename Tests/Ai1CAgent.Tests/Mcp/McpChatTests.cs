using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Modules.Mcp;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Ai1CAgent.Tests.Infrastructure;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ai1CAgent.Tests.Mcp;

[Collection(LocalModelsCollection.Name)]
public sealed class McpChatTests : IAsyncDisposable
{
    private static readonly IReadOnlyCollection<AiChatMessage> EchoPrompt =
    [
        new(AiChatRole.User, "Вызови инструмент echo с сообщением \"ping-1c\" и перескажи, что он вернул."),
    ];

    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromMinutes(5));
    private readonly McpToolProvider _mcpToolProvider;
    private ChatClientFactory? _factory;

    public McpChatTests()
    {
        _mcpToolProvider = new McpToolProvider(
            new FakeMcpSettingsStore(EverythingMcpServer.CreateSettings()),
            NullLoggerFactory.Instance,
            A.Dummy<ILogger<McpToolProvider>>());
    }

    [SkippableTheory]
    [InlineData("qwen3.5:9b")]
    [InlineData("gemma4:e4b")]
    public async Task SendAsync_WithMcpEchoTool_OllamaInvokesTool(string modelName)
    {
        // Arrange
        Skip.IfNot(CommandProbe.Exists(EverythingMcpServer.Command), "Не найден npx");
        Skip.IfNot(await OllamaProbe.HasModelAsync(modelName, _timeout.Token), $"В Ollama нет модели {modelName}");

        var echoTool = await GetCountingEchoToolAsync();
        var (systemUnderTests, factory) = ChatServiceBuilder.Build(
            ollamaOptions: new OllamaOptions { Endpoint = OllamaProbe.Endpoint },
            mcpToolProvider: CreateToolProvider(echoTool));
        _factory = factory;

        // Act
        await systemUnderTests.SendAsync(new AiModel(AiProvider.Ollama, modelName), EchoPrompt, _timeout.Token);

        // Assert
        echoTool.InvocationCount.Should().BePositive();
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        await _mcpToolProvider.DisposeAsync();
        _timeout.Dispose();
    }

    private async Task<CountingAIFunction> GetCountingEchoToolAsync()
    {
        var tools = await _mcpToolProvider.GetToolsAsync(_timeout.Token);
        var echo = tools.OfType<AIFunction>().Single(tool => tool.Name == EverythingMcpServer.EchoToolName);
        return new CountingAIFunction(echo);
    }

    private static IMcpToolProvider CreateToolProvider(AITool tool)
    {
        var provider = A.Fake<IMcpToolProvider>();
        A.CallTo(() => provider.GetToolsAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AITool>>([tool]));
        return provider;
    }
}
