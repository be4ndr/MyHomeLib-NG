using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class SettingsWindow : Window
{
    private readonly MainWindow? _ownerWindow;

    public SettingsWindow()
        : this(null)
    {
    }

    public SettingsWindow(MainWindow? ownerWindow)
    {
        _ownerWindow = ownerWindow;
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private async void OnOpenLibraryManagerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ownerWindow is null)
        {
            return;
        }

        await _ownerWindow.HandleManageLibrariesClickedAsync();
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
