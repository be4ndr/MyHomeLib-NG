using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
            Assert.False(string.IsNullOrWhiteSpace(window.OnlineSourceUrl));
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void AddLibraryWindow_RejectsInvalidOnlineUrl()
    {
        var window = new AddLibraryWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.IsOnlineSourceSelected = true;
            window.LibraryName = "Custom source";
            window.OnlineSourceUrl = "not-a-url";
            Dispatcher.UIThread.RunJobs();

            var saveButton = window.FindControl<Button>("AddLibrarySaveButton");
            Assert.NotNull(saveButton);

            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(window.HasValidationMessage);
            Assert.Equal("Online source URL must be an absolute HTTP or HTTPS URL.", window.ValidationMessage);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
