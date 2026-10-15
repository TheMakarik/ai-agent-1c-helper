using System;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.ViewModels;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Greeting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Ai1CAgent.Avalonia;

public sealed class Bootstrapper
{
    public async Task<IHost> LoadApplicationAsync()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = Environment.GetCommandLineArgs(),
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        RegisterOptions(builder);
        RegisterServices(builder.Services);

        builder.Logging
            .ClearProviders()
            .AddSerilog(Log.Logger, dispose: true);

        var host = builder.Build();

        await host.StartAsync();

        return host;
    }

    private static void RegisterOptions(HostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GreetingOptions>()
            .Bind(builder.Configuration.GetSection(nameof(GreetingOptions)));
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IGreetingProvider, GreetingProvider>();
        services.AddSingleton<MainViewModel>();
    }
}
