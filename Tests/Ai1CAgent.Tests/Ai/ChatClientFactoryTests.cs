using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Tests.Infrastructure;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ai1CAgent.Tests.Ai;

public sealed class ChatClientFactoryTests
{
    [Fact]
    public void Resolve_WhenCustomProviderAndCustomAnthropicModel_UsesCustomModelFormatAndProviderEndpointAndKey()
    {
        // Arrange
        var store = CreateStore(new AiProviderSettings
        {
            Name = "Corp Proxy",
            Format = AiApiFormat.OpenAi,
            Endpoint = "https://llm.corp.example",
            ApiKey = "corp-secret",
            CustomModels =
            [
                new CustomModel("corp-anthropic", AiApiFormat.Anthropic),
                new CustomModel("corp-openai", AiApiFormat.OpenAi),
            ],
        });

        using var factory = CreateFactory(store);

        // Act
        var resolved = factory.Resolve(new AiModel("Corp Proxy", "corp-anthropic"));

        // Assert
        resolved.Settings.Name.Should().Be("Corp Proxy");
        resolved.Format.Should().Be(AiApiFormat.Anthropic);
        resolved.Endpoint.Should().Be("https://llm.corp.example");
        resolved.ApiKey.Should().Be("corp-secret");
    }

    [Fact]
    public void Resolve_WhenCustomModelHasOpenAiFormat_UsesOpenAiFormat()
    {
        // Arrange
        var store = CreateStore(new AiProviderSettings
        {
            Name = "Corp Proxy",
            Format = AiApiFormat.Anthropic,
            Endpoint = "https://llm.corp.example",
            ApiKey = "corp-secret",
            CustomModels = [new CustomModel("corp-openai", AiApiFormat.OpenAi)],
        });

        using var factory = CreateFactory(store);

        // Act
        var resolved = factory.Resolve(new AiModel("Corp Proxy", "corp-openai"));

        // Assert
        resolved.Format.Should().Be(AiApiFormat.OpenAi);
    }

    [Fact]
    public void Resolve_WhenModelNotInCustomModels_FallsBackToProviderFormat()
    {
        // Arrange
        var store = CreateStore(new AiProviderSettings
        {
            Name = "Corp Proxy",
            Format = AiApiFormat.Anthropic,
            Endpoint = "https://llm.corp.example",
            ApiKey = "corp-secret",
            CustomModels = [new CustomModel("corp-openai", AiApiFormat.OpenAi)],
        });

        using var factory = CreateFactory(store);

        // Act
        var resolved = factory.Resolve(new AiModel("Corp Proxy", "other-model"));

        // Assert
        resolved.Format.Should().Be(AiApiFormat.Anthropic);
    }

    [Fact]
    public void Resolve_WhenBuiltInProviderMatchedByEnumKey_ReturnsDescriptorFormatAndDefaultEndpoint()
    {
        // Arrange
        var store = CreateStore(new AiProviderSettings
        {
            Provider = AiProvider.Claude,
            ApiKey = "claude-secret",
        });

        using var factory = CreateFactory(store);

        // Act
        var resolved = factory.Resolve(new AiModel(AiProvider.Claude, "claude-opus-5-5"));

        // Assert
        resolved.Format.Should().Be(AiApiFormat.Anthropic);
        resolved.Endpoint.Should().Be("https://api.anthropic.com");
        resolved.ApiKey.Should().Be("claude-secret");
    }

    [Fact]
    public void Resolve_WhenCustomEndpointEnabled_PrefersCustomEndpoint()
    {
        // Arrange
        var store = CreateStore(new AiProviderSettings
        {
            Name = "Corp Proxy",
            Endpoint = "https://llm.corp.example",
            IsCustomEndpoint = true,
            CustomEndpoint = "https://proxy.example/v1",
            ApiKey = "corp-secret",
        });

        using var factory = CreateFactory(store);

        // Act
        var resolved = factory.Resolve(new AiModel("Corp Proxy", "corp-model"));

        // Assert
        resolved.Endpoint.Should().Be("https://proxy.example/v1");
    }

    [Fact]
    public void ResolveOllamaEndpoint_WhenNoSettings_UsesOllamaOptions()
    {
        // Act
        var endpoint = ChatClientFactory.ResolveOllamaEndpoint(
            null,
            new OllamaOptions { Endpoint = "http://localhost:11434" });

        // Assert
        endpoint.Should().Be(new Uri("http://localhost:11434/v1"));
    }

    [Fact]
    public void ResolveOllamaEndpoint_WhenSettingsHaveDefaultEndpoint_UsesOllamaOptions()
    {
        // Arrange
        var settings = new AiProviderSettings { Provider = AiProvider.Ollama };

        // Act
        var endpoint = ChatClientFactory.ResolveOllamaEndpoint(
            settings,
            new OllamaOptions { Endpoint = "http://ollama.internal:11434" });

        // Assert
        endpoint.Should().Be(new Uri("http://ollama.internal:11434/v1"));
    }

    [Fact]
    public void ResolveOllamaEndpoint_WhenSettingsHaveCustomEndpoint_UsesSettingsEndpoint()
    {
        // Arrange
        var settings = new AiProviderSettings
        {
            Provider = AiProvider.Ollama,
            Endpoint = "http://custom.ollama:1234/v1",
        };

        // Act
        var endpoint = ChatClientFactory.ResolveOllamaEndpoint(settings, new OllamaOptions());

        // Assert
        endpoint.Should().Be(new Uri("http://custom.ollama:1234/v1"));
    }

    [Fact]
    public void GetClient_WhenOllamaWithoutSettings_DoesNotRequireApiKey()
    {
        // Arrange
        using var factory = CreateFactory(CreateStore());

        // Act
        var client = factory.GetClient(new AiModel(AiProvider.Ollama, "qwen3.5:9b"));

        // Assert
        client.Should().NotBeNull();
    }

    private static ChatClientFactory CreateFactory(IAiSettingsStore store) =>
        new(
            FakeOptions.Create(new OllamaOptions()),
            FakeOptions.Create(new AiOptions { MaxOutputTokens = 16000 }),
            store,
            NullLoggerFactory.Instance,
            A.Dummy<ILogger<ChatClientFactory>>());

    private static IAiSettingsStore CreateStore(params AiProviderSettings[] providers)
    {
        var store = A.Fake<IAiSettingsStore>();
        A.CallTo(() => store.GetProviders()).Returns(providers);
        return store;
    }
}
