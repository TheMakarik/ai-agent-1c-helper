using System.Text;

namespace Ai1CAgent.Modules.Mcp;

/// <summary>
/// Разбирает строку аргументов как командная строка: разделитель — пробелы,
/// двойные кавычки объединяют аргумент с пробелами, <c>""</c> внутри кавычек — литеральная кавычка.
/// Обратные слеши не экранируют, чтобы пути Windows вводились как есть.
/// </summary>
public static class CommandLineArguments
{
    public static IReadOnlyList<string> Split(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        List<string> arguments = [];
        var current = new StringBuilder();
        var inQuotes = false;
        var hasArgument = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var symbol = commandLine[index];

            if (symbol == '"')
            {
                if (inQuotes && index + 1 < commandLine.Length && commandLine[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                hasArgument = true;
                continue;
            }

            if (char.IsWhiteSpace(symbol) && !inQuotes)
            {
                if (!hasArgument)
                    continue;

                arguments.Add(current.ToString());
                current.Clear();
                hasArgument = false;
                continue;
            }

            current.Append(symbol);
            hasArgument = true;
        }

        if (hasArgument)
            arguments.Add(current.ToString());

        return arguments;
    }
}
