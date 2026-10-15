using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Ai1CAgent.Avalonia.Views.Controls;

public sealed class DiffView : ContentControl
{
    public static readonly StyledProperty<string?> DiffProperty =
        AvaloniaProperty.Register<DiffView, string?>(nameof(Diff));

    private readonly TextEditor _editor;

    public DiffView()
    {
        _editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = false,
            FontFamily = new FontFamily("monospace"),
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        _editor.TextArea.TextView.LineTransformers.Add(new DiffColorizer());
        Content = _editor;
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
            _editor.Text = Diff ?? string.Empty;
    }

    private sealed class DiffColorizer : DocumentColorizingTransformer
    {
        private static readonly IBrush Added = new SolidColorBrush(Color.Parse("#1B5E20"));
        private static readonly IBrush Removed = new SolidColorBrush(Color.Parse("#B71C1C"));
        private static readonly IBrush Header = new SolidColorBrush(Color.Parse("#6E6E6E"));

        protected override void ColorizeLine(DocumentLine line)
        {
            var text = CurrentContext.Document.GetText(line);

            IBrush? brush =
                text.StartsWith("+++", System.StringComparison.Ordinal) || text.StartsWith("---", System.StringComparison.Ordinal) ? Header
                : text.StartsWith('+') ? Added
                : text.StartsWith('-') ? Removed
                : text.StartsWith("@@", System.StringComparison.Ordinal) ? Header
                : null;

            if (brush is null)
                return;

            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}
