using System;
using System.Collections.Generic;
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
    private string _prompt = string.Empty;
    private string _answer = string.Empty;

    public MainViewModel(
        IAiModelCatalog modelCatalog,
        IAiChatService chatService,
        ILogger<MainViewModel> logger)
    {
        _chatService = chatService;
        Models = modelCatalog.GetModels();
        _selectedModel = Models.Count > 0 ? Models[0] : null;

        var canSend = Changed
            .Select(_ => CanSend())
            .StartWith(CanSend())
            .DistinctUntilChanged();

        SendCommand = ReactiveCommand.CreateFromTask(SendAsync, canSend);
        SendCommand.ThrownExceptions.Subscribe(ex =>
        {
            logger.LogError(ex, "Ошибка запроса к нейросети");
            Answer = $"Ошибка: {ex.Message}";
        });
    }

    public IReadOnlyList<AiModel> Models { get; }

    public AiModel? SelectedModel
    {
        get => _selectedModel;
        set => this.RaiseAndSetIfChanged(ref _selectedModel, value);
    }

    public string Prompt
    {
        get => _prompt;
        set => this.RaiseAndSetIfChanged(ref _prompt, value);
    }

    public string Answer
    {
        get => _answer;
        set => this.RaiseAndSetIfChanged(ref _answer, value);
    }

    public ReactiveCommand<Unit, Unit> SendCommand { get; }

    private bool CanSend() => SelectedModel is not null && !string.IsNullOrWhiteSpace(Prompt);

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (SelectedModel is null)
            return;

        Answer = string.Empty;
        AiChatMessage[] messages = [new(AiChatRole.User, Prompt)];

        await foreach (var chunk in _chatService.StreamAsync(SelectedModel, messages, cancellationToken))
            Answer += chunk;
    }
}
