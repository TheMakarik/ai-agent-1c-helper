using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Modules.Mcp.Interfaces;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ai1CAgent.Tests.Infrastructure;

public static class ChatServiceBuilder
{
    public static (AiChatService Service, ChatClientFactory Factory) Build(
        OllamaOptions? ollamaOptions = null,
        DeepSeekOptions? deepSeekOptions = null,
        ClaudeOptions? claudeOptions = null,
        IMcpToolProvider? mcpToolProvider = null)
    {
        var factory = new ChatClientFactory(
            FakeOptions.Create(ollamaOptions ?? new OllamaOptions()),
            FakeOptions.Create(deepSeekOptions ?? new DeepSeekOptions()),
            FakeOptions.Create(claudeOptions ?? new ClaudeOptions()),
            NullLoggerFactory.Instance,
            A.Dummy<ILogger<ChatClientFactory>>());

        var service = new AiChatService(
            factory,
            mcpToolProvider ?? EmptyMcpToolProvider.Create(),
            A.Dummy<ILogger<AiChatService>>());

        return (service, factory);
    }
}
