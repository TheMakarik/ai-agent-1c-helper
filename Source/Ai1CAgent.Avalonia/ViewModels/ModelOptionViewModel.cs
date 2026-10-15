using System;
using System.Collections.Generic;
using System.Reactive;
using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ModelOptionViewModel : ViewModelBase
{
    private string _name;
    private AiApiFormat _format;

    public ModelOptionViewModel(
        string name,
        AiApiFormat format,
        bool isUserAdded,
        Action<ModelOptionViewModel> remove)
    {
        _name = name;
        _format = format;
        IsUserAdded = isUserAdded;
        RemoveCommand = ReactiveCommand.Create(() => remove(this));
    }

    public event EventHandler? Modified;

    public IReadOnlyList<AiApiFormat> Formats { get; } = [AiApiFormat.OpenAi, AiApiFormat.Anthropic];

    public string Name
    {
        get => _name;
        set
        {
            value ??= string.Empty;

            if (string.Equals(_name, value, StringComparison.Ordinal))
                return;

            this.RaiseAndSetIfChanged(ref _name, value);
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    public AiApiFormat Format
    {
        get => _format;
        set
        {
            if (_format == value)
                return;

            this.RaiseAndSetIfChanged(ref _format, value);
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsUserAdded { get; }

    public bool CanChangeFormat => IsUserAdded;

    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }
}
