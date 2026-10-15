using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Ai1CAgent.Modules.Storage;

public sealed class Database
{
    private static readonly (int Version, string Sql)[] Migrations =
    [
        (1, Migration1),
        (2, Migration2),
        (3, Migration3),
    ];

    private const string Migration1 = """
        CREATE TABLE project (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            path TEXT NOT NULL,
            global_context TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE project_file (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            project_id INTEGER NOT NULL REFERENCES project (id) ON DELETE CASCADE,
            relative_path TEXT NOT NULL,
            last_write_time_utc TEXT NOT NULL,
            size INTEGER NOT NULL,
            indexed_at TEXT NOT NULL,
            UNIQUE (project_id, relative_path)
        );

        CREATE TABLE chat_session (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            project_id INTEGER NULL REFERENCES project (id) ON DELETE CASCADE,
            title TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE chat_message (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            session_id INTEGER NOT NULL REFERENCES chat_session (id) ON DELETE CASCADE,
            role TEXT NOT NULL,
            text TEXT NOT NULL,
            reasoning TEXT NOT NULL DEFAULT '',
            input_tokens INTEGER NULL,
            output_tokens INTEGER NULL,
            total_tokens INTEGER NULL,
            created_at TEXT NOT NULL
        );

        CREATE INDEX ix_project_file_project ON project_file (project_id);
        CREATE INDEX ix_chat_session_project ON chat_session (project_id);
        CREATE INDEX ix_chat_message_session ON chat_message (session_id, id);
        """;

    private const string Migration2 = """
        ALTER TABLE chat_message ADD COLUMN tools TEXT NOT NULL DEFAULT '';
        """;

    private const string Migration3 = """
        ALTER TABLE chat_message ADD COLUMN content TEXT NOT NULL DEFAULT '';
        """;

    private readonly string _connectionString;

    public Database(string? databaseFile = null)
    {
        SqliteTypeHandlers.Register();
        DatabaseFile = databaseFile ?? AppDataPaths.DatabaseFile;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabaseFile,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabaseFile { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(DatabaseFile);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, null, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL);",
            cancellationToken).ConfigureAwait(false);

        var current = await GetVersionAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var (version, sql) in Migrations)
        {
            if (version <= current)
                continue;

            using var transaction = connection.BeginTransaction();
            await ExecuteAsync(connection, transaction, sql, cancellationToken).ConfigureAwait(false);

            await using (var stamp = connection.CreateCommand())
            {
                stamp.Transaction = transaction;
                stamp.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES ($version, $appliedAt);";
                stamp.Parameters.AddWithValue("$version", version);
                stamp.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await stamp.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            transaction.Commit();
        }
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> GetVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return result is long value ? value : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
