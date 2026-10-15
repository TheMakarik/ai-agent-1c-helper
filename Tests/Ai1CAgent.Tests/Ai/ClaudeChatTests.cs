using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Tests.Infrastructure;
using FluentAssertions;

namespace Ai1CAgent.Tests.Ai;

public sealed class ClaudeChatTests : IDisposable
{
    private const string ApiKeyVariable = "ANTHROPIC_API_KEY";
    private const string ModelName = "claude-opus-5-5";

    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromMinutes(3));
    private readonly AiChatService _systemUnderTests;
    private readonly ChatClientFactory _factory;

    public ClaudeChatTests()
    {
        (_systemUnderTests, _factory) = ChatServiceBuilder.Build();
    }

    [SkippableFact]
    public async Task SendAsync_WhenAskedArithmetic_ReturnsCorrectAnswer()
    {
        // Arrange
        Skip.If(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyVariable)), $"Не задана переменная {ApiKeyVariable}");
        var model = new AiModel(AiProvider.Claude, ModelName);

        // Act
        var answer = await _systemUnderTests.SendAsync(model, ChatPrompts.Arithmetic, null, _timeout.Token);

        // Assert
        answer.Should().Contain("4");
    }

    [SkippableFact]
    public async Task StreamAsync_WhenAskedArithmetic_StreamsCorrectAnswer()
    {
        // Arrange
        Skip.If(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyVariable)), $"Не задана переменная {ApiKeyVariable}");
        var model = new AiModel(AiProvider.Claude, ModelName);

        // Act
        var chunks = await _systemUnderTests.StreamAsync(model, ChatPrompts.Arithmetic, null, _timeout.Token).ToListAsync();

        // Assert
        string.Concat(chunks.Select(chunk => chunk.Text)).Should().Contain("4");
    }

    [SkippableFact]
    public async Task SendAsync_WithConversationHistory_RemembersContext()
    {
        // Arrange
        Skip.If(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyVariable)), $"Не задана переменная {ApiKeyVariable}");
        var model = new AiModel(AiProvider.Claude, ModelName);

        // Act
        var answer = await _systemUnderTests.SendAsync(model, ChatPrompts.History, null, _timeout.Token);

        // Assert
        answer.Should().ContainEquivalentOf("Барсук");
    }

    public void Dispose()
    {
        _factory.Dispose();
        _timeout.Dispose();
    }
}
