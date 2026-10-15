using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChatDialogViewModel : ViewModelBase
{
    private const int MaxTitleLength = 40;
    private string _title = "Новый диалог";

    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public bool IsEmpty => Messages.Count == 0;

    public ChatMessageViewModel AddMessage(AiChatRole role, string text)
    {
        if (role is AiChatRole.User && IsEmpty)
            Title = BuildTitle(text);

        var message = new ChatMessageViewModel(role, text);
        Messages.Add(message);

        return message;
    }

    public IReadOnlyCollection<AiChatMessage> ToAiMessages()
        => Messages.Select(message => new AiChatMessage(message.Role, message.Text)).ToArray();

    private static string BuildTitle(string text)
    {
        var singleLine = text.ReplaceLineEndings(" ").Trim();

        return singleLine.Length <= MaxTitleLength
            ? singleLine
            : $"{singleLine[..MaxTitleLength].TrimEnd()}…";
    }
}
