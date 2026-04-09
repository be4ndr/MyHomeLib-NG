using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyHomeLibNG.App.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    private MainWindow? OwnerWindow => TopLevel.GetTopLevel(this) as MainWindow;

    private async void OnAddLibraryClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleAddLibraryClickedAsync();
    }

    private async void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleSearchClickedAsync();
    }

    private void OnClearSearchFiltersClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleClearSearchFiltersClicked();
    }

    private async void OnBooksSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleBooksSelectionChangedAsync();
    }

    private async void OnPrimaryBookActionClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandlePrimaryBookActionClickedAsync();
    }

    private async void OnCopyLinkClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleCopyLinkClickedAsync();
    }
}
