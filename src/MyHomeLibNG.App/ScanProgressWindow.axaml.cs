using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class ScanProgressWindow : Window
{
    private ScanProgressWindowViewModel? _viewModel;

    public ScanProgressWindow()
    {
        InitializeComponent();
    }

    public ScanProgressWindow(ScanProgressWindowViewModel viewModel)
        : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (_viewModel is not null)
        {
            await _viewModel.StartAsync();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _viewModel?.Dispose();
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.Cancel();
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
