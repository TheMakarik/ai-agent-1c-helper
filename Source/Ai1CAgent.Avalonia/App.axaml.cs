using System;
using Ai1CAgent.Avalonia.ViewModels;
using Ai1CAgent.Avalonia.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Ai1CAgent.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        base.OnFrameworkInitializationCompleted();

        try
        {
            var host = await new Bootstrapper().LoadApplicationAsync();

            desktop.MainWindow = new MainWindow
            {
                DataContext = host.Services.GetRequiredService<MainViewModel>(),
            };
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error while loading application");
        }
    }
}
