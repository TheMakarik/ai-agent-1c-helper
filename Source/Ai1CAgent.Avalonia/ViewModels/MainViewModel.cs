using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.Services;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Configuration.Interfaces;
using Ai1CAgent.Modules.Mcp.Interfaces;
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
    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly IMcpSettingsStore _mcpSettingsStore;
    private readonly IConfigurationToolProvider _configurationToolProvider;
    private readonly IConfigurationPathStore _configurationPathStore;
    private readonly IFolderPickerService _folderPicker;
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
    private bool? _mcpStatus;
    private bool _isMcpWarningOpen;
    private bool _mcpWarningShown;

    public MainViewModel(
        IOptions<AiOptions> aiOptions,
        IAiModelCatalog modelCatalog,
        IAiSettingsStore settingsStore,
        IAiChatService chatService,
        IMcpToolProvider mcpToolProvider,
        IMcpSettingsStore mcpSettingsStore,
        IConfigurationToolProvider configurationToolProvider,
        IConfigurationPathStore configurationPathStore,
        IFolderPickerService folderPicker,
        SettingsViewModel settings,
        ILogger<MainViewModel> logger)
    {
        _modelCatalog = modelCatalog;
        _settingsStore = settingsStore;
        _chatService = chatService;
        _mcpToolProvider = mcpToolProvider;
        _mcpSettingsStore = mcpSettingsStore;
        _configurationToolProvider = configurationToolProvider;
        _configurationPathStore = configurationPathStore;
        _folderPicker = folderPicker;
        _logger = logger;
        Settings = settings;
        Title = aiOptions.Value.Title;

        Sections =
        [
            new(AppSection.Chat, "Чат"),
            new(AppSection.Projects, "Проекты"),
            new(AppSection.Documentation, "Документация"),
            new(AppSection.Changes, "Изменения"),
            new(AppSection.Settings, "Настройки"),
        ];
        _selectedSection = Sections[0];

        _selectedDialog = CreateDialogViewModel();
        Dialogs.Add(_selectedDialog);

        RefreshModels();

        settingsStore.SettingsChanged += (_, _) => Dispatcher.UIThread.Post(RefreshModels);
        mcpSettingsStore.SettingsChanged += (_, _) => Dispatcher.UIThread.Post(OnMcpSettingsChanged);

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

        SelectConfigurationCommand = ReactiveCommand.CreateFromTask(SelectConfigurationAsync);
        SelectConfigurationCommand.ThrownExceptions.Subscribe(ex =>
            _logger.LogError(ex, "Не удалось выбрать папку выгрузки конфигурации"));

        ClearConfigurationCommand = ReactiveCommand.Create(ClearConfiguration);

        ConfirmUnsavedChanges = new Interaction<Unit, bool>();
        ConfirmMcpUnavailable = new Interaction<Unit, bool>();

        CheckMcpCommand = ReactiveCommand.CreateFromTask(CheckMcpAsync);
        CheckMcpCommand.ThrownExceptions.Subscribe(ex =>
            _logger.LogError(ex, "Ошибка проверки подключения к 1С MCP"));

        _ = CheckMcpAsync();
    }

    public string Title { get; }

    public ObservableCollection<ProviderPickerViewModel> ChatProviders { get; } = [];

    public IReadOnlyList<NavigationItemViewModel> Sections { get; }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<ChatDialogViewModel> Dialogs { get; } = [];

    public Interaction<Unit, bool> ConfirmUnsavedChanges { get; }

    public Interaction<Unit, bool> ConfirmMcpUnavailable { get; }

    public bool IsMcpAvailable => _mcpStatus is true;

    public bool IsMcpUnavailable => _mcpStatus is false;

    public bool IsMcpChecking => _mcpStatus is null;

    public bool IsMcpWarningOpen
    {
        get => _isMcpWarningOpen;
        set => this.RaiseAndSetIfChanged(ref _isMcpWarningOpen, value);
    }

    public string McpStatusTooltip => _mcpStatus switch
    {
        false when !_mcpSettingsStore.Get().IsConfigured =>
            "1С MCP — сервер не настроен: укажите адрес или команду запуска в «Настройки» → «MCP». " +
            "Нажмите, чтобы проверить соединение повторно.",
        true => "1С MCP — подключение к серверу 1С установлено: агент может работать с конфигурацией. " +
                "Нажмите, чтобы проверить соединение повторно.",
        false => "1С MCP — подключения к серверу 1С нет: агент отвечает на вопросы, но не работает с 1С. " +
                 "Нажмите, чтобы проверить соединение повторно.",
        _ => "1С MCP — соединение ещё не проверено. Нажмите, чтобы проверить подключение к серверу 1С.",
    };

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

    public bool IsSettingsSelected => SelectedSection.Section is AppSection.Settings;

    public bool IsPlaceholderSelected => !IsChatSelected && !IsSettingsSelected;

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

    public ReactiveCommand<AiModel, Unit> SelectChatModelCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectConfigurationCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearConfigurationCommand { get; }

    public ReactiveCommand<Unit, bool> CheckMcpCommand { get; }

    private bool CanSend() => SelectedModel is not null && !string.IsNullOrWhiteSpace(Prompt);

    private void CommitSection(NavigationItemViewModel value)
    {
        this.RaiseAndSetIfChanged(ref _selectedSection, value);
        this.RaisePropertyChanged(nameof(IsChatSelected));
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
        var lastPath = _configurationPathStore.GetLastPath();

        if (lastPath.Length > 0)
            dialog.SetConfiguration(lastPath, _configurationToolProvider.IsConfigurationDump(lastPath));

        return dialog;
    }

    private async Task SelectConfigurationAsync()
    {
        var dialog = SelectedDialog;
        var startPath = dialog.HasConfiguration ? dialog.ConfigurationPath : _configurationPathStore.GetLastPath();
        var path = await _folderPicker.PickFolderAsync("Папка выгрузки конфигурации 1С", startPath);

        if (string.IsNullOrWhiteSpace(path))
            return;

        dialog.SetConfiguration(path, _configurationToolProvider.IsConfigurationDump(path));
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

        if (!await EnsureMcpReadyAsync())
            return;

        Prompt = string.Empty;

        var dialog = SelectedDialog;
        dialog.AddMessage(AiChatRole.User, text);
        var history = dialog.ToAiMessages();

        ChatMessageViewModel? answer = null;

        try
        {
            await foreach (var chunk in _chatService.StreamAsync(SelectedModel, history, dialog.ConfigurationPath, cancellationToken))
            {
                if (chunk.Text.Length > 0 || chunk.Reasoning.Length > 0)
                {
                    answer ??= dialog.AddStreamingMessage();
                    answer.AppendReasoning(chunk.Reasoning);
                    answer.AppendText(chunk.Text);
                }

                if (chunk.Usage is { } usage)
                    dialog.ApplyUsage(usage);
            }
        }
        finally
        {
            answer?.FinishThinking();
            _ = CheckMcpAsync();
        }
    }

    private async Task<bool> EnsureMcpReadyAsync()
    {
        if (await CheckMcpAsync())
            return true;

        if (_mcpWarningShown || !_mcpSettingsStore.Get().IsConfigured)
            return true;

        _mcpWarningShown = true;

        return await HandleMcpUnavailableAsync();
    }

    private async Task<bool> CheckMcpAsync()
    {
        try
        {
            var isAvailable = await _mcpToolProvider.CheckAsync(CancellationToken.None);
            SetMcpStatus(isAvailable);
            return isAvailable;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось проверить подключение к 1С MCP");
            SetMcpStatus(false);
            return false;
        }
    }

    private void OnMcpSettingsChanged()
    {
        _mcpWarningShown = false;
        SetMcpStatus(null);
        _ = CheckMcpAsync();
    }

    private Task<bool> HandleMcpUnavailableAsync()
    {
        var request = new TaskCompletionSource<bool>();

        ConfirmMcpUnavailable.Handle(Unit.Default).Subscribe(
            proceed => request.TrySetResult(proceed),
            exception => request.TrySetException(exception),
            () => request.TrySetResult(true));

        return request.Task;
    }

    private void SetMcpStatus(bool? status)
    {
        void Apply()
        {
            _mcpStatus = status;
            this.RaisePropertyChanged(nameof(IsMcpAvailable));
            this.RaisePropertyChanged(nameof(IsMcpUnavailable));
            this.RaisePropertyChanged(nameof(IsMcpChecking));
            this.RaisePropertyChanged(nameof(McpStatusTooltip));
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }
}
