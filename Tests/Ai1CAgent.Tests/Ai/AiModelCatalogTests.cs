using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Tests.Infrastructure;
using FakeItEasy;
using FluentAssertions;

namespace Ai1CAgent.Tests.Ai;

public sealed class AiModelCatalogTests
{
    [Fact]
    public void GetModels_WhenBuiltInAndCustomProviders_ReturnsAllModelsWithoutDuplicates()
    {
        // Arrange
        var settingsStore = A.Fake<IAiSettingsStore>();
        A.CallTo(() => settingsStore.GetProviders()).Returns(
        [
            new AiProviderSettings
            {
                Provider = AiProvider.Ollama,
                Models = [new CustomModel("qwen3.5:9b"), new CustomModel("llama-local:8b")],
                CustomModels = [new CustomModel("QWEN3.5:9B")],
            },
            new AiProviderSettings
            {
                Name = "Corp Proxy",
                Format = AiApiFormat.Anthropic,
                CustomModels = [new CustomModel("corp-model", AiApiFormat.Anthropic)],
            },
        ]);

        var systemUnderTests = new AiModelCatalog(
            FakeOptions.Create(new OllamaOptions
            {
                Models = ["qwen3.5:9b", "gemma4:e4b"],
            }),
            settingsStore);

        // Act
        var models = systemUnderTests.GetModels();

        // Assert
        models.Should().HaveCount(4);
        models.Should().Contain(new AiModel(AiProvider.Ollama, "qwen3.5:9b"));
        models.Should().Contain(new AiModel(AiProvider.Ollama, "gemma4:e4b"));
        models.Should().Contain(new AiModel(AiProvider.Ollama, "llama-local:8b"));
        models.Should().Contain(new AiModel("Corp Proxy", "corp-model"));
        models.Count(model => model.ProviderKey == nameof(AiProvider.Ollama)).Should().Be(3);
        models.Should().OnlyContain(model => !string.IsNullOrWhiteSpace(model.ProviderKey));
    }
}
