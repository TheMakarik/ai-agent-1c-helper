using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Ai1CAgent.Avalonia.Views.Controls;

public sealed class DiffView : ContentControl
{
    private const int MaxLines = 5000;

    public static readonly StyledProperty<string?> DiffProperty =
        AvaloniaProperty.Register<DiffView, string?>(nameof(Diff));

    private static readonly IBrush AddedBrush = new SolidColorBrush(Color.Parse("#1B5E20"));
    private static readonly IBrush RemovedBrush = new SolidColorBrush(Color.Parse("#B71C1C"));
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.Parse("#6E6E6E"));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.Parse("#1F1F1F"));
    private static readonly FontFamily Mono = new("monospace");

    private readonly StackPanel _panel = new();

    public DiffView()
    {
        Content = new ScrollViewer
        {
            Content = _panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    public string? Diff
    {
        get => GetValue(DiffProperty);
        set => SetValue(DiffProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DiffProperty)
            Render(Diff);
    }

    private void Render(string? diff)
    {
        _panel.Children.Clear();

        if (string.IsNullOrEmpty(diff))
            return;

        var count = 0;

        foreach (var line in diff.ReplaceLineEndings("\n").Split('\n'))
        {
            if (count++ >= MaxLines)
            {
                _panel.Children.Add(CreateLine("… (diff обрезан)", HeaderBrush));
                break;
            }

            _panel.Children.Add(CreateLine(line, BrushFor(line)));
        }
    }

    private static IBrush BrushFor(string line) =>
        line.StartsWith("+++", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal) ? HeaderBrush
        : line.StartsWith('+') ? AddedBrush
        : line.StartsWith('-') ? RemovedBrush
        : line.StartsWith("@@", StringComparison.Ordinal) ? HeaderBrush
        : NormalBrush;

    private static TextBlock CreateLine(string text, IBrush brush) => new()
    {
        Text = text.Length == 0 ? " " : text,
        Foreground = brush,
        FontFamily = Mono,
        TextWrapping = TextWrapping.NoWrap,
    };
}
