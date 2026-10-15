using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Options;

public sealed class AiProviderSettings
{
    public AiProvider? Provider { get; set; }

    public string Name { get; set; } = string.Empty;

    public AiApiFormat Format { get; set; } = AiApiFormat.OpenAi;

    public string Model { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public IReadOnlyList<CustomModel> Models { get; set; } = [];

    public CustomModel? SelectedModel { get; set; }

    public IReadOnlyList<CustomModel> CustomModels { get; set; } = [];

    public bool IsCustomEndpoint { get; set; }

    public string CustomEndpoint { get; set; } = string.Empty;

    public bool IsCustom => Provider is null;

    public string DisplayName =>
        IsCustom ? Name : AiProviderDescriptors.DisplayName(Provider!.Value);

    public AiApiFormat ResolvedFormat() =>
        IsCustom ? Format : AiProviderDescriptors.Format(Provider!.Value);

    public string ResolvedEndpoint()
    {
        if (IsCustomEndpoint && !string.IsNullOrWhiteSpace(CustomEndpoint))
            return CustomEndpoint.Trim();

        if (!string.IsNullOrWhiteSpace(Endpoint))
            return Endpoint.Trim();

        return IsCustom
            ? string.Empty
            : AiProviderDescriptors.DefaultEndpoint(Provider!.Value) ?? string.Empty;
    }

    public string ResolvedApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
            return ApiKey.Trim();

        if (IsCustom)
            return string.Empty;

        var variable = AiProviderDescriptors.DefaultApiKeyEnvironmentVariable(Provider!.Value);

        return string.IsNullOrWhiteSpace(variable)
            ? string.Empty
            : Environment.GetEnvironmentVariable(variable) ?? string.Empty;
    }

    public AiProviderSettings Clone() => new()
    {
        Provider = Provider,
        Name = Name,
        Format = Format,
        Model = Model,
        Endpoint = Endpoint,
        ApiKey = ApiKey,
        Models = [.. Models],
        SelectedModel = SelectedModel,
        CustomModels = [.. CustomModels],
        IsCustomEndpoint = IsCustomEndpoint,
        CustomEndpoint = CustomEndpoint,
    };
}
