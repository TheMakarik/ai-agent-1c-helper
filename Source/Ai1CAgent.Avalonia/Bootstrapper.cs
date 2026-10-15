using System;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.Services;
using Ai1CAgent.Avalonia.ViewModels;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Configuration;
using Ai1CAgent.Modules.Configuration.Interfaces;
using Ai1CAgent.Modules.Context;
using Ai1CAgent.Modules.Context.Interfaces;
using Ai1CAgent.Modules.Development;
using Ai1CAgent.Modules.Development.Interfaces;
using Ai1CAgent.Modules.Storage;
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
        await host.Services.GetRequiredService<Database>().InitializeAsync(CancellationToken.None);

        return host;
    }

    private static void RegisterOptions(HostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<OllamaOptions>()
            .Bind(builder.Configuration.GetSection(nameof(OllamaOptions)));

        builder.Services
            .AddOptions<AiOptions>()
            .Bind(builder.Configuration.GetSection(nameof(AiOptions)));
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigurationToolProvider, ConfigurationToolProvider>();
        services.AddSingleton<IConfigurationPathStore, ConfigurationPathStore>();
        services.AddSingleton<ISystemDocumentation, AvaloniaSystemDocumentation>();
        services.AddSingleton<ISystemDocumentationToolProvider, SystemDocumentationToolProvider>();
        services.AddSingleton<ISkillStore, FileSkillStore>();
        services.AddSingleton<ISkillToolProvider, SkillToolProvider>();
        services.AddSingleton<IDeveloperToolProvider, DeveloperToolProvider>();
        services.AddSingleton<IEmbeddedSkills, EmbeddedSkills>();
        services.AddSingleton<IUiStateStore, JsonUiStateStore>();
        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton(new Database());
        services.AddSingleton<IChatStore, SqliteChatStore>();
        services.AddSingleton<IProjectStore, SqliteProjectStore>();
        services.AddSingleton<IProjectContextToolProvider, ProjectContextToolProvider>();
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<IAiSettingsStore, AiSettingsStore>();
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
        services.AddSingleton<IAiModelCatalog, AiModelCatalog>();
        services.AddSingleton<IAiChatService, AiChatService>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<SkillsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
