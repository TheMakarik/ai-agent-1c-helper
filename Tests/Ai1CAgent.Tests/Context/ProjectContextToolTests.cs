using Ai1CAgent.Modules.Context;
using Ai1CAgent.Modules.Storage;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ai1CAgent.Tests.Context;

public sealed class ProjectContextToolTests : IAsyncLifetime
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private Database _database = null!;

    public async Task InitializeAsync()
    {
        _database = new Database(Path.Join(_directory, "context.db"));
        await _database.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ContextSetThenGet_RoundTripsThroughTools()
    {
        // Arrange
        var projectStore = new SqliteProjectStore(_database);
        var folder = Path.Join(_directory, "project");
        Directory.CreateDirectory(folder);
        var project = await projectStore.AddProjectAsync("Демо", folder, CancellationToken.None);

        var systemUnderTests = new ProjectContextToolProvider(projectStore, A.Dummy<ILogger<ProjectContextToolProvider>>());
        var tools = systemUnderTests.GetTools(project.Id);

        var set = tools.OfType<AIFunction>().Single(tool => tool.Name == "context_set");
        var get = tools.OfType<AIFunction>().Single(tool => tool.Name == "context_get");

        // Act
        await set.InvokeAsync(new AIFunctionArguments { ["context"] = "правила проекта" });
        var result = await get.InvokeAsync(new AIFunctionArguments());

        // Assert
        result.Should().Be("правила проекта");
    }
}
