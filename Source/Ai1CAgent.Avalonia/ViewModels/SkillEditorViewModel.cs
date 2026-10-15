using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class SkillEditorViewModel : ViewModelBase
{
    private string _name;
    private string _text;

    public SkillEditorViewModel(SkillEditRequest request)
    {
        _name = request.Name;
        _text = request.Text;
        Editable = request.Editable;
        AskName = request.AskName;
    }

    public bool Editable { get; }

    public bool AskName { get; }

    public bool IsReadOnly => !Editable;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }
}
