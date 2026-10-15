using System;
using System.Threading.Tasks;
using Ai1CAgent.Avalonia.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Serilog;

namespace Ai1CAgent.Avalonia.Views;

public partial class MainWindow : Window
{
    private const double StickToBottomThreshold = 24;

    private MainViewModel? _handlerViewModel;
    private TaskCompletionSource<bool>? _confirmationRequest;
    private TaskCompletionSource<bool>? _mcpWarningRequest;
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

        viewModel.ConfirmMcpUnavailable.RegisterHandler(async context =>
        {
            _mcpWarningRequest = new TaskCompletionSource<bool>();
            viewModel.IsMcpWarningOpen = true;

            var proceed = await _mcpWarningRequest.Task;

            viewModel.IsMcpWarningOpen = false;
            _mcpWarningRequest = null;
            context.SetOutput(proceed);
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

    private void OnMcpContinueClick(object? sender, RoutedEventArgs e) => _mcpWarningRequest?.TrySetResult(true);

    private void OnMcpCancelClick(object? sender, RoutedEventArgs e) => _mcpWarningRequest?.TrySetResult(false);

    private void OnMessagesScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.ExtentProperty || e.OldValue is not Size oldExtent)
            return;

        var viewer = MessagesScrollViewer;
        var wasAtBottom = viewer.Offset.Y + viewer.Viewport.Height >= oldExtent.Height - StickToBottomThreshold;
        if (wasAtBottom)
            viewer.ScrollToEnd();
    }
}
