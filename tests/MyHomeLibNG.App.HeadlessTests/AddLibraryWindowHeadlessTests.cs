using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.App.HeadlessTests;

public sealed class AddLibraryWindowHeadlessTests
{
    [AvaloniaFact]
    public void AddLibraryWindow_LoadsWithExpectedDefaultState()
    {
        var window = new AddLibraryWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Same(window, window.DataContext);
            Assert.NotNull(window.FindControl<RadioButton>("OfflineSourceRadioButton"));
            Assert.NotNull(window.FindControl<RadioButton>("OnlineSourceRadioButton"));
            Assert.NotNull(window.FindControl<ComboBox>("ProviderOptionsComboBox"));
            Assert.NotNull(window.FindControl<TextBox>("LibraryNameTextBox"));

            var offlineFieldsPanel = window.FindControl<StackPanel>("OfflineFieldsPanel");
            var onlineDetailsPanel = window.FindControl<StackPanel>("OnlineDetailsPanel");

            Assert.NotNull(offlineFieldsPanel);
            Assert.NotNull(onlineDetailsPanel);
            Assert.True(offlineFieldsPanel.IsVisible);
            Assert.False(onlineDetailsPanel.IsVisible);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void AddLibraryWindow_ProviderSelectionUpdatesOfflineAndOnlinePanels()
    {
        var window = new AddLibraryWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.IsOnlineSourceSelected = true;
            Dispatcher.UIThread.RunJobs();

            var offlineFieldsPanel = window.FindControl<StackPanel>("OfflineFieldsPanel");
            var onlineDetailsPanel = window.FindControl<StackPanel>("OnlineDetailsPanel");
            var onlineSourceUrlTextBox = window.FindControl<TextBox>("OnlineSourceUrlTextBox");

            Assert.NotNull(offlineFieldsPanel);
            Assert.NotNull(onlineDetailsPanel);
            Assert.NotNull(onlineSourceUrlTextBox);
            Assert.False(offlineFieldsPanel.IsVisible);
            Assert.True(onlineDetailsPanel.IsVisible);
            Assert.True(onlineSourceUrlTextBox.IsReadOnly);
            Assert.False(string.IsNullOrWhiteSpace(window.OnlineSourceUrl));
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void AddLibraryWindow_SavesSelectedProviderPresetUrlForOnlineSources()
    {
        var window = new AddLibraryWindow
        {
            IsOnlineSourceSelected = true,
            LibraryName = "Open Library"
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var selectedOption = Assert.IsType<AddLibraryWindow.ProviderOption>(window.SelectedOption);
            var urlField = typeof(AddLibraryWindow).GetField("_onlineSourceUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(urlField);
            urlField.SetValue(window, "https://custom.example.test");

            var tryBuildProfile = typeof(AddLibraryWindow).GetMethod("TryBuildProfile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(tryBuildProfile);

            var arguments = new object?[] { null };
            var success = Assert.IsType<bool>(tryBuildProfile.Invoke(window, arguments));
            Assert.True(success);

            var profile = Assert.IsType<LibraryProfile>(arguments[0]);
            var onlineSource = Assert.IsType<OnlineLibrarySourceSettings>(profile.OnlineSource);
            Assert.Equal(selectedOption.ApiBaseUrl, onlineSource.ApiBaseUrl);
            Assert.Equal(selectedOption.SearchEndpoint, onlineSource.SearchEndpoint);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
