namespace Ai1CAgent.Avalonia.ViewModels;

public sealed record SkillEditRequest(string Name, string Text, bool Editable, bool AskName);

public sealed record SkillEditResult(string Name, string Text);
