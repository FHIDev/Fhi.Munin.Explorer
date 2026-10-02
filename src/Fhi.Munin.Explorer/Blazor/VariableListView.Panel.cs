using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The panel that opens under a row of an own list: what the variable is, and a way to share it.</summary>
public sealed partial class VariableListView
{
    /// <summary>
    /// The address that opens one variable in the explorer, absolute because it leaves the page by
    /// e-mail and the clipboard. Without it, or where it answers <see langword="null"/> for a row,
    /// that row's panel offers no way to share the variable.
    /// </summary>
    [Parameter] public Func<VariableListItem, string?>? VariableHref { get; set; }

    // One row open at a time, and only in the list it was opened in.
    private Guid? _openId;
    private Guid? _openListId;
    private VariableDetail? _openDetail;
    private string? _openError;
    private bool _openLoading;
    private int _openGeneration;
    private KodeverkCodeLists? _openCodes;
    private PanelTab _openTab = PanelTab.Data;
    private string? _linkStatus;
    private bool _linkNotCopied;

    private static bool IsOrphan(VariableListItem item) => string.IsNullOrWhiteSpace(item.VariableName);

    private bool IsOpen(VariableListItem item) => _openId == item.VariableId && _openListId == _shownList;

    private string RowPanelId(VariableListItem item) => $"munin-explorer-list-panel-{_instance}-{item.VariableId:N}";

    private string RowPanelHeadingId(VariableListItem item) =>
        $"munin-explorer-list-panel-heading-{_instance}-{item.VariableId:N}";

    private string RowLinkFieldId(VariableListItem item) =>
        $"munin-explorer-list-link-{_instance}-{item.VariableId:N}";

    private string RowShareHeadingId(VariableListItem item) =>
        $"munin-explorer-list-share-{_instance}-{item.VariableId:N}";

    // Drawn from the row, so the region has its name while the fetch is in flight.
    private RenderFragment RowPanelHeading(VariableListItem item) => builder =>
    {
        builder.OpenElement(0, $"h{RowPanelLevel}");
        builder.AddAttribute(1, "class", "munin-explorer-meta__heading");
        builder.AddAttribute(2, "id", RowPanelHeadingId(item));
        builder.AddAttribute(3, "lang", CatalogueLang(item.VariableName));
        builder.AddContent(4, RowName(item));
        builder.CloseElement();
    };

    private string? RowPanelControls(VariableListItem item) => IsOpen(item) ? RowPanelId(item) : null;

    private int RowPanelLevel => Math.Clamp(TitleLevel + 1, 1, 6);

    private int RowPanelSectionLevel => Math.Clamp(TitleLevel + 2, 1, 6);

    private string RowPanelStatusClass => _openError is null ? "caption" : "infobox infobox--bg-yellow";

    private string? RowPanelStatus => _openLoading ? T.DetailLoading : _openError;

    // A double-click takes a word of the name rather than opening and shutting the panel.
    private Task ToggleRowFromNameAsync(VariableListItem item, MouseEventArgs released) =>
        RowPress.WasSelectionStandingStill(released) ? Task.CompletedTask : ToggleRowAsync(item);

    private async Task ToggleRowAsync(VariableListItem item)
    {
        if (IsOpen(item))
        {
            CloseRow();

            return;
        }

        _openId = item.VariableId;
        _openListId = _shownList;
        _openTab = PanelTab.Data;
        ForgetLinkStatus();

        await LoadRowDetailAsync(item.VariableId);
    }

    private void CloseRow()
    {
        _openId = null;
        _openDetail = null;
        _openError = null;
        _openLoading = false;
        _openCodes = null;
        ForgetLinkStatus();

        // Disowns a fetch still in flight for the row that was open.
        _openGeneration++;
    }

