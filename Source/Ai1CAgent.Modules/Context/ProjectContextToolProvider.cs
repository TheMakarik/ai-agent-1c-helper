using Ai1CAgent.Core.Common;
using Ai1CAgent.Modules.Configuration;
using Ai1CAgent.Modules.Context.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Context;

public sealed class ProjectContextToolProvider(
    IProjectStore projectStore,
    ILogger<ProjectContextToolProvider> logger) : IProjectContextToolProvider
{
    private const string GetSchema = """
        { "type": "object", "properties": {} }
        """;

    private const string SetSchema = """
        {
          "type": "object",
          "properties": {
            "context": { "type": "string", "description": "Новый полный текст глобального контекста проекта." }
          },
          "required": ["context"]
        }
        """;

    public IReadOnlyList<AITool> GetTools(long projectId) =>
    [
        new ContextFunction(
            "context_get",
            "Прочитать глобальный контекст проекта — заметки, общие для всех чатов этого проекта.",
            GetSchema,
            async (_, cancellationToken) =>
            {
                logger.LogInformation("Модель читает контекст проекта {ProjectId}", projectId);
                var context = await projectStore.GetGlobalContextAsync(projectId, cancellationToken).ConfigureAwait(false);

                return context.Length == 0 ? "Контекст проекта пуст." : context;
            }),
        new ContextFunction(
            "context_set",
            "Полностью заменить глобальный контекст проекта новым текстом.",
            SetSchema,
            async (arguments, cancellationToken) =>
            {
                var context = ConfigurationFunction.GetString(arguments, "context") ?? string.Empty;
                logger.LogInformation("Модель обновляет контекст проекта {ProjectId}", projectId);
                await projectStore.SetGlobalContextAsync(projectId, context, cancellationToken).ConfigureAwait(false);

                return "Контекст проекта обновлён.";
            }),
    ];
}
