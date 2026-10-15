using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Avalonia.Threading;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChatDialogViewModel : ViewModelBase
{
    private const int MaxTitleLength = 40;
    private static readonly TimeSpan ThinkingTickInterval = TimeSpan.FromMilliseconds(320);

    private string _title = "Новый диалог";
    private string _configurationPath = string.Empty;
    private bool _isConfigurationDump;
    private bool _isRenaming;
    private string _pendingTitle = string.Empty;
    private DispatcherTimer? _thinkingTimer;
    private AiTokenUsage? _usage;

    public long? SessionId { get; set; }

    public long? ProjectId { get; set; }

    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => this.RaiseAndSetIfChanged(ref _isRenaming, value);
    }

    public string PendingTitle
    {
        get => _pendingTitle;
        set => this.RaiseAndSetIfChanged(ref _pendingTitle, value);
    }

    public string ConfigurationPath => _configurationPath;

    public bool HasConfiguration => _configurationPath.Length > 0;

    public bool IsConfigurationWarningVisible => HasConfiguration && !_isConfigurationDump;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public bool IsEmpty => Messages.Count == 0;

    public bool HasContextUsage => TotalTokens() is not null;

    public string ContextUsageText => TotalTokens() is { } total ? FormatTokens(total) : string.Empty;

    public string ContextUsageTooltip => TotalTokens() is { } total
        ? $"Занято токенов в текущем диалоге: {total:N0}"
        : string.Empty;

    public void SetConfiguration(string path, bool isConfigurationDump)
    {
        _configurationPath = path;
        _isConfigurationDump = isConfigurationDump;

        this.RaisePropertyChanged(nameof(ConfigurationPath));
        this.RaisePropertyChanged(nameof(HasConfiguration));
        this.RaisePropertyChanged(nameof(IsConfigurationWarningVisible));
    }

    public void SetTitle(string title)
    {
        Title = title;
        PendingTitle = title;
    }

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

    public async Task LoadMessagesAsync(IAsyncEnumerable<ChatMessageRecord> messages, CancellationToken cancellationToken)
    {
        Messages.Clear();
        AiTokenUsage? usage = null;

        await foreach (var record in messages.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var message = new ChatMessageViewModel(record.Role, string.Empty, record.CreatedAt);
            message.AppendReasoning(record.Reasoning);

            var blocks = MessageBlocksSerializer.Deserialize(record.Content);

            if (blocks.Count > 0)
                message.LoadBlocks(blocks);
            else if (record.Text.Length > 0)
                message.AppendText(record.Text);

            Messages.Add(message);

            if (record.Usage is { } recordUsage)
                usage = recordUsage;
        }

        if (usage is { } finalUsage)
            ApplyUsage(finalUsage);
    }

    public void ApplyUsage(AiTokenUsage usage)
    {
        _usage = usage;
        this.RaisePropertyChanged(nameof(HasContextUsage));
        this.RaisePropertyChanged(nameof(ContextUsageText));
        this.RaisePropertyChanged(nameof(ContextUsageTooltip));
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

    private long? TotalTokens()
    {
        if (_usage is null)
            return null;

        if (_usage.TotalTokens is { } total)
            return total;

        var sum = (_usage.InputTokens ?? 0) + (_usage.OutputTokens ?? 0);

        return sum > 0 ? sum : null;
    }

    private static string FormatTokens(long tokens) => tokens switch
    {
        >= 1_000_000 => $"{tokens / 1_000_000d:0.#}М",
        >= 1_000 => $"{tokens / 1_000d:0.#}К",
        _ => tokens.ToString(),
    };

    private static string BuildTitle(string text)
    {
        var singleLine = text.ReplaceLineEndings(" ").Trim();

        return singleLine.Length <= MaxTitleLength
            ? singleLine
            : $"{singleLine[..MaxTitleLength].TrimEnd()}…";
    }
}
