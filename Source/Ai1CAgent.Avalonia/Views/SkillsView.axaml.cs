using System;
using Ai1CAgent.Avalonia.ViewModels;
using Avalonia.Controls;

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

            var request = context.Input;
            var window = new SkillEditorWindow
            {
                DataContext = new SkillEditorViewModel(request.Title, request.Text, request.Editable),
            };

            context.SetOutput(await window.ShowDialog<string?>(owner));
        });
    }
}
