using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Configuration;

/// <summary>
/// Функция для модели с заранее заданной JSON-схемой параметров — без рефлексии
/// (<c>AIFunctionFactory</c> строит схему по сигнатуре метода, что несовместимо с full trimming).
/// </summary>
internal sealed class ConfigurationFunction(
    string name,
    string description,
    string jsonSchema,
    Func<AIFunctionArguments, string> handler) : AIFunction
{
    private readonly JsonElement _jsonSchema = JsonDocument.Parse(jsonSchema).RootElement.Clone();

    public override string Name => name;

    public override string Description => description;

    public override JsonElement JsonSchema => _jsonSchema;

    public static string? GetString(AIFunctionArguments arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => element.GetRawText(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    public static int? GetInt(AIFunctionArguments arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            int number => number,
            long number => (int)Math.Clamp(number, int.MinValue, int.MaxValue),
            double number => (int)number,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var number) => number,
            JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var number) => number,
            string text when int.TryParse(text, out var number) => number,
            _ => null,
        };
    }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return ValueTask.FromResult<object?>(handler(arguments));
        }
        catch (ArgumentException exception)
        {
            return ValueTask.FromResult<object?>($"Ошибка: {exception.Message}");
        }
        catch (IOException exception)
        {
            return ValueTask.FromResult<object?>($"Ошибка: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return ValueTask.FromResult<object?>($"Ошибка доступа: {exception.Message}");
        }
    }
}
