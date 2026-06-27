using System.Reflection;

namespace MyHomeLibNG.App.Tests;

public sealed class SearchTextNormalizerTests
{
    private static readonly Type NormalizerType = typeof(MyHomeLibNG.App.ViewModels.MainWindowViewModel)
        .Assembly
        .GetType("MyHomeLibNG.App.ViewModels.SearchTextNormalizer", throwOnError: true)!;

    [Theory]
    [InlineData(null, false, "")]
    [InlineData("", false, "")]
    [InlineData("   ", false, "")]
    [InlineData("  The Hobbit  ", false, "The Hobbit")]
    [InlineData("  The Hobbit  ", true, "Hobbit")]
    [InlineData("  Hello   World  ", false, "Hello   World")]
    [InlineData("  Текст  ", false, "Текст")]
    public void NormalizeForSearch_TrimsAndPreservesCurrentBehavior(string? value, bool ignoreLeadingArticles, string expected)
    {
        var actual = Invoke<string>("NormalizeForSearch", value, ignoreLeadingArticles);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("*", true, false, true)]
    [InlineData("#", false, true, true)]
    [InlineData("Ada", false, false, false)]
    public void TokenHelpers_DetectSpecialTokens(string? value, bool expectedMatchAll, bool expectedDigitBucket, bool expectedAnySpecial)
    {
        Assert.Equal(expectedMatchAll, Invoke<bool>("IsMatchAllToken", value));
        Assert.Equal(expectedDigitBucket, Invoke<bool>("IsDigitBucketToken", value));
        Assert.Equal(expectedAnySpecial, expectedMatchAll || expectedDigitBucket);
    }

    [Theory]
    [InlineData("The Hobbit", "hob", false, true, true)]
    [InlineData("The Hobbit", "hob", true, true, false)]
    [InlineData("1984", "#", false, false, true)]
    [InlineData("The 1984", "#", false, true, true)]
    [InlineData("Brave New World", "*", false, false, true)]
    public void MatchesValue_RespectsWildcardDigitBucketExactMatchAndArticles(
        string? candidate,
        string? filter,
        bool exactMatch,
        bool ignoreLeadingArticles,
        bool expected)
    {
        var actual = Invoke<bool>("MatchesValue", candidate, filter, exactMatch, ignoreLeadingArticles);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("42", false, "#")]
    [InlineData("The 42nd Street", true, "#")]
    [InlineData("Alpha", false, "A")]
    [InlineData("", false, "*")]
    public void GetAlphabetBucket_ReturnsExpectedBucket(string? value, bool ignoreLeadingArticles, string expected)
    {
        var actual = Invoke<string>("GetAlphabetBucket", value, ignoreLeadingArticles);

        Assert.Equal(expected, actual);
    }

    private static T Invoke<T>(string methodName, params object?[] args)
    {
        var method = NormalizerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                     ?? throw new MissingMethodException(NormalizerType.FullName, methodName);

        return (T)method.Invoke(null, args)!;
    }
}
