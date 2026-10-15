using System.Collections.ObjectModel;
using FluentIcons.Common;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class SkillNodeViewModel(
    string title,
    Icon icon,
    bool isSkill = false,
    string? category = null,
    string? name = null,
    bool editable = false,
    bool canAddSkill = false,
    ObservableCollection<SkillNodeViewModel>? children = null)
{
    public string Title { get; } = title;

    public Icon Icon { get; } = icon;

    public bool IsSkill { get; } = isSkill;

    public string? Category { get; } = category;

    public string? Name { get; } = name;

    public bool Editable { get; } = editable;

    public bool CanAddSkill { get; } = canAddSkill;

    public ObservableCollection<SkillNodeViewModel> Children { get; } = children ?? [];
}
