using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Markdown.Avalonia;

namespace Ai1CAgent.Avalonia.Views.Controls;

public sealed class MarkdownView : ContentControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private static readonly Regex CodeFence = new(
        @"^[ \t]*```[ \t]*(?<lang>[A-Za-z0-9#+._-А-Яа-яЁё]*)[ \t]*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly BslRegistryOptions Registry = new();

    private readonly MarkdownScrollViewer _viewer;
    private readonly HashSet<TextEditor> _highlighted = [];
    private string[] _languages = [];
    private string? _pending;
    private bool _renderQueued;

    static MarkdownView()
        => MarkdownProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.ScheduleRender());

    public MarkdownView()
    {
        _viewer = new MarkdownScrollViewer();

        Content = _viewer;
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => InstallGrammars();

    private void ScheduleRender()
    {
        _pending = Markdown;

        if (_renderQueued)
            return;

        _renderQueued = true;
        Dispatcher.UIThread.Post(Render, DispatcherPriority.Background);
    }

    private void Render()
    {
        _renderQueued = false;

        var markdown = _pending ?? string.Empty;
        _pending = null;

        _languages = ParseLanguages(markdown);
        _highlighted.Clear();
        _viewer.Markdown = markdown;

        InstallGrammars();
    }

    private void InstallGrammars()
    {
        var editors = this.GetVisualDescendants().OfType<TextEditor>().ToList();

        _highlighted.IntersectWith(editors);

        for (var index = 0; index < editors.Count; index++)
        {
            var editor = editors[index];

            if (!_highlighted.Add(editor))
                continue;

            var scope = index < _languages.Length ? Registry.ResolveScope(_languages[index]) : null;

            if (scope is null)
                continue;

            editor.InstallTextMate(Registry).SetGrammar(scope);
        }
    }

    private static string[] ParseLanguages(string markdown)
    {
        if (markdown.Length == 0)
            return [];

        var fences = CodeFence.Matches(markdown);
        var languages = new string[fences.Count / 2];

        for (var index = 0; index < languages.Length; index++)
            languages[index] = fences[index * 2].Groups["lang"].Value;

        return languages;
    }
}
