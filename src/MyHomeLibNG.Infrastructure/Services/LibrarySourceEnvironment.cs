namespace MyHomeLibNG.Infrastructure.Services;

public sealed class LibrarySourceEnvironment : ILibrarySourceEnvironment
{
    public bool IsValidAbsoluteUri(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);

    public bool FileExists(string path)
        => File.Exists(path);

    public long? GetFileSize(string path)
    {
        var fileInfo = new FileInfo(path);
        return fileInfo.Exists ? fileInfo.Length : null;
    }

    public bool DirectoryExists(string path)
        => Directory.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string directoryPath)
        => Directory.EnumerateFiles(directoryPath).ToArray();
}