    private async Task LoadRowDetailAsync(Guid id)
    {
        var generation = ++_openGeneration;

        _openDetail = null;
        _openError = null;
        _openLoading = true;
        _openCodes = new KodeverkCodeLists(id, Client, Log);
        StateHasChanged();

        try
        {
            var detail = await Client.GetVariableAsync(id);

            if (_openGeneration != generation)
            {
                return;
            }

            _openDetail = detail;
            _openError = detail is null ? T.DetailMissing : null;
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused variable {VariableId}", id);

            if (_openGeneration == generation)
            {
                _openError = T.RateLimitError;
            }
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not load variable {VariableId}", id);

            if (_openGeneration == generation)
            {
                _openError = T.DetailError;
            }
        }
        finally
        {
            if (_openGeneration == generation)
            {
                _openLoading = false;
            }
        }

        if (_openGeneration == generation && _openDetail is { } loaded && _openCodes is { } lists)
        {
            // Drawn first, so the panel does not wait for these fetches to appear.
            StateHasChanged();
            await lists.LoadUnnamedAsync(loaded, () => ReferenceEquals(_openCodes, lists));
        }
    }

    private RenderFragment RowPanelTabs(VariableListItem item, VariableDetail detail) => builder =>
    {
        builder.OpenComponent<VariablePanelTabs>(0);
        builder.AddComponentParameter(1, nameof(VariablePanelTabs.Detail), detail);
        builder.AddComponentParameter(2, nameof(VariablePanelTabs.Lists), _openCodes);
        builder.AddComponentParameter(3, nameof(VariablePanelTabs.LabelledBy), RowPanelHeadingId(item));
        builder.AddComponentParameter(4, nameof(VariablePanelTabs.Tab), _openTab);
        builder.AddComponentParameter(5, nameof(VariablePanelTabs.TabChanged),
            EventCallback.Factory.Create<PanelTab>(this, tab => _openTab = tab));
        builder.AddComponentParameter(6, nameof(VariablePanelTabs.SectionLevel), RowPanelSectionLevel);
        builder.AddComponentParameter(7, nameof(VariablePanelTabs.Language), Language);
        builder.AddComponentParameter(8, nameof(VariablePanelTabs.ShowDescription), false);
        builder.CloseComponent();
    };

    private static string? RowPanelDescription(VariableDetail detail) => DisplayText.Trimmed(detail.Description);

    private RenderFragment RowPanelTrail(VariableDetail detail) =>
        KildeTrailBlock.Write(KildeTrailBlock.Steps(detail, T, kildeTypeApiName: null), T);

    private string? RowPanelVariabelgrupper(VariableDetail detail) =>
        KildeTrailBlock.NamedVariabelgrupper(detail) is { Count: > 0 } groups
            ? string.Join(", ", groups.Select(group => group.Name))
            : null;

    // One source for both ends, so a period is never half the detail's and half the list's.
    private string RowPanelPeriod(VariableListItem item, VariableDetail detail) =>
        (detail.DataFrom is null && detail.DataTo is null
            ? CatalogueDate.Period(item.DataFrom, item.DataTo, Language, T, DateWidth.Narrow)
            : CatalogueDate.Period(detail.DataFrom, detail.DataTo, Language, T, DateWidth.Narrow))
        ?? T.NotSpecified;

    private string VariableMailTo(VariableListItem item, string link)
    {
        var name = RowName(item);
        var subject = T.ShareVariableMailSubject(name);
        var body = T.ShareVariableMailBody(name, link);

        return $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
    }

    private void ForgetLinkStatus()
    {
        _linkStatus = null;
        _linkNotCopied = false;
    }

    // Safari refuses a clipboard write that arrives after a server round trip, so a refusal shows the
    // link itself to be copied by hand. Emptied first, so a second press is announced again.
    private async Task CopyVariableLinkAsync(string link)
    {
        ForgetLinkStatus();
        StateHasChanged();

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", link);
            _linkStatus = T.VariableLinkCopied;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            Log?.LogWarning(ex, "the browser refused to copy a variable link");
            _linkStatus = T.VariableLinkNotCopied;
            _linkNotCopied = true;
        }
    }
}
