using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public interface IUiStateStore
{
    UiState Get();

    Task SaveAsync(UiState state, CancellationToken cancellationToken);
}
