using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai.Interfaces;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IAiSettingsStore _settingsStore;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly ILogger<SettingsViewModel> _logger;
    private ProviderSettingsViewModel? _selectedProvider;
    private string _newProviderName = string.Empty;
    private AiApiFormat _newProviderFormat = AiApiFormat.OpenAi;
    private string _statusMessage = string.Empty;
    private bool _isSaving;
    private bool _isDirty;
    private bool _allowWrite;
    private bool _allowTerminal;

    public SettingsViewModel(
        IAiSettingsStore settingsStore,
        IAppSettingsStore appSettingsStore,
        ILogger<SettingsViewModel> logger)
    {
        _settingsStore = settingsStore;
        _appSettingsStore = appSettingsStore;
        _logger = logger;
        _allowWrite = appSettingsStore.Get().AllowWrite;
        _allowTerminal = appSettingsStore.Get().AllowTerminal;

        Providers = [];
        BuildProviders();
        _selectedProvider = Providers.FirstOrDefault();

        var canSave = Changed
            .Select(_ => !IsSaving)
            .StartWith(!IsSaving)
            .DistinctUntilChanged();

        var canAddProvider = Changed
            .Select(_ => CanAddProvider())
            .StartWith(CanAddProvider())
            .DistinctUntilChanged();

        var canRemoveProvider = Changed
            .Select(_ => SelectedProvider is { IsCustom: true })
            .StartWith(SelectedProvider is { IsCustom: true })
            .DistinctUntilChanged();

        SaveCommand = ReactiveCommand.CreateFromTask(SaveCoreAsync, canSave);
        AddProviderCommand = ReactiveCommand.Create(AddProvider, canAddProvider);
        RemoveProviderCommand = ReactiveCommand.Create(RemoveProvider, canRemoveProvider);

        SaveCommand.ThrownExceptions.Subscribe(exception =>
        {
            _logger.LogError(exception, "Не удалось сохранить настройки");
            StatusMessage = $"Не удалось сохранить: {exception.Message}";
        });

        AddProviderCommand.ThrownExceptions.Subscribe(exception =>
            _logger.LogError(exception, "Не удалось добавить провайдера"));

        RemoveProviderCommand.ThrownExceptions.Subscribe(exception =>
            _logger.LogError(exception, "Не удалось удалить провайдера"));
    }

    public ObservableCollection<ProviderSettingsViewModel> Providers { get; }

    public ProviderSettingsViewModel? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProvider, value);
            this.RaisePropertyChanged(nameof(HasSelectedProvider));
        }
    }

    public bool HasSelectedProvider => _selectedProvider is not null;

    public bool AllowWrite
    {
        get => _allowWrite;
        set
        {
            this.RaiseAndSetIfChanged(ref _allowWrite, value);
            _ = _appSettingsStore.SaveAsync(new AppSettings(value, AllowTerminal), CancellationToken.None);
        }
    }

    public bool AllowTerminal
    {
        get => _allowTerminal;
        set
        {
            this.RaiseAndSetIfChanged(ref _allowTerminal, value);
            _ = _appSettingsStore.SaveAsync(new AppSettings(AllowWrite, value), CancellationToken.None);
        }
    }

    public IReadOnlyList<AiApiFormat> Formats { get; } = [AiApiFormat.OpenAi, AiApiFormat.Anthropic];

    public string NewProviderName
    {
        get => _newProviderName;
        set => this.RaiseAndSetIfChanged(ref _newProviderName, value);
    }

    public AiApiFormat NewProviderFormat
    {
        get => _newProviderFormat;
        set => this.RaiseAndSetIfChanged(ref _newProviderFormat, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => this.RaiseAndSetIfChanged(ref _isSaving, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> AddProviderCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveProviderCommand { get; }

    public Task SaveAsync() => SaveCoreAsync(CancellationToken.None);

    public void DiscardChanges()
    {
        var index = _selectedProvider is null ? 0 : Providers.IndexOf(_selectedProvider);

        BuildProviders();
        SelectedProvider = Providers.Count == 0 ? null : Providers[Math.Clamp(index, 0, Providers.Count - 1)];
        NewProviderName = string.Empty;
        StatusMessage = string.Empty;
        IsDirty = false;
    }

    private void BuildProviders()
    {
        foreach (var provider in Providers)
            provider.Modified -= OnProviderModified;

        Providers.Clear();

        foreach (var settings in _settingsStore.GetProviders())
        {
            var provider = new ProviderSettingsViewModel(settings);
            provider.Modified += OnProviderModified;
            Providers.Add(provider);
        }
    }

    private bool CanAddProvider() =>
        !string.IsNullOrWhiteSpace(NewProviderName) &&
        !Providers.Any(provider =>
            string.Equals(provider.DisplayName, NewProviderName.Trim(), StringComparison.OrdinalIgnoreCase));

    private void AddProvider()
    {
        var name = NewProviderName.Trim();

        if (!CanAddProvider())
            return;

        var settings = new AiProviderSettings { Provider = null, Name = name, Format = NewProviderFormat };
        var provider = new ProviderSettingsViewModel(settings);
        provider.Modified += OnProviderModified;
        Providers.Add(provider);
        SelectedProvider = provider;
        NewProviderName = string.Empty;
        IsDirty = true;
    }

    private void RemoveProvider()
    {
        if (SelectedProvider is not { IsCustom: true } provider)
            return;

        var index = Providers.IndexOf(provider);
        provider.Modified -= OnProviderModified;
        Providers.Remove(provider);

        SelectedProvider = Providers.Count == 0
            ? null
            : Providers[Math.Clamp(index, 0, Providers.Count - 1)];
        IsDirty = true;
    }

    private void OnProviderModified(object? sender, EventArgs e) => IsDirty = true;

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            List<AiProviderSettings> settings = [.. Providers.Select(provider => provider.ToSettings())];
            await _settingsStore.SaveProvidersAsync(settings, cancellationToken);

            IsDirty = false;
            StatusMessage = "Настройки сохранены";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
