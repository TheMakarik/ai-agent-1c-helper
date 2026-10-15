using Avalonia;
using Avalonia.Controls;

namespace Ai1CAgent.Avalonia.Views;

public partial class MainWindow : Window
{
    private const double StickToBottomThreshold = 24;

    public MainWindow()
    {
        InitializeComponent();
        MessagesScrollViewer.PropertyChanged += OnMessagesScrollViewerPropertyChanged;
    }

    private void OnMessagesScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.ExtentProperty || e.OldValue is not Size oldExtent)
            return;

        var viewer = MessagesScrollViewer;
        var wasAtBottom = viewer.Offset.Y + viewer.Viewport.Height >= oldExtent.Height - StickToBottomThreshold;
        if (wasAtBottom)
            viewer.ScrollToEnd();
    }
}
