using Microsoft.Extensions.AI;

namespace Ai1CAgent.Modules.Configuration.Interfaces;

public interface IConfigurationToolProvider
{
    bool IsConfigurationDump(string path);

    IReadOnlyList<AITool> GetTools(string configurationPath);
}
