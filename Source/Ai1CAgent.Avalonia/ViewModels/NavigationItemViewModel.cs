namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class NavigationItemViewModel(AppSection section, string title) : ViewModelBase
{
    public AppSection Section { get; } = section;

    public string Title { get; } = title;
}
