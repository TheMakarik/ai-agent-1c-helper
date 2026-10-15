using Ai1CAgent.Modules.Mcp.Interfaces;
using FakeItEasy;
using Microsoft.Extensions.AI;

namespace Ai1CAgent.Tests.Infrastructure;

public static class EmptyMcpToolProvider
{
    public static IMcpToolProvider Create()
    {
        var provider = A.Fake<IMcpToolProvider>();
        A.CallTo(() => provider.GetToolsAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AITool>>([]));
        A.CallTo(() => provider.CheckAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(true));
        return provider;
    }
}
