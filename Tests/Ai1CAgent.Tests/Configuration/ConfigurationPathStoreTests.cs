using Ai1CAgent.Modules.Configuration;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Tests.Configuration;

public sealed class ConfigurationPathStoreTests : IDisposable
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _settingsFile;

    public ConfigurationPathStoreTests()
    {
        _settingsFile = Path.Join(_directory, "configuration.json");
    }

    [Fact]
    public void GetLastPath_WhenFileMissing_ReturnsEmpty()
    {
        // Act
        var path = CreateSystemUnderTests().GetLastPath();

        // Assert
        path.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveLastPathAsync_WhenSaved_RestoresAfterReload()
    {
        // Arrange
        const string expected = @"D:\Выгрузки\Курсы";

        // Act
        await CreateSystemUnderTests().SaveLastPathAsync(expected);

        // Assert
        CreateSystemUnderTests().GetLastPath().Should().Be(expected);
    }

    [Fact]
    public void GetLastPath_WhenFileCorrupted_ReturnsEmpty()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFile, "{ broken");

        // Act
        var path = CreateSystemUnderTests().GetLastPath();

        // Assert
        path.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private ConfigurationPathStore CreateSystemUnderTests() =>
        new(A.Dummy<ILogger<ConfigurationPathStore>>(), _settingsFile);
}
