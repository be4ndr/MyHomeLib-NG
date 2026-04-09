using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyHomeLibNG.App.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    private MainWindow OwnerWindow =>
        TopLevel.GetTopLevel(this) as MainWindow
        ?? throw new InvalidOperationException("SearchView must be hosted inside MainWindow.");

    private async void OnAddLibraryClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleAddLibraryClickedAsync();
    }

    private async void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleSearchClickedAsync();
    }

    private void OnClearSearchFiltersClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleClearSearchFiltersClicked();
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
