using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App;

public partial class AddLibraryWindow : Window, INotifyPropertyChanged
{
    private ProviderOption? _selectedOption;
    private string _libraryName = string.Empty;
    private string _inpxFilePath = string.Empty;
    private string _archiveDirectoryPath = string.Empty;
    private string? _validationMessage;

    public AddLibraryWindow()
    {
        InitializeComponent();
        ProviderOptions =
        [
            new ProviderOption(
                "Offline INPX",
                BookProviderIds.OfflineInpx,
                LibraryType.Folder,
                "Connect a local INPX index plus the archive folder where book files live.",
                string.Empty,
                string.Empty),
            new ProviderOption(
                "Project Gutenberg",
                BookProviderIds.ProjectGutenberg,
                LibraryType.Online,
                "A public-domain catalog with lightweight metadata and online reading links.",
                "https://www.gutenberg.org",
                "https://www.gutenberg.org/ebooks/search.opds"),
            new ProviderOption(
                "Open Library",
                BookProviderIds.OpenLibrary,
                LibraryType.Online,
                "A broad open catalog with work details and external reader links.",
                "https://openlibrary.org",
                "https://openlibrary.org/search.json"),
            new ProviderOption(
                "Google Books",
                BookProviderIds.GoogleBooks,
                LibraryType.Online,
                "Google Books metadata and reader links for broad discovery.",
                "https://www.googleapis.com",
                "https://www.googleapis.com/books/v1/volumes")
        ];

        SelectedOption = ProviderOptions[0];
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProviderOption> ProviderOptions { get; }

    public ProviderOption SelectedOption
    {
        get => _selectedOption ?? ProviderOptions[0];
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                if (string.IsNullOrWhiteSpace(LibraryName))
                {
                    LibraryName = value.Label;
                }

                ValidationMessage = null;
            }
        }
    }

    public string LibraryName
    {
        get => _libraryName;
        set => SetProperty(ref _libraryName, value);
    }

    public string InpxFilePath
    {
        get => _inpxFilePath;
        set => SetProperty(ref _inpxFilePath, value);
    }

    public string ArchiveDirectoryPath
    {
        get => _archiveDirectoryPath;
        set => SetProperty(ref _archiveDirectoryPath, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private async void OnBrowseInpxClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            ValidationMessage = "File browsing is not available in this window.";
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select INPX catalog",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("INPX catalog")
                {
                    Patterns = ["*.inpx"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        InpxFilePath = file.TryGetLocalPath() ?? file.Name;
        if (string.IsNullOrWhiteSpace(LibraryName) || string.Equals(LibraryName, SelectedOption.Label, StringComparison.Ordinal))
        {
            LibraryName = Path.GetFileNameWithoutExtension(InpxFilePath);
        }
    }

    private async void OnBrowseArchiveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            ValidationMessage = "Folder browsing is not available in this window.";
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select archive directory",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        ArchiveDirectoryPath = folder.TryGetLocalPath() ?? folder.Name;
        if (string.IsNullOrWhiteSpace(LibraryName) || string.Equals(LibraryName, SelectedOption.Label, StringComparison.Ordinal))
        {
            LibraryName = Path.GetFileName(ArchiveDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
    }

    private void OnSaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!TryBuildProfile(out var profile))
        {
            return;
        }

        Close(profile);
    }

    private bool TryBuildProfile(out LibraryProfile? profile)
    {
        profile = null;
        ValidationMessage = null;

        if (string.IsNullOrWhiteSpace(LibraryName))
        {
            ValidationMessage = "Enter a display name for the library.";
            return false;
        }

        if (SelectedOption.IsOffline)
        {
            if (string.IsNullOrWhiteSpace(InpxFilePath))
            {
                ValidationMessage = "Offline libraries require an INPX file path.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ArchiveDirectoryPath))
            {
                ValidationMessage = "Offline libraries require an archive directory path.";
                return false;
            }

            if (!File.Exists(InpxFilePath.Trim()))
            {
                ValidationMessage = "The selected INPX file does not exist.";
                return false;
            }

            if (!Directory.Exists(ArchiveDirectoryPath.Trim()))
            {
                ValidationMessage = "The selected archive directory does not exist.";
                return false;
            }

            profile = new LibraryProfile
            {
                Name = LibraryName.Trim(),
                ProviderId = SelectedOption.ProviderId,
                LibraryType = LibraryType.Folder,
                FolderSource = new FolderLibrarySourceSettings
                {
                    InpxFilePath = InpxFilePath.Trim(),
                    ArchiveDirectoryPath = ArchiveDirectoryPath.Trim()
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            return true;
        }

        profile = new LibraryProfile
        {
            Name = LibraryName.Trim(),
            ProviderId = SelectedOption.ProviderId,
            LibraryType = LibraryType.Online,
            OnlineSource = new OnlineLibrarySourceSettings
            {
                ApiBaseUrl = SelectedOption.ApiBaseUrl,
                SearchEndpoint = SelectedOption.SearchEndpoint
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        return true;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class ProviderOption
    {
        public ProviderOption(
            string label,
            string providerId,
            LibraryType libraryType,
            string description,
            string apiBaseUrl,
            string searchEndpoint)
        {
            Label = label;
            ProviderId = providerId;
            LibraryType = libraryType;
            Description = description;
            ApiBaseUrl = apiBaseUrl;
            SearchEndpoint = searchEndpoint;
        }

        public string Label { get; }
        public string ProviderId { get; }
        public LibraryType LibraryType { get; }
        public string Description { get; }
        public string ApiBaseUrl { get; }
        public string SearchEndpoint { get; }
        public bool IsOffline => LibraryType == LibraryType.Folder;
        public bool IsOnline => LibraryType == LibraryType.Online;

        public override string ToString()
            => Label;
    }
}
