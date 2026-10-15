using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ai1CAgent.Avalonia.ViewModels;

/// <summary>
/// Человекочитаемые (русские, PascalCase) названия инструментов агента.
/// Техническое имя функции остаётся ASCII — так его требуют провайдеры (OpenAI/Anthropic).
/// </summary>
internal static class ToolNameDisplay
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["list_files"] = "СписокФайлов",
        ["read_file"] = "ПрочитатьФайл",
        ["search_files"] = "ПоискПоТексту",
        ["find_files"] = "НайтиФайлы",
        ["file_info"] = "СведенияОФайле",
        ["context_get"] = "ПрочитатьКонтекст",
        ["context_set"] = "ОбновитьКонтекст",
        ["ReadSystemDocumentation"] = "ПрочитатьСистемнуюДокументацию",
        ["GitStatus"] = "СтатусGit",
        ["GitLog"] = "ИсторияGit",
        ["GitDiff"] = "РазницаGit",
        ["GitBranches"] = "ВеткиGit",
        ["CSharpOutline"] = "СтруктураCSharp",
        ["RunCommand"] = "КомандаТерминала",
        ["CanWrite"] = "МожноЗаписать",
        ["Write"] = "ЗаписатьФайл",
        ["Touch"] = "СоздатьФайл",
        ["SearchSkills"] = "ПоискСкиллов",
    };

    public static string Resolve(string toolName) =>
        Names.TryGetValue(toolName, out var display) ? display : ToPascalCase(toolName);

    private static string ToPascalCase(string value)
    {
        if (value.Length == 0)
            return value;

        var builder = new StringBuilder(value.Length);
        var upperNext = true;

        foreach (var character in value)
        {
            if (character is '_' or '-' or ' ')
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpper(character, CultureInfo.InvariantCulture) : character);
            upperNext = false;
        }

        return builder.ToString();
    }
}
