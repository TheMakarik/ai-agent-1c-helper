using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Configuration;
using Ai1CAgent.Modules.Context.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Context;

public sealed class SkillToolProvider(
    ISkillStore skillStore,
    ILogger<SkillToolProvider> logger) : ISkillToolProvider
{
    private const string ScopeProperty =
        "\"scope\": { \"type\": \"string\", \"enum\": [\"global\", \"local\"], "
        + "\"description\": \"global — общие скиллы, local — скиллы текущего проекта.\" }";

    private const string ListSchema = $$"""
        { "type": "object", "properties": { {{ScopeProperty}} }, "required": ["scope"] }
        """;

    private const string ReadSchema = $$"""
        {
          "type": "object",
          "properties": {
            {{ScopeProperty}},
            "name": { "type": "string", "description": "Имя скилла." }
          },
          "required": ["scope", "name"]
        }
        """;

    private const string WriteSchema = $$"""
        {
          "type": "object",
          "properties": {
            {{ScopeProperty}},
            "name": { "type": "string", "description": "Имя скилла (папка со SKILL.md)." },
            "content": { "type": "string", "description": "Полный текст скилла в Markdown." }
          },
          "required": ["scope", "name", "content"]
        }
        """;

    private const string DeleteSchema = ReadSchema;

    public IReadOnlyList<AITool> GetTools(long? projectId) =>
    [
        new ContextFunction(
            "ListSkills",
            "Список скиллов базы знаний: global — общие, local — скиллы текущего проекта.",
            ListSchema,
            (arguments, _) => ValueTask.FromResult(List(arguments, projectId))),
        new ContextFunction(
            "ReadSkill",
            "Прочитать скилл базы знаний по имени (Markdown).",
            ReadSchema,
            (arguments, _) => ValueTask.FromResult(Read(arguments, projectId))),
        new ContextFunction(
            "CreateSkill",
            "Создать скилл базы знаний (Markdown). Вызывай, когда нужно сохранить новые знания.",
            WriteSchema,
            (arguments, _) => ValueTask.FromResult(Write(arguments, projectId, "создан"))),
        new ContextFunction(
            "UpdateSkill",
            "Полностью перезаписать существующий скилл базы знаний новым текстом (Markdown).",
            WriteSchema,
            (arguments, _) => ValueTask.FromResult(Write(arguments, projectId, "обновлён"))),
        new ContextFunction(
            "DeleteSkill",
            "Удалить скилл базы знаний по имени.",
            DeleteSchema,
            (arguments, _) => ValueTask.FromResult(Delete(arguments, projectId))),
    ];

    private string List(AIFunctionArguments arguments, long? projectId)
    {
        var scope = ResolveScope(arguments, projectId);
        var names = skillStore.List(scope, projectId);

        return names.Count == 0 ? "Скиллов нет." : string.Join('\n', names);
    }

    private string Read(AIFunctionArguments arguments, long? projectId)
    {
        var scope = ResolveScope(arguments, projectId);
        var name = RequiredName(arguments);
        var content = skillStore.Read(scope, projectId, name);

        return content ?? $"Скилл «{name}» не найден.";
    }

    private string Write(AIFunctionArguments arguments, long? projectId, string action)
    {
        var scope = ResolveScope(arguments, projectId);
        var name = RequiredName(arguments);
        var content = ConfigurationFunction.GetString(arguments, "content")
            ?? throw new ArgumentException("Не задано содержимое скилла.");

        skillStore.Write(scope, projectId, name, content);
        logger.LogInformation("Скилл {Scope}/{Name} {Action}", scope, name, action);

        return $"Скилл «{name}» {action}.";
    }

    private string Delete(AIFunctionArguments arguments, long? projectId)
    {
        var scope = ResolveScope(arguments, projectId);
        var name = RequiredName(arguments);

        skillStore.Delete(scope, projectId, name);
        logger.LogInformation("Скилл {Scope}/{Name} удалён", scope, name);

        return $"Скилл «{name}» удалён.";
    }

    private static SkillScope ResolveScope(AIFunctionArguments arguments, long? projectId)
    {
        var raw = ConfigurationFunction.GetString(arguments, "scope");
        var scope = string.Equals(raw, "local", StringComparison.OrdinalIgnoreCase)
            ? SkillScope.Local
            : SkillScope.Global;

        if (scope is SkillScope.Local && projectId is null)
            throw new InvalidOperationException("Локальные скиллы доступны только внутри проекта.");

        return scope;
    }

    private static string RequiredName(AIFunctionArguments arguments)
    {
        var name = ConfigurationFunction.GetString(arguments, "name");

        return string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Не задано имя скилла.")
            : name.Trim();
    }
}
