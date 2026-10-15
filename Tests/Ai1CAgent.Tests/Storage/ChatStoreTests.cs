using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Storage;
using FluentAssertions;
using Xunit;

namespace Ai1CAgent.Tests.Storage;

public sealed class ChatStoreTests : IAsyncLifetime
{
    private readonly string _directory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private Database _database = null!;

    public async Task InitializeAsync()
    {
        _database = new Database(Path.Join(_directory, "chat.db"));
        await _database.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateAndLoadMessages_RoundTripsInOrderWithUsage()
    {
        // Arrange
        var systemUnderTests = new SqliteChatStore(_database);
        var session = await systemUnderTests.CreateSessionAsync(null, "Диалог", CancellationToken.None);

        await systemUnderTests.AppendMessageAsync(
            Message(session.Id, AiChatRole.User, "Привет", string.Empty),
            CancellationToken.None);
        await systemUnderTests.AppendMessageAsync(
            Message(session.Id, AiChatRole.Assistant, "Ответ", "думал") with { InputTokens = 10, OutputTokens = 20, TotalTokens = 30 },
            CancellationToken.None);

        // Act
        var messages = new List<ChatMessageRecord>();

        await foreach (var message in systemUnderTests.GetMessagesAsync(session.Id, CancellationToken.None))
            messages.Add(message);

        // Assert
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(AiChatRole.User);
        messages[0].Text.Should().Be("Привет");
        messages[1].Reasoning.Should().Be("думал");
        messages[1].Usage.Should().Be(new AiTokenUsage(10, 20, 30));
    }

    [Fact]
    public async Task GetSessionsAsync_WhenProjectScoped_ReturnsOnlyMatchingAndGlobal()
    {
        // Arrange
        var systemUnderTests = new SqliteChatStore(_database);
        var projectId = await AddProjectAsync(systemUnderTests);

        var global = await systemUnderTests.CreateSessionAsync(null, "Глобальный", CancellationToken.None);
        var scoped = await systemUnderTests.CreateSessionAsync(projectId, "Проектный", CancellationToken.None);
        var other = await systemUnderTests.CreateSessionAsync(null, "Другой", CancellationToken.None);

        // Act
        var globalSessions = await systemUnderTests.GetSessionsAsync(null, CancellationToken.None);
        var projectSessions = await systemUnderTests.GetSessionsAsync(projectId, CancellationToken.None);

        // Assert
        globalSessions.Select(session => session.Id)
            .Should().Contain(global.Id).And.Contain(other.Id).And.NotContain(scoped.Id);
        projectSessions.Should().ContainSingle().Which.Id.Should().Be(scoped.Id);
    }

    [Fact]
    public async Task RenameSessionAsync_UpdatesTitle()
    {
        // Arrange
        var systemUnderTests = new SqliteChatStore(_database);
        var session = await systemUnderTests.CreateSessionAsync(null, "Старое", CancellationToken.None);

        // Act
        await systemUnderTests.RenameSessionAsync(session.Id, "Новое", CancellationToken.None);

        // Assert
        var reloaded = await systemUnderTests.GetSessionAsync(session.Id, CancellationToken.None);
        reloaded!.Title.Should().Be("Новое");
    }

    [Fact]
    public async Task DeleteSessionAsync_CascadesMessages()
    {
        // Arrange
        var systemUnderTests = new SqliteChatStore(_database);
        var session = await systemUnderTests.CreateSessionAsync(null, "Диалог", CancellationToken.None);
        await systemUnderTests.AppendMessageAsync(
            Message(session.Id, AiChatRole.User, "Привет", string.Empty),
            CancellationToken.None);

        // Act
        await systemUnderTests.DeleteSessionAsync(session.Id, CancellationToken.None);

        // Assert
        (await systemUnderTests.GetSessionAsync(session.Id, CancellationToken.None)).Should().BeNull();

        var messages = new List<ChatMessageRecord>();
        await foreach (var message in systemUnderTests.GetMessagesAsync(session.Id, CancellationToken.None))
            messages.Add(message);

        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ReassignOrphanSessionsAsync_MovesGlobalSessionsToProject()
    {
        // Arrange
        var systemUnderTests = new SqliteChatStore(_database);
        var global = await systemUnderTests.CreateSessionAsync(null, "Глобальный", CancellationToken.None);
        var projectId = await AddProjectAsync(systemUnderTests);

        // Act
        var moved = await systemUnderTests.ReassignOrphanSessionsAsync(projectId, CancellationToken.None);

        // Assert
        moved.Should().Be(1);
        var session = await systemUnderTests.GetSessionAsync(global.Id, CancellationToken.None);
        session!.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task AppendMessageAsync_WithContent_RoundTripsOrderedBlocks()
    {
        // Arrange
        var systemUnderTests = new SqliteChatStore(_database);
        var session = await systemUnderTests.CreateSessionAsync(null, "Диалог", CancellationToken.None);
        var content = MessageBlocksSerializer.Serialize(
        [
            new MessageBlock("До", null),
            new MessageBlock(null, "СписокФайлов"),
            new MessageBlock("После", null),
        ]);

        // Act
        await systemUnderTests.AppendMessageAsync(
            new ChatMessageRecord(0, session.Id, AiChatRole.Assistant, "ДоПосле", string.Empty, null, null, null, DateTimeOffset.UtcNow, content),
            CancellationToken.None);

        var messages = new List<ChatMessageRecord>();
        await foreach (var message in systemUnderTests.GetMessagesAsync(session.Id, CancellationToken.None))
            messages.Add(message);

        // Assert
        var restored = MessageBlocksSerializer.Deserialize(messages.Should().ContainSingle().Subject.Content);
        restored.Should().HaveCount(3);
        restored[0].Text.Should().Be("До");
        restored[1].Tool.Should().Be("СписокФайлов");
        restored[2].Text.Should().Be("После");
    }

    private static ChatMessageRecord Message(long sessionId, AiChatRole role, string text, string reasoning) =>
        new(0, sessionId, role, text, reasoning, null, null, null, DateTimeOffset.UtcNow);

    private async Task<long> AddProjectAsync(SqliteChatStore store)
    {
        var projectStore = new SqliteProjectStore(_database);
        var folder = Path.Join(_directory, "project");
        Directory.CreateDirectory(folder);
        var project = await projectStore.AddProjectAsync("Проект", folder, CancellationToken.None);

        return project.Id;
    }
}
