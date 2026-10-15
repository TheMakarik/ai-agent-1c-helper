using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Tests.Infrastructure;
using FluentAssertions;

namespace Ai1CAgent.Tests.Ai;

[Collection(LocalModelsCollection.Name)]
public sealed class OllamaChatTests : IDisposable
{
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromMinutes(3));
    private readonly AiChatService _systemUnderTests;
    private readonly ChatClientFactory _factory;

    public OllamaChatTests()
    {
        (_systemUnderTests, _factory) = ChatServiceBuilder.Build(ollamaOptions: new OllamaOptions { Endpoint = OllamaProbe.Endpoint });
    }

    [SkippableTheory]
    [InlineData("qwen3.5:9b")]
    [InlineData("gemma4:e4b")]
    public async Task SendAsync_WhenAskedArithmetic_ReturnsCorrectAnswer(string modelName)
    {
        // Arrange
        Skip.IfNot(await OllamaProbe.HasModelAsync(modelName, _timeout.Token), $"В Ollama нет модели {modelName}");
        var model = new AiModel(AiProvider.Ollama, modelName);

        // Act
        var answer = await _systemUnderTests.SendAsync(model, ChatPrompts.Arithmetic, _timeout.Token);

        // Assert
        answer.Should().Contain("4");
    }

    [SkippableTheory]
    [InlineData("qwen3.5:9b")]
    [InlineData("gemma4:e4b")]
    public async Task StreamAsync_WhenAskedArithmetic_StreamsCorrectAnswer(string modelName)
    {
        // Arrange
        Skip.IfNot(await OllamaProbe.HasModelAsync(modelName, _timeout.Token), $"В Ollama нет модели {modelName}");
        var model = new AiModel(AiProvider.Ollama, modelName);

        // Act
        var chunks = await _systemUnderTests.StreamAsync(model, ChatPrompts.Arithmetic, _timeout.Token).ToListAsync();

        // Assert
        string.Concat(chunks.Select(chunk => chunk.Text)).Should().Contain("4");
    }

    [SkippableTheory]
    [InlineData("qwen3.5:9b")]
    [InlineData("gemma4:e4b")]
    public async Task SendAsync_WithConversationHistory_RemembersContext(string modelName)
    {
        // Arrange
        Skip.IfNot(await OllamaProbe.HasModelAsync(modelName, _timeout.Token), $"В Ollama нет модели {modelName}");
        var model = new AiModel(AiProvider.Ollama, modelName);

        // Act
        var answer = await _systemUnderTests.SendAsync(model, ChatPrompts.History, _timeout.Token);

        // Assert
        answer.Should().ContainEquivalentOf("Барсук");
    }

    public void Dispose()
    {
        _factory.Dispose();
        _timeout.Dispose();
    }
}
