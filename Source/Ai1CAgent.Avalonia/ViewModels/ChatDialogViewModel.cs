using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ai1CAgent.Core.Models;
using Avalonia.Threading;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChatDialogViewModel : ViewModelBase
{
    private const int MaxTitleLength = 40;
    private static readonly TimeSpan ThinkingTickInterval = TimeSpan.FromMilliseconds(320);

    private string _title = "Новый диалог";
    private DispatcherTimer? _thinkingTimer;

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

    public ChatMessageViewModel AddStreamingMessage()
    {
        var message = AddMessage(AiChatRole.Assistant, string.Empty);
        message.BeginThinking();
        StartThinkingAnimation();

        return message;
    }

    public IReadOnlyCollection<AiChatMessage> ToAiMessages()
        => Messages
            .Where(message => message.Text.Length > 0 || message.Reasoning.Length > 0)
            .Select(message => new AiChatMessage(message.Role, message.Text))
            .ToArray();

    private void StartThinkingAnimation()
    {
        _thinkingTimer ??= CreateThinkingTimer();
        _thinkingTimer.Start();
    }

    private DispatcherTimer CreateThinkingTimer()
    {
        var timer = new DispatcherTimer { Interval = ThinkingTickInterval };
        timer.Tick += OnThinkingTick;
        return timer;
    }

    private void OnThinkingTick(object? sender, EventArgs e)
    {
        var isThinking = false;

        foreach (var message in Messages)
        {
            if (!message.IsThinking)
                continue;

            isThinking = true;
            message.AdvanceThinkingDots();
        }

        if (!isThinking)
            _thinkingTimer?.Stop();
    }

    private static string BuildTitle(string text)
    {
        var singleLine = text.ReplaceLineEndings(" ").Trim();

        return singleLine.Length <= MaxTitleLength
            ? singleLine
            : $"{singleLine[..MaxTitleLength].TrimEnd()}…";
    }
}
