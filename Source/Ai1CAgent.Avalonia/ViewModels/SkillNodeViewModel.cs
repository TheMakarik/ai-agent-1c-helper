using System.Collections.ObjectModel;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class SkillNodeViewModel(
    string title,
    bool isSkill = false,
    string? category = null,
    string? name = null,
    bool editable = false,
    ObservableCollection<SkillNodeViewModel>? children = null)
{
    public string Title { get; } = title;

    public bool IsSkill { get; } = isSkill;

    public string? Category { get; } = category;

    public string? Name { get; } = name;

    public bool Editable { get; } = editable;

    public ObservableCollection<SkillNodeViewModel> Children { get; } = children ?? [];
}
