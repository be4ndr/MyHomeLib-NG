using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.App.Views;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App;

public partial class MainWindow : Window
{
    public static readonly DirectProperty<MainWindow, Control?> CurrentViewProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, Control?>(
            nameof(CurrentView),
            window => window.CurrentView);

    private readonly MainWindowViewModel _viewModel;
    private readonly LibrariesView _librariesView = new();
    private readonly SearchView _searchView = new();
    private readonly DirectoryView _directoryView = new();
    private Control? _currentView;

    public MainWindow()
        : this(((App)Avalonia.Application.Current!).Services.GetRequiredService<MainWindowViewModel>())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;
        Opened += OnOpened;
        UpdateCurrentView();
    }

    public Control? CurrentView
    {
        get => _currentView;
        private set => SetAndRaise(CurrentViewProperty, ref _currentView, value);
    }

    internal async Task HandleLibrariesSelectionChangedAsync()
    {
        await _viewModel.OnSelectedLibraryChangedAsync();
    }

    internal async Task HandleBooksSelectionChangedAsync()
    {
        await _viewModel.OnSelectedBookChangedAsync();
    }

    internal async Task HandleSearchClickedAsync()
    {
        _viewModel.SetMode(AppMode.Search);
        await _viewModel.SearchAsync();
    }

    internal async Task HandleRefreshClickedAsync()
    {
        await _viewModel.RefreshAsync();
    }

    internal void HandleSettingsClicked()
    {
        _viewModel.ReportActionSuccess("Settings will land in a dedicated preferences screen in the next iteration.");
    }

    internal async Task HandleAddLibraryClickedAsync()
    {
        var dialog = new AddLibraryWindow();
        var profile = await dialog.ShowDialog<LibraryProfile?>(this);
        if (profile is null)
        {
            return;
        }

        await _viewModel.AddLibraryAsync(profile);

        if (profile.LibraryType == LibraryType.Folder && _viewModel.SelectedLibrary?.Profile is { } savedProfile)
        {
            await HandleScanLocalClickedAsync(savedProfile);
        }
    }

    internal async Task HandleDeleteLibraryClickedAsync(LibraryProfileItemViewModel library)
    {
        ArgumentNullException.ThrowIfNull(library);

        var dialog = new DeleteLibraryDialog(library);
        var shouldDelete = await dialog.ShowDialog<bool>(this);
        if (!shouldDelete)
        {
            return;
        }

        await _viewModel.DeleteLibraryAsync(library);
    }

    internal async Task HandlePrimaryBookActionClickedAsync()
    {
        try
        {
            var request = await _viewModel.PreparePrimaryActionAsync();
            if (request is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(request.FilePath))
            {
                Process.Start(new ProcessStartInfo(request.FilePath) { UseShellExecute = true });
                _viewModel.ReportActionSuccess("Opened book content with the system default application.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(request.Uri))
            {
                Process.Start(new ProcessStartInfo(request.Uri) { UseShellExecute = true });
                _viewModel.ReportActionSuccess("Opened the book link in your default browser.");
            }
        }
        catch (Exception exception)
        {
            _viewModel.ReportActionFailure(exception.Message);
        }
    }

    internal async Task HandleCopyLinkClickedAsync()
    {
        var link = _viewModel.GetPreferredLink();
        if (string.IsNullOrWhiteSpace(link))
        {
            _viewModel.ReportActionFailure("There is no shareable link for the selected title.");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            _viewModel.ReportActionFailure("Clipboard access is not available in this window.");
            return;
        }

        await topLevel.Clipboard.SetTextAsync(link);
        _viewModel.ReportActionSuccess("Copied the best available book link to the clipboard.");
    }

    internal void HandleLibrariesModeClicked()
    {
        _viewModel.SetMode(AppMode.Libraries);
    }

    internal void HandleSearchModeClicked()
    {
        _viewModel.SetMode(AppMode.Search);
    }

    internal async Task HandleDirectoryModeClickedAsync()
    {
        await _viewModel.OpenDirectoryModeAsync();
    }

    internal Task HandleScanLocalClickedAsync()
    {
        if (_viewModel.SelectedLibrary?.Profile is not { } profile)
        {
            return Task.CompletedTask;
        }

        return HandleScanLocalClickedAsync(profile);
    }

    private Task HandleScanLocalClickedAsync(LibraryProfile profile)
    {
        try
        {
            var coordinator = ((App)Avalonia.Application.Current!).Services.GetRequiredService<LocalLibraryScanCoordinator>();
            var window = new ScanProgressWindow(new ScanProgressWindowViewModel(coordinator, profile));
            window.Closed += OnScanWindowClosed;
            window.Show(this);
            _viewModel.ReportActionSuccess($"Started background scan for {profile.Name}.");
        }
        catch (Exception exception)
        {
            _viewModel.ReportActionFailure(exception.Message);
        }

        return Task.CompletedTask;
    }

    private async void OnScanWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Closed -= OnScanWindowClosed;
        }

        await _viewModel.RefreshAsync();
    }

    internal void HandleClearSearchFiltersClicked()
    {
        _viewModel.ResetStructuredSearch();
    }

    internal void HandleDirectoryAuthorsClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Authors);
    }

    internal void HandleDirectoryTitlesClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Titles);
    }

    internal void HandleDirectorySeriesClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Series);
    }

    internal void HandleDirectoryGenresClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Genres);
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await _viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.CurrentMode))
        {
            UpdateCurrentView();
        }
    }

    private void UpdateCurrentView()
    {
        CurrentView = _viewModel.CurrentMode switch
        {
            AppMode.Search => _searchView,
            AppMode.Directory => _directoryView,
            _ => _librariesView
        };
    }

    private async void OnLibrariesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await HandleLibrariesSelectionChangedAsync();
    }

    private async void OnBooksSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await HandleBooksSelectionChangedAsync();
    }

    private async void OnSearchClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleSearchClickedAsync();
    }

    private async void OnRefreshClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleRefreshClickedAsync();
    }

    private void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleSettingsClicked();
    }

    private async void OnAddLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleAddLibraryClickedAsync();
    }

    private async void OnDeleteLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: LibraryProfileItemViewModel library })
        {
            return;
        }

        await HandleDeleteLibraryClickedAsync(library);
    }

    private async void OnPrimaryBookActionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandlePrimaryBookActionClickedAsync();
    }

    private async void OnCopyLinkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleCopyLinkClickedAsync();
    }

    private async void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await HandleSearchClickedAsync();
    }

    private void OnLibrariesModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleLibrariesModeClicked();
    }

    private void OnSearchModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleSearchModeClicked();
    }

    private async void OnDirectoryModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleDirectoryModeClickedAsync();
    }

    private async void OnScanLocalClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleScanLocalClickedAsync();
    }

    private void OnClearSearchFiltersClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleClearSearchFiltersClicked();
    }

    private void OnDirectoryAuthorsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleDirectoryAuthorsClicked();
    }

    private void OnDirectoryTitlesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleDirectoryTitlesClicked();
    }

    private void OnDirectorySeriesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleDirectorySeriesClicked();
    }

    private void OnDirectoryGenresClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleDirectoryGenresClicked();
    }
}
