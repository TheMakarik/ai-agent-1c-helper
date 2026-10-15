using System.Threading;
using Ai1CAgent.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Ai1CAgent.Avalonia.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView() => InitializeComponent();

    private void OnBeginRenameClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ProjectItemViewModel project } || DataContext is not ProjectsViewModel viewModel)
            return;

        viewModel.BeginRename(project);
    }

    private async void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter)
            return;

        if (sender is not Control { DataContext: ProjectItemViewModel project } || DataContext is not ProjectsViewModel viewModel)
            return;

        await viewModel.CommitRenameAsync(project, CancellationToken.None);
    }

    private async void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ProjectItemViewModel project } || DataContext is not ProjectsViewModel viewModel)
            return;

        if (!project.IsRenaming)
            return;

        await viewModel.CommitRenameAsync(project, CancellationToken.None);
    }
}
