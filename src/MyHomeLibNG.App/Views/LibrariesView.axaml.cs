using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MyHomeLibNG.App.Views;

public partial class LibrariesView : UserControl
{
    public LibrariesView()
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

    private void OnSearchModeClicked(object? sender, RoutedEventArgs e)
    {
        OwnerWindow?.HandleSearchModeClicked();
    }

    private async void OnDirectoryModeClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleDirectoryModeClickedAsync();
    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } ownerWindow)
        {
            return;
        }

        await ownerWindow.HandleRefreshClickedAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
