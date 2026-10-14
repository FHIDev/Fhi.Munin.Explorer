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

    /// <summary>
    /// Absolute address of the host's VariableExplorer page, without Explorer query state or a fragment.
    /// Null leaves the downloaded file's link columns empty. VariableExplorer supplies its own address automatically.
    /// </summary>
    [Parameter] public Uri? ExplorerPageUrl { get; set; }

    private enum DownloadFailure
    {
        None,
        Failed,
        Throttled
    }

    private bool _downloadOpen;
    private bool _includeKodeverk;
    private bool _downloading;
    private ExportFormat? _downloadingFormat;
    private SaveAllQuery? _downloadDoneFor;
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

    // A large selection takes many seconds. The ready text is for the search it was made from, as the failure is.
    private string? DownloadStatus =>
        _downloadingFormat switch
        {
            ExportFormat.Xlsx => T.DownloadingXlsx,
            ExportFormat.Csv => T.DownloadingCsv,
            _ => _downloadDoneFor == CurrentQuery ? T.DownloadDone : null
        };

    // The list's «Last ned» fold, so the explorer has one way of offering a file and Stiler already styles it.
    private RenderFragment DownloadControl() => builder =>
    {
        if (!ShowDownload)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(10, "class", "munin-explorer-list-menu");
        builder.AddAttribute(20, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, CloseDownloadOnEscapeAsync));

        // Dressed as «Kolonner» beside it: their ghost square, an icon, and their chevron for the state we hold.
        builder.OpenElement(30, "button");
        builder.AddAttribute(40, "class", "hd-button-square button-square--ghost");
        builder.AddAttribute(50, "type", "button");
        builder.AddAttribute(60, "id", DownloadToggleId);
        builder.AddAttribute(70, "aria-expanded", _downloadOpen ? "true" : "false");
        builder.AddAttribute(80, "aria-controls", DownloadPanelId);
        builder.AddAttribute(85, "aria-busy", _downloading ? "true" : null);
        builder.AddAttribute(90, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ToggleDownload));
        builder.AddElementReferenceCapture(100, element => _downloadToggle = element);
        builder.OpenElement(102, "span");
        builder.AddAttribute(103, "class", "icon icon-download");
        builder.AddAttribute(104, "aria-hidden", "true");
        builder.CloseElement();
        builder.AddContent(110, _downloading ? T.Downloading : T.DownloadSelection);
        builder.OpenElement(112, "span");
        builder.AddAttribute(113, "class", _downloadOpen ? "icon icon--right icon-keyboard-arrow-up" : "icon icon--right icon-keyboard-arrow-down");
        builder.AddAttribute(114, "aria-hidden", "true");
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(120, "div");
        builder.AddAttribute(130, "class", "munin-explorer-list-menu__panel munin-explorer-list-menu__panel--rows");
        builder.AddAttribute(140, "id", DownloadPanelId);
        builder.AddAttribute(150, "role", "group");
        builder.AddAttribute(160, "aria-label", T.DownloadSelection);
        builder.AddAttribute(170, "hidden", !_downloadOpen);

        if (WarnDownload)
        {
            builder.OpenElement(180, "p");
            builder.AddAttribute(190, "class", "munin-explorer-list-menu__note");
            builder.AddAttribute(200, "id", DownloadWarningId);
            builder.AddContent(210, T.DownloadSelectionLarge(TotalCount));
            builder.CloseElement();
        }

        builder.OpenElement(220, "label");
        builder.AddAttribute(225, "class", "munin-explorer-list-menu__check");
        builder.OpenElement(230, "input");
        builder.AddAttribute(240, "type", "checkbox");
        builder.AddAttribute(250, "checked", _includeKodeverk);
        builder.AddAttribute(255, "disabled", _downloading);
        builder.AddAttribute(260, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => _includeKodeverk = e.Value is true));
        builder.CloseElement();
        builder.AddContent(270, " ");
        builder.AddContent(280, T.IncludeKodeverk);
        builder.CloseElement();

        // aria-disabled while a download or a search is out, not disabled: disabling the pressed button drops focus to <body>.
        AddDownloadButton(builder, 290, ExportFormat.Xlsx, T.DownloadXlsx, T.DownloadXlsxHint);
        AddDownloadButton(builder, 360, ExportFormat.Csv, T.DownloadCsv, T.DownloadCsvHint);

        // Only while the panel is open, and empty then: the result count stays the page's one resting status region.
        if (_downloadOpen)
        {
            builder.OpenElement(440, "p");
            builder.AddAttribute(450, "class", "munin-explorer-list-menu__note");
            builder.AddAttribute(460, "role", "status");
            builder.AddAttribute(470, "aria-live", "polite");
            builder.AddAttribute(480, "aria-atomic", "true");
            builder.AddContent(490, DownloadStatus);
            builder.CloseElement();
        }

        builder.CloseElement();

        // Always present and empty until needed: a region inserted and filled at once is announced unreliably.
        builder.OpenElement(500, "div");
        builder.AddAttribute(510, "class", "caption");
        builder.AddAttribute(520, "role", "alert");
        builder.AddAttribute(530, "aria-live", "assertive");
        builder.AddAttribute(540, "aria-atomic", "true");
        builder.AddContent(550, DownloadFailureShown switch
        {
            DownloadFailure.Throttled => T.RateLimitError,
            DownloadFailure.Failed => T.DownloadError,
            _ => null
        });
        builder.CloseElement();

        builder.CloseElement();
    };

    private void AddDownloadButton(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int sequence, ExportFormat format, string text, string fileType)
    {
        builder.OpenElement(sequence, "button");
        builder.AddAttribute(sequence + 1, "class", "munin-explorer-list-menu__item");
        builder.AddAttribute(sequence + 2, "type", "button");
        builder.AddAttribute(sequence + 3, "aria-disabled", DownloadReady ? null : "true");
        builder.AddAttribute(sequence + 4, "aria-busy", _downloadingFormat == format ? "true" : null);
        builder.AddAttribute(sequence + 5, "aria-describedby", WarnDownload ? DownloadWarningId : null);
        builder.AddAttribute(sequence + 6, "onclick", EventCallback.Factory.Create(this, () => DownloadSelectionAsync(format)));
        if (_downloadingFormat == format)
        {
            builder.AddContent(sequence + 7, DownloadStatus);
        }
        else
        {
            builder.AddContent(sequence + 8, text);
            builder.OpenElement(sequence + 9, "span");
            builder.AddAttribute(sequence + 10, "class", "munin-explorer-list-menu__hint");
            // The format is the row's name; the file type is a detail for the eye, not a second word to hear.
            builder.AddAttribute(sequence + 11, "aria-hidden", "true");
            builder.AddContent(sequence + 12, fileType);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    // A double-click's second click or a shift-click is a selection, not a press. (Fhi.Metadata-zel47)
    private void ToggleDownload(MouseEventArgs released)
    {
        if (!RowPress.WasSelectionStandingStill(released))
        {
            _downloadOpen = !_downloadOpen;
            _downloadDoneFor = null;
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
        _downloadingFormat = format;
        _downloadDoneFor = null;
        _downloadFailedFor = null;

        try
        {
            var file = await Client.ExportVariablesAsync(query.Search, query.Filter, ExplorerPageUrl, format, _includeKodeverk);
            await BrowserDownload.OfferAsync(ServiceProvider.GetRequiredService<IJSRuntime>(), file);
            _downloadDoneFor = query;

            if (DownloadPanel.ClosesOnDelivery)
            {
                _downloadOpen = false;
                await _downloadToggle.FocusAsync();
            }
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
            _downloadingFormat = null;
        }
    }
}
