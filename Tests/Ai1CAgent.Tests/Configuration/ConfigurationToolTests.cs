using Ai1CAgent.Modules.Configuration;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ai1CAgent.Tests.Configuration;

public sealed class ConfigurationToolTests : IDisposable
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    public ConfigurationToolTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Join(_directory, "Configuration.xml"), "<Configuration/>");
        File.WriteAllText(Path.Join(_directory, "CommonModule.bsl"), "Процедура Тест()\nКонецПроцедуры");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void GetTools_ReturnsReadOnlyConfigurationTools()
    {
        // Arrange
        var systemUnderTests = new ConfigurationToolProvider(A.Dummy<ILogger<ConfigurationToolProvider>>());

        // Act
        var tools = systemUnderTests.GetTools(_directory);

        // Assert
        tools.Should().AllBeAssignableTo<AIFunction>();
        tools.Select(tool => tool.Name).Should()
            .BeEquivalentTo("list_files", "read_file", "search_files", "find_files", "file_info");
    }

    [Fact]
    public async Task FindFiles_WithMask_ReturnsMatchingFile()
    {
        // Arrange
        var systemUnderTests = new ConfigurationToolProvider(A.Dummy<ILogger<ConfigurationToolProvider>>());
        var find = systemUnderTests.GetTools(_directory).OfType<AIFunction>().Single(tool => tool.Name == "find_files");

        // Act
        var result = await find.InvokeAsync(new AIFunctionArguments { ["pattern"] = "*.bsl" });

        // Assert
        result.Should().BeOfType<string>().Which.Should().Contain("CommonModule.bsl");
    }

    [Fact]
    public async Task FileInfo_ReturnsSizeAndTimestamp()
    {
        // Arrange
        var systemUnderTests = new ConfigurationToolProvider(A.Dummy<ILogger<ConfigurationToolProvider>>());
        var info = systemUnderTests.GetTools(_directory).OfType<AIFunction>().Single(tool => tool.Name == "file_info");

        // Act
        var result = await info.InvokeAsync(new AIFunctionArguments { ["path"] = "CommonModule.bsl" });

        // Assert
        result.Should().BeOfType<string>().Which.Should().Contain("CommonModule.bsl");
    }
}
