using Ai1CAgent.Core.Common;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public class MainViewModel : ViewModelBase
{
    private string _greeting;

    public MainViewModel(IGreetingProvider greetingProvider)
    {
        _greeting = greetingProvider.GetGreeting();
    }

    public string Greeting
    {
        get => _greeting;
        set => this.RaiseAndSetIfChanged(ref _greeting, value);
    }
}
