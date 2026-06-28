using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MyHomeLibNG.App.HeadlessTests.TestDoubles;
using MyHomeLibNG.App.ViewModels;
using Xunit;

namespace MyHomeLibNG.App.HeadlessTests;

public sealed class MainWindowHeadlessTests
{
    [AvaloniaFact]
    public async Task MainWindow_LoadsWithTestViewModel()
    {
        var viewModel = TestViewModelFactory.CreateWithOnlineLibrary();
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var searchBox = window.FindControl<TextBox>("SearchBox");
            var activeLibrarySelector = window.FindControl<ComboBox>("ActiveLibrarySelector");
            var shellAddLibraryButton = window.FindControl<Button>("ShellAddLibraryButton");

            Assert.NotNull(searchBox);
            Assert.NotNull(activeLibrarySelector);
            Assert.NotNull(shellAddLibraryButton);
            Assert.Same(viewModel, window.DataContext);
            Assert.Same(viewModel, searchBox.DataContext);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task MainWindow_DoesNotHostWorkspacePages()
    {
        var viewModel = TestViewModelFactory.CreateWithOnlineLibrary();
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(window.FindControl<Button>("ShellAddLibraryButton"));
            Assert.Null(window.FindControl<ListBox>("LibrariesList"));
            Assert.Null(window.FindControl<ListBox>("BooksList"));
            Assert.Null(window.FindControl<ListBox>("DirectoryEntriesList"));
            Assert.Null(window.FindControl<StackPanel>("LibrariesFirstRunState"));
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task LauncherButtons_OpenSecondaryWindows()
    {
        var viewModel = TestViewModelFactory.CreateWithOnlineLibrary();
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.FindControl<Button>("LibrariesLaunchButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.FindControl<Button>("SearchLaunchButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.FindControl<Button>("BrowseLaunchButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.FindControl<Button>("ShellSettingsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(window.LastOpenedLibraryManagerWindow);
            Assert.NotNull(window.LastOpenedSearchWindow);
            Assert.NotNull(window.LastOpenedBrowseWindow);
            Assert.NotNull(window.LastOpenedSettingsWindow);

            var librariesList = window.LastOpenedLibraryManagerWindow.FindControl<ListBox>("LibrariesList");
            Assert.NotNull(librariesList);
            var managerViewModel = Assert.IsType<LibraryManagerViewModel>(window.LastOpenedLibraryManagerWindow.DataContext);
            Assert.Same(viewModel.Libraries, managerViewModel.Libraries);
            Assert.IsType<SettingsViewModel>(window.LastOpenedSettingsWindow.DataContext);
        }
        finally
        {
            window.LastOpenedSettingsWindow?.Close();
            window.LastOpenedBrowseWindow?.Close();
            window.LastOpenedSearchWindow?.Close();
            window.LastOpenedLibraryManagerWindow?.Close();
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task AddLibraryAction_OpensAddLibraryDialog()
    {
        var viewModel = TestViewModelFactory.CreateEmptyWorkspace();
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var addTask = window.HandleAddLibraryClickedAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(window.LastOpenedAddLibraryWindow);
            window.LastOpenedAddLibraryWindow.Close(null);
            await addTask;
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
