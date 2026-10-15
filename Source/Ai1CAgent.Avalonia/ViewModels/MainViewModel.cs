using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.Services;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Configuration.Interfaces;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IAiModelCatalog _modelCatalog;
    private readonly IAiSettingsStore _settingsStore;
    private readonly IAiChatService _chatService;
    private readonly IChatStore _chatStore;
    private readonly IConfigurationToolProvider _configurationToolProvider;
    private readonly IConfigurationPathStore _configurationPathStore;
    private readonly IFolderPickerService _folderPicker;
    private readonly IUiStateStore _uiStateStore;
    private readonly ILogger<MainViewModel> _logger;
    private List<AiModel> _allModels = [];
    private ProviderPickerViewModel? _selectedChatProvider;
    private AiModel? _selectedChatModel;
    private NavigationItemViewModel _selectedSection;
    private ChatDialogViewModel _selectedDialog;
    private string _prompt = string.Empty;
    private bool _isConfirmSaveOpen;
    private bool _isModelPickerOpen;
    private bool _isConfirmPending;
    private string _confirmSaveError = string.Empty;
    private bool _initialized;

    public MainViewModel(
        IOptions<AiOptions> aiOptions,
        IAiModelCatalog modelCatalog,
        IAiSettingsStore settingsStore,
        IAiChatService chatService,
        IChatStore chatStore,
        IConfigurationToolProvider configurationToolProvider,
        IConfigurationPathStore configurationPathStore,
        IFolderPickerService folderPicker,
        IUiStateStore uiStateStore,
        ProjectsViewModel projects,
        SkillsViewModel skills,
        ChangesViewModel changes,
        SettingsViewModel settings,
        ILogger<MainViewModel> logger)
    {
        _modelCatalog = modelCatalog;
        _settingsStore = settingsStore;
        _chatService = chatService;
        _chatStore = chatStore;
        _configurationToolProvider = configurationToolProvider;
        _configurationPathStore = configurationPathStore;
        _folderPicker = folderPicker;
        _uiStateStore = uiStateStore;
        _logger = logger;
        Projects = projects;
        Skills = skills;
        Changes = changes;
        Settings = settings;
        Title = aiOptions.Value.Title;

        Sections =
        [
            new(AppSection.Chat, "Чат"),
            new(AppSection.Projects, "Проекты"),
            new(AppSection.Documentation, "База Знаний"),
            new(AppSection.Changes, "Изменения"),
            new(AppSection.Settings, "Настройки"),
        ];
        _selectedSection = Sections[0];

        _selectedDialog = CreateDialogViewModel();
        Dialogs.Add(_selectedDialog);

        RefreshModels();
        ApplySavedModel(_uiStateStore.Get());

        settingsStore.SettingsChanged += (_, _) => Dispatcher.UIThread.Post(RefreshModels);
        Projects.SelectionChanged += OnProjectSelectionChanged;

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
        SelectChatModelCommand = ReactiveCommand.Create<AiModel>(SelectChatModel);
        OpenProjectFolderCommand = ReactiveCommand.Create<string>(
            path => OpenFolder.Handle(path).Subscribe(_ => { }));

        BeginRenameDialogCommand = ReactiveCommand.Create<ChatDialogViewModel>(BeginRenameDialog);
        CancelRenameDialogCommand = ReactiveCommand.Create<ChatDialogViewModel>(CancelRenameDialog);

        CommitRenameDialogCommand = ReactiveCommand.CreateFromTask<ChatDialogViewModel>(CommitRenameDialogAsync);
        CommitRenameDialogCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Не удалось переименовать диалог"));

        SelectConfigurationCommand = ReactiveCommand.CreateFromTask(SelectConfigurationAsync);
        SelectConfigurationCommand.ThrownExceptions.Subscribe(ex =>
            _logger.LogError(ex, "Не удалось выбрать папку выгрузки конфигурации"));

        ClearConfigurationCommand = ReactiveCommand.Create(ClearConfiguration);

        ConfirmUnsavedChanges = new Interaction<Unit, bool>();

        _ = InitializeAsync();
    }

    public string Title { get; }

    public ObservableCollection<ProviderPickerViewModel> ChatProviders { get; } = [];

    public IReadOnlyList<NavigationItemViewModel> Sections { get; }

    public ProjectsViewModel Projects { get; }

    public SkillsViewModel Skills { get; }

    public ChangesViewModel Changes { get; }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<ChatDialogViewModel> Dialogs { get; } = [];

    public Interaction<Unit, bool> ConfirmUnsavedChanges { get; }

    public Interaction<string, Unit> OpenFolder { get; } = new();

    public ProviderPickerViewModel? SelectedChatProvider
    {
        get => _selectedChatProvider;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChatProvider, value);
            SyncChatModelToProvider();
        }
    }

    public AiModel? SelectedChatModel
    {
        get => _selectedChatModel;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChatModel, value);
            this.RaisePropertyChanged(nameof(SelectedModel));
            SaveUiState();
        }
    }

    public AiModel? SelectedModel => SelectedChatModel;

    public bool IsModelPickerOpen
    {
        get => _isModelPickerOpen;
        set
        {
            this.RaiseAndSetIfChanged(ref _isModelPickerOpen, value);

            if (value)
                SyncChatProviderToSelection();
        }
    }

    public bool IsConfirmSaveOpen
    {
        get => _isConfirmSaveOpen;
        set
        {
            this.RaiseAndSetIfChanged(ref _isConfirmSaveOpen, value);
            if (value)
                ConfirmSaveError = string.Empty;
        }
    }

    public string ConfirmSaveError
    {
        get => _confirmSaveError;
        set => this.RaiseAndSetIfChanged(ref _confirmSaveError, value);
    }

    public NavigationItemViewModel SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedSection))
                return;

            if (_selectedSection.Section is AppSection.Settings && Settings.IsDirty)
            {
                this.RaisePropertyChanged(nameof(SelectedSection));

                if (!_isConfirmPending)
                    ConfirmSectionChange(value);

                return;
            }

            CommitSection(value);
        }
    }

    public bool IsChatSelected => SelectedSection.Section is AppSection.Chat;

    public bool IsProjectsSelected => SelectedSection.Section is AppSection.Projects;

    public bool IsDocumentationSelected => SelectedSection.Section is AppSection.Documentation;

    public bool IsChangesSelected => SelectedSection.Section is AppSection.Changes;

    public bool IsSettingsSelected => SelectedSection.Section is AppSection.Settings;

    public bool IsPlaceholderSelected => !IsChatSelected && !IsProjectsSelected && !IsDocumentationSelected && !IsChangesSelected && !IsSettingsSelected;

    public ChatDialogViewModel SelectedDialog
    {
        get => _selectedDialog;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDialog, value);
            _ = LoadDialogMessagesAsync(value);
            SaveUiState();
        }
    }

    public string Prompt
    {
        get => _prompt;
        set => this.RaiseAndSetIfChanged(ref _prompt, value);
    }

    public ReactiveCommand<Unit, Unit> SendCommand { get; }

    public ReactiveCommand<Unit, Unit> NewDialogCommand { get; }

    public ReactiveCommand<AiModel, Unit> SelectChatModelCommand { get; }

    public ReactiveCommand<string, Unit> OpenProjectFolderCommand { get; }

    public ReactiveCommand<ChatDialogViewModel, Unit> BeginRenameDialogCommand { get; }

    public ReactiveCommand<ChatDialogViewModel, Unit> CommitRenameDialogCommand { get; }

    public ReactiveCommand<ChatDialogViewModel, Unit> CancelRenameDialogCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectConfigurationCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearConfigurationCommand { get; }

    private bool CanSend() => SelectedModel is not null && !string.IsNullOrWhiteSpace(Prompt);

    private void CommitSection(NavigationItemViewModel value)
    {
        this.RaiseAndSetIfChanged(ref _selectedSection, value);
        this.RaisePropertyChanged(nameof(IsChatSelected));
        this.RaisePropertyChanged(nameof(IsProjectsSelected));
        this.RaisePropertyChanged(nameof(IsDocumentationSelected));
        this.RaisePropertyChanged(nameof(IsChangesSelected));
        this.RaisePropertyChanged(nameof(IsSettingsSelected));
        this.RaisePropertyChanged(nameof(IsPlaceholderSelected));
    }

    private void ConfirmSectionChange(NavigationItemViewModel section)
    {
        _isConfirmPending = true;

        ConfirmUnsavedChanges.Handle(Unit.Default).Subscribe(proceed =>
        {
            _isConfirmPending = false;

            if (proceed)
            {
                CommitSection(section);
            }
        });
    }

    private void SyncChatProviderToSelection()
    {
        if (SelectedChatModel is not { } model)
            return;

        var provider = ChatProviders.FirstOrDefault(item => item.ProviderKey == model.ProviderKey);

        if (provider is not null)
            SelectedChatProvider = provider;
    }

    private void SyncChatModelToProvider()
    {
        if (SelectedChatProvider is null)
            return;

        if (SelectedChatModel is { } current && SelectedChatProvider.Models.Contains(current))
            return;

        SelectedChatModel = SelectedChatProvider.Models.FirstOrDefault();
    }

    private void SelectChatModel(AiModel model)
    {
        SelectedChatModel = model;
        IsModelPickerOpen = false;
    }

    private void RefreshModels()
    {
        _allModels = [.. _modelCatalog.GetModels()];

        var previous = SelectedChatModel;
        ChatProviders.Clear();

        foreach (var settings in _settingsStore.GetProviders())
        {
            var key = settings.Provider is { } provider ? provider.ToString() : settings.Name;
            var models = _allModels.Where(model => model.ProviderKey == key).ToList();
            ChatProviders.Add(new ProviderPickerViewModel(key, settings.DisplayName, models));
        }

        var selected = ChatProviders.FirstOrDefault(item => item.ProviderKey == previous?.ProviderKey)
            ?? ChatProviders.FirstOrDefault();

        SelectedChatProvider = selected;
        SelectedChatModel = selected?.Models.FirstOrDefault(model => model == previous)
            ?? selected?.Models.FirstOrDefault();
    }

    private async Task InitializeAsync()
    {
        var state = _uiStateStore.Get();

        await Projects.LoadAsync(CancellationToken.None);
        Projects.Select(state.ProjectId);
        Skills.Reload(Projects.SelectedProject?.Id);
        Changes.Reload(Projects.SelectedProject?.Path);
        await ReloadDialogsAsync(state.SessionId);

        _initialized = true;
    }

    private void OnProjectSelectionChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            Skills.Reload(Projects.SelectedProject?.Id);
            Changes.Reload(Projects.SelectedProject?.Path);
            _ = ReloadDialogsAsync();
            SaveUiState();
        });

    private async Task ReloadDialogsAsync(long? preferredSessionId = null)
    {
        try
        {
            var projectId = Projects.SelectedProject?.Id;
            var sessions = await _chatStore.GetSessionsAsync(projectId, CancellationToken.None);

            Dialogs.Clear();

            foreach (var session in sessions)
            {
                var dialog = new ChatDialogViewModel { SessionId = session.Id, ProjectId = session.ProjectId };
                dialog.SetTitle(session.Title);
                ApplyProjectConfiguration(dialog);
                Dialogs.Add(dialog);
            }

            if (Dialogs.Count == 0)
                Dialogs.Add(CreateDialogViewModel());

            SelectedDialog = (preferredSessionId is { } sessionId
                ? Dialogs.FirstOrDefault(dialog => dialog.SessionId == sessionId)
                : null) ?? Dialogs[0];
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить список диалогов");
        }
    }

    private async Task LoadDialogMessagesAsync(ChatDialogViewModel dialog)
    {
        if (dialog is null || dialog.SessionId is not { } sessionId || !dialog.IsEmpty)
            return;

        try
        {
            await dialog.LoadMessagesAsync(_chatStore.GetMessagesAsync(sessionId, CancellationToken.None), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить сообщения диалога");
        }
    }

    private void CreateDialog()
    {
        var chatSection = Sections[0];
        if (!ReferenceEquals(SelectedSection, chatSection))
        {
            if (_selectedSection.Section is AppSection.Settings && Settings.IsDirty)
            {
                if (!_isConfirmPending)
                    ConfirmSectionChange(chatSection);

                return;
            }

            CommitSection(chatSection);
        }

        if (SelectedDialog.IsEmpty)
            return;

        var dialog = CreateDialogViewModel();
        Dialogs.Insert(0, dialog);
        SelectedDialog = dialog;
    }

    private ChatDialogViewModel CreateDialogViewModel()
    {
        var dialog = new ChatDialogViewModel();
        ApplyProjectConfiguration(dialog);

        return dialog;
    }

    private void ApplyProjectConfiguration(ChatDialogViewModel dialog)
    {
        if (Projects.SelectedProject is { CanSelect: true } project)
        {
            dialog.ProjectId = project.Id;
            dialog.SetConfiguration(project.Path, IsConfigurationFolder(project.Path));
            return;
        }

        var lastPath = _configurationPathStore.GetLastPath();

        if (lastPath.Length > 0)
            dialog.SetConfiguration(lastPath, IsConfigurationFolder(lastPath));
    }

    private bool IsConfigurationFolder(string path) =>
        Settings.AllowWrite || _configurationToolProvider.IsConfigurationDump(path);

    public void BeginRenameDialog(ChatDialogViewModel dialog)
    {
        dialog.PendingTitle = dialog.Title;
        dialog.IsRenaming = true;
    }

    public void CancelRenameDialog(ChatDialogViewModel dialog)
    {
        dialog.PendingTitle = dialog.Title;
        dialog.IsRenaming = false;
    }

    public async Task CommitRenameDialogAsync(ChatDialogViewModel dialog, CancellationToken cancellationToken)
    {
        var title = dialog.PendingTitle.Trim();

        if (title.Length == 0)
        {
            CancelRenameDialog(dialog);
            return;
        }

        if (!string.Equals(title, dialog.Title, StringComparison.Ordinal))
        {
            if (dialog.SessionId is { } sessionId)
                await _chatStore.RenameSessionAsync(sessionId, title, cancellationToken);

            dialog.SetTitle(title);
        }

        dialog.IsRenaming = false;
    }

    public async Task DeleteDialogAsync(ChatDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (dialog.SessionId is { } sessionId)
            await _chatStore.DeleteSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);

        Dialogs.Remove(dialog);

        if (Dialogs.Count == 0)
            Dialogs.Add(CreateDialogViewModel());

        if (SelectedDialog is null || !Dialogs.Contains(SelectedDialog))
            SelectedDialog = Dialogs[0];

        SaveUiState();
    }

    private async Task SelectConfigurationAsync()
    {
        var dialog = SelectedDialog;
        var startPath = dialog.HasConfiguration ? dialog.ConfigurationPath : _configurationPathStore.GetLastPath();
        var path = await _folderPicker.PickFolderAsync("Папка выгрузки конфигурации 1С", startPath);

        if (string.IsNullOrWhiteSpace(path))
            return;

        dialog.SetConfiguration(path, IsConfigurationFolder(path));
        await _configurationPathStore.SaveLastPathAsync(path);
    }

    private void ClearConfiguration() => SelectedDialog.SetConfiguration(string.Empty, isConfigurationDump: false);

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (SelectedModel is null)
            return;

        var text = Prompt.Trim();

        if (text.Length == 0)
            return;

        Prompt = string.Empty;

        var dialog = SelectedDialog;
        dialog.AddMessage(AiChatRole.User, text);

        var sessionId = await EnsureSessionAsync(dialog, cancellationToken);
        await AppendMessageAsync(sessionId, AiChatRole.User, text, string.Empty, null, string.Empty, cancellationToken);

        var history = dialog.ToAiMessages();
        ChatMessageViewModel? answer = null;
        var answerText = new StringBuilder();
        var answerReasoning = new StringBuilder();
        AiTokenUsage? usage = null;

        try
        {
            await foreach (var chunk in _chatService.StreamAsync(SelectedModel, history, dialog.ConfigurationPath, dialog.ProjectId, cancellationToken))
            {
                if (chunk.ToolName is { Length: > 0 } toolName)
                {
                    answer ??= dialog.AddStreamingMessage();
                    var toolText = ToolNameDisplay.Resolve(toolName);

                    if (chunk.ToolDetail is { Length: > 0 } detail)
                        toolText = $"{toolText} · {detail}";

                    answer.AddTool(toolText);
                }

                if (chunk.Text.Length > 0 || chunk.Reasoning.Length > 0)
                {
                    answer ??= dialog.AddStreamingMessage();
                    answer.AppendReasoning(chunk.Reasoning);
                    answer.AppendText(chunk.Text);
                    answerText.Append(chunk.Text);
                    answerReasoning.Append(chunk.Reasoning);
                }

                if (chunk.Usage is { } chunkUsage)
                {
                    usage = chunkUsage;
                    dialog.ApplyUsage(chunkUsage);
                }
            }
        }
        finally
        {
            answer?.FinishThinking();
        }

        if (answerText.Length > 0 || answerReasoning.Length > 0)
        {
            await AppendMessageAsync(
                sessionId,
                AiChatRole.Assistant,
                answerText.ToString(),
                answerReasoning.ToString(),
                usage,
                answer?.SerializeContent() ?? string.Empty,
                cancellationToken);
        }
    }

    private void ApplySavedModel(UiState state)
    {
        if (state.ProviderKey is not { Length: > 0 } key)
            return;

        var provider = ChatProviders.FirstOrDefault(item => item.ProviderKey == key);

        if (provider is null)
            return;

        SelectedChatProvider = provider;

        if (state.ModelName is { Length: > 0 } name &&
            provider.Models.FirstOrDefault(model => model.Name == name) is { } model)
        {
            SelectedChatModel = model;
        }
    }

    private void SaveUiState()
    {
        if (!_initialized)
            return;

        var state = new UiState(
            SelectedChatProvider?.ProviderKey,
            SelectedChatModel?.Name,
            Projects.SelectedProject?.Id,
            SelectedDialog?.SessionId);

        _ = _uiStateStore.SaveAsync(state, CancellationToken.None);
    }

    private async Task<long> EnsureSessionAsync(ChatDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (dialog.SessionId is { } existing)
            return existing;

        var session = await _chatStore.CreateSessionAsync(dialog.ProjectId, dialog.Title, cancellationToken);
        dialog.SessionId = session.Id;

        return session.Id;
    }

    private Task AppendMessageAsync(
        long sessionId,
        AiChatRole role,
        string text,
        string reasoning,
        AiTokenUsage? usage,
        string content,
        CancellationToken cancellationToken)
    {
        var record = new ChatMessageRecord(
            0,
            sessionId,
            role,
            text,
            reasoning,
            usage?.InputTokens,
            usage?.OutputTokens,
            usage?.TotalTokens,
            DateTimeOffset.UtcNow,
            content);

        return _chatStore.AppendMessageAsync(record, cancellationToken);
    }
}
