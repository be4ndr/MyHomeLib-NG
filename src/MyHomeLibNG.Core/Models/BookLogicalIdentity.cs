namespace MyHomeLibNG.Core.Models;

public sealed record BookLogicalIdentity(
    string NormalizedTitle,
    string NormalizedAuthorList,
    int? PublishedYear,
    string? Isbn10,
    string? Isbn13,
    string? Series);
