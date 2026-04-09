using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App;

public partial class MainWindow : Window
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
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await _viewModel.InitializeAsync();
    }

    private async void OnLibrariesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await _viewModel.OnSelectedLibraryChangedAsync();
    }

    private async void OnBooksSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await _viewModel.OnSelectedBookChangedAsync();
    }

    private async void OnSearchClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetMode(AppMode.Search);
        await _viewModel.SearchAsync();
    }

    private async void OnRefreshClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
    }

    private void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.ReportActionSuccess("Settings will land in a dedicated preferences screen in the next iteration.");
    }

    private async void OnAddLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new AddLibraryWindow();
        var profile = await dialog.ShowDialog<LibraryProfile?>(this);
        if (profile is null)
        {
            return;
        }

        await _viewModel.AddLibraryAsync(profile);
    }

    private async void OnDeleteLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: LibraryProfileItemViewModel library })
        {
            return;
        }

        var dialog = new DeleteLibraryDialog(library);
        var shouldDelete = await dialog.ShowDialog<bool>(this);
        if (!shouldDelete)
        {
            return;
        }

        await _viewModel.DeleteLibraryAsync(library);
    }

    private async void OnPrimaryBookActionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private async void OnCopyLinkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private async void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        _viewModel.SetMode(AppMode.Search);
        await _viewModel.SearchAsync();
    }

    private void OnLibrariesModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetMode(AppMode.Libraries);
    }

    private void OnSearchModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetMode(AppMode.Search);
    }

    private async void OnDirectoryModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _viewModel.OpenDirectoryModeAsync();
    }

    private void OnClearSearchFiltersClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.ResetStructuredSearch();
    }

    private void OnDirectoryAuthorsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Authors);
    }

    private void OnDirectoryTitlesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Titles);
    }

    private void OnDirectorySeriesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Series);
    }

    private void OnDirectoryGenresClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.SetDirectoryBrowseMode(DirectoryBrowseMode.Genres);
    }
}
