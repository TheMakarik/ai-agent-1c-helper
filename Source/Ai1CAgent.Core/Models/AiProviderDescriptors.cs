namespace Ai1CAgent.Core.Models;

public static class AiProviderDescriptors
{
    public static IReadOnlyList<AiProvider> All { get; } =
    [
        AiProvider.Ollama,
        AiProvider.DeepSeek,
        AiProvider.Claude,
        AiProvider.Kimi,
        AiProvider.Glm,
        AiProvider.ChatGpt,
        AiProvider.Qwen,
    ];

    public static AiApiFormat Format(AiProvider provider) => provider switch
    {
        AiProvider.Claude => AiApiFormat.Anthropic,
        _ => AiApiFormat.OpenAi,
    };

    public static string DisplayName(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => "Ollama",
        AiProvider.DeepSeek => "DeepSeek",
        AiProvider.Claude => "Claude",
        AiProvider.Kimi => "Kimi",
        AiProvider.Glm => "GLM",
        AiProvider.ChatGpt => "ChatGPT",
        AiProvider.Qwen => "Qwen",
        _ => provider.ToString(),
    };

    public static string? DefaultEndpoint(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => "http://localhost:11434/v1",
        AiProvider.DeepSeek => "https://api.deepseek.com",
        AiProvider.Claude => "https://api.anthropic.com",
        AiProvider.Kimi => "https://api.moonshot.cn/v1",
        AiProvider.Glm => "https://open.bigmodel.cn/api/paas/v4",
        AiProvider.ChatGpt => "https://api.openai.com/v1",
        AiProvider.Qwen => "https://dashscope.aliyuncs.com/compatible-mode/v1",
        _ => null,
    };

    public static string DefaultApiKeyEnvironmentVariable(AiProvider provider) => provider switch
    {
        AiProvider.DeepSeek => "DEEPSEEK_API_KEY",
        AiProvider.Claude => "ANTHROPIC_API_KEY",
        AiProvider.Kimi => "MOONSHOT_API_KEY",
        AiProvider.Glm => "ZHIPUAI_API_KEY",
        AiProvider.ChatGpt => "OPENAI_API_KEY",
        AiProvider.Qwen => "DASHSCOPE_API_KEY",
        _ => string.Empty,
    };
}
