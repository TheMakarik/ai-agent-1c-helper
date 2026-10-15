using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public interface IAppSettingsStore
{
    AppSettings Get();

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
