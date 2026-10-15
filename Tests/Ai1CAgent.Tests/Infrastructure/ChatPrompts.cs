using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Tests.Infrastructure;

public static class ChatPrompts
{
    public static IReadOnlyCollection<AiChatMessage> Arithmetic { get; } =
    [
        new(AiChatRole.User, "Сколько будет 2 + 2? Ответь одним числом без пояснений."),
    ];

    public static IReadOnlyCollection<AiChatMessage> History { get; } =
    [
        new(AiChatRole.System, "Ты краткий помощник. Отвечай одним словом."),
        new(AiChatRole.User, "Запомни кодовое слово: Барсук."),
        new(AiChatRole.Assistant, "Запомнил."),
        new(AiChatRole.User, "Какое кодовое слово я назвал? Ответь только им."),
    ];
}
