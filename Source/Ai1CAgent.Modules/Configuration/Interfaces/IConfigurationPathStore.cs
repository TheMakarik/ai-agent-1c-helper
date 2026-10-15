namespace Ai1CAgent.Modules.Configuration.Interfaces;

public interface IConfigurationPathStore
{
    string GetLastPath();

    Task SaveLastPathAsync(string path, CancellationToken cancellationToken = default);
}
