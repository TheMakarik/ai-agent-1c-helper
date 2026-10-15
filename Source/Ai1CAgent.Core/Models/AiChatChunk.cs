namespace Ai1CAgent.Core.Models;

public sealed record AiChatChunk(
    string Text,
    string Reasoning,
    AiTokenUsage? Usage = null,
    string? ToolName = null,
    string? ToolDetail = null);
