using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public abstract class MessageBlockViewModel : ViewModelBase
{
    public abstract MessageBlock ToBlock();

    public virtual bool IsTool => false;

    public virtual string ToolName => string.Empty;

    public virtual bool IsUser => false;

    public virtual string Text
    {
        get => string.Empty;
        set { }
    }
}

public sealed class MessageTextViewModel(bool isUser) : MessageBlockViewModel
{
    private string _text = string.Empty;

    public override bool IsUser { get; } = isUser;

    public override string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    public void Append(string delta) => Text += delta;

    public override MessageBlock ToBlock() => new(Text, null);
}

public sealed class MessageToolViewModel(string name) : MessageBlockViewModel
{
    public override bool IsTool => true;

    public override string ToolName { get; } = name;

    public override MessageBlock ToBlock() => new(null, ToolName);
}
