using System.Text.Json;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Ai;
using FluentAssertions;

namespace Ai1CAgent.Tests.Ai;

public sealed class AiSettingsJsonTests
{
    private const string LegacyJson = """
        [
          {
            "Provider": "DeepSeek",
            "Model": "deepseek-flash",
            "Endpoint": "https://api.deepseek.com",
            "ApiKey": "plain-text-key"
          },
          {
            "Provider": "Claude",
            "Model": "claude-opus-5-5"
          }
        ]
        """;

    [Fact]
    public void Deserialize_WhenJsonFromOldVersion_UsesDefaultsForNewFields()
    {
        // Act
        var persisted = JsonSerializer.Deserialize(LegacyJson, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        persisted.Should().HaveCount(2);

        var deepSeek = persisted![0];
        deepSeek.Provider.Should().Be("DeepSeek");
        deepSeek.Name.Should().BeEmpty();
        deepSeek.Format.Should().BeEmpty();
        deepSeek.Model.Should().Be("deepseek-flash");
        deepSeek.Endpoint.Should().Be("https://api.deepseek.com");
        deepSeek.ProtectedApiKey.Should().BeEmpty();
        deepSeek.Models.Should().BeNull();
        deepSeek.SelectedModel.Should().BeNull();
        deepSeek.CustomModels.Should().BeNull();
        deepSeek.IsCustomEndpoint.Should().BeFalse();
        deepSeek.CustomEndpoint.Should().BeEmpty();

        var claude = persisted[1];
        claude.Provider.Should().Be("Claude");
        claude.Endpoint.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_WhenJsonHasLegacyPlaintextApiKey_KeepsItOutOfProtectedField()
    {
        // Act
        var persisted = JsonSerializer.Deserialize(LegacyJson, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        persisted![0].ApiKey.Should().Be("plain-text-key");
        persisted[0].ProtectedApiKey.Should().BeEmpty();
        persisted.Should().NotContain(item => !string.IsNullOrEmpty(item.ProtectedApiKey));
    }

    [Fact]
    public void RoundTrip_WhenAllFieldsSet_KeepsValues()
    {
        // Arrange
        List<PersistedProvider> original =
        [
            new()
            {
                Provider = "Claude",
                Name = string.Empty,
                Format = "OpenAi",
                Model = "claude-opus-5-5",
                Endpoint = "https://api.anthropic.com",
                ProtectedApiKey = "encrypted-key",
                Models =
                [
                    new PersistedCustomModel { Name = "model-a" },
                    new PersistedCustomModel { Name = "model-b", Format = AiApiFormat.Anthropic },
                ],
                SelectedModel = new PersistedCustomModel { Name = "model-a", Format = AiApiFormat.Anthropic },
                CustomModels =
                [
                    new PersistedCustomModel
                    {
                        Name = "custom-anthropic",
                        Format = AiApiFormat.Anthropic,
                        IsUserAdded = true,
                    },
                ],
                IsCustomEndpoint = true,
                CustomEndpoint = "https://proxy.example.com",
            },
            new()
            {
                Provider = null,
                Name = "Corp Proxy",
                Format = "Anthropic",
                ProtectedApiKey = "encrypted-key-2",
            },
        ];

        // Act
        var json = JsonSerializer.Serialize(original, AiSettingsJsonContext.Default.ListPersistedProvider);
        var restored = JsonSerializer.Deserialize(json, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        json.Should().Contain("\"Claude\"");
        json.Should().Contain("\"Anthropic\"");
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Deserialize_WhenModelsAreLegacyStrings_ReadsNamesWithDefaultFormat()
    {
        // Arrange
        const string json = """
            [
              {
                "Provider": "Claude",
                "Models": ["claude-opus-5-5", "claude-sonnet-5-5"]
              }
            ]
            """;

        // Act
        var persisted = JsonSerializer.Deserialize(json, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        var provider = persisted.Should().ContainSingle().Subject;
        provider.Models.Should().NotBeNull();
        provider.Models!.Select(model => model.Name).Should().Equal("claude-opus-5-5", "claude-sonnet-5-5");
        provider.Models.Should().OnlyContain(model => model.Format == AiApiFormat.OpenAi);
    }

    [Fact]
    public void Serialize_WhenModelsHaveFormats_WritesObjectsWithFormat()
    {
        // Arrange
        List<PersistedProvider> original =
        [
            new()
            {
                Provider = "Claude",
                Models = [new PersistedCustomModel { Name = "claude-opus-5-5", Format = AiApiFormat.Anthropic }],
            },
        ];

        // Act
        var json = JsonSerializer.Serialize(original, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        json.Should().Contain("\"Name\": \"claude-opus-5-5\"");
        json.Should().Contain("\"Format\": \"Anthropic\"");
    }

    [Fact]
    public void Deserialize_WhenProviderValueRemovedFromEnum_KeepsRawString()
    {
        // Arrange
        const string json = """
            [
              {
                "Provider": "OpenAiCompatible",
                "Model": "gpt-5",
                "Endpoint": "",
                "ProtectedApiKey": ""
              }
            ]
            """;

        // Act
        var persisted = JsonSerializer.Deserialize(json, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        var provider = persisted.Should().ContainSingle().Subject;
        provider.Provider.Should().Be("OpenAiCompatible");
        provider.Name.Should().BeEmpty();
        provider.Format.Should().BeEmpty();
        provider.ProtectedApiKey.Should().BeEmpty();
        provider.Models.Should().BeNull();
        provider.CustomModels.Should().BeNull();
        provider.IsCustomEndpoint.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_WhenCustomProviderFieldsPresent_KeepsNameAndFormat()
    {
        // Arrange
        const string json = """
            [
              {
                "Provider": null,
                "Name": "Corp Proxy",
                "Format": "Anthropic",
                "Endpoint": "https://llm.corp.example"
              }
            ]
            """;

        // Act
        var persisted = JsonSerializer.Deserialize(json, AiSettingsJsonContext.Default.ListPersistedProvider);

        // Assert
        var provider = persisted.Should().ContainSingle().Subject;
        provider.Provider.Should().BeNull();
        provider.Name.Should().Be("Corp Proxy");
        provider.Format.Should().Be("Anthropic");
        provider.Endpoint.Should().Be("https://llm.corp.example");
    }
}
