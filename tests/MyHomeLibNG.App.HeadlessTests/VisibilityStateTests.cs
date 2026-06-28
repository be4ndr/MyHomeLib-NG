using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MyHomeLibNG.App.HeadlessTests.TestDoubles;
using MyHomeLibNG.App.Views;
using Xunit;

namespace MyHomeLibNG.App.HeadlessTests;

public sealed class VisibilityStateTests
{
    [AvaloniaFact]
    public async Task EmptyWorkspace_ShowsLauncherEmptyStateWithoutPageHost()
    {
        var viewModel = TestViewModelFactory.CreateEmptyWorkspace();
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var firstRunState = window.FindControl<StackPanel>("LauncherEmptyState");
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
    public async Task SearchWindow_ShowsPromptBeforeSearchingAndNoResultsAfterEmptySearch()
    {
        var viewModel = TestViewModelFactory.CreateWithOnlineLibrary();
        await viewModel.InitializeAsync();

        var owner = new MainWindow(viewModel);
        var window = new SearchWindow(owner, viewModel, executeOnOpen: false);
        try
        {
            owner.Show();
            window.Show(owner);
            Dispatcher.UIThread.RunJobs();

            var searchView = Assert.IsType<SearchView>(window.Content);
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
            owner.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
