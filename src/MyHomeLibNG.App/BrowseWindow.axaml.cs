using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class BrowseWindow : Window, IWorkspaceWindowActions
{
    private readonly MainWindow? _ownerWindow;
    private readonly MainWindowViewModel? _viewModel;

    public BrowseWindow()
    {
        InitializeComponent();
    }

    public BrowseWindow(MainWindow ownerWindow, MainWindowViewModel viewModel)
    {
        _ownerWindow = ownerWindow;
        _viewModel = viewModel;
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
        => _ownerWindow?.HandleSearchModeClicked();

    public Task HandleDirectoryModeClickedAsync()
        => _ownerWindow?.OpenDirectoryAsync() ?? Task.CompletedTask;

    public void HandleDirectoryAuthorsClicked()
        => _ownerWindow?.HandleDirectoryAuthorsClicked();

    public void HandleDirectoryTitlesClicked()
        => _ownerWindow?.HandleDirectoryTitlesClicked();

    public void HandleDirectorySeriesClicked()
        => _ownerWindow?.HandleDirectorySeriesClicked();

    public void HandleDirectoryGenresClicked()
        => _ownerWindow?.HandleDirectoryGenresClicked();

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (_viewModel?.HasSelectedLibrary == true)
        {
            await HandleDirectoryModeClickedAsync();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
