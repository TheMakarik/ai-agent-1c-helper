using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Microsoft.Extensions.Options;

namespace Ai1CAgent.Modules.Ai;

public sealed class AiModelCatalog(
    IOptions<OllamaOptions> ollamaOptions,
    IAiSettingsStore settingsStore) : IAiModelCatalog
{
    public IReadOnlyList<AiModel> GetModels()
    {
        List<AiModel> models = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void Add(string providerKey, string name)
        {
            if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(name))
                return;

            var trimmed = name.Trim();

            if (seen.Add($"{providerKey}\n{trimmed}"))
                models.Add(new AiModel(providerKey, trimmed));
        }

        foreach (var name in ollamaOptions.Value.Models)
            Add(nameof(AiProvider.Ollama), name);

        foreach (var settings in settingsStore.GetProviders())
        {
            var providerKey = settings.Provider?.ToString() ?? settings.Name;

            foreach (var model in settings.Models)
                Add(providerKey, model.Name);

            foreach (var model in settings.CustomModels)
                Add(providerKey, model.Name);

            if (settings.Models.Count == 0)
            {
                Add(providerKey, settings.Model);

                if (settings.SelectedModel is { } selected)
                    Add(providerKey, selected.Name);
            }
        }

        return models;
    }
}
