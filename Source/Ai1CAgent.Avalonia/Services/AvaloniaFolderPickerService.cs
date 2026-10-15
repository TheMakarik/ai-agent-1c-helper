using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Ai1CAgent.Avalonia.Services;

public sealed class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string title, string? startPath)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return null;

        var storage = window.StorageProvider;
        var startFolder = !string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath)
            ? await storage.TryGetFolderFromPathAsync(startPath)
            : null;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
