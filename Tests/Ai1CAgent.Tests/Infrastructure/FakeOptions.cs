using FakeItEasy;
using Microsoft.Extensions.Options;

namespace Ai1CAgent.Tests.Infrastructure;

public static class FakeOptions
{
    public static IOptions<T> Create<T>(T value)
        where T : class
    {
        var options = A.Fake<IOptions<T>>();
        A.CallTo(() => options.Value).Returns(value);
        return options;
    }
}
