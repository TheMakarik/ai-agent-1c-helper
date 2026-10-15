using System.Text.Json;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Ai1CAgent.Modules.Storage;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Mcp;

public sealed class McpSettingsStore : IMcpSettingsStore
{
    private readonly ILogger<McpSettingsStore> _logger;
    private readonly string _settingsFile;
    private readonly Lock _gate = new();

    private McpServerSettings? _current;

    public event EventHandler? SettingsChanged;

    public McpSettingsStore(ILogger<McpSettingsStore> logger, string? settingsFile = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _settingsFile = settingsFile ?? AppDataPaths.McpSettingsFile;
    }

    public McpServerSettings Get()
    {
        lock (_gate)
        {
            _current ??= ReadFromDisk();
            return _current.Clone();
        }
    }

    public async Task SaveAsync(McpServerSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var snapshot = settings.Clone();

        lock (_gate)
        {
            _current = snapshot;
        }

        await WriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Настройки MCP-сервера сохранены: {Path}", _settingsFile);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private McpServerSettings ReadFromDisk()
    {
        if (!File.Exists(_settingsFile))
            return new McpServerSettings();

        try
        {
            var json = File.ReadAllText(_settingsFile);
            var persisted = JsonSerializer.Deserialize(json, McpSettingsJsonContext.Default.PersistedMcpServer);

            return persisted is null ? new McpServerSettings() : ToSettings(persisted);
        }
        catch (JsonException exception)
        {
            QuarantineBrokenFile(exception);
            return new McpServerSettings();
        }
        catch (IOException exception)
        {
            QuarantineBrokenFile(exception);
            return new McpServerSettings();
        }
    }

    private void QuarantineBrokenFile(Exception exception)
    {
        _logger.LogError(exception, "Не удалось прочитать настройки MCP-сервера: {Path}", _settingsFile);

        try
        {
            var brokenPath = _settingsFile + ".broken";
            File.Move(_settingsFile, brokenPath, overwrite: true);
            _logger.LogWarning("Повреждённый файл настроек MCP перемещён: {BrokenPath}", brokenPath);
        }
        catch (IOException moveException)
        {
            _logger.LogWarning(
                moveException,
                "Не удалось переместить повреждённый файл настроек MCP, он останется на месте: {Path}",
                _settingsFile);
        }
    }

    private async Task WriteAsync(McpServerSettings settings, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(ToPersisted(settings), McpSettingsJsonContext.Default.PersistedMcpServer);

        var directory = Path.GetDirectoryName(_settingsFile);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(_settingsFile, json, cancellationToken).ConfigureAwait(false);
    }

    private McpServerSettings ToSettings(PersistedMcpServer persisted) => new()
    {
        Transport = ParseTransport(persisted.Transport),
        Endpoint = persisted.Endpoint ?? string.Empty,
        Command = persisted.Command ?? string.Empty,
        Arguments = persisted.Arguments ?? string.Empty,
    };

    private static PersistedMcpServer ToPersisted(McpServerSettings settings) => new()
    {
        Transport = settings.Transport.ToString(),
        Endpoint = settings.Endpoint,
        Command = settings.Command,
        Arguments = settings.Arguments,
    };

    private McpTransportType ParseTransport(string? value)
    {
        if (Enum.TryParse<McpTransportType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        if (!string.IsNullOrEmpty(value))
            _logger.LogWarning("Неизвестный транспорт MCP {Transport} в файле настроек, используется HTTP", value);

        return McpTransportType.Http;
    }
}
