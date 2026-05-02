namespace MyHomeLibNG.Core.Models;

public sealed class BookImportBatchResult
{
    public int BooksAdded { get; init; }
    public int BooksUpdated { get; init; }
    public int BooksSkipped { get; init; }
}
