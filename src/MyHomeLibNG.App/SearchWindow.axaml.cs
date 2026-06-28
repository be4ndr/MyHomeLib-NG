using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class SearchWindow : Window, IWorkspaceWindowActions
{
    private readonly MainWindow? _ownerWindow;
    private readonly MainWindowViewModel? _viewModel;
    private readonly bool _executeOnOpen;

    public SearchWindow()
    {
        InitializeComponent();
    }

    public SearchWindow(MainWindow ownerWindow, MainWindowViewModel viewModel, bool executeOnOpen)
    {
        _ownerWindow = ownerWindow;
        _viewModel = viewModel;
        _executeOnOpen = executeOnOpen;
        InitializeComponent();
        DataContext = viewModel;
        Opened += OnOpened;
    }

    public Task HandleAddLibraryClickedAsync()
        => _ownerWindow?.HandleAddLibraryClickedAsync() ?? Task.CompletedTask;

    public Task HandleRefreshClickedAsync()
        => _ownerWindow?.HandleRefreshClickedAsync() ?? Task.CompletedTask;

    public Task HandleSearchClickedAsync()
        => _ownerWindow?.ExecuteSearchAsync() ?? Task.CompletedTask;

    public Task HandleBooksSelectionChangedAsync()
        => _ownerWindow?.HandleBooksSelectionChangedAsync() ?? Task.CompletedTask;

    public Task HandlePrimaryBookActionClickedAsync()
        => _ownerWindow?.HandlePrimaryBookActionClickedAsync() ?? Task.CompletedTask;

    public Task HandleCopyLinkClickedAsync()
        => _ownerWindow?.HandleCopyLinkClickedAsync() ?? Task.CompletedTask;

    public void HandleClearSearchFiltersClicked()
        => _ownerWindow?.HandleClearSearchFiltersClicked();

    public void HandleSearchModeClicked()
    {
    }

    public Task HandleDirectoryModeClickedAsync()
        => _ownerWindow?.HandleDirectoryModeClickedAsync() ?? Task.CompletedTask;

    public void HandleDirectoryAuthorsClicked()
    {
    }

    public void HandleDirectoryTitlesClicked()
    {
    }

    public void HandleDirectorySeriesClicked()
    {
    }

    public void HandleDirectoryGenresClicked()
    {
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (_executeOnOpen && _viewModel?.CanSearch == true)
        {
            await HandleSearchClickedAsync();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
