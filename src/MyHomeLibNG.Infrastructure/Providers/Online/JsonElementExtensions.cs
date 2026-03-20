using System.Text.Json;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

internal static class JsonElementExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;
    }
}
