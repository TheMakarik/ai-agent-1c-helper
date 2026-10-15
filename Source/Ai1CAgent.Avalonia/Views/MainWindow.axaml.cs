using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Serilog;

namespace Ai1CAgent.Avalonia.Views;

public partial class MainWindow : Window
{
    private const double StickToBottomThreshold = 24;

    private MainViewModel? _handlerViewModel;
    private TaskCompletionSource<bool>? _confirmationRequest;
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        MessagesScrollViewer.PropertyChanged += OnMessagesScrollViewerPropertyChanged;
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || ReferenceEquals(viewModel, _handlerViewModel))
            return;

        _handlerViewModel = viewModel;

        viewModel.ConfirmUnsavedChanges.RegisterHandler(async context =>
            context.SetOutput(await ConfirmAsync()));

        viewModel.OpenFolder.RegisterHandler(async context =>
        {
            if (Directory.Exists(context.Input))
            {
                var folder = await StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(context.Input)));
                if (folder is not null)
                    await Launcher.LaunchFileAsync(folder);
            }

            context.SetOutput(Unit.Default);
        });
    }

    private Task<bool> ConfirmAsync()
    {
        if (_confirmationRequest is { } existing)
            return existing.Task;

        var request = new TaskCompletionSource<bool>();
        _confirmationRequest = request;

        if (_handlerViewModel is { } viewModel)
            viewModel.IsConfirmSaveOpen = true;

        return request.Task;
    }

    private void CompleteConfirmation(bool proceed)
    {
        if (_confirmationRequest is not { } request)
            return;

        _confirmationRequest = null;

        if (_handlerViewModel is { } viewModel)
            viewModel.IsConfirmSaveOpen = false;

        request.TrySetResult(proceed);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
            return;

        if (DataContext is not MainViewModel viewModel || !viewModel.Settings.IsDirty)
            return;

        e.Cancel = true;

        try
        {
            if (!await ConfirmAsync())
                return;

            _closeConfirmed = true;
            Close();
        }
        catch (Exception exception)
        {
            Log.Logger.Error(exception, "Не удалось подтвердить закрытие окна с несохранёнными изменениями");
        }
    }

    private bool _isConfirmSaving;

    private async void OnConfirmSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_confirmationRequest is null || DataContext is not MainViewModel viewModel)
            return;

        if (_isConfirmSaving)
            return;

        _isConfirmSaving = true;

        try
        {
            await viewModel.Settings.SaveAsync();
            CompleteConfirmation(true);
        }
        catch (Exception exception)
        {
            Log.Logger.Error(exception, "Не удалось сохранить настройки из подтверждения перехода");
            viewModel.ConfirmSaveError = "Не удалось сохранить настройки. Проверьте параметры провайдера и подключение.";
        }
        finally
        {
            _isConfirmSaving = false;
        }
    }

    private void OnConfirmDiscardClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        viewModel.Settings.DiscardChanges();
        CompleteConfirmation(true);
    }

    private void OnConfirmCancelClick(object? sender, RoutedEventArgs e) => CompleteConfirmation(false);

    private void OnMessagesScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.ExtentProperty || e.OldValue is not Size oldExtent)
            return;

        var viewer = MessagesScrollViewer;
        var wasAtBottom = viewer.Offset.Y + viewer.Viewport.Height >= oldExtent.Height - StickToBottomThreshold;
        if (wasAtBottom)
            viewer.ScrollToEnd();
    }

    private void OnRenameDialogClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ChatDialogViewModel dialog } || DataContext is not MainViewModel viewModel)
            return;

        viewModel.BeginRenameDialog(dialog);
    }

    private async void OnDialogRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter)
            return;

        if (sender is not Control { DataContext: ChatDialogViewModel dialog } || DataContext is not MainViewModel viewModel)
            return;

        await viewModel.CommitRenameDialogAsync(dialog, CancellationToken.None);
    }

    private async void OnDialogRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ChatDialogViewModel dialog } || DataContext is not MainViewModel viewModel)
            return;

        if (!dialog.IsRenaming)
            return;

        await viewModel.CommitRenameDialogAsync(dialog, CancellationToken.None);
    }

    private async void OnDeleteDialogClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ChatDialogViewModel dialog } || DataContext is not MainViewModel viewModel)
            return;

        await viewModel.DeleteDialogAsync(dialog, CancellationToken.None);
    }
}
