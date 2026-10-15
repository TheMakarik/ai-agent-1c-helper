using Ai1CAgent.Core.Models;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ProjectItemViewModel : ViewModelBase
{
    private string _name;
    private bool _isRenaming;
    private bool _isCurrent;
    private string _pendingName;

    public ProjectItemViewModel(Project project)
    {
        Id = project.Id;
        Path = project.Path;
        FolderExists = project.FolderExists;
        _name = project.Name;
        _pendingName = project.Name;
    }

    public long Id { get; }

    public string Path { get; }

    public bool FolderExists { get; }

    public bool CanSelect => FolderExists;

    public bool IsFolderMissing => !FolderExists;

    public string FolderStateText => FolderExists ? Path : "Папка не найдена — проект недоступен";

    public string Name
    {
        get => _name;
        private set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => this.RaiseAndSetIfChanged(ref _isRenaming, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => this.RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    public string PendingName
    {
        get => _pendingName;
        set => this.RaiseAndSetIfChanged(ref _pendingName, value);
    }

    public void SetName(string name)
    {
        Name = name;
        PendingName = name;
    }
}
