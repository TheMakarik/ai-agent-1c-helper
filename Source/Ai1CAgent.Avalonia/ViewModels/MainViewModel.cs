using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Modules.Ai.Interfaces;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IAiChatService _chatService;
    private AiModel? _selectedModel;
    private NavigationItemViewModel _selectedSection;
    private ChatDialogViewModel _selectedDialog;
    private string _prompt = string.Empty;

    public MainViewModel(
        IAiModelCatalog modelCatalog,
        IAiChatService chatService,
        ILogger<MainViewModel> logger)
    {
        _chatService = chatService;
        Models = modelCatalog.GetModels();
        _selectedModel = Models.Count > 0 ? Models[0] : null;

        Sections =
        [
            new(AppSection.Chat, "Чат"),
            new(AppSection.Projects, "Проекты"),
            new(AppSection.Documentation, "Документация"),
            new(AppSection.Changes, "Изменения"),
            new(AppSection.Settings, "Настройки"),
        ];
        _selectedSection = Sections[0];

        _selectedDialog = new ChatDialogViewModel();
        Dialogs.Add(_selectedDialog);

        var canSend = Changed
            .Select(_ => CanSend())
            .StartWith(CanSend())
            .DistinctUntilChanged();

        SendCommand = ReactiveCommand.CreateFromTask(SendAsync, canSend);
        SendCommand.ThrownExceptions.Subscribe(ex =>
        {
            logger.LogError(ex, "Ошибка запроса к нейросети");
            SelectedDialog.AddMessage(AiChatRole.Assistant, $"Ошибка: {ex.Message}");
        });

        NewDialogCommand = ReactiveCommand.Create(CreateDialog);
    }

    public IReadOnlyList<AiModel> Models { get; }

    public IReadOnlyList<NavigationItemViewModel> Sections { get; }

    public ObservableCollection<ChatDialogViewModel> Dialogs { get; } = [];

    public AiModel? SelectedModel
    {
        get => _selectedModel;
        set => this.RaiseAndSetIfChanged(ref _selectedModel, value);
    }

    public NavigationItemViewModel SelectedSection
    {
        get => _selectedSection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSection, value);
            this.RaisePropertyChanged(nameof(IsChatSelected));
        }
    }

    public bool IsChatSelected => SelectedSection.Section is AppSection.Chat;

    public ChatDialogViewModel SelectedDialog
    {
        get => _selectedDialog;
        set => this.RaiseAndSetIfChanged(ref _selectedDialog, value);
    }

    public string Prompt
    {
        get => _prompt;
        set => this.RaiseAndSetIfChanged(ref _prompt, value);
    }

    public ReactiveCommand<Unit, Unit> SendCommand { get; }

    public ReactiveCommand<Unit, Unit> NewDialogCommand { get; }

    private bool CanSend() => SelectedModel is not null && !string.IsNullOrWhiteSpace(Prompt);

    private void CreateDialog()
    {
        SelectedSection = Sections[0];

        if (SelectedDialog.IsEmpty)
            return;

        var dialog = new ChatDialogViewModel();
        Dialogs.Insert(0, dialog);
        SelectedDialog = dialog;
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (SelectedModel is null)
            return;

        var dialog = SelectedDialog;
        var text = Prompt.Trim();
        Prompt = string.Empty;

        dialog.AddMessage(AiChatRole.User, text);
        var history = dialog.ToAiMessages();

        ChatMessageViewModel? answer = null;
        await foreach (var chunk in _chatService.StreamAsync(SelectedModel, history, cancellationToken))
        {
            answer ??= dialog.AddMessage(AiChatRole.Assistant, string.Empty);
            answer.Text += chunk;
        }
    }
}
