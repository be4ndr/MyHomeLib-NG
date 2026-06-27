namespace MyHomeLibNG.Tests;

internal static class FixtureReader
{
    public static string ReadText(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(AppContext.BaseDirectory, normalizedPath);
        return File.ReadAllText(fullPath);
    }
}
