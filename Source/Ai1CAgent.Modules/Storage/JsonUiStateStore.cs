using System.Text.Json;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Storage;

public sealed class JsonUiStateStore(ILogger<JsonUiStateStore> logger, string? file = null) : IUiStateStore
{
    private static readonly UiState Empty = new(null, null, null, null);

    private readonly string _file = file ?? AppDataPaths.UiStateFile;
    private UiState _current = Load(file);

    public UiState Get() => _current;

    public async Task SaveAsync(UiState state, CancellationToken cancellationToken)
    {
        _current = state;

        try
        {
            var directory = Path.GetDirectoryName(_file);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(state, UiStateJsonContext.Default.UiState);
            await File.WriteAllTextAsync(_file, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить состояние интерфейса");
        }
    }

    private static UiState Load(string? file)
    {
        var path = file ?? AppDataPaths.UiStateFile;

        if (!File.Exists(path))
            return Empty;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, UiStateJsonContext.Default.UiState) ?? Empty;
        }
        catch (Exception)
        {
            return Empty;
        }
    }
}
