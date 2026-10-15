using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Options;
using Microsoft.Extensions.Options;

namespace Ai1CAgent.Modules.Greeting;

public sealed class GreetingProvider : IGreetingProvider
{
    private readonly GreetingOptions _options;

    public GreetingProvider(IOptions<GreetingOptions> options)
    {
        _options = options.Value;
    }

    public string GetGreeting() => _options.Message;
}
