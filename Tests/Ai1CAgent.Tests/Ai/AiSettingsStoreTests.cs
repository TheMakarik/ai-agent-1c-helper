using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Tests.Infrastructure;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Tests.Ai;

public sealed class AiSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _settingsFile;
    private readonly string _keyFile;

    public AiSettingsStoreTests()
    {
        _settingsFile = Path.Join(_directory, "ai-providers.json");
        _keyFile = Path.Join(_directory, "secret.key");
    }

    [Fact]
    public void GetProviders_WhenFileAndDefaultsMissing_ReturnsAllBuiltInProviders()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var providers = systemUnderTests.GetProviders();

        // Assert
        providers.Should().HaveCount(AiProviderDescriptors.All.Count);

        for (var index = 0; index < AiProviderDescriptors.All.Count; index++)
            providers[index].Provider.Should().Be(AiProviderDescriptors.All[index]);

        providers.Should().OnlyContain(settings => !settings.IsCustom);
    }

    [Fact]
    public void GetProviders_WhenLegacyJsonOnDisk_MigratesBuiltInsAndCustomProvider()
    {
        // Arrange
        const string legacyJson = """
            [
              {
                "Provider": "DeepSeek",
                "Model": "deepseek-flash",
                "Endpoint": "https://api.deepseek.com",
                "ApiKey": "legacy-plain-key"
              },
              {
                "Provider": "OpenAiCompatible",
                "Model": "gpt-5",
                "Endpoint": "https://corp.example/v1"
              }
            ]
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, legacyJson);

        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var providers = systemUnderTests.GetProviders();

        // Assert
        providers.Should().HaveCount(AiProviderDescriptors.All.Count + 1);

        var deepSeek = providers[1];
        deepSeek.Provider.Should().Be(AiProvider.DeepSeek);
        deepSeek.Model.Should().Be("deepseek-flash");
        deepSeek.Endpoint.Should().Be("https://api.deepseek.com");
        deepSeek.ApiKey.Should().Be("legacy-plain-key");

        var custom = providers[^1];
        custom.Provider.Should().BeNull();
        custom.Name.Should().Be("OpenAiCompatible");
        custom.Endpoint.Should().Be("https://corp.example/v1");
        custom.IsCustom.Should().BeTrue();
    }

    [Fact]
    public async Task SaveProvidersAsync_WhenCustomProviderSaved_RoundTripsAndProtectsSecret()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        List<AiProviderSettings> providers = [.. systemUnderTests.GetProviders()];
        providers.Add(new AiProviderSettings
        {
            Name = "Corp Proxy",
            Format = AiApiFormat.Anthropic,
            Endpoint = "https://llm.corp.example",
            ApiKey = "super-secret",
            CustomModels = [new CustomModel("corp-model", AiApiFormat.Anthropic)],
        });

        // Act
        await systemUnderTests.SaveProvidersAsync(providers);

        // Assert
        var json = await File.ReadAllTextAsync(_settingsFile);
        json.Should().NotContain("super-secret");
        json.Should().NotContain("\"ApiKey\"");
        json.Should().Contain("Corp Proxy");

        var restored = CreateSystemUnderTests().GetProviders();
        restored.Should().HaveCount(providers.Count);

        var restoredCustom = restored[^1];
        restoredCustom.Provider.Should().BeNull();
        restoredCustom.Name.Should().Be("Corp Proxy");
        restoredCustom.Format.Should().Be(AiApiFormat.Anthropic);
        restoredCustom.Endpoint.Should().Be("https://llm.corp.example");
        restoredCustom.ApiKey.Should().Be("super-secret");
        restoredCustom.CustomModels.Should()
            .ContainSingle()
            .Which.Should().Be(new CustomModel("corp-model", AiApiFormat.Anthropic));
    }

    [Fact]
    public void GetProviders_WhenLegacyPlaintextApiKeyOnDisk_ReadsIt()
    {
        // Arrange
        const string legacyJson = """
            [
              {
                "Provider": "DeepSeek",
                "Model": "deepseek-flash",
                "ApiKey": "legacy-plain-key"
              }
            ]
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, legacyJson);

        // Act
        var providers = CreateSystemUnderTests().GetProviders();

        // Assert
        providers[1].ApiKey.Should().Be("legacy-plain-key");
    }

    [Fact]
    public void GetProviders_WhenDuplicateCustomNames_DeduplicatesFirstWins()
    {
        // Arrange
        const string json = """
            [
              { "Provider": null, "Name": "Corp", "Endpoint": "https://first.example" },
              { "Provider": null, "Name": "corp", "Endpoint": "https://second.example" }
            ]
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, json);

        // Act
        var providers = CreateSystemUnderTests().GetProviders();

        // Assert
        var customs = providers.Where(provider => provider.IsCustom).ToList();
        customs.Should().ContainSingle();
        customs[0].Name.Should().Be("Corp");
        customs[0].Endpoint.Should().Be("https://first.example");
    }

    [Fact]
    public void GetProviders_WhenCustomNameMatchesBuiltInDisplayName_SkipsIt()
    {
        // Arrange
        const string json = """
            [
              { "Provider": null, "Name": "Claude", "Endpoint": "https://fake.example" }
            ]
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, json);

        // Act
        var providers = CreateSystemUnderTests().GetProviders();

        // Assert
        providers.Should().HaveCount(AiProviderDescriptors.All.Count);
        providers.Should().OnlyContain(provider => !provider.IsCustom);
    }

    [Fact]
    public void GetProviders_WhenBuiltInModelsAndLegacyModelEmpty_FillsModelsFromDefaults()
    {
        // Arrange
        const string json = """
            [
              { "Provider": "DeepSeek" }
            ]
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, json);

        var defaults = new AiOptions
        {
            Providers =
            [
                new AiProviderSettings
                {
                    Provider = AiProvider.DeepSeek,
                    Models = [new CustomModel("deepseek-flash"), new CustomModel("deepseek-v4-pro")],
                },
            ],
        };

        // Act
        var providers = CreateSystemUnderTests(defaults).GetProviders();

        // Assert
        var deepSeek = providers.Single(provider => provider.Provider == AiProvider.DeepSeek);
        deepSeek.Models.Select(model => model.Name).Should().Equal("deepseek-flash", "deepseek-v4-pro");
    }

    [Fact]
    public async Task SaveProvidersAsync_WhenClaudeModelHasAnthropicFormat_PreservesFormatAfterReload()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        List<AiProviderSettings> providers = [.. systemUnderTests.GetProviders()];
        var claude = providers.Single(provider => provider.Provider == AiProvider.Claude);
        claude.Models = [new CustomModel("claude-opus-5-5", AiApiFormat.Anthropic)];
        claude.SelectedModel = new CustomModel("claude-opus-5-5", AiApiFormat.Anthropic);

        // Act
        await systemUnderTests.SaveProvidersAsync(providers);

        // Assert
        var restored = CreateSystemUnderTests().GetProviders()
            .Single(provider => provider.Provider == AiProvider.Claude);
        restored.Models.Should().ContainSingle()
            .Which.Should().Be(new CustomModel("claude-opus-5-5", AiApiFormat.Anthropic));
        restored.SelectedModel.Should().Be(new CustomModel("claude-opus-5-5", AiApiFormat.Anthropic));
    }

    [Fact]
    public void GetProviders_WhenSettingsFileCorrupted_QuarantinesFileAndReturnsBuiltIns()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, "{ this is not valid json }");

        // Act
        var providers = CreateSystemUnderTests().GetProviders();

        // Assert
        providers.Should().HaveCount(AiProviderDescriptors.All.Count);
        File.Exists(_settingsFile + ".broken").Should().BeTrue();
        File.Exists(_settingsFile).Should().BeFalse();
    }

    [Fact]
    public void GetProviders_WhenCustomProviderHasNoKey_DoesNotRequireIt()
    {
        // Arrange
        const string json = """
            [
              { "Provider": null, "Name": "Corp", "Endpoint": "https://llm.corp.example", "ApiKey": "" }
            ]
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, json);

        // Act
        var providers = CreateSystemUnderTests().GetProviders();

        // Assert
        var custom = providers.Single(provider => provider.IsCustom);
        custom.ResolvedApiKey().Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private AiSettingsStore CreateSystemUnderTests(AiOptions? defaults = null) =>
        new(
            FakeOptions.Create(defaults ?? new AiOptions()),
            new AesGcmSecretProtector(A.Dummy<ILogger<AesGcmSecretProtector>>(), _keyFile),
            A.Dummy<ILogger<AiSettingsStore>>(),
            _settingsFile);
}
