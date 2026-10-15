using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Mcp.Interfaces;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class McpSettingsViewModel : ViewModelBase
{
    private readonly IMcpToolProvider _toolProvider;
    private McpTransportType _transport = McpTransportType.Http;
    private string _endpoint = string.Empty;
    private string _command = string.Empty;
    private string _arguments = string.Empty;
    private bool? _checkResult;
    private string _checkMessage = string.Empty;
    private bool _isChecking;

    public McpSettingsViewModel(IMcpToolProvider toolProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(toolProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _toolProvider = toolProvider;

        var canCheck = Changed
            .Select(_ => !IsChecking)
            .StartWith(!IsChecking)
            .DistinctUntilChanged();

        CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync, canCheck);
        CheckCommand.ThrownExceptions.Subscribe(exception =>
        {
            logger.LogError(exception, "Не удалось проверить подключение к MCP-серверу");
            SetCheckResult(false, $"Нет подключения: {exception.Message}");
        });
    }

    public event EventHandler? Modified;

    public IReadOnlyList<McpTransportType> Transports { get; } = [McpTransportType.Http, McpTransportType.Stdio];

    public McpTransportType Transport
    {
        get => _transport;
        set
        {
            if (_transport == value)
                return;

            this.RaiseAndSetIfChanged(ref _transport, value);
            this.RaisePropertyChanged(nameof(IsHttp));
            this.RaisePropertyChanged(nameof(IsStdio));
            OnEdited();
        }
    }

    public bool IsHttp => _transport is McpTransportType.Http;

    public bool IsStdio => _transport is McpTransportType.Stdio;

    public string Endpoint
    {
        get => _endpoint;
        set => SetText(ref _endpoint, value);
    }

    public string Command
    {
        get => _command;
        set => SetText(ref _command, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetText(ref _arguments, value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set => this.RaiseAndSetIfChanged(ref _isChecking, value);
    }

    public bool IsCheckSucceeded => _checkResult is true;

    public bool IsCheckFailed => _checkResult is false;

    public string CheckMessage
    {
        get => _checkMessage;
        private set => this.RaiseAndSetIfChanged(ref _checkMessage, value);
    }

    public ReactiveCommand<Unit, Unit> CheckCommand { get; }

    public void Load(McpServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _transport = settings.Transport;
        _endpoint = settings.Endpoint;
        _command = settings.Command;
        _arguments = settings.Arguments;

        this.RaisePropertyChanged(nameof(Transport));
        this.RaisePropertyChanged(nameof(IsHttp));
        this.RaisePropertyChanged(nameof(IsStdio));
        this.RaisePropertyChanged(nameof(Endpoint));
        this.RaisePropertyChanged(nameof(Command));
        this.RaisePropertyChanged(nameof(Arguments));
        SetCheckResult(null, string.Empty);
    }

    public McpServerSettings ToSettings() => new()
    {
        Transport = _transport,
        Endpoint = _endpoint.Trim(),
        Command = _command.Trim(),
        Arguments = _arguments.Trim(),
    };

    private void SetText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        value ??= string.Empty;

        if (string.Equals(field, value, StringComparison.Ordinal))
            return;

        field = value;
        this.RaisePropertyChanged(propertyName);
        OnEdited();
    }

    private void OnEdited()
    {
        SetCheckResult(null, string.Empty);
        Modified?.Invoke(this, EventArgs.Empty);
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        SetCheckResult(null, "Проверка подключения…");

        try
        {
            var result = await _toolProvider.ProbeAsync(ToSettings(), cancellationToken);

            SetCheckResult(
                result.IsAvailable,
                result.IsAvailable
                    ? $"Подключение установлено, инструментов: {result.ToolCount}"
                    : $"Нет подключения: {result.Error}");
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void SetCheckResult(bool? result, string message)
    {
        _checkResult = result;
        CheckMessage = message;
        this.RaisePropertyChanged(nameof(IsCheckSucceeded));
        this.RaisePropertyChanged(nameof(IsCheckFailed));
    }
}
