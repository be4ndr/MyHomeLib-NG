using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.ViewModels;

public sealed class LibraryProfileItemViewModel
{
    public LibraryProfileItemViewModel(LibraryProfile profile)
    {
        Profile = profile;
    }

    public LibraryProfile Profile { get; }
    public long Id => Profile.Id;
    public string Name => Profile.Name;
    public string ProviderId => Profile.ProviderId;
    public string TypeLabel => Profile.LibraryType == LibraryType.Folder ? "Offline" : "Online";

    public string ProviderLabel => Profile.ProviderId switch
    {
        BookProviderIds.OfflineInpx => "INPX catalog",
        BookProviderIds.ProjectGutenberg => "Project Gutenberg",
        BookProviderIds.OpenLibrary => "Open Library",
        BookProviderIds.GoogleBooks => "Google Books",
        _ => Profile.ProviderId
    };

    public string Summary => Profile.LibraryType switch
    {
        LibraryType.Folder => Profile.FolderSource?.ArchiveDirectoryPath ?? "Local folder source",
        _ => Profile.OnlineSource?.ApiBaseUrl ?? "Remote catalog"
    };

    public string AccentGlyph => Profile.LibraryType == LibraryType.Folder ? "OF" : "ON";
}
