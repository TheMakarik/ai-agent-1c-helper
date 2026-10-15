using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Options;
using Ai1CAgent.Modules.Ai;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Configuration;
using Ai1CAgent.Modules.Context;
using Ai1CAgent.Modules.Development;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ai1CAgent.Tests.Infrastructure;

public static class ChatServiceBuilder
{
    public static (AiChatService Service, ChatClientFactory Factory) Build(
        OllamaOptions? ollamaOptions = null,
        AiOptions? aiOptions = null,
        IAiSettingsStore? settingsStore = null)
    {
        var factory = new ChatClientFactory(
            FakeOptions.Create(ollamaOptions ?? new OllamaOptions()),
            FakeOptions.Create(aiOptions ?? new AiOptions()),
            settingsStore ?? CreateEmptySettingsStore(),
            NullLoggerFactory.Instance,
            A.Dummy<ILogger<ChatClientFactory>>());

        var service = new AiChatService(
            factory,
            new ConfigurationToolProvider(A.Dummy<ILogger<ConfigurationToolProvider>>()),
            new ProjectContextToolProvider(A.Dummy<IProjectStore>(), A.Dummy<ILogger<ProjectContextToolProvider>>()),
            new SkillToolProvider(A.Dummy<ISkillStore>(), A.Dummy<ILogger<SkillToolProvider>>()),
            new DeveloperToolProvider(A.Dummy<ILogger<DeveloperToolProvider>>()),
            new SystemDocumentationToolProvider(A.Dummy<ISystemDocumentation>()),
            A.Dummy<IAppSettingsStore>(),
            A.Dummy<ILogger<AiChatService>>());

        return (service, factory);
    }

    private static IAiSettingsStore CreateEmptySettingsStore()
    {
        var settingsStore = A.Fake<IAiSettingsStore>();
        A.CallTo(() => settingsStore.GetProviders()).Returns([]);
        return settingsStore;
    }
}
