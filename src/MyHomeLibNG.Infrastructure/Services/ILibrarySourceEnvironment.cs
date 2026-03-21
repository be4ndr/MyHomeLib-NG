namespace MyHomeLibNG.Infrastructure.Services;

public interface ILibrarySourceEnvironment
{
    bool IsValidAbsoluteUri(string value);
    bool FileExists(string path);
    long? GetFileSize(string path);
    bool DirectoryExists(string path);
    IReadOnlyList<string> EnumerateFiles(string directoryPath);
}
