using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MyHomeLibNG.App.HeadlessTests.TestDoubles;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.App.Views;
using Xunit;

namespace MyHomeLibNG.App.HeadlessTests;

public sealed class VisibilityStateTests
{
    [AvaloniaFact]
    public async Task EmptyWorkspace_ShowsFirstRunStateWithoutCrashing()
    {
        var viewModel = TestViewModelFactory.CreateEmptyWorkspace();
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var librariesView = Assert.IsType<LibrariesView>(window.CurrentView);
            var firstRunState = librariesView.FindControl<StackPanel>("LibrariesFirstRunState");
            Assert.NotNull(firstRunState);
            Assert.True(firstRunState.IsVisible);
            Assert.False(viewModel.HasLibraries);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task SearchView_ShowsPromptBeforeSearchingAndNoResultsAfterEmptySearch()
    {
        var viewModel = TestViewModelFactory.CreateWithOnlineLibrary();
        await viewModel.InitializeAsync();
        viewModel.SetMode(AppMode.Search);

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var searchView = Assert.IsType<SearchView>(window.CurrentView);
            var promptState = searchView.FindControl<StackPanel>("SearchPromptState");
            var noResultsState = searchView.FindControl<StackPanel>("SearchNoResultsState");

            Assert.NotNull(promptState);
            Assert.NotNull(noResultsState);
            Assert.True(promptState.IsVisible);
            Assert.False(noResultsState.IsVisible);

            viewModel.SearchQuery = "No matching title";
            await viewModel.SearchAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.False(promptState.IsVisible);
            Assert.True(noResultsState.IsVisible);
            Assert.Empty(viewModel.Results);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
