using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public interface IWorkspaceWindowActions
{
    Task HandleAddLibraryClickedAsync();
    Task HandleRefreshClickedAsync();
    Task HandleSearchClickedAsync();
    Task HandleBooksSelectionChangedAsync();
    Task HandlePrimaryBookActionClickedAsync();
    Task HandleCopyLinkClickedAsync();
    void HandleClearSearchFiltersClicked();
    void HandleSearchModeClicked();
    Task HandleDirectoryModeClickedAsync();
    void HandleDirectoryAuthorsClicked();
    void HandleDirectoryTitlesClicked();
    void HandleDirectorySeriesClicked();
    void HandleDirectoryGenresClicked();
}
