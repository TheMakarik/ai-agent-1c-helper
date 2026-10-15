using Ai1CAgent.Modules.Mcp;
using FluentAssertions;

namespace Ai1CAgent.Tests.Mcp;

public sealed class CommandLineArgumentsTests
{
    [Fact]
    public void Split_WhenArgumentsSeparatedBySpaces_ReturnsEachArgument()
    {
        // Act
        var arguments = CommandLineArguments.Split("-y  @modelcontextprotocol/server-everything");

        // Assert
        arguments.Should().Equal("-y", "@modelcontextprotocol/server-everything");
    }

    [Fact]
    public void Split_WhenPathQuoted_KeepsSpacesAndBackslashes()
    {
        // Act
        var arguments = CommandLineArguments.Split("\"C:\\Users\\Admin\\Новая папка (2)\\server.py\" --port 6001");

        // Assert
        arguments.Should().Equal("C:\\Users\\Admin\\Новая папка (2)\\server.py", "--port", "6001");
    }

    [Fact]
    public void Split_WhenDoubledQuoteInsideQuotes_ReturnsLiteralQuote()
    {
        // Act
        var arguments = CommandLineArguments.Split("\"say \"\"hi\"\"\"");

        // Assert
        arguments.Should().Equal("say \"hi\"");
    }

    [Fact]
    public void Split_WhenEmptyQuotes_ReturnsEmptyArgument()
    {
        // Act
        var arguments = CommandLineArguments.Split("--name \"\"");

        // Assert
        arguments.Should().Equal("--name", string.Empty);
    }

    [Fact]
    public void Split_WhenOnlyWhitespace_ReturnsEmpty()
    {
        // Act
        var arguments = CommandLineArguments.Split("   ");

        // Assert
        arguments.Should().BeEmpty();
    }
}
