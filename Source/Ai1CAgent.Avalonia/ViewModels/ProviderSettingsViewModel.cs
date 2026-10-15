using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ProviderSettingsViewModel : ViewModelBase
{
    private readonly string _defaultEndpoint;
    private string _endpoint;
    private string? _endpointBeforeEditing;
    private bool _isEditingEndpoint;
    private string _apiKey;
    private bool _revealApiKey;
    private AiApiFormat _providerFormat;
    private ModelOptionViewModel? _selectedModel;
    private string _newModelName = string.Empty;
    private AiApiFormat _newModelFormat = AiApiFormat.OpenAi;

    public ProviderSettingsViewModel(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Source = settings;
        _defaultEndpoint = settings.Provider is { } provider
            ? AiProviderDescriptors.DefaultEndpoint(provider) ?? string.Empty
            : string.Empty;
        _endpoint = settings.ResolvedEndpoint();
        _apiKey = settings.ApiKey;
        _providerFormat = settings.ResolvedFormat();

        var models = settings.Models
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .Select(model => CreateModel(model.Name, model.Format, isUserAdded: false))
            .ToList();

        if (models.Count == 0 && !string.IsNullOrWhiteSpace(settings.Model))
            models.Add(CreateModel(settings.Model.Trim(), _providerFormat, isUserAdded: false));

        foreach (var custom in settings.CustomModels.Where(model => !string.IsNullOrWhiteSpace(model.Name)))
            models.Add(CreateModel(custom.Name, custom.Format, isUserAdded: true));

        Models = new ObservableCollection<ModelOptionViewModel>(models);
        Models.CollectionChanged += OnModelsChanged;

        foreach (var model in Models)
            model.Modified += OnModelModified;

        _selectedModel = settings.SelectedModel is { } selected
            ? models.FirstOrDefault(model =>
                string.Equals(model.Name, selected.Name, StringComparison.OrdinalIgnoreCase))
            : null;
        _selectedModel ??= Models.FirstOrDefault();

        EndpointCommand = ReactiveCommand.Create(PerformEndpointAction);
        ConfirmEndpointCommand = ReactiveCommand.Create(ConfirmEndpointEditing);
        AddModelCommand = ReactiveCommand.Create(AddModel);
    }

    public event EventHandler? Modified;

    public AiProviderSettings Source { get; }

    public string DisplayName => Source.DisplayName;

    public bool IsCustom => Source.IsCustom;

    public string EndpointPlaceholder => IsCustom ? "Введите адрес API" : _defaultEndpoint;

    public string ApiKeyHint
    {
        get
        {
            var variable = Source.Provider is { } provider
                ? AiProviderDescriptors.DefaultApiKeyEnvironmentVariable(provider)
                : string.Empty;

            return string.IsNullOrWhiteSpace(variable)
                ? "Введите API-ключ"
                : $"API-ключ или переменная {variable}";
        }
    }

    public IReadOnlyList<AiApiFormat> Formats { get; } = [AiApiFormat.OpenAi, AiApiFormat.Anthropic];

    public ObservableCollection<ModelOptionViewModel> Models { get; }

    public ModelOptionViewModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (ReferenceEquals(_selectedModel, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedModel, value);
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    public AiApiFormat ProviderFormat
    {
        get => _providerFormat;
        set
        {
            if (_providerFormat == value)
                return;

            this.RaiseAndSetIfChanged(ref _providerFormat, value);
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Endpoint
    {
        get => _endpoint;
        set
        {
            value ??= string.Empty;

            if (string.Equals(_endpoint, value, StringComparison.Ordinal))
                return;

            this.RaiseAndSetIfChanged(ref _endpoint, value);
            this.RaisePropertyChanged(nameof(IsCustomEndpoint));
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsCustomEndpoint => UsesNonDefaultEndpoint(_endpoint);

    public bool IsEditingEndpoint
    {
        get => _isEditingEndpoint;
        set
        {
            if (_isEditingEndpoint == value)
                return;

            _isEditingEndpoint = value;
            this.RaisePropertyChanged(nameof(IsEditingEndpoint));
            this.RaisePropertyChanged(nameof(IsEndpointDisplayMode));
            this.RaisePropertyChanged(nameof(EndpointActionTooltip));
        }
    }

    public bool IsEndpointDisplayMode => !_isEditingEndpoint;

    public string EndpointActionTooltip => _isEditingEndpoint
        ? _defaultEndpoint.Length > 0
            ? "Сбросить к стандартному адресу"
            : "Отменить изменение"
        : "Изменить адрес";

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            value ??= string.Empty;

            if (string.Equals(_apiKey, value, StringComparison.Ordinal))
                return;

            this.RaiseAndSetIfChanged(ref _apiKey, value);
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool RevealApiKey
    {
        get => _revealApiKey;
        set
        {
            this.RaiseAndSetIfChanged(ref _revealApiKey, value);
            this.RaisePropertyChanged(nameof(ApiKeyChar));
        }
    }

    public char ApiKeyChar => RevealApiKey ? '\0' : '●';

    public string NewModelName
    {
        get => _newModelName;
        set => this.RaiseAndSetIfChanged(ref _newModelName, value);
    }

    public AiApiFormat NewModelFormat
    {
        get => _newModelFormat;
        set => this.RaiseAndSetIfChanged(ref _newModelFormat, value);
    }

    public ReactiveCommand<Unit, Unit> EndpointCommand { get; }

    public ReactiveCommand<Unit, Unit> ConfirmEndpointCommand { get; }

    public ReactiveCommand<Unit, Unit> AddModelCommand { get; }

    public AiProviderSettings ToSettings()
    {
        var endpoint = _endpoint.Trim();
        var isCustom = UsesNonDefaultEndpoint(endpoint);
        var value = isCustom ? endpoint : string.Empty;

        return new AiProviderSettings
        {
            Provider = Source.Provider,
            Name = Source.Name,
            Format = ProviderFormat,
            Model = Models.FirstOrDefault(model => !model.IsUserAdded)?.Name ?? string.Empty,
            Endpoint = value,
            ApiKey = ApiKey.Trim(),
            Models = [.. Models.Where(model => !model.IsUserAdded).Select(ToCustomModel)],
            SelectedModel = SelectedModel is { } selected ? ToCustomModel(selected) : null,
            CustomModels = [.. Models.Where(model => model.IsUserAdded).Select(ToCustomModel)],
            IsCustomEndpoint = isCustom,
            CustomEndpoint = value,
        };
    }

    private static CustomModel ToCustomModel(ModelOptionViewModel model) => new(model.Name, model.Format);

    private bool UsesNonDefaultEndpoint(string endpoint)
    {
        var value = endpoint.Trim();

        if (value.Length == 0)
            return false;

        return _defaultEndpoint.Length == 0 ||
            !string.Equals(value, _defaultEndpoint, StringComparison.OrdinalIgnoreCase);
    }

    private ModelOptionViewModel CreateModel(string name, AiApiFormat format, bool isUserAdded)
        => new(name, format, isUserAdded, model => Models.Remove(model));

    private void AddModel()
    {
        var name = NewModelName.Trim();

        if (name.Length == 0 ||
            Models.Any(model => string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Models.Add(CreateModel(name, NewModelFormat, isUserAdded: true));
        NewModelName = string.Empty;
    }

    private void PerformEndpointAction()
    {
        if (IsEditingEndpoint)
        {
            IsEditingEndpoint = false;
            RestoreDefaultEndpoint();
            return;
        }

        if (string.IsNullOrWhiteSpace(_endpoint))
            Endpoint = _defaultEndpoint;

        _endpointBeforeEditing = _endpoint;
        IsEditingEndpoint = true;
    }

    private void ConfirmEndpointEditing()
    {
        _endpointBeforeEditing = null;
        IsEditingEndpoint = false;
    }

    private void RestoreDefaultEndpoint()
    {
        var revertTo = _defaultEndpoint.Length > 0 ? _defaultEndpoint : _endpointBeforeEditing;
        _endpointBeforeEditing = null;

        if (revertTo is not null)
            Endpoint = revertTo;
    }

    private void OnModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ModelOptionViewModel model in e.OldItems)
                model.Modified -= OnModelModified;
        }

        if (e.NewItems is not null)
        {
            foreach (ModelOptionViewModel model in e.NewItems)
                model.Modified += OnModelModified;
        }

        Modified?.Invoke(this, EventArgs.Empty);
    }

    private void OnModelModified(object? sender, EventArgs e) => Modified?.Invoke(this, EventArgs.Empty);
}
