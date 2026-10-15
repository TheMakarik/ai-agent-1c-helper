using System.Text.Json;
using Ai1CAgent.Modules.Configuration;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Tests.Configuration;

public sealed class ConfigurationToolProviderTests : IDisposable
{
    private readonly string _root = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"),
        "Выгрузка Курсы");

    private readonly ConfigurationToolProvider _systemUnderTests =
        new(A.Dummy<ILogger<ConfigurationToolProvider>>());

    public ConfigurationToolProviderTests()
    {
        WriteFile("Configuration.xml", "<MetaDataObject />");
        WriteFile("Catalogs/Склады.xml", "<Catalog><Name>Склады</Name></Catalog>");
        WriteFile(
            "Documents/ПродажаТоваров/Ext/ObjectModule.bsl",
            "Процедура ОбработкаПроведения(Отказ, Режим)\n\tДвижения.ТоварыНаСкладах.Записать();\nКонецПроцедуры");
    }

    [Fact]
    public void IsConfigurationDump_WhenConfigurationXmlPresent_ReturnsTrue()
    {
        // Act
        var isDump = _systemUnderTests.IsConfigurationDump(_root);

        // Assert
        isDump.Should().BeTrue();
    }

    [Fact]
    public void IsConfigurationDump_WhenConfigurationXmlMissing_ReturnsFalse()
    {
        // Act
        var isDump = _systemUnderTests.IsConfigurationDump(Path.Join(_root, "Catalogs"));

        // Assert
        isDump.Should().BeFalse();
    }

    [Fact]
    public async Task ListFiles_WhenRoot_ReturnsFoldersAndFiles()
    {
        // Act
        var result = await InvokeAsync("list_files", new AIFunctionArguments());

        // Assert
        result.Should().Contain("Catalogs/").And.Contain("Documents/").And.Contain("Configuration.xml");
    }

    [Fact]
    public async Task ReadFile_WhenOffsetAndLimitAsJson_ReturnsNumberedLines()
    {
        // Arrange
        var arguments = new AIFunctionArguments
        {
            ["path"] = Json("\"Documents/ПродажаТоваров/Ext/ObjectModule.bsl\""),
            ["offset"] = Json("2"),
            ["limit"] = Json("1"),
        };

        // Act
        var result = await InvokeAsync("read_file", arguments);

        // Assert
        result.Should().Contain("2: \tДвижения.ТоварыНаСкладах.Записать();").And.NotContain("1: Процедура");
    }

    [Fact]
    public async Task SearchFiles_WhenPatternIsBsl_FindsOnlyModules()
    {
        // Arrange
        var arguments = new AIFunctionArguments { ["query"] = "товары", ["file_pattern"] = "*.bsl" };

        // Act
        var result = await InvokeAsync("search_files", arguments);

        // Assert
        result.Should().Be("Documents/ПродажаТоваров/Ext/ObjectModule.bsl:2: Движения.ТоварыНаСкладах.Записать();" + Environment.NewLine);
    }

    [Fact]
    public async Task ReadFile_WhenPathEscapesRoot_ReturnsError()
    {
        // Arrange
        WriteFile("../secret.txt", "секрет");
        var arguments = new AIFunctionArguments { ["path"] = "../secret.txt" };

        // Act
        var result = await InvokeAsync("read_file", arguments);

        // Assert
        result.Should().StartWith("Ошибка").And.NotContain("секрет");
    }

    [Fact]
    public async Task ReadFile_WhenFileMissing_ReturnsError()
    {
        // Arrange
        var arguments = new AIFunctionArguments { ["path"] = "Catalogs/Нет.xml" };

        // Act
        var result = await InvokeAsync("read_file", arguments);

        // Assert
        result.Should().StartWith("Ошибка");
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_root)!;

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private async Task<string> InvokeAsync(string name, AIFunctionArguments arguments)
    {
        var function = _systemUnderTests.GetTools(_root).OfType<AIFunction>().Single(tool => tool.Name == name);
        var result = await function.InvokeAsync(arguments);
        return result as string ?? string.Empty;
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.GetFullPath(Path.Join(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
