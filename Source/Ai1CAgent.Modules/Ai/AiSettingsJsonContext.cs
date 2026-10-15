using System.Text.Json;
using System.Text.Json.Serialization;
using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Modules.Ai;

[JsonConverter(typeof(PersistedCustomModelConverter))]
internal sealed class PersistedCustomModel
{
    public string Name { get; set; } = string.Empty;

    public AiApiFormat Format { get; set; }

    public bool IsUserAdded { get; set; }
}

internal sealed class PersistedProvider
{
    public string? Provider { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string ProtectedApiKey { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ApiKey { get; set; }

    public List<PersistedCustomModel>? Models { get; set; }

    public PersistedCustomModel? SelectedModel { get; set; }

    public List<PersistedCustomModel>? CustomModels { get; set; }

    public bool IsCustomEndpoint { get; set; }

    public string CustomEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// Читает модель как объект <c>{Name, Format}</c> и дополнительно поддерживает legacy-вариант
/// строкой, чтобы старые файлы настроек (массив имён моделей) продолжали читаться.
/// </summary>
internal sealed class PersistedCustomModelConverter : JsonConverter<PersistedCustomModel>
{
    public override PersistedCustomModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new PersistedCustomModel { Name = reader.GetString() ?? string.Empty };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Модель должна быть строкой или объектом");

        var name = string.Empty;
        var format = AiApiFormat.OpenAi;
        var isUserAdded = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var propertyName = reader.GetString();
            reader.Read();

            if (string.Equals(propertyName, nameof(PersistedCustomModel.Name), StringComparison.OrdinalIgnoreCase))
                name = reader.TokenType == JsonTokenType.Null ? string.Empty : reader.GetString() ?? string.Empty;
            else if (string.Equals(propertyName, nameof(PersistedCustomModel.Format), StringComparison.OrdinalIgnoreCase))
                format = ParseFormat(reader.TokenType == JsonTokenType.Null ? null : reader.GetString());
            else if (string.Equals(propertyName, nameof(PersistedCustomModel.IsUserAdded), StringComparison.OrdinalIgnoreCase))
                isUserAdded = reader.TokenType == JsonTokenType.True;
            else
                reader.Skip();
        }

        return new PersistedCustomModel { Name = name, Format = format, IsUserAdded = isUserAdded };
    }

    public override void Write(Utf8JsonWriter writer, PersistedCustomModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(PersistedCustomModel.Name), value.Name);
        writer.WriteString(nameof(PersistedCustomModel.Format), value.Format.ToString());
        writer.WriteBoolean(nameof(PersistedCustomModel.IsUserAdded), value.IsUserAdded);
        writer.WriteEndObject();
    }

    private static AiApiFormat ParseFormat(string? value) =>
        Enum.TryParse<AiApiFormat>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : AiApiFormat.OpenAi;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    Converters = [typeof(JsonStringEnumConverter<AiApiFormat>)])]
[JsonSerializable(typeof(List<PersistedProvider>))]
[JsonSerializable(typeof(List<PersistedCustomModel>))]
internal sealed partial class AiSettingsJsonContext : JsonSerializerContext;
