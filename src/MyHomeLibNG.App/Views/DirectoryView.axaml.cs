using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MyHomeLibNG.App.Views;

public partial class DirectoryView : UserControl
{
    public DirectoryView()
    {
        InitializeComponent();
    }

    private MainWindow? OwnerWindow => TopLevel.GetTopLevel(this) as MainWindow;

    private void OnDirectoryAuthorsClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleDirectoryAuthorsClicked();
    }

    private void OnDirectoryTitlesClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleDirectoryTitlesClicked();
    }

    private void OnDirectorySeriesClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleDirectorySeriesClicked();
    }

    private void OnDirectoryGenresClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleDirectoryGenresClicked();
    }

    private async void OnAddLibraryClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleAddLibraryClickedAsync();
    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleRefreshClickedAsync();
    }

    private void OnSearchModeClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleSearchModeClicked();
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
