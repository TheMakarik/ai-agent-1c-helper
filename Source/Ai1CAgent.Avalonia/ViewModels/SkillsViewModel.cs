using System;
using System.Collections.ObjectModel;
using System.Reactive;
using Ai1CAgent.Avalonia.Services;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class SkillsViewModel : ViewModelBase
{
    private readonly ISkillStore _skillStore;
    private readonly IEmbeddedSkills _embeddedSkills;
    private readonly ILogger<SkillsViewModel> _logger;

    private long? _projectId;
    private SkillNodeViewModel? _selectedNode;

    public SkillsViewModel(
        ISkillStore skillStore,
        IEmbeddedSkills embeddedSkills,
        ILogger<SkillsViewModel> logger)
    {
        _skillStore = skillStore;
        _embeddedSkills = embeddedSkills;
        _logger = logger;
    }

    public ObservableCollection<SkillNodeViewModel> Roots { get; } = [];

    public Interaction<SkillEditRequest, string?> EditSkill { get; } = new();

    public SkillNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(value, _selectedNode))
                return;

            this.RaiseAndSetIfChanged(ref _selectedNode, value);

            if (value is { IsSkill: true } node)
                OpenSkill(node);
        }
    }

    public void Reload(long? projectId)
    {
        _projectId = projectId;
        Build();
    }

    private void Build()
    {
        Roots.Clear();

        Roots.Add(new SkillNodeViewModel(
            "Системная Документация",
            isSkill: true,
            category: "System",
            name: "Системная документация"));

        var oneC = new SkillNodeViewModel("Документация 1С");
        foreach (var name in _embeddedSkills.List("1C"))
            oneC.Children.Add(new SkillNodeViewModel(name, isSkill: true, category: "1C", name: name));
        Roots.Add(oneC);

        var global = new SkillNodeViewModel("Глобальная Документация");
        foreach (var name in _skillStore.List(SkillScope.Global, null))
            global.Children.Add(new SkillNodeViewModel(name, isSkill: true, category: "Global", name: name, editable: true));
        Roots.Add(global);

        var local = new SkillNodeViewModel("Локальная Документация");
        if (_projectId is { } projectId)
        {
            foreach (var name in _skillStore.List(SkillScope.Local, projectId))
                local.Children.Add(new SkillNodeViewModel(name, isSkill: true, category: "Local", name: name, editable: true));
        }

        Roots.Add(local);
    }

    private void OpenSkill(SkillNodeViewModel node)
    {
        var text = Read(node);

        EditSkill.Handle(new SkillEditRequest(node.Title, text, node.Editable)).Subscribe(
            edited => ApplyEdit(node, text, edited),
            exception => _logger.LogError(exception, "Не удалось открыть скилл «{Name}»", node.Name));
    }

    private void ApplyEdit(SkillNodeViewModel node, string original, string? edited)
    {
        if (!node.Editable || edited is null || string.Equals(edited, original, StringComparison.Ordinal))
            return;

        var scope = node.Category == "Local" ? SkillScope.Local : SkillScope.Global;
        _skillStore.Write(scope, _projectId, node.Name!, edited);
        Build();
    }

    private string Read(SkillNodeViewModel node) => node.Category switch
    {
        "Global" => _skillStore.Read(SkillScope.Global, null, node.Name!) ?? string.Empty,
        "Local" => _skillStore.Read(SkillScope.Local, _projectId, node.Name!) ?? string.Empty,
        _ => _embeddedSkills.Read(node.Category!, node.Name!) ?? string.Empty,
    };
}
