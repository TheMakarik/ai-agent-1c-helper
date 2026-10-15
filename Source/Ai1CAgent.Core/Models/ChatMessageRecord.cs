namespace Ai1CAgent.Core.Models;

public sealed record ChatMessageRecord(
    long Id,
    long SessionId,
    AiChatRole Role,
    string Text,
    string Reasoning,
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens,
    DateTimeOffset CreatedAt,
    string Content = "")
{
    public AiTokenUsage? Usage =>
        InputTokens is null && OutputTokens is null && TotalTokens is null
            ? null
            : new AiTokenUsage(InputTokens, OutputTokens, TotalTokens);
}
