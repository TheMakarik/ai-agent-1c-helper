using System.Text.Json;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ai1CAgent.Modules.Ai;

public sealed class AiSettingsStore : IAiSettingsStore
{
    private readonly IOptions<AiOptions> _defaults;
    private readonly ISecretProtector _protector;
    private readonly ILogger<AiSettingsStore> _logger;
    private readonly string _settingsFile;
    private readonly Lock _gate = new();

    private List<AiProviderSettings>? _current;

    public event EventHandler? SettingsChanged;

    public AiSettingsStore(
        IOptions<AiOptions> defaults,
        ISecretProtector protector,
        ILogger<AiSettingsStore> logger,
        string? settingsFile = null)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(logger);

        _defaults = defaults;
        _protector = protector;
        _logger = logger;
        _settingsFile = settingsFile ?? AppDataPaths.SettingsFile;
    }

    public IReadOnlyList<AiProviderSettings> GetProviders()
    {
        lock (_gate)
        {
            return _current ??= Load();
        }
    }

    public async Task SaveProvidersAsync(
        IReadOnlyList<AiProviderSettings> providers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var snapshot = providers.Select(provider => provider.Clone()).ToList();

        lock (_gate)
        {
            _current = snapshot;
        }

        await WriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Настройки провайдеров сохранены: {Path}", _settingsFile);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<AiProviderSettings> Load()
    {
        var persisted = ReadFromDisk();

        var persistedBuiltins = persisted
            .Where(settings => settings.Provider is not null)
            .GroupBy(settings => settings.Provider!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var defaults = _defaults.Value.Providers
            .Where(settings => settings.Provider is not null)
            .GroupBy(settings => settings.Provider!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        List<AiProviderSettings> result = [];

        foreach (var provider in AiProviderDescriptors.All)
        {
            if (persistedBuiltins.TryGetValue(provider, out var saved))
            {
                var restored = saved.Clone();

                if (restored.Models.Count == 0 &&
                    string.IsNullOrWhiteSpace(restored.Model) &&
                    defaults.TryGetValue(provider, out var fallback))
                {
                    restored.Models = [.. fallback.Models];
                }

                result.Add(restored);
            }
            else if (defaults.TryGetValue(provider, out var fallback))
            {
                result.Add(fallback.Clone());
            }
            else
            {
                result.Add(new AiProviderSettings { Provider = provider });
            }
        }

        result.AddRange(ReadCustomProviders(persisted));

        return result;
    }

    private List<AiProviderSettings> ReadCustomProviders(List<AiProviderSettings> persisted)
    {
        var builtInDisplayNames = AiProviderDescriptors.All
            .Select(AiProviderDescriptors.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return persisted
            .Where(settings => settings.Provider is null && !string.IsNullOrWhiteSpace(settings.Name))
            .GroupBy(settings => settings.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(settings =>
            {
                if (!builtInDisplayNames.Contains(settings.Name.Trim()))
                    return true;

                _logger.LogWarning(
                    "Пользовательский провайдер {Name} совпадает с именем встроенного провайдера и будет пропущен",
                    settings.Name);
                return false;
            })
            .Select(settings => settings.Clone())
            .ToList();
    }

    private List<AiProviderSettings> ReadFromDisk()
    {
        if (!File.Exists(_settingsFile))
            return [];

        try
        {
            var json = File.ReadAllText(_settingsFile);
            var persisted = JsonSerializer.Deserialize(json, AiSettingsJsonContext.Default.ListPersistedProvider) ?? [];

            return persisted.Select(ToSettings).ToList();
        }
        catch (JsonException exception)
        {
            QuarantineBrokenFile(exception);
            return [];
        }
        catch (IOException exception)
        {
            QuarantineBrokenFile(exception);
            return [];
        }
    }

    private void QuarantineBrokenFile(Exception exception)
    {
        _logger.LogError(exception, "Не удалось прочитать настройки провайдеров: {Path}", _settingsFile);

        try
        {
            var brokenPath = _settingsFile + ".broken";
            File.Move(_settingsFile, brokenPath, overwrite: true);
            _logger.LogWarning("Повреждённый файл настроек перемещён: {BrokenPath}", brokenPath);
        }
        catch (IOException moveException)
        {
            _logger.LogWarning(
                moveException,
                "Не удалось переместить повреждённый файл настроек, он останется на месте: {Path}",
                _settingsFile);
        }
    }

    private async Task WriteAsync(List<AiProviderSettings> providers, CancellationToken cancellationToken)
    {
        var persisted = providers.Select(ToPersisted).ToList();
        var json = JsonSerializer.Serialize(persisted, AiSettingsJsonContext.Default.ListPersistedProvider);

        var directory = Path.GetDirectoryName(_settingsFile);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(_settingsFile, json, cancellationToken).ConfigureAwait(false);
    }

    private AiProviderSettings ToSettings(PersistedProvider persisted)
    {
        AiProvider? provider = null;

        if (persisted.Provider is { } rawProvider)
        {
            if (Enum.TryParse<AiProvider>(rawProvider, out var parsed) && Enum.IsDefined(parsed))
            {
                provider = parsed;
            }
            else
            {
                _logger.LogWarning(
                    "Неизвестный провайдер {Provider} в файле настроек, запись прочитана как пользовательская",
                    rawProvider);
            }
        }

        var name = persisted.Name;

        if (provider is null && string.IsNullOrWhiteSpace(name))
            name = persisted.Provider ?? string.Empty;

        var apiKey = string.Empty;

        if (!string.IsNullOrEmpty(persisted.ProtectedApiKey))
        {
            if (!_protector.TryUnprotect(persisted.ProtectedApiKey, out apiKey))
            {
                _logger.LogWarning(
                    "Не удалось расшифровать API-ключ провайдера {Provider}, ключ будет сброшен",
                    persisted.Provider);
                apiKey = string.Empty;
            }
        }
        else if (!string.IsNullOrEmpty(persisted.ApiKey))
        {
            apiKey = persisted.ApiKey;
        }

        return new AiProviderSettings
        {
            Provider = provider,
            Name = name,
            Format = ParseFormat(persisted.Format),
            Model = persisted.Model,
            Endpoint = persisted.Endpoint,
            ApiKey = apiKey,
            Models = persisted.Models is null ? [] : [.. persisted.Models.Select(model => new CustomModel(model.Name, model.Format))],
            SelectedModel = persisted.SelectedModel is null || string.IsNullOrEmpty(persisted.SelectedModel.Name)
                ? null
                : new CustomModel(persisted.SelectedModel.Name, persisted.SelectedModel.Format),
            CustomModels = persisted.CustomModels is null ? [] : [.. persisted.CustomModels.Select(model => new CustomModel(model.Name, model.Format))],
            IsCustomEndpoint = persisted.IsCustomEndpoint,
            CustomEndpoint = persisted.CustomEndpoint,
        };
    }

    private PersistedProvider ToPersisted(AiProviderSettings settings) => new()
    {
        Provider = settings.Provider?.ToString(),
        Name = settings.Name,
        Format = settings.Format.ToString(),
        Model = settings.Model,
        Endpoint = settings.Endpoint,
        ProtectedApiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? string.Empty : _protector.Protect(settings.ApiKey),
        Models = settings.Models.Count == 0
            ? null
            : [.. settings.Models.Select(model => new PersistedCustomModel { Name = model.Name, Format = model.Format })],
        SelectedModel = settings.SelectedModel is null
            ? null
            : new PersistedCustomModel { Name = settings.SelectedModel.Name, Format = settings.SelectedModel.Format },
        CustomModels = settings.CustomModels.Count == 0
            ? null
            : [.. settings.CustomModels.Select(model => new PersistedCustomModel { Name = model.Name, Format = model.Format })],
        IsCustomEndpoint = settings.IsCustomEndpoint,
        CustomEndpoint = settings.CustomEndpoint,
    };

    private static AiApiFormat ParseFormat(string format) =>
        Enum.TryParse<AiApiFormat>(format, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : AiApiFormat.OpenAi;
}
