using System.Text.Json;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The Blob, object URL, anchor and revoke sequence a list export goes through.
/// </summary>
/// <remarks>
/// Strict on purpose: under bUnit's loose mode every one of these calls answers <c>default</c>, so a
/// misspelt identifier or a revoke that never runs leaves the rest of the suite green and the reader
/// with a button that downloads nothing or a blob pinned in memory (Fhi.Metadata-eeuey.4.2).
/// </remarks>
public class BrowserDownloadTest
{
    private const string Url = "blob:https://host.example/6f1c2d";

    private static readonly ExportedList Export = new([1, 2, 3], "text/csv", "variabler.csv");

    /// <summary>bUnit refuses <c>Setup&lt;IJSObjectReference&gt;</c>, steering to modules; these are not modules.</summary>
    private sealed class ObjectHandler : JSRuntimeInvocationHandler<IJSObjectReference>
    {
        public ObjectHandler(string identifier, string method, IJSObjectReference result)
            : base(i => i.Identifier == identifier && i.InvocationMethodName == method, isCatchAllHandler: false) =>
            SetResult(result);
    }

    private static (BunitJSInterop Interop, RecordingJsObject Blob, RecordingJsObject Anchor) Arrange(
        Exception? clickThrows = null)
    {
        var interop = new BunitJSInterop { Mode = JSRuntimeMode.Strict };
        var blob = new RecordingJsObject();
        var anchor = new RecordingJsObject(clickThrows, onClick: () => Assert.True(
            interop.Invocations["URL.revokeObjectURL"].Count == 0,
            "the object URL was revoked before the anchor was clicked"));

        interop.AddInvocationHandler(new ObjectHandler("Blob", "InvokeConstructorAsync", blob));
        interop.Setup<string>("URL.createObjectURL", _ => true).SetResult(Url);
        interop.AddInvocationHandler(new ObjectHandler("document.createElement", "InvokeAsync", anchor));
        interop.SetupVoid("URL.revokeObjectURL", _ => true).SetVoidResult();

        return (interop, blob, anchor);
    }

    [Fact]
    public async Task OfferAsync_WhenCalled_ThenTheBlobCarriesTheBytesAndTheContentType()
    {
        var (interop, blob, _) = Arrange();

        await BrowserDownload.OfferAsync(interop.JSRuntime, Export);

        var constructed = interop.VerifyInvoke("Blob");
        Assert.Equal("InvokeConstructorAsync", constructed.InvocationMethodName);
        var parts = Assert.IsType<object[]>(constructed.Arguments[0]);
        Assert.Same(Export.Bytes, Assert.Single(parts));
        Assert.Equal("""{"type":"text/csv"}""", JsonSerializer.Serialize(constructed.Arguments[1]));

        Assert.Same(blob, Assert.Single(interop.VerifyInvoke("URL.createObjectURL").Arguments));
        Assert.True(blob.Disposed, "the Blob reference was never released");
        Assert.Empty(blob.Set);
        Assert.Empty(blob.Invoked);
    }

    [Fact]
    public async Task OfferAsync_WhenCalled_ThenTheAnchorIsPointedAtTheUrlNamedAndClickedOnce()
    {
        var (interop, _, anchor) = Arrange();

        await BrowserDownload.OfferAsync(interop.JSRuntime, Export);

        Assert.Equal("a", Assert.Single(interop.VerifyInvoke("document.createElement").Arguments));
        Assert.Equal(new (string, object?)[] { ("href", Url), ("download", Export.FileName) }, anchor.Set);
        Assert.Equal(["click"], anchor.Invoked);
        Assert.True(anchor.Disposed, "the anchor reference was never released");
        Assert.Equal(Url, Assert.Single(interop.VerifyInvoke("URL.revokeObjectURL").Arguments));
    }

    [Fact]
    public async Task OfferAsync_WhenTheClickThrows_ThenTheSameUrlIsStillRevokedAndTheErrorTravelsOn()
    {
        var refused = new JSException("blocked by Content-Security-Policy");
        var (interop, blob, anchor) = Arrange(clickThrows: refused);

        var thrown = await Assert.ThrowsAsync<JSException>(() => BrowserDownload.OfferAsync(interop.JSRuntime, Export));

        Assert.Same(refused, thrown);
        Assert.Equal(["click"], anchor.Invoked);
        Assert.Equal(Url, Assert.Single(interop.VerifyInvoke("URL.revokeObjectURL").Arguments));
        Assert.True(anchor.Disposed, "the anchor reference was never released");
        Assert.True(blob.Disposed, "the Blob reference was never released");
    }
}
