using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Back or Forward lands the browser on an address the component then writes again, late: a reader
/// already gone Forward had the entry they reached overwritten with the one they left (Fhi.Metadata-l9l2n.124).
/// </summary>
public class UrlMirrorArrivedTest
{
    private static UrlMirror Mirror(string address) =>
        new(new Uri(address), new RefusingJsRuntime(new InvalidOperationException("written")), name => name == "kilde");

    [Fact]
    public async Task MirrorAsync_WhenTheArrivedAddressIsWrittenAgain_ThenNothingReachesTheBrowser()
    {
        var mirror = Mirror("https://host.example/kilder?ref=1&kilde=a");

        mirror.Arrived();

        await mirror.MirrorAsync("kilde=a");
    }

    [Fact]
    public async Task MirrorAsync_WhenTheStateMovesOnAfterArriving_ThenItIsWritten()
    {
        var mirror = Mirror("https://host.example/kilder?ref=1&kilde=a");

        mirror.Arrived();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mirror.MirrorAsync("kilde=b").AsTask());
    }

    [Fact]
    public async Task MirrorAsync_WithoutArriving_ThenTheFirstWriteStillReachesTheBrowser()
    {
        var mirror = Mirror("https://host.example/kilder?ref=1&kilde=a");

        await Assert.ThrowsAsync<InvalidOperationException>(() => mirror.MirrorAsync("kilde=a").AsTask());
    }
}
