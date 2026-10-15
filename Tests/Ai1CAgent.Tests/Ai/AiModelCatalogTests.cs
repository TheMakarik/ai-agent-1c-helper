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
                Provider = AiProvider.DeepSeek,
                Models = [new CustomModel("deepseek-flash"), new CustomModel("deepseek-v4-pro")],
                CustomModels = [new CustomModel("DEEPSEEK-FLASH")],
            },
            new AiProviderSettings
            {
                Provider = AiProvider.Claude,
                SelectedModel = new CustomModel("claude-sonnet-5-5"),
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
        models.Should().HaveCount(6);
        models.Should().Contain(new AiModel(AiProvider.Ollama, "qwen3.5:9b"));
        models.Should().Contain(new AiModel(AiProvider.DeepSeek, "deepseek-v4-pro"));
        models.Should().Contain(new AiModel(AiProvider.Claude, "claude-sonnet-5-5"));
        models.Should().Contain(new AiModel("Corp Proxy", "corp-model"));
        models.Count(model => model.ProviderKey == "DeepSeek").Should().Be(2);
        models.Should().OnlyContain(model => !string.IsNullOrWhiteSpace(model.ProviderKey));
    }
}
