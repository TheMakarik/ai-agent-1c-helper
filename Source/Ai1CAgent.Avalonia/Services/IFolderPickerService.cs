using System.Threading.Tasks;

namespace Ai1CAgent.Avalonia.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string title, string? startPath);
}
