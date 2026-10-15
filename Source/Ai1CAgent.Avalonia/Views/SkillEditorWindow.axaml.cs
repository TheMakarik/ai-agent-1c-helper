using Ai1CAgent.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Ai1CAgent.Avalonia.Views;

public partial class SkillEditorWindow : Window
{
    public SkillEditorWindow() => InitializeComponent();

    private void OnSaveClick(object? sender, RoutedEventArgs e) =>
        Close((DataContext as SkillEditorViewModel)?.Text);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(null);
}
