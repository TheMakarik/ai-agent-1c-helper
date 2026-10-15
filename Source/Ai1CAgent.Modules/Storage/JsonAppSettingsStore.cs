using System.Text.Json;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Storage;

public sealed class JsonAppSettingsStore(ILogger<JsonAppSettingsStore> logger, string? file = null) : IAppSettingsStore
{
    private static readonly AppSettings Default = new(false);

    private readonly string _file = file ?? AppDataPaths.AppSettingsFile;
    private AppSettings _current = Load(file);

    public AppSettings Get() => _current;

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        _current = settings;

        try
        {
            var directory = Path.GetDirectoryName(_file);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
            await File.WriteAllTextAsync(_file, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить настройки приложения");
        }
    }

    private static AppSettings Load(string? file)
    {
        var path = file ?? AppDataPaths.AppSettingsFile;

        if (!File.Exists(path))
            return Default;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? Default;
        }
        catch (Exception)
        {
            return Default;
        }
    }
}
