using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App;

public partial class MainWindow : Window, IWorkspaceWindowActions
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
        : this(((App)Avalonia.Application.Current!).Services.GetRequiredService<MainWindowViewModel>())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        Closed += OnClosed;
        Opened += OnOpened;
    }

    public LibraryManagerWindow? LastOpenedLibraryManagerWindow { get; private set; }
    public SearchWindow? LastOpenedSearchWindow { get; private set; }
    public BrowseWindow? LastOpenedBrowseWindow { get; private set; }
    public SettingsWindow? LastOpenedSettingsWindow { get; private set; }
    public AddLibraryWindow? LastOpenedAddLibraryWindow { get; private set; }

    public async Task HandleLibrariesSelectionChangedAsync()
    {
        await _viewModel.OnSelectedLibraryChangedAsync();
    }

    public async Task HandleBooksSelectionChangedAsync()
    {
        await _viewModel.OnSelectedBookChangedAsync();
    }

    public async Task HandleSearchClickedAsync()
    {
        OpenSearchWindow(executeOnOpen: !string.IsNullOrWhiteSpace(_viewModel.SearchQuery) && _viewModel.CanSearch);
        await Task.CompletedTask;
    }

    public async Task ExecuteSearchAsync()
    {
        _viewModel.SetMode(AppMode.Search);
        await _viewModel.SearchAsync();
    }

    public async Task HandleRefreshClickedAsync()
    {
        await _viewModel.RefreshAsync();
    }

    public async Task HandleSettingsClickedAsync()
    {
        LastOpenedSettingsWindow = new SettingsWindow(this);
        LastOpenedSettingsWindow.Show(this);
        await Task.CompletedTask;
    }

    public async Task HandleManageLibrariesClickedAsync()
    {
        LastOpenedLibraryManagerWindow = new LibraryManagerWindow(this, _viewModel);
        LastOpenedLibraryManagerWindow.Show(this);
        await Task.CompletedTask;
    }

    public async Task HandleAddLibraryClickedAsync()
    {
        LastOpenedAddLibraryWindow = new AddLibraryWindow();
        var profile = await LastOpenedAddLibraryWindow.ShowDialog<LibraryProfile?>(this);
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

    public async Task HandleDeleteLibraryClickedAsync(LibraryProfileItemViewModel library)
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

    public async Task HandlePrimaryBookActionClickedAsync()
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

    public async Task HandleCopyLinkClickedAsync()
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

    public void HandleLibrariesModeClicked()
    {
        _ = HandleManageLibrariesClickedAsync();
    }

    public void HandleSearchModeClicked()
    {
        OpenSearchWindow(executeOnOpen: false);
    }

    public async Task HandleDirectoryModeClickedAsync()
    {
        LastOpenedBrowseWindow = new BrowseWindow(this, _viewModel);
        LastOpenedBrowseWindow.Show(this);
        await Task.CompletedTask;
    }

    public async Task OpenDirectoryAsync()
    {
        await _viewModel.OpenDirectoryModeAsync();
    }

    public Task HandleScanLocalClickedAsync()
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

    public void HandleClearSearchFiltersClicked()
    {
        _viewModel.ResetStructuredSearch();
    }

    public void HandleDirectoryAuthorsClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Authors);
    }

    public void HandleDirectoryTitlesClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Titles);
    }

    public void HandleDirectorySeriesClicked()
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Series);
    }

    public void HandleDirectoryGenresClicked()
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
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OpenSearchWindow(bool executeOnOpen)
    {
        LastOpenedSearchWindow = new SearchWindow(this, _viewModel, executeOnOpen);
        LastOpenedSearchWindow.Show(this);
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

    private async void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await HandleSettingsClickedAsync();
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
        _ = HandleManageLibrariesClickedAsync();
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
