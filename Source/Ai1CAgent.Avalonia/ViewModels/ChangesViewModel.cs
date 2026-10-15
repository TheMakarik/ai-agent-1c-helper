using System;
using System.Collections.ObjectModel;
using System.Linq;
using Ai1CAgent.Core.Common;
using Ai1CAgent.Core.Models;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Ai1CAgent.Avalonia.ViewModels;

public sealed class ChangesViewModel : ViewModelBase
{
    private readonly IGitDiffService _git;
    private readonly ILogger<ChangesViewModel> _logger;

    private string? _rootPath;
    private GitCommitInfo? _selectedCommit;
    private GitFileChange? _selectedFile;
    private string _diff = string.Empty;
    private bool _hasRepository;

    public ChangesViewModel(IGitDiffService git, ILogger<ChangesViewModel> logger)
    {
        _git = git;
        _logger = logger;
    }

    public ObservableCollection<GitCommitInfo> Commits { get; } = [];

    public ObservableCollection<GitFileChange> Files { get; } = [];

    public bool HasRepository
    {
        get => _hasRepository;
        private set => this.RaiseAndSetIfChanged(ref _hasRepository, value);
    }

    public string Diff
    {
        get => _diff;
        private set => this.RaiseAndSetIfChanged(ref _diff, value);
    }

    public GitCommitInfo? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            if (ReferenceEquals(value, _selectedCommit))
                return;

            this.RaiseAndSetIfChanged(ref _selectedCommit, value);
            LoadFiles();
        }
    }

    public GitFileChange? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (ReferenceEquals(value, _selectedFile))
                return;

            this.RaiseAndSetIfChanged(ref _selectedFile, value);
            LoadDiff();
        }
    }

    public void Reload(string? rootPath)
    {
        _rootPath = rootPath;
        Commits.Clear();
        Files.Clear();
        Diff = string.Empty;
        _selectedCommit = null;
        _selectedFile = null;
        this.RaisePropertyChanged(nameof(SelectedCommit));
        this.RaisePropertyChanged(nameof(SelectedFile));

        HasRepository = rootPath is { Length: > 0 } && _git.IsRepository(rootPath);

        if (!HasRepository)
            return;

        try
        {
            foreach (var commit in _git.GetCommits(rootPath!, 300))
                Commits.Add(commit);

            SelectedCommit = Commits.FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить коммиты");
        }
    }

    private void LoadFiles()
    {
        Files.Clear();
        Diff = string.Empty;
        _selectedFile = null;
        this.RaisePropertyChanged(nameof(SelectedFile));

        if (_rootPath is not { } root || SelectedCommit is not { } commit)
            return;

        try
        {
            foreach (var file in _git.GetChangedFiles(root, commit.Sha))
                Files.Add(file);

            SelectedFile = Files.FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить файлы коммита");
        }
    }

    private void LoadDiff()
    {
        if (_rootPath is not { } root || SelectedCommit is not { } commit || SelectedFile is not { } file)
        {
            Diff = string.Empty;
            return;
        }

        try
        {
            Diff = _git.GetDiff(root, commit.Sha, file.Path);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось получить diff");
            Diff = $"Ошибка: {exception.Message}";
        }
    }
}
