using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChatMessageViewModel : ViewModelBase
{
    private const int MaxDots = 3;
    private const char DotFill = ' ';
    private const char Dot = '.';

    private readonly DateTimeOffset _timestamp;
    private readonly StringBuilder _text = new();

    private string _reasoning = string.Empty;
    private bool _isThinking;
    private bool _isReasoningExpanded;
    private int _dots;

    public ChatMessageViewModel(AiChatRole role, string text, DateTimeOffset? timestamp = null)
    {
        Role = role;
        _timestamp = timestamp ?? DateTimeOffset.Now;

        if (text.Length > 0)
            AppendText(text);
    }

    public AiChatRole Role { get; }

    public bool IsUser => Role is AiChatRole.User;

    public bool IsAssistant => Role is not AiChatRole.User;

    public ObservableCollection<MessageBlockViewModel> Blocks { get; } = [];

    public string Text => _text.ToString();

    public string TimeText => _timestamp.ToString("HH:mm");

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

        _text.Append(delta);

        if (Blocks.Count == 0 || Blocks[^1] is not MessageTextViewModel textBlock)
        {
            textBlock = new MessageTextViewModel(IsUser);
            Blocks.Add(textBlock);
        }

        textBlock.Append(delta);
        FinishThinking();
    }

    public void AddTool(string name)
    {
        if (name.Length == 0)
            return;

        Blocks.Add(new MessageToolViewModel(name));
    }

    public void AppendReasoning(string delta)
    {
        if (delta.Length == 0)
            return;

        Reasoning += delta;
    }

    public void LoadBlocks(IReadOnlyList<MessageBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.Tool is { Length: > 0 } tool)
            {
                Blocks.Add(new MessageToolViewModel(tool));
            }
            else if (block.Text is { Length: > 0 } text)
            {
                Blocks.Add(new MessageTextViewModel(IsUser) { Text = text });
                _text.Append(text);
            }
        }
    }

    public string SerializeContent() =>
        MessageBlocksSerializer.Serialize([.. Blocks.Select(block => block.ToBlock())]);

    public void AdvanceThinkingDots()
    {
        _dots = (_dots + 1) % (MaxDots + 1);
        this.RaisePropertyChanged(nameof(ReasoningTitle));
    }
}
