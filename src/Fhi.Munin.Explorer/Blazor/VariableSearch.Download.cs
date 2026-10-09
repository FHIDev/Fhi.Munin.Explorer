using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>«Last ned utvalg»: everything the search matches, as a file, signed in or not (Fhi.Metadata-idusm).</summary>
public partial class VariableSearch
{
    /// <summary>Past this many hits the download is warned about, as Inge asked: it is slower, not refused.</summary>
    internal const int WarnDownloadFrom = 2000;

    private enum DownloadFailure
    {
        None,
        Failed,
        Throttled
    }

    private bool _downloadOpen;
    private bool _includeKodeverk;
    private bool _downloading;
    private (SaveAllQuery Query, DownloadFailure Failure)? _downloadFailedFor;
    private ElementReference _downloadToggle;

    private string DownloadToggleId => $"munin-explorer-download-{_instance}";
    private string DownloadPanelId => $"munin-explorer-download-panel-{_instance}";
    private string DownloadWarningId => $"munin-explorer-download-warning-{_instance}";

    // As SaveAll: while a fetch is out, _filter is already the next one and the count on screen is the last one's.
    private bool DownloadReady => !_loading && !_downloading;

    private DownloadFailure DownloadFailureShown =>
        _downloadFailedFor is { } failed && failed.Query == CurrentQuery ? failed.Failure : DownloadFailure.None;

    private bool WarnDownload => TotalCount > WarnDownloadFrom;

    private bool ShowDownload => _result is { Items.Count: > 0 } && _resultsTab == ExplorerTab.Search;

    // The list's «Last ned» fold, so the explorer has one way of offering a file and Stiler already styles it.
    private RenderFragment DownloadControl() => builder =>
    {
        if (!ShowDownload)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-list-menu");
        builder.AddAttribute(2, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, CloseDownloadOnEscapeAsync));

        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "class", "hd-button-square button-square--ghost-blue");
        builder.AddAttribute(5, "type", "button");
        builder.AddAttribute(6, "id", DownloadToggleId);
        builder.AddAttribute(7, "aria-expanded", _downloadOpen ? "true" : "false");
        builder.AddAttribute(8, "aria-controls", DownloadPanelId);
        builder.AddAttribute(9, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ToggleDownload));
        builder.AddElementReferenceCapture(10, element => _downloadToggle = element);
        builder.AddContent(11, T.DownloadSelection);
        builder.CloseElement();

        builder.OpenElement(12, "div");
        builder.AddAttribute(13, "class", "munin-explorer-list-menu__panel");
        builder.AddAttribute(14, "id", DownloadPanelId);
        builder.AddAttribute(15, "role", "group");
        builder.AddAttribute(16, "aria-label", T.DownloadSelection);
        builder.AddAttribute(17, "hidden", !_downloadOpen);

        if (WarnDownload)
        {
            builder.OpenElement(18, "p");
            builder.AddAttribute(19, "class", "caption");
            builder.AddAttribute(20, "id", DownloadWarningId);
            builder.AddContent(21, T.DownloadSelectionLarge(TotalCount));
            builder.CloseElement();
        }

        builder.OpenElement(22, "label");
        builder.OpenElement(23, "input");
        builder.AddAttribute(24, "type", "checkbox");
        builder.AddAttribute(25, "checked", _includeKodeverk);
        builder.AddAttribute(26, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => _includeKodeverk = e.Value is true));
        builder.CloseElement();
        builder.AddContent(27, " ");
        builder.AddContent(28, T.IncludeKodeverk);
        builder.CloseElement();

        // aria-disabled while a download is out, not disabled: disabling the pressed button drops focus to <body>.
        AddDownloadButton(builder, 29, ExportFormat.Xlsx, T.DownloadXlsx);
        AddDownloadButton(builder, 36, ExportFormat.Csv, T.DownloadCsv);

        builder.CloseElement();

        // Always present and empty until needed: a region inserted and filled at once is announced unreliably.
        builder.OpenElement(50, "div");
        builder.AddAttribute(51, "class", "caption");
        builder.AddAttribute(52, "role", "alert");
        builder.AddAttribute(53, "aria-live", "assertive");
        builder.AddAttribute(54, "aria-atomic", "true");
        builder.AddContent(55, DownloadFailureShown switch
        {
            DownloadFailure.Throttled => T.RateLimitError,
            DownloadFailure.Failed => T.DownloadError,
            _ => null
        });
        builder.CloseElement();

        builder.CloseElement();
    };

    private void AddDownloadButton(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int sequence, ExportFormat format, string text)
    {
        builder.OpenElement(sequence, "button");
        builder.AddAttribute(sequence + 1, "class", "hd-button-square button-square--ghost-blue");
        builder.AddAttribute(sequence + 2, "type", "button");
        builder.AddAttribute(sequence + 3, "aria-disabled", DownloadReady ? null : "true");
        builder.AddAttribute(sequence + 4, "aria-describedby", WarnDownload ? DownloadWarningId : null);
        builder.AddAttribute(sequence + 5, "onclick", EventCallback.Factory.Create(this, () => DownloadSelectionAsync(format)));
        builder.AddContent(sequence + 6, text);
        builder.CloseElement();
    }

    // A double-click's second click or a shift-click is a selection, not a press. (Fhi.Metadata-zel47)
    private void ToggleDownload(MouseEventArgs released)
    {
        if (!RowPress.WasSelectionStandingStill(released))
        {
            _downloadOpen = !_downloadOpen;
        }
    }

    private async Task CloseDownloadOnEscapeAsync(KeyboardEventArgs e)
    {
        if (e.Key != "Escape" || !_downloadOpen)
        {
            return;
        }

        _downloadOpen = false;
        await _downloadToggle.FocusAsync();
    }

    /// <remarks>The executed search, not the half-typed one in the box: the file holds what the count says.</remarks>
    private async Task DownloadSelectionAsync(ExportFormat format)
    {
        if (!DownloadReady)
        {
            return;
        }

        var query = CurrentQuery;
        _downloading = true;
        _downloadFailedFor = null;

        try
        {
            var file = await Client.ExportVariablesAsync(query.Search, query.Filter, format, _includeKodeverk);
            await BrowserDownload.OfferAsync(ServiceProvider.GetRequiredService<IJSRuntime>(), file);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the {Format} download of the search result", format);
            _downloadFailedFor = (query, DownloadFailure.Throttled);
        }
        catch (Exception ex)
        {
            // Includes the API's 503 when the codebooks cannot be fetched, and a browser refusing the blob.
            Log?.LogError(ex, "could not download the search result as {Format}", format);
            _downloadFailedFor = (query, DownloadFailure.Failed);
        }
        finally
        {
            _downloading = false;
        }
    }
}
