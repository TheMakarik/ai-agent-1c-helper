using Microsoft.Extensions.AI;

namespace Ai1CAgent.Tests.Infrastructure;

public sealed class CountingAIFunction(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    private int _invocationCount;

    public int InvocationCount => _invocationCount;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocationCount);
        return base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
