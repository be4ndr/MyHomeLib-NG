using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class DeleteLibraryDialog : Window
{
    public DeleteLibraryDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public DeleteLibraryDialog(LibraryProfileItemViewModel library)
        : this()
    {
        ArgumentNullException.ThrowIfNull(library);

        LibraryName = library.Name;
        LibraryDetails = $"{library.TypeLabel} | {library.ProviderLabel}";
        Message = $"This removes '{library.Name}' from your library list. Search results and directory state for that library will be cleared.";
    }

    public string LibraryName { get; private set; } = string.Empty;
    public string LibraryDetails { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
