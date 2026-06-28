using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class LibraryManagerWindow : Window
{
    private readonly MainWindow? _ownerWindow;
    private readonly MainWindowViewModel? _viewModel;
    private readonly LibraryManagerViewModel? _managerViewModel;

    public LibraryManagerWindow()
    {
        InitializeComponent();
    }

    public LibraryManagerWindow(MainWindow ownerWindow, MainWindowViewModel viewModel)
    {
        _ownerWindow = ownerWindow;
        _viewModel = viewModel;
        _managerViewModel = new LibraryManagerViewModel(viewModel);
        InitializeComponent();
        DataContext = _managerViewModel;
        Closed += OnClosed;
    }

    private async void OnLibrariesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_managerViewModel is null)
        {
            return;
        }

        await _managerViewModel.ActivateSelectedLibraryAsync();
    }

    private async void OnAddLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ownerWindow is null)
        {
            return;
        }

        await _ownerWindow.HandleAddLibraryClickedAsync();
    }

    private async void OnSetActiveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_managerViewModel is null)
        {
            return;
        }

        await _managerViewModel.ActivateSelectedLibraryAsync();
    }

    private async void OnRefreshClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ownerWindow is null)
        {
            return;
        }

        await _ownerWindow.HandleRefreshClickedAsync();
    }

    private async void OnScanLocalClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ownerWindow is null)
        {
            return;
        }

        await _ownerWindow.HandleScanLocalClickedAsync();
    }

    private async void OnDeleteLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ownerWindow is null || _viewModel?.SelectedLibrary is null)
        {
            return;
        }

        await _ownerWindow.HandleDeleteLibraryClickedAsync(_viewModel.SelectedLibrary);
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _managerViewModel?.Dispose();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
