using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Storage;
using FluentAssertions;
using Xunit;

namespace Ai1CAgent.Tests.Storage;

public sealed class ProjectStoreTests : IAsyncLifetime
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _projectFolder;

    private Database _database = null!;

    public ProjectStoreTests() => _projectFolder = Path.Join(_directory, "project");

    public async Task InitializeAsync()
    {
        _database = new Database(Path.Join(_directory, "project.db"));
        await _database.InitializeAsync(CancellationToken.None);

        Directory.CreateDirectory(_projectFolder);
        await File.WriteAllTextAsync(Path.Join(_projectFolder, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Join(_projectFolder, "sub"));
        await File.WriteAllTextAsync(Path.Join(_projectFolder, "sub", "b.bsl"), "Процедура");
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddProjectAsync_IndexesFilesWithRelativePaths()
    {
        // Arrange
        var systemUnderTests = new SqliteProjectStore(_database);

        // Act
        var project = await systemUnderTests.AddProjectAsync("Демо", _projectFolder, CancellationToken.None);

        // Assert
        project.Name.Should().Be("Демо");
        project.Path.Should().Be(_projectFolder);
        project.GlobalContext.Should().BeEmpty();

        var files = await systemUnderTests.GetFilesAsync(project.Id, CancellationToken.None);
        files.Select(file => file.RelativePath)
            .Should().BeEquivalentTo("a.txt", Path.Join("sub", "b.bsl"));
    }

    [Fact]
    public async Task IndexFilesAsync_WhenNewFileAppears_UpdatesIndex()
    {
        // Arrange
        var systemUnderTests = new SqliteProjectStore(_database);
        var project = await systemUnderTests.AddProjectAsync("Демо", _projectFolder, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Join(_projectFolder, "c.md"), "content");

        // Act
        var files = await systemUnderTests.IndexFilesAsync(project.Id, CancellationToken.None);

        // Assert
        files.Select(file => file.RelativePath).Should().Contain("c.md");
    }

    [Fact]
    public async Task IndexFilesAsync_WhenFolderMissing_Throws()
    {
        // Arrange
        var systemUnderTests = new SqliteProjectStore(_database);
        var project = await systemUnderTests.AddProjectAsync("Демо", _projectFolder, CancellationToken.None);
        Directory.Delete(_projectFolder, recursive: true);

        // Act
        var act = () => systemUnderTests.IndexFilesAsync(project.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GlobalContext_RoundTrips()
    {
        // Arrange
        var systemUnderTests = new SqliteProjectStore(_database);
        var project = await systemUnderTests.AddProjectAsync("Демо", _projectFolder, CancellationToken.None);

        // Act
        await systemUnderTests.SetGlobalContextAsync(project.Id, "важный контекст", CancellationToken.None);

        // Assert
        (await systemUnderTests.GetGlobalContextAsync(project.Id, CancellationToken.None)).Should().Be("важный контекст");
    }

    [Fact]
    public async Task RenameProjectAsync_UpdatesNameOnly()
    {
        // Arrange
        var systemUnderTests = new SqliteProjectStore(_database);
        var project = await systemUnderTests.AddProjectAsync("Демо", _projectFolder, CancellationToken.None);

        // Act
        await systemUnderTests.RenameProjectAsync(project.Id, "Новое имя", CancellationToken.None);

        // Assert
        var reloaded = await systemUnderTests.GetProjectAsync(project.Id, CancellationToken.None);
        reloaded!.Name.Should().Be("Новое имя");
        reloaded.Path.Should().Be(_projectFolder);
    }
}
