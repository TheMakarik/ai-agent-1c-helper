using System;
using System.Collections.ObjectModel;
using System.Reactive;
using Ai1CAgent.Avalonia.Services;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using FluentIcons.Common;
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

    public Interaction<SkillEditRequest, SkillEditResult?> EditSkill { get; } = new();

    public bool HasProject => _projectId is not null;

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
        this.RaisePropertyChanged(nameof(HasProject));
        Build();
    }

    public void AddSkill(SkillScope scope)
    {
        if (scope is SkillScope.Local && _projectId is null)
            return;

        EditSkill.Handle(new SkillEditRequest("Новый скилл", string.Empty, Editable: true, AskName: true)).Subscribe(
            result => ApplyNew(scope, result),
            exception => _logger.LogError(exception, "Не удалось создать скилл"));
    }

    public void DeleteSkill(SkillNodeViewModel node)
    {
        if (!node.Editable || node.Name is not { } name)
            return;

        var scope = node.Category == "Local" ? SkillScope.Local : SkillScope.Global;
        _skillStore.Delete(scope, _projectId, name);
        Build();
    }

    private void ApplyNew(SkillScope scope, SkillEditResult? result)
    {
        var name = result?.Name.Trim();

        if (result is null || string.IsNullOrEmpty(name))
            return;

        _skillStore.Write(scope, _projectId, name, result.Text);
        Build();
    }

    private void Build()
    {
        Roots.Clear();

        Roots.Add(new SkillNodeViewModel(
            "Системная Документация",
            Icon.Book,
            isSkill: true,
            category: "System",
            name: "Системная документация"));

        var oneC = new SkillNodeViewModel("Документация 1С", Icon.Library);
        foreach (var name in _embeddedSkills.List("1C"))
            oneC.Children.Add(new SkillNodeViewModel(name, Icon.DocumentText, isSkill: true, category: "1C", name: name));
        Roots.Add(oneC);

        var global = new SkillNodeViewModel("Глобальная Документация", Icon.Globe, canAddSkill: true);
        foreach (var name in _skillStore.List(SkillScope.Global, null))
            global.Children.Add(new SkillNodeViewModel(name, Icon.DocumentText, isSkill: true, category: "Global", name: name, editable: true));
        Roots.Add(global);

        var local = new SkillNodeViewModel("Локальная Документация", Icon.Folder, canAddSkill: _projectId is not null);
        if (_projectId is { } projectId)
        {
            foreach (var name in _skillStore.List(SkillScope.Local, projectId))
                local.Children.Add(new SkillNodeViewModel(name, Icon.DocumentText, isSkill: true, category: "Local", name: name, editable: true));
        }

        Roots.Add(local);
    }

    private void OpenSkill(SkillNodeViewModel node)
    {
        var text = Read(node);

        EditSkill.Handle(new SkillEditRequest(node.Title, text, node.Editable, AskName: false)).Subscribe(
            result => ApplyEdit(node, text, result?.Text),
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
