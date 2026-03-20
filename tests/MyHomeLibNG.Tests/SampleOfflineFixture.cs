using System.IO.Compression;
using System.Text;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Tests;

internal static class SampleOfflineFixture
{
    public const string RootPath = "/library";
    public const string InpxPath = "/library/catalog.inpx";
    public const string ZipPath = "/library/archives/sample-books.zip";
    public const string FilePath = "/library/files/unicode/naive-book.fb2";

    public static LibraryProfile CreateProfile()
    {
        return new LibraryProfile
        {
            Id = 700,
            Name = "Offline Fixture",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = InpxPath,
                ArchiveDirectoryPath = RootPath
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static InMemoryOfflineLibraryFileSystem CreateFileSystem()
    {
        var fileSystem = new InMemoryOfflineLibraryFileSystem();
        fileSystem.AddFile(InpxPath, BuildInpxArchive());
        fileSystem.AddFile(FilePath, Encoding.UTF8.GetBytes("plain file content"));
        fileSystem.AddFile(ZipPath, BuildZipArchive());
        return fileSystem;
    }

    private static byte[] BuildInpxArchive()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var structure = archive.CreateEntry("structure.info");
            using (var writer = new StreamWriter(structure.Open(), Encoding.UTF8, leaveOpen: false))
            {
                writer.Write("source_id,title,authors,language,description,subjects,publisher,published_year,isbn10,isbn13,container_path,archive_entry_path");
            }

            var data = archive.CreateEntry("books.inp");
            using var dataWriter = new StreamWriter(data.Open(), Encoding.UTF8, leaveOpen: false);
            dataWriter.WriteLine("offline-file-1\u0004Naive Book\u0004Ana Author\u0004en\u0004File description\u0004Fiction\u0004Test Press\u00042024\u00041234567890\u00049781234567897\u0004files/unicode/naive-book.fb2\u0004");
            dataWriter.WriteLine("offline-zip-1\u0004Archive Book\u0004Zip Author\u0004en\u0004Archive description\u0004Classics\u0004Archive Press\u00042001\u0004\u0004\u0004archives/sample-books.zip\u0004books/archive-book.fb2");
            dataWriter.WriteLine("offline-zip-1\u0004Archive Book Duplicate\u0004Zip Author\u0004en\u0004Archive description\u0004Classics\u0004Archive Press\u00042001\u0004\u0004\u0004archives/sample-books.zip\u0004books/archive-book.fb2");
            dataWriter.WriteLine("broken-row");
        }

        return memory.ToArray();
    }

    private static byte[] BuildZipArchive()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("books/archive-book.fb2");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write("zip archive content");
        }

        return memory.ToArray();
    }
}
