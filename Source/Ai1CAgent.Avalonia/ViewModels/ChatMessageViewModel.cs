using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChatMessageViewModel(AiChatRole role, string text) : ViewModelBase
{
    private string _text = text;

    public AiChatRole Role { get; } = role;

    public bool IsUser => Role is AiChatRole.User;

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }
}
