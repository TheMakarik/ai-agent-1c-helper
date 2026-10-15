using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class SkillEditorViewModel : ViewModelBase
{
    private string _text;

    public SkillEditorViewModel(string title, string text, bool editable)
    {
        Title = title;
        _text = text;
        Editable = editable;
    }

    public string Title { get; }

    public bool Editable { get; }

    public bool IsReadOnly => !Editable;

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }
}
