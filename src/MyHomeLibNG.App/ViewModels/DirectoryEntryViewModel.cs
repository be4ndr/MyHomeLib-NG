namespace MyHomeLibNG.App.ViewModels;

public sealed class DirectoryEntryViewModel
{
    public DirectoryEntryViewModel(
        DirectoryBrowseMode browseMode,
        string displayName,
        string normalizedValue,
        string alphabetKey,
        int count)
    {
        BrowseMode = browseMode;
        DisplayName = displayName;
        NormalizedValue = normalizedValue;
        AlphabetKey = alphabetKey;
        Count = count;
    }

    public DirectoryBrowseMode BrowseMode { get; }
    public string DisplayName { get; }
    public string NormalizedValue { get; }
    public string AlphabetKey { get; }
    public int Count { get; }
    public string CountLabel => $"{Count} book{(Count == 1 ? string.Empty : "s")}";
}
