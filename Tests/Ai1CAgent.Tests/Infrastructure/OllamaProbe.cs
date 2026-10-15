using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Ai1CAgent.Tests.Infrastructure;

public static partial class OllamaProbe
{
    public const string Endpoint = "http://localhost:11434";

    public static async Task<bool> HasModelAsync(string model, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            var response = await httpClient.GetFromJsonAsync($"{Endpoint}/api/tags", ProbeJsonContext.Default.TagsResponse, cancellationToken);
            return response?.Models.Any(item => item.Name == model) ?? false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    internal sealed record TagsResponse([property: JsonPropertyName("models")] IReadOnlyList<TagModel> Models);

    internal sealed record TagModel([property: JsonPropertyName("name")] string Name);

    [JsonSerializable(typeof(TagsResponse))]
    private sealed partial class ProbeJsonContext : JsonSerializerContext;
}
