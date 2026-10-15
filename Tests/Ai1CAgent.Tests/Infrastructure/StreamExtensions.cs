namespace Ai1CAgent.Tests.Infrastructure;

public static class StreamExtensions
{
    public static async Task<IReadOnlyList<string>> ToListAsync(this IAsyncEnumerable<string> source)
    {
        List<string> items = [];

        await foreach (var item in source)
            items.Add(item);

        return items;
    }
}
