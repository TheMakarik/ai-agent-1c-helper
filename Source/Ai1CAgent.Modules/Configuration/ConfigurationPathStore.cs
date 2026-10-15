using System.Text.Json;
using Ai1CAgent.Modules.Configuration.Interfaces;
using Ai1CAgent.Modules.Storage;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Configuration;

public sealed class ConfigurationPathStore : IConfigurationPathStore
{
    private readonly ILogger<ConfigurationPathStore> _logger;
    private readonly string _settingsFile;
    private readonly Lock _gate = new();

    private string? _lastPath;

    public ConfigurationPathStore(ILogger<ConfigurationPathStore> logger, string? settingsFile = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _settingsFile = settingsFile ?? AppDataPaths.ConfigurationPathFile;
    }

    public string GetLastPath()
    {
        lock (_gate)
        {
            return _lastPath ??= ReadFromDisk();
        }
    }

    public async Task SaveLastPathAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        lock (_gate)
        {
            _lastPath = path;
        }

        var json = JsonSerializer.Serialize(
            new PersistedConfigurationPath { LastPath = path },
            ConfigurationPathJsonContext.Default.PersistedConfigurationPath);

        var directory = Path.GetDirectoryName(_settingsFile);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(_settingsFile, json, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Путь к выгрузке конфигурации сохранён: {Path}", path);
    }

    private string ReadFromDisk()
    {
        if (!File.Exists(_settingsFile))
            return string.Empty;

        try
        {
            var json = File.ReadAllText(_settingsFile);
            var persisted = JsonSerializer.Deserialize(json, ConfigurationPathJsonContext.Default.PersistedConfigurationPath);

            return persisted?.LastPath ?? string.Empty;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Не удалось прочитать путь к выгрузке конфигурации: {Path}", _settingsFile);
            return string.Empty;
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Не удалось прочитать путь к выгрузке конфигурации: {Path}", _settingsFile);
            return string.Empty;
        }
    }
}
