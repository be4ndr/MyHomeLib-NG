using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyHomeLibNG.App.Views;

public partial class DirectoryView : UserControl
{
    public DirectoryView()
    {
        InitializeComponent();
    }

    private MainWindow OwnerWindow =>
        TopLevel.GetTopLevel(this) as MainWindow
        ?? throw new InvalidOperationException("DirectoryView must be hosted inside MainWindow.");

    private void OnDirectoryAuthorsClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleDirectoryAuthorsClicked();
    }

    private void OnDirectoryTitlesClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleDirectoryTitlesClicked();
    }

    private void OnDirectorySeriesClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleDirectorySeriesClicked();
    }

    private void OnDirectoryGenresClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleDirectoryGenresClicked();
    }

    private async void OnAddLibraryClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleAddLibraryClickedAsync();
    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleRefreshClickedAsync();
    }

    private void OnSearchModeClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleSearchModeClicked();
    }

    private async void OnBooksSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await OwnerWindow.HandleBooksSelectionChangedAsync();
    }

    private async void OnPrimaryBookActionClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandlePrimaryBookActionClickedAsync();
    }

    private async void OnCopyLinkClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleCopyLinkClickedAsync();
    }
}
