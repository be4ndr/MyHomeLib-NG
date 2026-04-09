using Avalonia.Controls;
using MyHomeLibNG.App.ViewModels;

namespace MyHomeLibNG.App;

public partial class DeleteLibraryDialog : Window
{
    public DeleteLibraryDialog()
    {
        InitializeComponent();
    }

    public DeleteLibraryDialog(LibraryProfileItemViewModel library)
        : this()
    {
        ArgumentNullException.ThrowIfNull(library);

        LibraryName = library.Name;
        LibraryDetails = $"{library.TypeLabel} | {library.ProviderLabel}";
        Message = $"This removes '{library.Name}' from your library list. Search results and directory state for that library will be cleared.";
        DataContext = this;
    }

    public string LibraryName { get; } = string.Empty;
    public string LibraryDetails { get; } = string.Empty;
    public string Message { get; } = string.Empty;

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
}
