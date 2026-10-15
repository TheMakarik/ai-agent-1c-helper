using Ai1CAgent.Core.Common;
using Ai1CAgent.Modules.Context.Interfaces;
using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Context;

public sealed class SystemDocumentationToolProvider(ISystemDocumentation documentation) : ISystemDocumentationToolProvider
{
    private const string Schema = """
        { "type": "object", "properties": {} }
        """;

    public IReadOnlyList<AITool> GetTools() =>
    [
        new ContextFunction(
            "ReadSystemDocumentation",
            "Прочитать системную документацию — основной шаблон ответов ИИ-помощника разработчика 1С. "
            + "Вызови перед ответом в новом чате, чтобы следовать формату ответов.",
            Schema,
            (_, _) => ValueTask.FromResult(documentation.Read())),
    ];
}
