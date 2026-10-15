using System.Text.Json;
using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public static class MessageBlocksSerializer
{
    public static string Serialize(IReadOnlyList<MessageBlock> blocks) =>
        blocks.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(blocks.ToList(), MessageBlocksJsonContext.Default.ListMessageBlock);

    public static IReadOnlyList<MessageBlock> Deserialize(string content) =>
        string.IsNullOrEmpty(content)
            ? []
            : JsonSerializer.Deserialize(content, MessageBlocksJsonContext.Default.ListMessageBlock) ?? [];
}
