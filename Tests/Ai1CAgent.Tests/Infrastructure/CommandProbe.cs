namespace Ai1CAgent.Tests.Infrastructure;

public static class CommandProbe
{
    public static bool Exists(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Join(directory, command)));
    }
}
