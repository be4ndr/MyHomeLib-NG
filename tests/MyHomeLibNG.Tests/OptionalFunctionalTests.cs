using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class OptionalFunctionalTests
{
    [Fact(Skip = "Optional integration fixture test; excluded from default runs.")]
    [Trait("Category", "Integration")]
    public void OfflineFixture_EndToEnd()
    {
    }

    [Fact(Skip = "Optional live smoke test; excluded from default runs.")]
    [Trait("Category", "LiveOnline")]
    public void OpenLibrary_LiveSmoke()
    {
    }
}
