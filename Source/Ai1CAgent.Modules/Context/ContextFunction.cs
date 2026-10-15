using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Context;

/// <summary>
/// Асинхронная функция для модели с заранее заданной JSON-схемой параметров — без рефлексии
/// (<c>AIFunctionFactory</c> строит схему по сигнатуре метода, что несовместимо с full trimming).
/// </summary>
internal sealed class ContextFunction(
    string name,
    string description,
    string jsonSchema,
    Func<AIFunctionArguments, CancellationToken, ValueTask<string>> handler) : AIFunction
{
    private readonly JsonElement _jsonSchema = JsonDocument.Parse(jsonSchema).RootElement.Clone();

    public override string Name => name;

    public override string Description => description;

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await handler(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return $"Ошибка: {exception.Message}";
        }
    }
}
