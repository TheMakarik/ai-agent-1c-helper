namespace Ai1CAgent.Core.Models;

public static class AiProviderDescriptors
{
    public static IReadOnlyList<AiProvider> All { get; } =
    [
        AiProvider.Ollama,
    ];

    /// <summary>
    /// Встроенные провайдеры прошлых версий. Их сохранённые записи при загрузке отбрасываются,
    /// чтобы не всплывать в настройках как пользовательские.
    /// </summary>
    public static IReadOnlySet<string> Retired { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DeepSeek",
        "Claude",
        "Kimi",
        "Glm",
        "ChatGpt",
        "Qwen",
    };

    public static AiApiFormat Format(AiProvider provider) => AiApiFormat.OpenAi;

    public static string DisplayName(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => "Ollama",
        _ => provider.ToString(),
    };

    public static string? DefaultEndpoint(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => "http://localhost:11434/v1",
        _ => null,
    };

    public static string DefaultApiKeyEnvironmentVariable(AiProvider provider) => string.Empty;
}
