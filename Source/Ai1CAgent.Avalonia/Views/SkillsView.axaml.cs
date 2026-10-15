using System;
using Ai1CAgent.Avalonia.ViewModels;
using Ai1CAgent.Core.Models;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Ai1CAgent.Avalonia.Views;

public partial class SkillsView : UserControl
{
    public SkillsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not SkillsViewModel viewModel)
            return;

        viewModel.EditSkill.RegisterHandler(async context =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                context.SetOutput(null);
                return;
            }

            var window = new SkillEditorWindow
            {
                DataContext = new SkillEditorViewModel(context.Input),
            };

            context.SetOutput(await window.ShowDialog<SkillEditResult?>(owner));
        });
    }

    private void OnAddGlobalClick(object? sender, RoutedEventArgs e) =>
        (DataContext as SkillsViewModel)?.AddSkill(SkillScope.Global);

    private void OnAddLocalClick(object? sender, RoutedEventArgs e) =>
        (DataContext as SkillsViewModel)?.AddSkill(SkillScope.Local);

    private void OnAddSkillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SkillNodeViewModel node } && DataContext is SkillsViewModel viewModel)
            viewModel.AddSkill(node.Category == "Local" ? SkillScope.Local : SkillScope.Global);
    }

    private void OnDeleteSkillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SkillNodeViewModel node } && DataContext is SkillsViewModel viewModel)
            viewModel.DeleteSkill(node);
    }
}
