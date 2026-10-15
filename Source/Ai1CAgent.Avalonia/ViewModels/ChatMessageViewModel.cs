using System;
using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChatMessageViewModel(AiChatRole role, string text, DateTimeOffset? timestamp = null)
    : ViewModelBase
{
    private const int MaxDots = 3;
    private const char DotFill = ' ';
    private const char Dot = '.';

    private readonly DateTimeOffset _timestamp = timestamp ?? DateTimeOffset.Now;

    private string _text = text;
    private string _reasoning = string.Empty;
    private bool _isThinking;
    private bool _isReasoningExpanded;
    private int _dots;

    public AiChatRole Role { get; } = role;

    public bool IsUser => Role is AiChatRole.User;

    public bool IsAssistant => Role is not AiChatRole.User;

    public string TimeText => _timestamp.ToString("HH:mm");

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    public string Reasoning
    {
        get => _reasoning;
        private set
        {
            this.RaiseAndSetIfChanged(ref _reasoning, value);
            this.RaisePropertyChanged(nameof(HasReasoning));
            this.RaisePropertyChanged(nameof(IsReasoningVisible));
            this.RaisePropertyChanged(nameof(IsReasoningBodyVisible));
            this.RaisePropertyChanged(nameof(ReasoningTitle));
        }
    }

    public bool HasReasoning => _reasoning.Length > 0;

    public bool IsReasoningVisible => IsAssistant && (_isThinking || HasReasoning);

    public bool IsReasoningBodyVisible => _isReasoningExpanded && HasReasoning;

    public bool IsThinking
    {
        get => _isThinking;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isThinking, value);
            this.RaisePropertyChanged(nameof(IsReasoningVisible));
            this.RaisePropertyChanged(nameof(ReasoningTitle));
        }
    }

    public bool IsReasoningExpanded
    {
        get => _isReasoningExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isReasoningExpanded, value);
            this.RaisePropertyChanged(nameof(IsReasoningBodyVisible));
        }
    }

    public string ReasoningTitle => _isThinking
        ? $"Думает{new string(Dot, _dots)}{new string(DotFill, MaxDots - _dots)}"
        : "Ход размышлений";

    public void BeginThinking() => IsThinking = true;

    public void FinishThinking() => IsThinking = false;

    public void AppendText(string delta)
    {
        if (delta.Length == 0)
            return;

        Text += delta;
        FinishThinking();
    }

    public void AppendReasoning(string delta)
    {
        if (delta.Length == 0)
            return;

        Reasoning += delta;
    }

    public void AdvanceThinkingDots()
    {
        _dots = (_dots + 1) % (MaxDots + 1);
        this.RaisePropertyChanged(nameof(ReasoningTitle));
    }
}
