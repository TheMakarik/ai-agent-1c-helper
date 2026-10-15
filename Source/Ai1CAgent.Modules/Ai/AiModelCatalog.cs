using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Microsoft.Extensions.Options;

namespace Ai1CAgent.Modules.Ai;

public sealed class AiModelCatalog(
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<DeepSeekOptions> deepSeekOptions,
    IOptions<ClaudeOptions> claudeOptions) : IAiModelCatalog
{
    public IReadOnlyList<AiModel> GetModels()
    {
        List<AiModel> models = [];

        models.AddRange(ollamaOptions.Value.Models.Select(name => new AiModel(AiProvider.Ollama, name)));

        if (!string.IsNullOrWhiteSpace(deepSeekOptions.Value.Model))
            models.Add(new AiModel(AiProvider.DeepSeek, deepSeekOptions.Value.Model));

        if (!string.IsNullOrWhiteSpace(claudeOptions.Value.Model))
            models.Add(new AiModel(AiProvider.Claude, claudeOptions.Value.Model));

        return models;
    }
}
