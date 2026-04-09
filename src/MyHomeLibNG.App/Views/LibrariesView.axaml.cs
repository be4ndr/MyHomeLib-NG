using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyHomeLibNG.App.Views;

public partial class LibrariesView : UserControl
{
    public LibrariesView()
    {
        InitializeComponent();
    }

    private MainWindow OwnerWindow =>
        TopLevel.GetTopLevel(this) as MainWindow
        ?? throw new InvalidOperationException("LibrariesView must be hosted inside MainWindow.");

    private async void OnAddLibraryClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleAddLibraryClickedAsync();
    }

    private void OnSearchModeClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow.HandleSearchModeClicked();
    }

    private async void OnDirectoryModeClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleDirectoryModeClickedAsync();
    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        await OwnerWindow.HandleRefreshClickedAsync();
    }
}
