using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ProviderOptionViewModel(AiProvider provider) : ViewModelBase
{
    public AiProvider Provider { get; } = provider;

    public string DisplayName { get; } = AiProviderDescriptors.DisplayName(provider);
}
