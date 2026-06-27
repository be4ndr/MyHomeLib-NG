using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
            var librariesList = window.FindControl<ListBox>("LibrariesList");

            Assert.NotNull(searchBox);
            Assert.NotNull(librariesList);
            Assert.Same(viewModel, window.DataContext);
            Assert.Same(viewModel, searchBox.DataContext);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
