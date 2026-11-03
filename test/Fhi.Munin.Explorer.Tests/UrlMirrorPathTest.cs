using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A page path that starts with // must not turn the share, variable and export links into links to another host
/// (Fhi.Metadata-ayqlp.6, R2-L2).
/// </summary>
public class UrlMirrorPathTest
{
    private static UrlMirror Mirror(string address) =>
        new(new Uri(address), new RefusingJsRuntime(new InvalidOperationException()), name => name == "kilde");

    [Theory]
    [InlineData("https://host.example//evil.example/side")]
    [InlineData("https://host.example///evil.example/side")]
    [InlineData("https://host.example/\\evil.example/side")]
    [InlineData("https://host.example/%5Cevil.example/side")]
    public void Address_WhenThePathStartsWithTwoSlashes_ThenTheLinkStaysOnThisHost(string address)
    {
        var link = Mirror(address).Address("kilde=a");

        Assert.StartsWith("/", link, StringComparison.Ordinal);
        Assert.False(link.StartsWith("//", StringComparison.Ordinal), link);
        Assert.False(link.StartsWith("/\\", StringComparison.Ordinal), link);
        Assert.Equal("host.example", new Uri(new Uri("https://host.example/"), link).Host);
    }

    [Fact]
    public void Address_WhenThePathStartsWithTwoSlashes_ThenOneSlashIsKeptBeforeTheRest()
    {
        var mirror = Mirror("https://host.example//evil.example/side");

        Assert.Equal("/evil.example/side", mirror.Path);
        Assert.Equal("/evil.example/side?kilde=a", mirror.Address("kilde=a"));
    }

    /// <summary>Why the fold trims slashes only: if Uri ever kept a backslash, the fold would need it too.</summary>
    [Theory]
    [InlineData("https://host.example/\\evil.example", "//evil.example")]
    [InlineData("https://host.example//\\evil.example", "///evil.example")]
    [InlineData("https://host.example/a\\b", "/a/b")]
    public void Uri_WhenThePathHasABackslash_ThenAbsolutePathHasASlashInstead(string address, string absolutePath)
    {
        Assert.Equal(absolutePath, new Uri(address).AbsolutePath);
    }

    [Fact]
    public void Address_WhenThePathIsOrdinary_ThenItIsUnchanged()
    {
        Assert.Equal("/sti/side?kilde=a", Mirror("https://host.example/sti/side").Address("kilde=a"));
        Assert.Equal("/", Mirror("https://host.example/").Address(""));
    }
}
