using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MyHomeLibNG.App.HeadlessTests.TestDoubles;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.App.Views;
using Xunit;

namespace MyHomeLibNG.App.HeadlessTests;

public sealed class BindingSmokeTests
{
    [AvaloniaFact]
    public async Task SearchControls_BindToExpectedViewModelProperties()
    {
        var viewModel = TestViewModelFactory.CreateWithOnlineLibrary();
        await viewModel.InitializeAsync();
        viewModel.SetMode(AppMode.Search);

        var window = new MainWindow(viewModel);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var headerSearchBox = window.FindControl<TextBox>("SearchBox");
            var searchView = Assert.IsType<SearchView>(window.CurrentView);
            var authorTextBox = searchView.FindControl<TextBox>("SearchAuthorTextBox");
            var titleTextBox = searchView.FindControl<TextBox>("SearchTitleTextBox");
            var seriesTextBox = searchView.FindControl<TextBox>("SearchSeriesTextBox");
            var genreTextBox = searchView.FindControl<TextBox>("SearchGenreTextBox");
            var yearTextBox = searchView.FindControl<TextBox>("SearchYearTextBox");
            var languageTextBox = searchView.FindControl<TextBox>("SearchLanguageTextBox");

            Assert.NotNull(headerSearchBox);
            Assert.NotNull(authorTextBox);
            Assert.NotNull(titleTextBox);
            Assert.NotNull(seriesTextBox);
            Assert.NotNull(genreTextBox);
            Assert.NotNull(yearTextBox);
            Assert.NotNull(languageTextBox);

            headerSearchBox.Text = "Dune";
            authorTextBox.Text = "Frank Herbert";
            titleTextBox.Text = "Dune";
            seriesTextBox.Text = "Dune";
            genreTextBox.Text = "Science Fiction";
            yearTextBox.Text = "1965";
            languageTextBox.Text = "en";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Dune", viewModel.SearchQuery);
            Assert.Equal("Frank Herbert", viewModel.SearchAuthor);
            Assert.Equal("Dune", viewModel.SearchTitle);
            Assert.Equal("Dune", viewModel.SearchSeries);
            Assert.Equal("Science Fiction", viewModel.SearchGenre);
            Assert.Equal("1965", viewModel.SearchYear);
            Assert.Equal("en", viewModel.SearchLanguage);

            viewModel.SearchQuery = "Foundation";
            viewModel.SearchAuthor = "Isaac Asimov";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Foundation", headerSearchBox.Text);
            Assert.Equal("Isaac Asimov", authorTextBox.Text);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
