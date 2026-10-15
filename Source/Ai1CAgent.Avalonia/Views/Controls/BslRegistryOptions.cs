using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Ai1CAgent.Avalonia.Views.Controls;

/// <summary>
/// Реестр грамматик TextMate со встроенной поддержкой языка 1С (BSL).
/// Встроенного набора TextMateSharp.Grammars для 1С нет, поэтому грамматика
/// поставляется собственным ресурсом и подкладывается по имени scope.
/// </summary>
internal sealed class BslRegistryOptions : IRegistryOptions
{
    public const string ScopeName = "source.bsl";

    private const string ResourceName = "Ai1CAgent.Avalonia.Grammars.bsl.tmLanguage.json";

    private static readonly HashSet<string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "bsl",
        "1c",
        "1с",
        "onec",
        "onescript",
        "oscript",
        "1c-enterprise",
    };

    private static readonly Lazy<IRawGrammar> BslGrammar = new(LoadBslGrammar);

    private readonly RegistryOptions _inner = new(ThemeName.Light);

    public IRawGrammar GetGrammar(string scopeName) =>
        string.Equals(scopeName, ScopeName, StringComparison.Ordinal)
            ? BslGrammar.Value
            : _inner.GetGrammar(scopeName);

    public ICollection<string> GetInjections(string scopeName) => _inner.GetInjections(scopeName);

    public IRawTheme GetTheme(string scopeName) => _inner.GetTheme(scopeName);

    public IRawTheme GetDefaultTheme() => _inner.GetDefaultTheme();

    /// <summary>
    /// Определяет TextMate-scope для языка из infostring-пометки markdown-блока кода.
    /// </summary>
    public string? ResolveScope(string language)
    {
        if (Aliases.Contains(language))
            return ScopeName;

        var extension = language.StartsWith('.') ? language : $".{language}";
        var scope = _inner.GetScopeByExtension(extension);

        if (string.IsNullOrEmpty(scope))
            scope = _inner.GetScopeByLanguageId(language);

        return string.IsNullOrEmpty(scope) ? null : scope;
    }

    private static IRawGrammar LoadBslGrammar()
    {
        using var stream = typeof(BslRegistryOptions).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Не найден встроенный грамматический ресурс «{ResourceName}»");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return GrammarReader.ReadGrammarSync(reader);
    }
}
