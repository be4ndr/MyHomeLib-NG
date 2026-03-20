namespace MyHomeLibNG.Tests;

internal static class FixtureReader
{
    public static string ReadText(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        return File.ReadAllText(fullPath);
    }
}
