using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly LibraryProfilesService _libraryProfilesService;
    private readonly LibraryBooksService _libraryBooksService;
    private readonly IActiveLibraryContext _activeLibraryContext;
    private readonly ILibraryRepository _libraryRepository;
    private readonly ILibrarySourceResolver _librarySourceResolver;
    private bool _hasPerformedSearch;
    private bool _isBusy;
    private string _statusMessage = "Loading library workspace...";
    private string? _errorMessage;
    private string _searchQuery = string.Empty;
    private string _activeLibraryName = "No library selected";
    private string _activeLibrarySourceStatus = "Waiting";
    private string _activeLibrarySourceSummary = "No source connected yet.";
    private bool _activeLibraryHasSourceIssues;
    private LibraryProfileItemViewModel? _selectedLibrary;
    private BookListItemViewModel? _selectedBook;
    private BookDetailsViewModel? _selectedBookDetails;
    private long? _loadedLibraryId;
    private string? _loadedBookSourceId;
    private bool _isSwitchingLibrary;
    private bool _isLoadingBookDetails;

    public MainWindowViewModel(
        LibraryProfilesService libraryProfilesService,
        LibraryBooksService libraryBooksService,
        IActiveLibraryContext activeLibraryContext,
        ILibraryRepository libraryRepository,
        ILibrarySourceResolver librarySourceResolver)
    {
        _libraryProfilesService = libraryProfilesService;
        _libraryBooksService = libraryBooksService;
        _activeLibraryContext = activeLibraryContext;
        _libraryRepository = libraryRepository;
        _librarySourceResolver = librarySourceResolver;

        Libraries.CollectionChanged += OnCollectionsChanged;
        Results.CollectionChanged += OnCollectionsChanged;
    }

    public ObservableCollection<LibraryProfileItemViewModel> Libraries { get; } = [];
    public ObservableCollection<BookListItemViewModel> Results { get; } = [];

    public LibraryProfileItemViewModel? SelectedLibrary
    {
        get => _selectedLibrary;
        set
        {
            if (SetProperty(ref _selectedLibrary, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public BookListItemViewModel? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (SetProperty(ref _selectedBook, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public BookDetailsViewModel? SelectedBookDetails
    {
        get => _selectedBookDetails;
        private set
        {
            if (SetProperty(ref _selectedBookDetails, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string ActiveLibraryName
    {
        get => _activeLibraryName;
        private set => SetProperty(ref _activeLibraryName, value);
    }

    public string ActiveLibrarySourceStatus
    {
        get => _activeLibrarySourceStatus;
        private set => SetProperty(ref _activeLibrarySourceStatus, value);
    }

    public string ActiveLibrarySourceSummary
    {
        get => _activeLibrarySourceSummary;
        private set => SetProperty(ref _activeLibrarySourceSummary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ShowSearchPromptState));
                OnPropertyChanged(nameof(ShowNoResultsState));
                OnPropertyChanged(nameof(ShowFirstRunState));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanTriggerActions => !IsBusy;
    public bool CanSearch => !IsBusy && HasSelectedLibrary;
    public bool CanRefresh => !IsBusy && HasLibraries;
    public bool CanBrowseLibraries => !IsBusy && HasLibraries;
    public bool CanBrowseResults => !IsBusy && HasResults;
    public bool HasLibraries => Libraries.Count > 0;
    public bool HasSelectedLibrary => SelectedLibrary is not null;
    public bool HasResults => Results.Count > 0;
    public bool HasSelectedBook => SelectedBookDetails is not null;
    public bool ActiveLibraryHasSourceIssues => _activeLibraryHasSourceIssues;
    public bool ShowHealthySourceStatus => HasSelectedLibrary && !_activeLibraryHasSourceIssues;
    public bool ShowWarningSourceStatus => HasSelectedLibrary && _activeLibraryHasSourceIssues;
    public bool ShowDetailsPlaceholderState => !HasSelectedBook;
    public bool ShowFirstRunState => !HasLibraries && !IsBusy;
    public bool ShowSearchPromptState => HasLibraries && HasSelectedLibrary && !_hasPerformedSearch && !IsBusy;
    public bool ShowNoResultsState => HasLibraries && HasSelectedLibrary && _hasPerformedSearch && !HasResults && !IsBusy;
    public string ResultsSummary => HasResults
        ? $"{Results.Count} result{(Results.Count == 1 ? string.Empty : "s")}"
        : "Search results";
    public string ActiveLibraryCaption => HasSelectedLibrary ? "Active catalog" : "Choose a library";
    public string SidebarCaption => HasLibraries ? "Your libraries" : "Start with a source";

    public async Task InitializeAsync()
    {
        await LoadLibrariesAsync();
    }

    public async Task LoadLibrariesAsync(long? selectLibraryId = null)
    {
        await RunBusyAsync("Loading libraries...", async () =>
        {
            var profiles = await _libraryProfilesService.GetAllAsync();

            Libraries.Clear();
            foreach (var profile in profiles)
            {
                Libraries.Add(new LibraryProfileItemViewModel(profile));
            }

            var target = Libraries.FirstOrDefault(item => item.Id == selectLibraryId)
                         ?? Libraries.FirstOrDefault(item => item.Id == _activeLibraryContext.Current?.Id)
                         ?? Libraries.FirstOrDefault();

            if (target is null)
            {
                SelectedLibrary = null;
                ActiveLibraryName = "No library selected";
                ActiveLibrarySourceStatus = "Waiting";
                ActiveLibrarySourceSummary = "No source connected yet.";
                _activeLibraryHasSourceIssues = false;
                Results.Clear();
                SelectedBook = null;
                SelectedBookDetails = null;
                _loadedLibraryId = null;
                _loadedBookSourceId = null;
                _hasPerformedSearch = false;
                StatusMessage = "Add an offline folder or online catalog to begin building your shelf.";
                UpdateDerivedState();
                return;
            }

            SelectedLibrary = target;
            await ActivateCurrentLibraryAsync(autoSearch: !string.IsNullOrWhiteSpace(SearchQuery));
        });
    }

    public async Task OnSelectedLibraryChangedAsync()
    {
        if (SelectedLibrary is null)
        {
            return;
        }

        if (_isSwitchingLibrary || _loadedLibraryId == SelectedLibrary.Id)
        {
            return;
        }

        await ActivateCurrentLibraryAsync(autoSearch: !string.IsNullOrWhiteSpace(SearchQuery));
    }

    public async Task SearchAsync()
    {
        if (SelectedLibrary is null)
        {
            StatusMessage = "Choose a library before searching.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Results.Clear();
            SelectedBook = null;
            SelectedBookDetails = null;
            _hasPerformedSearch = false;
            StatusMessage = "Search by title, author, ISBN, or keyword.";
            UpdateDerivedState();
            return;
        }

        await RunBusyAsync($"Searching {SelectedLibrary.Name}...", async () =>
        {
            var results = await _libraryBooksService.SearchActiveLibraryAsync(SearchQuery.Trim());

            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(new BookListItemViewModel(result));
            }

            _hasPerformedSearch = true;
            StatusMessage = Results.Count == 0
                ? $"No titles matched '{SearchQuery.Trim()}'."
                : $"Found {Results.Count} title{(Results.Count == 1 ? string.Empty : "s")} in {SelectedLibrary.Name}.";

            SelectedBook = Results.FirstOrDefault();
            if (SelectedBook is not null)
            {
                await LoadBookDetailsAsync(SelectedBook);
            }
            else
            {
                SelectedBookDetails = null;
            }

            UpdateDerivedState();
        });
    }

    public async Task RefreshAsync()
    {
        var currentLibraryId = SelectedLibrary?.Id;
        await LoadLibrariesAsync(currentLibraryId);
    }

    public async Task OnSelectedBookChangedAsync()
    {
        if (SelectedBook is null)
        {
            SelectedBookDetails = null;
            _loadedBookSourceId = null;
            UpdateDerivedState();
            return;
        }

        if (_isLoadingBookDetails || string.Equals(_loadedBookSourceId, SelectedBook.Book.Source.SourceId, StringComparison.Ordinal))
        {
            return;
        }

        await LoadBookDetailsAsync(SelectedBook);
    }

    public async Task AddLibraryAsync(LibraryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await RunBusyAsync("Saving library profile...", async () =>
        {
            var newId = await _libraryRepository.AddAsync(profile);
            StatusMessage = $"Added {profile.Name}.";
            await LoadLibrariesAsync(newId);
        });
    }

    public async Task<BookLaunchRequest?> PreparePrimaryActionAsync()
    {
        var details = SelectedBookDetails?.Details;
        if (details is null)
        {
            return null;
        }

        if (details.ContentHandle is not null)
        {
            await RunBusyAsync("Opening book content...", async () =>
            {
                await using var sourceStream = await _libraryBooksService.OpenBookContentFromActiveLibraryAsync(details.ContentHandle.SourceId);
                var extension = details.Formats.FirstOrDefault();
                var fileName = $"myhomelib-{Guid.NewGuid():N}.{(string.IsNullOrWhiteSpace(extension) ? "bin" : extension)}";
                var path = Path.Combine(Path.GetTempPath(), fileName);

                await using var targetStream = File.Create(path);
                await sourceStream.CopyToAsync(targetStream);

                PendingLaunchRequest = new BookLaunchRequest
                {
                    FilePath = path
                };
            });

            return ConsumePendingLaunchRequest();
        }

        var link = details.ReadLink ?? details.DownloadLinks.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(link))
        {
            StatusMessage = "This title does not expose an open action yet.";
            return null;
        }

        StatusMessage = "Opening external reader...";
        return new BookLaunchRequest
        {
            Uri = link
        };
    }

    public string? GetPreferredLink()
        => SelectedBookDetails?.PreferredLink;

    public void ReportActionSuccess(string message)
    {
        ErrorMessage = null;
        StatusMessage = message;
    }

    public void ReportActionFailure(string message)
    {
        ErrorMessage = message;
        StatusMessage = "The last action did not complete.";
    }

    private BookLaunchRequest? PendingLaunchRequest { get; set; }

    private async Task ActivateCurrentLibraryAsync(bool autoSearch)
    {
        if (SelectedLibrary is null)
        {
            return;
        }

        _isSwitchingLibrary = true;

        try
        {
            await _activeLibraryContext.SetActiveAsync(SelectedLibrary.Profile);
            var sourceHealth = await BuildSourceHealthAsync(SelectedLibrary.Profile);

            ActiveLibraryName = SelectedLibrary.Name;
            ActiveLibrarySourceStatus = sourceHealth.Status;
            ActiveLibrarySourceSummary = sourceHealth.Summary;
            _activeLibraryHasSourceIssues = sourceHealth.HasIssues;
            ErrorMessage = null;

            Results.Clear();
            SelectedBook = null;
            SelectedBookDetails = null;
            _loadedLibraryId = SelectedLibrary.Id;
            _loadedBookSourceId = null;
            _hasPerformedSearch = false;
            StatusMessage = sourceHealth.HasIssues
                ? $"{SelectedLibrary.Name} needs source attention before every action will work reliably."
                : autoSearch
                    ? $"Ready to search {SelectedLibrary.Name}."
                    : $"Search {SelectedLibrary.Name} by title, author, ISBN, or keyword.";

            UpdateDerivedState();

            if (autoSearch)
            {
                await SearchAsync();
            }
        }
        finally
        {
            _isSwitchingLibrary = false;
        }
    }

    private async Task LoadBookDetailsAsync(BookListItemViewModel book)
    {
        _isLoadingBookDetails = true;

        try
        {
            await RunBusyAsync("Loading book details...", async () =>
            {
                var details = await _libraryBooksService.GetBookByIdFromActiveLibraryAsync(book.Book.Source.SourceId);
                SelectedBookDetails = new BookDetailsViewModel(details ?? BuildFallbackDetails(book.Book));
                _loadedBookSourceId = book.Book.Source.SourceId;
                StatusMessage = $"Inspecting {book.Title}.";
            }, preserveResults: true);
        }
        finally
        {
            _isLoadingBookDetails = false;
        }
    }

    private static BookDetails BuildFallbackDetails(BookSearchResult result)
    {
        return new BookDetails
        {
            Title = result.Title,
            Source = result.Source,
            Authors = result.Authors,
            Language = result.Language,
            Description = result.Description,
            Subjects = result.Subjects,
            Publisher = result.Publisher,
            PublishedYear = result.PublishedYear,
            Isbn10 = result.Isbn10,
            Isbn13 = result.Isbn13,
            CoverUrl = result.CoverUrl,
            Formats = result.Formats,
            DownloadLinks = result.DownloadLinks,
            ReadLink = result.ReadLink,
            BorrowLink = result.BorrowLink,
            ContentHandle = result.ContentHandle
        };
    }

    private async Task RunBusyAsync(
        string statusMessage,
        Func<Task> action,
        bool preserveResults = false)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = statusMessage;

            if (!preserveResults)
            {
                UpdateDerivedState();
            }

            await action();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Something needs attention before the workspace can continue.";
        }
        finally
        {
            IsBusy = false;
            UpdateDerivedState();
        }
    }

    private BookLaunchRequest? ConsumePendingLaunchRequest()
    {
        var request = PendingLaunchRequest;
        PendingLaunchRequest = null;
        return request;
    }

    private void OnCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDerivedState();
    }

    private void UpdateDerivedState()
    {
        OnPropertyChanged(nameof(CanTriggerActions));
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanBrowseLibraries));
        OnPropertyChanged(nameof(CanBrowseResults));
        OnPropertyChanged(nameof(HasLibraries));
        OnPropertyChanged(nameof(HasSelectedLibrary));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasSelectedBook));
        OnPropertyChanged(nameof(ActiveLibraryHasSourceIssues));
        OnPropertyChanged(nameof(ShowHealthySourceStatus));
        OnPropertyChanged(nameof(ShowWarningSourceStatus));
        OnPropertyChanged(nameof(ShowDetailsPlaceholderState));
        OnPropertyChanged(nameof(ShowFirstRunState));
        OnPropertyChanged(nameof(ShowSearchPromptState));
        OnPropertyChanged(nameof(ShowNoResultsState));
        OnPropertyChanged(nameof(ResultsSummary));
        OnPropertyChanged(nameof(ActiveLibraryCaption));
        OnPropertyChanged(nameof(SidebarCaption));
    }

    private async Task<SourceHealth> BuildSourceHealthAsync(LibraryProfile profile)
    {
        var structure = await _librarySourceResolver.ResolveAsync(profile);
        if (structure.Sources.Count == 0)
        {
            return new SourceHealth("Unavailable", "No source locations detected.", true);
        }

        return structure.LibraryType switch
        {
            LibraryType.Folder => BuildFolderSourceHealth(structure),
            LibraryType.Online => BuildOnlineSourceHealth(structure),
            _ => new SourceHealth("Unknown", "Source status is unavailable.", true)
        };
    }

    private static SourceHealth BuildFolderSourceHealth(LibraryStructure structure)
    {
        var indexSource = structure.Sources.FirstOrDefault(source => source.Kind == SourceKind.FileSystem);
        var archiveSources = structure.Sources.Where(source => source.Kind == SourceKind.Archive).ToArray();
        var availableArchives = archiveSources.Count(source => source.Exists);

        if (indexSource?.Exists != true)
        {
            return new SourceHealth("Check source", "The INPX index file is missing.", true);
        }

        if (archiveSources.Length == 0)
        {
            return new SourceHealth("Check source", "The archive source is not configured.", true);
        }

        if (archiveSources.Length == 1 && !archiveSources[0].Exists)
        {
            return new SourceHealth("Check source", "The archive directory is missing.", true);
        }

        if (availableArchives == 0)
        {
            return new SourceHealth("Limited", "The index is ready, but no archive files were detected.", true);
        }

        var summary = availableArchives == 1
            ? "Index ready with 1 archive file available."
            : $"Index ready with {availableArchives} archive files available.";

        return new SourceHealth("Ready", summary, false);
    }

    private static SourceHealth BuildOnlineSourceHealth(LibraryStructure structure)
    {
        var configuredEndpoints = structure.Sources.Count(source => source.Exists);
        var totalEndpoints = structure.Sources.Count;

        if (configuredEndpoints == 0)
        {
            return new SourceHealth("Check source", "The online source endpoints are invalid or missing.", true);
        }

        if (configuredEndpoints < totalEndpoints)
        {
            return new SourceHealth("Limited", $"{configuredEndpoints} of {totalEndpoints} online endpoints are configured.", true);
        }

        return new SourceHealth("Ready", $"{configuredEndpoints} online endpoint{(configuredEndpoints == 1 ? string.Empty : "s")} configured.", false);
    }

    private sealed record SourceHealth(string Status, string Summary, bool HasIssues);
}
