using System.Collections.Generic;
using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ProviderPickerViewModel(
    string providerKey,
    string displayName,
    IReadOnlyList<AiModel> models) : ViewModelBase
{
    public string ProviderKey { get; } = providerKey;

    public string DisplayName { get; } = displayName;

    public IReadOnlyList<AiModel> Models { get; } = models;
}
