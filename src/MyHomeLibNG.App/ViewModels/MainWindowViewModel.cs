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
    private AppMode _currentMode = AppMode.Libraries;
    private string _searchAuthor = string.Empty;
    private string _searchTitle = string.Empty;
    private string _searchSeries = string.Empty;
    private string _searchGenre = string.Empty;
    private string _searchYear = string.Empty;
    private string _searchLanguage = string.Empty;
    private bool _searchOnlyLocal;
    private bool _searchExactMatch;
    private readonly List<BookSearchResult> _directoryCatalog = [];
    private bool _hasLoadedDirectoryCatalog;
    private long? _directoryCatalogLibraryId;
    private DirectoryBrowseMode _currentDirectoryBrowseMode = DirectoryBrowseMode.Authors;
    private DirectoryEntryViewModel? _selectedDirectoryEntry;
    private string _directoryFilterText = string.Empty;
    private string _selectedDirectoryAlphabet = "*";

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
        DirectoryEntries.CollectionChanged += OnCollectionsChanged;
        DirectoryBooks.CollectionChanged += OnCollectionsChanged;
        DirectoryAlphabetOptions.CollectionChanged += OnCollectionsChanged;
    }

    public ObservableCollection<LibraryProfileItemViewModel> Libraries { get; } = [];
    public ObservableCollection<BookListItemViewModel> Results { get; } = [];
    public ObservableCollection<DirectoryEntryViewModel> DirectoryEntries { get; } = [];
    public ObservableCollection<BookListItemViewModel> DirectoryBooks { get; } = [];
    public ObservableCollection<string> DirectoryAlphabetOptions { get; } = [];

    public AppMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (SetProperty(ref _currentMode, value))
            {
                UpdateDerivedState();
            }
        }
    }

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
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchAuthor
    {
        get => _searchAuthor;
        set
        {
            if (SetProperty(ref _searchAuthor, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchTitle
    {
        get => _searchTitle;
        set
        {
            if (SetProperty(ref _searchTitle, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchSeries
    {
        get => _searchSeries;
        set
        {
            if (SetProperty(ref _searchSeries, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchGenre
    {
        get => _searchGenre;
        set
        {
            if (SetProperty(ref _searchGenre, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchYear
    {
        get => _searchYear;
        set
        {
            if (SetProperty(ref _searchYear, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public string SearchLanguage
    {
        get => _searchLanguage;
        set
        {
            if (SetProperty(ref _searchLanguage, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public bool SearchOnlyLocal
    {
        get => _searchOnlyLocal;
        set
        {
            if (SetProperty(ref _searchOnlyLocal, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public bool SearchExactMatch
    {
        get => _searchExactMatch;
        set
        {
            if (SetProperty(ref _searchExactMatch, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public DirectoryBrowseMode CurrentDirectoryBrowseMode
    {
        get => _currentDirectoryBrowseMode;
        private set
        {
            if (SetProperty(ref _currentDirectoryBrowseMode, value))
            {
                RebuildDirectoryBrowserState(selectFirstEntry: true);
                UpdateDerivedState();
            }
        }
    }

    public DirectoryEntryViewModel? SelectedDirectoryEntry
    {
        get => _selectedDirectoryEntry;
        set
        {
            if (SetProperty(ref _selectedDirectoryEntry, value))
            {
                ApplyDirectorySelection();
                UpdateDerivedState();
            }
        }
    }

    public string DirectoryFilterText
    {
        get => _directoryFilterText;
        set
        {
            if (SetProperty(ref _directoryFilterText, value))
            {
                RebuildDirectoryBrowserState(selectFirstEntry: true);
                UpdateDerivedState();
            }
        }
    }

    public string SelectedDirectoryAlphabet
    {
        get => _selectedDirectoryAlphabet;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "*" : value.Trim().ToUpperInvariant();
            if (SetProperty(ref _selectedDirectoryAlphabet, normalized))
            {
                RebuildDirectoryBrowserState(selectFirstEntry: true);
                UpdateDerivedState();
            }
        }
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
    public bool HasSelectedOfflineLibrary => SelectedLibrary?.Profile.LibraryType == LibraryType.Folder;
    public bool HasResults => Results.Count > 0;
    public bool HasSelectedBook => SelectedBookDetails is not null;
    public bool CanScanSelectedLibrary => !IsBusy && HasSelectedOfflineLibrary;
    public bool IsLibrariesMode => CurrentMode == AppMode.Libraries;
    public bool IsSearchMode => CurrentMode == AppMode.Search;
    public bool IsDirectoryMode => CurrentMode == AppMode.Directory;
    public bool IsDirectoryAuthorsMode => CurrentDirectoryBrowseMode == DirectoryBrowseMode.Authors;
    public bool IsDirectoryTitlesMode => CurrentDirectoryBrowseMode == DirectoryBrowseMode.Titles;
    public bool IsDirectorySeriesMode => CurrentDirectoryBrowseMode == DirectoryBrowseMode.Series;
    public bool IsDirectoryGenresMode => CurrentDirectoryBrowseMode == DirectoryBrowseMode.Genres;
    public bool ActiveLibraryHasSourceIssues => _activeLibraryHasSourceIssues;
    public bool ShowHealthySourceStatus => HasSelectedLibrary && !_activeLibraryHasSourceIssues;
    public bool ShowWarningSourceStatus => HasSelectedLibrary && _activeLibraryHasSourceIssues;
    public bool ShowDetailsPlaceholderState => !HasSelectedBook;
    public bool ShowFirstRunState => !HasLibraries && !IsBusy;
    public bool ShowSearchPromptState => HasLibraries && HasSelectedLibrary && !_hasPerformedSearch && !IsBusy;
    public bool ShowNoResultsState => HasLibraries && HasSelectedLibrary && _hasPerformedSearch && !HasResults && !IsBusy;
    public bool HasDirectoryEntries => DirectoryEntries.Count > 0;
    public bool HasDirectoryBooks => DirectoryBooks.Count > 0;
    public bool HasDirectorySelection => SelectedDirectoryEntry is not null;
    public bool ShowDirectoryEntryEmptyState => HasSelectedLibrary && IsDirectoryMode && _hasLoadedDirectoryCatalog && !HasDirectoryEntries && !IsBusy;
    public bool ShowDirectoryBooksEmptyState => HasDirectorySelection && !HasDirectoryBooks && !IsBusy;
    public bool ShowDirectoryPromptState => HasSelectedLibrary && IsDirectoryMode && !_hasLoadedDirectoryCatalog && !IsBusy;
    public bool CanBrowseDirectoryEntries => !IsBusy && HasDirectoryEntries;
    public bool CanBrowseDirectoryBooks => !IsBusy && HasDirectoryBooks;
    public bool HasStructuredSearchFilters =>
        !string.IsNullOrWhiteSpace(SearchAuthor) ||
        !string.IsNullOrWhiteSpace(SearchTitle) ||
        !string.IsNullOrWhiteSpace(SearchSeries) ||
        !string.IsNullOrWhiteSpace(SearchGenre) ||
        !string.IsNullOrWhiteSpace(SearchYear) ||
        !string.IsNullOrWhiteSpace(SearchLanguage);
    public bool HasAdvancedSearchToggles => SearchOnlyLocal || SearchExactMatch;
    public string SearchModeSummary => BuildSearchModeSummary();
    public string DirectoryFilterWatermark => CurrentDirectoryBrowseMode switch
    {
        DirectoryBrowseMode.Authors => "Filter authors",
        DirectoryBrowseMode.Titles => "Filter titles",
        DirectoryBrowseMode.Series => "Filter series",
        DirectoryBrowseMode.Genres => "Filter genres",
        _ => "Filter directory"
    };
    public string DirectoryModeSummary => BuildDirectoryModeSummary();
    public string DirectoryBooksSummary => HasDirectoryBooks
        ? $"{DirectoryBooks.Count} matching book{(DirectoryBooks.Count == 1 ? string.Empty : "s")}"
        : "No books in this selection";
    public string LibraryCountSummary => HasLibraries
        ? $"{Libraries.Count} librar{(Libraries.Count == 1 ? "y" : "ies")} available"
        : "No libraries added yet";
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
                await _activeLibraryContext.ClearAsync();
                ClearActiveLibraryState();
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
        SetMode(AppMode.Search);

        if (SelectedLibrary is null)
        {
            StatusMessage = "Choose a library before searching.";
            return;
        }

        if (!TryBuildSearchRequest(out var request))
        {
            Results.Clear();
            SelectedBook = null;
            SelectedBookDetails = null;
            _hasPerformedSearch = false;
            StatusMessage = "Enter a title, author, genre, language, year, or use * to search broadly.";
            UpdateDerivedState();
            return;
        }

        await RunBusyAsync($"Searching {SelectedLibrary.Name}...", async () =>
        {
            var results = await _libraryBooksService.SearchActiveLibraryAsync(request.BackendQuery);
            var filteredResults = results
                .Where(result => MatchesStructuredSearch(result, request))
                .ToArray();

            Results.Clear();
            foreach (var result in filteredResults)
            {
                Results.Add(new BookListItemViewModel(result, _activeLibraryHasSourceIssues));
            }

            _hasPerformedSearch = true;
            StatusMessage = Results.Count == 0
                ? BuildNoResultsMessage(request)
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

    public async Task DeleteLibraryAsync(LibraryProfileItemViewModel library)
    {
        ArgumentNullException.ThrowIfNull(library);

        await RunBusyAsync($"Deleting {library.Name}...", async () =>
        {
            var wasSelected = SelectedLibrary?.Id == library.Id;
            var wasActive = _loadedLibraryId == library.Id || _activeLibraryContext.Current?.Id == library.Id;

            if (wasSelected)
            {
                SelectedLibrary = null;
            }

            await _libraryProfilesService.RemoveAsync(library.Id);

            var existingItem = Libraries.FirstOrDefault(item => item.Id == library.Id);
            if (existingItem is not null)
            {
                Libraries.Remove(existingItem);
            }

            if (wasSelected || wasActive)
            {
                await _activeLibraryContext.ClearAsync();
                ClearActiveLibraryState();
                StatusMessage = Libraries.Count == 0
                    ? $"Deleted {library.Name}. Add an offline folder or online catalog to begin building your shelf."
                    : $"Deleted {library.Name}. Choose another library to continue.";
            }
            else
            {
                StatusMessage = $"Deleted {library.Name}.";
            }

            UpdateDerivedState();
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

    public void SetMode(AppMode mode)
    {
        CurrentMode = mode;
    }

    public async Task OpenDirectoryModeAsync()
    {
        SetMode(AppMode.Directory);

        if (!HasSelectedLibrary)
        {
            StatusMessage = "Choose a library before browsing its directory.";
            ResetDirectoryBrowser(clearCache: true);
            return;
        }

        if (_hasLoadedDirectoryCatalog && _directoryCatalogLibraryId == SelectedLibrary!.Id)
        {
            RebuildDirectoryBrowserState(selectFirstEntry: !HasDirectorySelection);
            return;
        }

        await LoadDirectoryCatalogAsync();
    }

    public void SetDirectoryBrowseMode(DirectoryBrowseMode mode)
    {
        CurrentDirectoryBrowseMode = mode;
    }

    public void ResetStructuredSearch()
    {
        SearchAuthor = string.Empty;
        SearchTitle = string.Empty;
        SearchSeries = string.Empty;
        SearchGenre = string.Empty;
        SearchYear = string.Empty;
        SearchLanguage = string.Empty;
        SearchOnlyLocal = false;
        SearchExactMatch = false;
    }

    public void ResetDirectoryFilters()
    {
        DirectoryFilterText = string.Empty;
        SelectedDirectoryAlphabet = "*";
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

            ResetLibraryWorkspace();
            _loadedLibraryId = SelectedLibrary.Id;
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
            else if (IsDirectoryMode)
            {
                await OpenDirectoryModeAsync();
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
            Series = result.Series,
            Language = result.Language,
            Description = result.Description,
            Subjects = result.Subjects,
            Publisher = result.Publisher,
            PublishedYear = result.PublishedYear,
            Isbn10 = result.Isbn10,
            Isbn13 = result.Isbn13,
            CoverUrl = result.CoverUrl,
            CoverThumbnail = result.CoverThumbnail,
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

    private void ClearActiveLibraryState()
    {
        ActiveLibraryName = "No library selected";
        ActiveLibrarySourceStatus = "Waiting";
        ActiveLibrarySourceSummary = "No source connected yet.";
        _activeLibraryHasSourceIssues = false;
        ErrorMessage = null;
        _loadedLibraryId = null;
        ResetLibraryWorkspace();
    }

    private void ResetLibraryWorkspace()
    {
        Results.Clear();
        SelectedBook = null;
        SelectedBookDetails = null;
        _loadedBookSourceId = null;
        _hasPerformedSearch = false;
        ResetDirectoryBrowser(clearCache: true);
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
        OnPropertyChanged(nameof(HasSelectedOfflineLibrary));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasSelectedBook));
        OnPropertyChanged(nameof(CanScanSelectedLibrary));
        OnPropertyChanged(nameof(IsLibrariesMode));
        OnPropertyChanged(nameof(IsSearchMode));
        OnPropertyChanged(nameof(IsDirectoryMode));
        OnPropertyChanged(nameof(IsDirectoryAuthorsMode));
        OnPropertyChanged(nameof(IsDirectoryTitlesMode));
        OnPropertyChanged(nameof(IsDirectorySeriesMode));
        OnPropertyChanged(nameof(IsDirectoryGenresMode));
        OnPropertyChanged(nameof(ActiveLibraryHasSourceIssues));
        OnPropertyChanged(nameof(ShowHealthySourceStatus));
        OnPropertyChanged(nameof(ShowWarningSourceStatus));
        OnPropertyChanged(nameof(ShowDetailsPlaceholderState));
        OnPropertyChanged(nameof(ShowFirstRunState));
        OnPropertyChanged(nameof(ShowSearchPromptState));
        OnPropertyChanged(nameof(ShowNoResultsState));
        OnPropertyChanged(nameof(HasDirectoryEntries));
        OnPropertyChanged(nameof(HasDirectoryBooks));
        OnPropertyChanged(nameof(HasDirectorySelection));
        OnPropertyChanged(nameof(ShowDirectoryEntryEmptyState));
        OnPropertyChanged(nameof(ShowDirectoryBooksEmptyState));
        OnPropertyChanged(nameof(ShowDirectoryPromptState));
        OnPropertyChanged(nameof(CanBrowseDirectoryEntries));
        OnPropertyChanged(nameof(CanBrowseDirectoryBooks));
        OnPropertyChanged(nameof(HasStructuredSearchFilters));
        OnPropertyChanged(nameof(HasAdvancedSearchToggles));
        OnPropertyChanged(nameof(SearchModeSummary));
        OnPropertyChanged(nameof(DirectoryFilterWatermark));
        OnPropertyChanged(nameof(DirectoryModeSummary));
        OnPropertyChanged(nameof(DirectoryBooksSummary));
        OnPropertyChanged(nameof(LibraryCountSummary));
        OnPropertyChanged(nameof(ResultsSummary));
        OnPropertyChanged(nameof(ActiveLibraryCaption));
        OnPropertyChanged(nameof(SidebarCaption));
    }

    private bool TryBuildSearchRequest(out SearchRequest request)
    {
        var hasWildcardRequest =
            SearchTextNormalizer.IsMatchAllToken(SearchQuery) ||
            SearchTextNormalizer.IsMatchAllToken(SearchAuthor) ||
            SearchTextNormalizer.IsMatchAllToken(SearchTitle) ||
            SearchTextNormalizer.IsMatchAllToken(SearchSeries) ||
            SearchTextNormalizer.IsMatchAllToken(SearchGenre) ||
            SearchTextNormalizer.IsMatchAllToken(SearchYear) ||
            SearchTextNormalizer.IsMatchAllToken(SearchLanguage) ||
            SearchTextNormalizer.IsDigitBucketToken(SearchQuery) ||
            SearchTextNormalizer.IsDigitBucketToken(SearchAuthor) ||
            SearchTextNormalizer.IsDigitBucketToken(SearchTitle) ||
            SearchTextNormalizer.IsDigitBucketToken(SearchSeries);

        var tokens = new[]
        {
            SearchQuery,
            SearchTitle,
            SearchAuthor,
            SearchGenre,
            SearchSeries,
            SearchYear,
            SearchLanguage
        }
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => !SearchTextNormalizer.IsMatchAllToken(value))
            .Where(value => !SearchTextNormalizer.IsDigitBucketToken(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0 && !hasWildcardRequest)
        {
            request = default;
            return false;
        }

        request = new SearchRequest(
            tokens.Length == 0 ? string.Empty : string.Join(" ", tokens),
            SearchQuery,
            SearchAuthor,
            SearchTitle,
            SearchSeries,
            SearchGenre,
            SearchYear,
            SearchLanguage,
            SearchOnlyLocal,
            SearchExactMatch);
        return true;
    }

    private bool MatchesStructuredSearch(BookSearchResult result, SearchRequest request)
    {
        if (!MatchesKeyword(result, request.Query, request.ExactMatch))
        {
            return false;
        }

        if (!SearchTextNormalizer.MatchesValue(result.Title, request.Title, request.ExactMatch, ignoreLeadingArticles: true))
        {
            return false;
        }

        if (!MatchesAny(result.Authors, request.Author, request.ExactMatch))
        {
            return false;
        }

        if (!MatchesSeries(result, request.Series, request.ExactMatch))
        {
            return false;
        }

        if (!MatchesAny(result.Subjects, request.Genre, request.ExactMatch))
        {
            return false;
        }

        if (!MatchesYear(result.PublishedYear, request.Year))
        {
            return false;
        }

        if (!SearchTextNormalizer.MatchesValue(result.Language, request.Language, request.ExactMatch))
        {
            return false;
        }

        if (request.OnlyLocal && result.ContentHandle is null)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesAny(IReadOnlyList<string> values, string? filter, bool exactMatch)
    {
        if (string.IsNullOrWhiteSpace(filter) || SearchTextNormalizer.IsMatchAllToken(filter))
        {
            return true;
        }

        if (SearchTextNormalizer.IsDigitBucketToken(filter))
        {
            return values.Any(value => SearchTextNormalizer.StartsWithDigit(value));
        }

        if (values.Count == 0)
        {
            return false;
        }

        return values.Any(value => SearchTextNormalizer.MatchesValue(value, filter, exactMatch));
    }

    private static bool MatchesSeries(BookSearchResult result, string? filter, bool exactMatch)
    {
        if (string.IsNullOrWhiteSpace(filter) || SearchTextNormalizer.IsMatchAllToken(filter))
        {
            return true;
        }

        if (SearchTextNormalizer.IsDigitBucketToken(filter))
        {
            return SearchTextNormalizer.StartsWithDigit(result.Series, ignoreLeadingArticles: true);
        }

        return SearchTextNormalizer.MatchesValue(result.Series, filter, exactMatch, ignoreLeadingArticles: true);
    }

    private static bool MatchesKeyword(BookSearchResult result, string? filter, bool exactMatch)
    {
        if (string.IsNullOrWhiteSpace(filter) || SearchTextNormalizer.IsMatchAllToken(filter))
        {
            return true;
        }

        if (SearchTextNormalizer.IsDigitBucketToken(filter))
        {
            return SearchTextNormalizer.StartsWithDigit(result.Title, ignoreLeadingArticles: true) ||
                   result.Authors.Any(author => SearchTextNormalizer.StartsWithDigit(author)) ||
                   SearchTextNormalizer.StartsWithDigit(result.Series, ignoreLeadingArticles: true);
        }

        return SearchTextNormalizer.MatchesValue(result.Title, filter, exactMatch, ignoreLeadingArticles: true) ||
               MatchesAny(result.Authors, filter, exactMatch) ||
               SearchTextNormalizer.MatchesValue(result.Series, filter, exactMatch, ignoreLeadingArticles: true) ||
               MatchesAny(result.Subjects, filter, exactMatch) ||
               SearchTextNormalizer.MatchesValue(result.Description, filter, exactMatch);
    }

    private static bool MatchesYear(int? publishedYear, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || SearchTextNormalizer.IsMatchAllToken(filter))
        {
            return true;
        }

        if (!publishedYear.HasValue)
        {
            return false;
        }

        if (SearchTextNormalizer.IsDigitBucketToken(filter))
        {
            return publishedYear.Value >= 0;
        }

        return publishedYear.Value.ToString().Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private string BuildSearchModeSummary()
    {
        var parts = new List<string>();

        AddSummaryPart(parts, "Keyword", SearchQuery);
        AddSummaryPart(parts, "Author", SearchAuthor);
        AddSummaryPart(parts, "Title", SearchTitle);
        AddSummaryPart(parts, "Series", SearchSeries);
        AddSummaryPart(parts, "Genre", SearchGenre);
        AddSummaryPart(parts, "Year", SearchYear);
        AddSummaryPart(parts, "Language", SearchLanguage);

        if (SearchOnlyLocal)
        {
            parts.Add("Only local");
        }

        if (SearchExactMatch)
        {
            parts.Add("Exact match");
        }

        return parts.Count == 0
            ? "Use title, author, series, genre, year, language, * for all, or # for titles/authors starting with a digit."
            : string.Join("  |  ", parts);
    }

    private static void AddSummaryPart(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label}: {value.Trim()}");
        }
    }

    private static string BuildNoResultsMessage(SearchRequest request)
    {
        var target = string.IsNullOrWhiteSpace(request.BackendQuery) ? "that search" : $"'{request.BackendQuery}'";
        return $"No titles matched {target}.";
    }

    private async Task LoadDirectoryCatalogAsync()
    {
        if (SelectedLibrary is null)
        {
            return;
        }

        await RunBusyAsync($"Loading {SelectedLibrary.Name} directory...", async () =>
        {
            var catalog = await _libraryBooksService.SearchActiveLibraryAsync(string.Empty);
            _directoryCatalog.Clear();
            _directoryCatalog.AddRange(catalog);
            _directoryCatalogLibraryId = SelectedLibrary.Id;
            _hasLoadedDirectoryCatalog = true;

            RebuildDirectoryBrowserState(selectFirstEntry: true);
            StatusMessage = _directoryCatalog.Count == 0
                ? $"No directory entries were returned for {SelectedLibrary.Name}."
                : $"Loaded {_directoryCatalog.Count} browseable book{(_directoryCatalog.Count == 1 ? string.Empty : "s")} for {SelectedLibrary.Name}.";
        }, preserveResults: true);
    }

    private void ResetDirectoryBrowser(bool clearCache)
    {
        if (clearCache)
        {
            _directoryCatalog.Clear();
            _directoryCatalogLibraryId = null;
            _hasLoadedDirectoryCatalog = false;
        }

        DirectoryEntries.Clear();
        DirectoryBooks.Clear();
        DirectoryAlphabetOptions.Clear();
        if (_selectedDirectoryEntry is not null)
        {
            _selectedDirectoryEntry = null;
            OnPropertyChanged(nameof(SelectedDirectoryEntry));
        }

        if (!string.Equals(_selectedDirectoryAlphabet, "*", StringComparison.Ordinal))
        {
            _selectedDirectoryAlphabet = "*";
            OnPropertyChanged(nameof(SelectedDirectoryAlphabet));
        }

        if (!string.IsNullOrEmpty(_directoryFilterText))
        {
            _directoryFilterText = string.Empty;
            OnPropertyChanged(nameof(DirectoryFilterText));
        }
    }

    private void RebuildDirectoryBrowserState(bool selectFirstEntry)
    {
        if (!_hasLoadedDirectoryCatalog)
        {
            DirectoryEntries.Clear();
            DirectoryBooks.Clear();
            DirectoryAlphabetOptions.Clear();
            SelectedDirectoryEntry = null;
            return;
        }

        var allEntries = BuildDirectoryEntries().ToArray();
        var alphabetOptions = BuildDirectoryAlphabetOptions(allEntries);
        SyncCollection(DirectoryAlphabetOptions, alphabetOptions);

        if (!alphabetOptions.Contains(SelectedDirectoryAlphabet, StringComparer.OrdinalIgnoreCase))
        {
            _selectedDirectoryAlphabet = "*";
            OnPropertyChanged(nameof(SelectedDirectoryAlphabet));
        }

        var entries = allEntries
            .Where(MatchesSelectedAlphabet)
            .Where(MatchesDirectoryFilter)
            .ToArray();
        SyncCollection(DirectoryEntries, entries);

        if (entries.Length == 0)
        {
            SelectedDirectoryEntry = null;
            return;
        }

        if (!selectFirstEntry &&
            SelectedDirectoryEntry is not null &&
            entries.Any(entry => entry.NormalizedValue == SelectedDirectoryEntry.NormalizedValue &&
                                 entry.BrowseMode == SelectedDirectoryEntry.BrowseMode))
        {
            ApplyDirectorySelection();
            return;
        }

        SelectedDirectoryEntry = entries[0];
    }

    private IEnumerable<DirectoryEntryViewModel> BuildDirectoryEntries()
    {
        return CurrentDirectoryBrowseMode switch
        {
            DirectoryBrowseMode.Authors => BuildGroupedDirectoryEntries(
                _directoryCatalog.SelectMany(book => GetAuthorValues(book).Select(value => (Book: book, Value: value))),
                ignoreLeadingArticles: false),
            DirectoryBrowseMode.Titles => BuildGroupedDirectoryEntries(
                _directoryCatalog.Select(book => (Book: book, Value: book.Title)),
                ignoreLeadingArticles: true),
            DirectoryBrowseMode.Series => BuildGroupedDirectoryEntries(
                _directoryCatalog.SelectMany(book => GetSeriesValues(book).Select(value => (Book: book, Value: value))),
                ignoreLeadingArticles: true),
            DirectoryBrowseMode.Genres => BuildGroupedDirectoryEntries(
                _directoryCatalog.SelectMany(book => GetGenreValues(book).Select(value => (Book: book, Value: value))),
                ignoreLeadingArticles: false),
            _ => Array.Empty<DirectoryEntryViewModel>()
        };
    }

    private IEnumerable<DirectoryEntryViewModel> BuildGroupedDirectoryEntries(
        IEnumerable<(BookSearchResult Book, string Value)> values,
        bool ignoreLeadingArticles)
    {
        return values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(
                item => SearchTextNormalizer.NormalizeForSearch(item.Value, ignoreLeadingArticles),
                StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group =>
            {
                var displayName = group
                    .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.Value.Trim())
                    .First();
                var count = group
                    .Select(item => item.Book.Source.SourceId)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                return new DirectoryEntryViewModel(
                    CurrentDirectoryBrowseMode,
                    displayName,
                    group.Key,
                    SearchTextNormalizer.GetAlphabetBucket(displayName, ignoreLeadingArticles),
                    count);
            })
            .OrderBy(entry => entry.AlphabetKey == "#" ? "0" : entry.AlphabetKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => SearchTextNormalizer.NormalizeForSearch(entry.DisplayName, ignoreLeadingArticles), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildDirectoryAlphabetOptions(IEnumerable<DirectoryEntryViewModel> entries)
    {
        var buckets = entries
            .Select(entry => entry.AlphabetKey)
            .Where(bucket => bucket != "*")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(bucket => bucket == "#" ? "0" : bucket, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var options = new List<string> { "*" };
        options.AddRange(buckets);
        return options;
    }

    private bool MatchesSelectedAlphabet(DirectoryEntryViewModel entry)
    {
        if (string.Equals(SelectedDirectoryAlphabet, "*", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(entry.AlphabetKey, SelectedDirectoryAlphabet, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesDirectoryFilter(DirectoryEntryViewModel entry)
    {
        if (string.IsNullOrWhiteSpace(DirectoryFilterText))
        {
            return true;
        }

        var ignoreLeadingArticles = entry.BrowseMode is DirectoryBrowseMode.Titles or DirectoryBrowseMode.Series;
        return SearchTextNormalizer.MatchesValue(entry.DisplayName, DirectoryFilterText, exactMatch: false, ignoreLeadingArticles);
    }

    private void ApplyDirectorySelection()
    {
        DirectoryBooks.Clear();

        if (SelectedDirectoryEntry is null)
        {
            SelectedBook = null;
            SelectedBookDetails = null;
            return;
        }

        var matchingBooks = _directoryCatalog
            .Where(book => MatchesDirectoryEntry(book, SelectedDirectoryEntry))
            .OrderBy(book => SearchTextNormalizer.NormalizeForSearch(book.Title, ignoreLeadingArticles: true), StringComparer.OrdinalIgnoreCase)
            .ThenBy(book => string.Join(", ", book.Authors), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var book in matchingBooks)
        {
            DirectoryBooks.Add(new BookListItemViewModel(book, _activeLibraryHasSourceIssues));
        }

        SelectedBook = DirectoryBooks.FirstOrDefault();
        if (SelectedBook is not null)
        {
            _ = OnSelectedBookChangedAsync();
        }
        else
        {
            SelectedBookDetails = null;
        }
    }

    private static bool MatchesDirectoryEntry(BookSearchResult book, DirectoryEntryViewModel entry)
    {
        return entry.BrowseMode switch
        {
            DirectoryBrowseMode.Authors => GetAuthorValues(book).Any(value =>
                string.Equals(SearchTextNormalizer.NormalizeForSearch(value), entry.NormalizedValue, StringComparison.OrdinalIgnoreCase)),
            DirectoryBrowseMode.Titles => string.Equals(
                SearchTextNormalizer.NormalizeForSearch(book.Title, ignoreLeadingArticles: true),
                entry.NormalizedValue,
                StringComparison.OrdinalIgnoreCase),
            DirectoryBrowseMode.Series => GetSeriesValues(book).Any(value =>
                string.Equals(SearchTextNormalizer.NormalizeForSearch(value, ignoreLeadingArticles: true), entry.NormalizedValue, StringComparison.OrdinalIgnoreCase)),
            DirectoryBrowseMode.Genres => GetGenreValues(book).Any(value =>
                string.Equals(SearchTextNormalizer.NormalizeForSearch(value), entry.NormalizedValue, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private string BuildDirectoryModeSummary()
    {
        if (!_hasLoadedDirectoryCatalog)
        {
            return "Directory mode loads a browseable snapshot of the active library.";
        }

        if (SelectedDirectoryEntry is null)
        {
            return "Choose an author, title, series, or genre to browse matching books.";
        }

        return $"{CurrentDirectoryBrowseMode}: {SelectedDirectoryEntry.DisplayName}  |  {SelectedDirectoryEntry.CountLabel}";
    }

    private static IReadOnlyList<string> GetAuthorValues(BookSearchResult book)
    {
        return book.Authors.Count == 0 ? ["Unknown author"] : book.Authors;
    }

    private static IReadOnlyList<string> GetGenreValues(BookSearchResult book)
    {
        return book.Subjects.Count == 0 ? ["Unfiled"] : book.Subjects;
    }

    private static IReadOnlyList<string> GetSeriesValues(BookSearchResult book)
    {
        return string.IsNullOrWhiteSpace(book.Series) ? ["Standalone / uncategorized"] : [book.Series!];
    }

    private static void SyncCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
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
    private readonly record struct SearchRequest(
        string BackendQuery,
        string? Query,
        string? Author,
        string? Title,
        string? Series,
        string? Genre,
        string? Year,
        string? Language,
        bool OnlyLocal,
        bool ExactMatch);
}
