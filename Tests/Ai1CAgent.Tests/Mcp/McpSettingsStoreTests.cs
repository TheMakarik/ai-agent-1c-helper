using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Tests.Mcp;

public sealed class McpSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _settingsFile;

    public McpSettingsStoreTests()
    {
        _settingsFile = Path.Join(_directory, "mcp-server.json");
    }

    [Fact]
    public void Get_WhenFileMissing_ReturnsNotConfigured()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var settings = systemUnderTests.Get();

        // Assert
        settings.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenStdioSettingsSaved_RoundTripsAfterReload()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var expected = new McpServerSettings
        {
            Transport = McpTransportType.Stdio,
            Endpoint = "http://localhost:6001/mcp",
            Command = "python",
            Arguments = "\"C:\\mcp 1c\\server.py\" --verbose",
        };

        // Act
        await systemUnderTests.SaveAsync(expected);

        // Assert
        CreateSystemUnderTests().Get().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task SaveAsync_WhenSaved_RaisesSettingsChanged()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var raised = false;
        systemUnderTests.SettingsChanged += (_, _) => raised = true;

        // Act
        await systemUnderTests.SaveAsync(new McpServerSettings { Endpoint = "http://localhost:6001/mcp" });

        // Assert
        raised.Should().BeTrue();
    }

    [Fact]
    public void Get_WhenReturnedSettingsModified_DoesNotChangeStoredSettings()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var settings = systemUnderTests.Get();

        // Act
        settings.Endpoint = "http://localhost:6001/mcp";

        // Assert
        systemUnderTests.Get().Endpoint.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenTransportUnknown_FallsBackToHttp()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, """{ "Transport": "Pigeon", "Endpoint": "http://localhost:6001/mcp" }""");

        // Act
        var settings = CreateSystemUnderTests().Get();

        // Assert
        settings.Transport.Should().Be(McpTransportType.Http);
    }

    [Fact]
    public void Get_WhenFileCorrupted_QuarantinesFileAndReturnsNotConfigured()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, "{ this is not valid json }");

        // Act
        var settings = CreateSystemUnderTests().Get();

        // Assert
        settings.IsConfigured.Should().BeFalse();
        File.Exists(_settingsFile + ".broken").Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private McpSettingsStore CreateSystemUnderTests() =>
        new(A.Dummy<ILogger<McpSettingsStore>>(), _settingsFile);
}
