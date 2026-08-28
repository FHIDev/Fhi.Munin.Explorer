using Fhi.Munin.Explorer.Contracts;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Hands a file the circuit is holding to the reader's browser.
/// </summary>
/// <remarks>
/// <para>
/// A download started inside a Blazor Server circuit is not a link click. The bytes are on the
/// server, the reader is at the other end of a WebSocket, and the component cannot write to their
/// disk. So the browser is asked to do it: a <c>Blob</c> is built from the bytes, an object URL is
/// minted for it, a synthetic anchor carrying <c>download</c> is clicked, and the URL is revoked.
/// </para>
/// <para>
/// <b>No JavaScript file ships with this package.</b> Every call below is a browser built-in —
/// <c>Blob</c>, <c>URL</c>, <c>document</c> — reached through <see cref="IJSRuntime"/>. The
/// packaging guard forbids a <c>wwwroot</c> because a stylesheet riding along would compete with
/// the host's own (<c>scripts/assert-package-contents.sh</c>); it is not a ban on interop, and the
/// sample host already drives <c>history.replaceState</c> this way.
/// </para>
/// <para>
/// The bytes cross the circuit to get here, which is the real cost of this approach: a large export
/// is held in server memory and pushed over SignalR. The API caps an export at 2000 variables,
/// which bounds it.
/// </para>
/// </remarks>
internal static class BrowserDownload
{
    /// <summary>
    /// Offers <paramref name="file"/> to the reader as a download.
    /// </summary>
    /// <exception cref="JSException">
    /// The browser refused. A Content-Security-Policy without <c>blob:</c> is the likely cause
    /// (Fhi.Metadata-vsrys), and the caller is expected to say so rather than leave a button that
    /// appears to do nothing.
    /// </exception>
    public static async Task OfferAsync(
        IJSRuntime js,
        ExportedList file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(file);

        // The byte[] crosses as a real Uint8Array — the interop layer marshals it, so nothing here
        // encodes or decodes.
        await using var blob = await js
            .InvokeConstructorAsync("Blob", [new object[] { file.Bytes }, new { type = file.ContentType }])
            .ConfigureAwait(false);

        var url = await js
            .InvokeAsync<string>("URL.createObjectURL", cancellationToken, blob)
            .ConfigureAwait(false);

        try
        {
            await using var anchor = await js
                .InvokeAsync<IJSObjectReference>("document.createElement", cancellationToken, "a")
                .ConfigureAwait(false);

            await anchor.SetValueAsync("href", url).ConfigureAwait(false);
            await anchor.SetValueAsync("download", file.FileName).ConfigureAwait(false);
            await anchor.InvokeVoidAsync("click", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Revoked even when the click threw: the object URL pins the whole blob in the
            // browser's memory until it is.
            await js.InvokeVoidAsync("URL.revokeObjectURL", CancellationToken.None, url).ConfigureAwait(false);
        }
    }
}
