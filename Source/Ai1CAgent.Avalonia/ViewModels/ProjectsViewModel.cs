using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.Services;
using Ai1CAgent.Core.Common;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectStore _projectStore;
    private readonly IChatStore _chatStore;
    private readonly IFolderPickerService _folderPicker;
    private readonly ILogger<ProjectsViewModel> _logger;

    private ProjectItemViewModel? _selectedProject;

    public ProjectsViewModel(
        IProjectStore projectStore,
        IChatStore chatStore,
        IFolderPickerService folderPicker,
        ILogger<ProjectsViewModel> logger)
    {
        _projectStore = projectStore;
        _chatStore = chatStore;
        _folderPicker = folderPicker;
        _logger = logger;

        AddProjectCommand = ReactiveCommand.CreateFromTask(AddProjectAsync);
        AddProjectCommand.ThrownExceptions.Subscribe(exception => _logger.LogError(exception, "Не удалось добавить проект"));

        SelectProjectCommand = ReactiveCommand.Create<ProjectItemViewModel>(SelectProject);

        DeleteProjectCommand = ReactiveCommand.CreateFromTask<ProjectItemViewModel>(DeleteProjectAsync);
        DeleteProjectCommand.ThrownExceptions.Subscribe(exception => _logger.LogError(exception, "Не удалось удалить проект"));

        RefreshProjectCommand = ReactiveCommand.CreateFromTask<ProjectItemViewModel>(RefreshProjectAsync);
        RefreshProjectCommand.ThrownExceptions.Subscribe(exception => _logger.LogWarning(exception, "Не удалось обновить индекс проекта"));

        Projects.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasProjects));
    }

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    public bool HasProjects => Projects.Count > 0;

    public ProjectItemViewModel? SelectedProject
    {
        get => _selectedProject;
        private set
        {
            if (ReferenceEquals(value, _selectedProject))
                return;

            if (_selectedProject is { } previous)
                previous.IsCurrent = false;

            this.RaiseAndSetIfChanged(ref _selectedProject, value);
            this.RaisePropertyChanged(nameof(HasSelection));

            if (value is not null)
                value.IsCurrent = true;

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasSelection => _selectedProject is not null;

    public ReactiveCommand<Unit, Unit> AddProjectCommand { get; }

    public ReactiveCommand<ProjectItemViewModel, Unit> SelectProjectCommand { get; }

    public ReactiveCommand<ProjectItemViewModel, Unit> DeleteProjectCommand { get; }

    public ReactiveCommand<ProjectItemViewModel, Unit> RefreshProjectCommand { get; }

    public event EventHandler? SelectionChanged;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var projects = await _projectStore.GetProjectsAsync(cancellationToken).ConfigureAwait(false);

        Projects.Clear();

        foreach (var project in projects)
            Projects.Add(new ProjectItemViewModel(project));

        SelectedProject = Projects.FirstOrDefault(project => project.CanSelect);
    }

    public void Select(long? projectId)
    {
        if (projectId is not { } id)
            return;

        var match = Projects.FirstOrDefault(project => project.Id == id);

        if (match is { CanSelect: true })
            SelectedProject = match;
    }

    public void BeginRename(ProjectItemViewModel project)
    {
        project.PendingName = project.Name;
        project.IsRenaming = true;
    }

    public void CancelRename(ProjectItemViewModel project)
    {
        project.PendingName = project.Name;
        project.IsRenaming = false;
    }

    public async Task CommitRenameAsync(ProjectItemViewModel project, CancellationToken cancellationToken)
    {
        var name = project.PendingName.Trim();

        if (name.Length == 0)
        {
            CancelRename(project);
            return;
        }

        if (!string.Equals(name, project.Name, StringComparison.Ordinal))
        {
            await _projectStore.RenameProjectAsync(project.Id, name, cancellationToken).ConfigureAwait(false);
            project.SetName(name);
        }

        project.IsRenaming = false;
    }

    private async Task AddProjectAsync(CancellationToken cancellationToken)
    {
        var path = await _folderPicker.PickFolderAsync("Папка проекта — выгрузка конфигурации 1С", string.Empty).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(path))
            return;

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        var project = await _projectStore.AddProjectAsync(name, path, cancellationToken).ConfigureAwait(false);
        await _chatStore.ReassignOrphanSessionsAsync(project.Id, cancellationToken).ConfigureAwait(false);

        var item = new ProjectItemViewModel(project);

        Projects.Add(item);
        SelectedProject = item;
    }

    private void SelectProject(ProjectItemViewModel project)
    {
        if (project.CanSelect)
            SelectedProject = project;
    }

    private async Task DeleteProjectAsync(ProjectItemViewModel project, CancellationToken cancellationToken)
    {
        await _projectStore.DeleteProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        Projects.Remove(project);

        if (ReferenceEquals(project, SelectedProject))
            SelectedProject = Projects.FirstOrDefault(item => item.CanSelect);
    }

    private async Task RefreshProjectAsync(ProjectItemViewModel project, CancellationToken cancellationToken)
    {
        if (!project.FolderExists)
            return;

        await _projectStore.IndexFilesAsync(project.Id, cancellationToken).ConfigureAwait(false);
    }
}
