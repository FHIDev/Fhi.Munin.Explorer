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
    private VariableDatasamlingKey? _openId;
    private Guid? _openListId;
    private VariableDetail? _openDetail;
    private string? _openError;
    private bool _openLoading;
    private int _openGeneration;
    private KodeverkCodeLists? _openCodes;
    private PanelTab _openTab = PanelTab.Data;

    // What the open row's notes field shows.
    private string _notesDraft = "";

    // What this session wrote per list and variable: saved text bridges until the page is read
    // again, and unsaved text (refused or failed) stays until written again, as Ønskede data's does.
    private readonly Dictionary<(Guid List, VariableDatasamlingKey Variable), NotesWrite> _notesWritten = [];
    private readonly Dictionary<(Guid List, VariableDatasamlingKey Variable), int> _notesWrites = [];

    // One write out per row at a time, the newest text waiting behind it, so the API keeps the latest.
    private readonly HashSet<(Guid List, VariableDatasamlingKey Variable)> _notesInFlight = [];
    private readonly Dictionary<(Guid List, VariableDatasamlingKey Variable), string> _notesQueued = [];

    private DesiredDataFailure _notesFailure;
    private (Guid List, VariableDatasamlingKey Variable)? _notesFailureKey;

    private sealed record NotesWrite(
        string Text, bool Saved, int? RefusedMax, bool Pending = false,
        DesiredDataFailure Failure = DesiredDataFailure.None);
    private string? _linkStatus;
    private bool _linkNotCopied;

    private static bool IsOrphan(VariableListItem item) => string.IsNullOrWhiteSpace(item.VariableName);

    private bool IsOpen(VariableListItem item) => _openId == VariableDatasamlingKey.Of(item) && _openListId == _shownList;

    // The id half that tells two items of one variable apart; just the variable for an item with no datasamling.
    private static string ItemSuffix(VariableListItem item) =>
        item.DatasamlingId is { } datasamling ? $"{item.VariableId:N}-{datasamling:N}" : item.VariableId.ToString("N");

    private string RowPanelId(VariableListItem item) => $"munin-explorer-list-panel-{_instance}-{ItemSuffix(item)}";

    private string RowPanelHeadingId(VariableListItem item) =>
        $"munin-explorer-list-panel-heading-{_instance}-{ItemSuffix(item)}";

    private string RowLinkFieldId(VariableListItem item) =>
        $"munin-explorer-list-link-{_instance}-{ItemSuffix(item)}";

    private string RowShareHeadingId(VariableListItem item) =>
        $"munin-explorer-list-share-{_instance}-{ItemSuffix(item)}";

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

        _openId = VariableDatasamlingKey.Of(item);
        _openListId = _shownList;
        _openTab = PanelTab.Data;
        ForgetLinkStatus();
        ForgetPicker();
        _notesDraft = OpenNotesWrite is { } written ? written.Text : item.Notes ?? "";

        await LoadRowDetailAsync(item.VariableId, VersionStatusRule.IsHistorical(item.VersionStatus), item.DatasamlingId);
    }

    private void CloseRow()
    {
        _openId = null;
        _openDetail = null;
        _openError = null;
        _openLoading = false;
        _openCodes = null;
        ForgetLinkStatus();
        ClearSource();

        // Disowns a fetch still in flight for the row that was open.
        _openGeneration++;
    }

    // The item's own datasamling, so its period and its Data tab's statistics are that datasamling's.
    private async Task LoadRowDetailAsync(Guid id, bool historical, Guid? datasamlingId)
    {
        var generation = ++_openGeneration;

        _openDetail = null;
        _openError = null;
        _openLoading = true;
        _openCodes = new KodeverkCodeLists(id, Client, Log);
        StateHasChanged();

        try
        {
            var detail = datasamlingId is { } datasamling
                ? await Client.GetVariableAsync(id, historical, datasamling)
                : await Client.GetVariableAsync(id, includeHistorical: historical);

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
        builder.AddComponentParameter(9, nameof(VariablePanelTabs.DataTop), RowDesiredDataField(item));
        builder.AddComponentParameter(10, nameof(VariablePanelTabs.Notes), RowNotesField(item));
        builder.CloseComponent();
    };

    private static string? RowPanelDescription(VariableDetail detail) => DisplayText.Trimmed(detail.Description);

    private RenderFragment RowPanelTrail(VariableDetail detail) =>
        KildeTrailBlock.Write(KildeTrailBlock.Steps(detail, T, kildeTypeApiName: null), T);

    // Each group opens the search narrowed to it; words only where no search tab is there to open.
    private RenderFragment RowPanelVariabelgrupper(IReadOnlyList<VariabelgruppeReference> groups) => builder =>
    {
        var asButtons = ShowSearch is not null;

        if (asButtons)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "style", OwnerButtonGrid);
        }

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];

            if (ShowSearch is { } search && group.Id != Guid.Empty)
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "class", "hd-button-square button-square--ghost");
                builder.AddAttribute(2, "style", OwnerButtonCell);
                builder.AddAttribute(3, "type", "button");
                builder.AddAttribute(5, "onclick",
                    EventCallback.Factory.Create(this, () => search.ShowVariabelgruppeAsync(group.Id)));

                // Spoken, not shown, and kept out of aria-label so the name keeps its own lang.
                builder.OpenElement(14, "span");
                builder.AddAttribute(15, "class", "screenreader-only");
                builder.AddContent(16, T.ShowVariabelgruppeVariables + " ");
                builder.CloseElement();
                builder.OpenElement(17, "span");
                builder.AddAttribute(18, "lang", CatalogueLang(group.Name));
                builder.AddContent(19, group.Name);
                builder.CloseElement();
                builder.CloseElement();
            }
            else
            {
                builder.AddContent(8, i == 0 || asButtons ? "" : ", ");
                builder.OpenElement(9, "span");
                builder.AddAttribute(10, "lang", CatalogueLang(group.Name));
                builder.AddContent(11, group.Name);
                builder.CloseElement();
            }
        }

        if (asButtons)
        {
            builder.CloseElement();
        }
    };

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
        var row = _openGeneration;

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", link);
            SayLinkStatus(row, T.VariableLinkCopied, notCopied: false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            Log?.LogWarning(ex, "the browser refused to copy a variable link");
            SayLinkStatus(row, T.VariableLinkNotCopied, notCopied: true);
        }
    }

    // Only into the panel that asked: another row opened meanwhile has its own link.
    private void SayLinkStatus(int row, string status, bool notCopied)
    {
        if (row != _openGeneration)
        {
            return;
        }

        _linkStatus = status;
        _linkNotCopied = notCopied;
    }

    private string RowDesiredDataId(VariableListItem item) =>
        $"munin-explorer-list-panel-desired-{_instance}-{ItemSuffix(item)}";

    private string RowDesiredDataHintId(VariableListItem item) => $"{RowDesiredDataId(item)}-hint";

    // The hint always, and the refusal sentence the column's field points at too while this row stands refused.
    private string RowDesiredDataDescribedBy(VariableListItem item) =>
        DesiredDataDescribedBy(item) is { } refusal ? $"{RowDesiredDataHintId(item)} {refusal}" : RowDesiredDataHintId(item);

    private string RowNotesId(VariableListItem item) =>
        $"munin-explorer-list-panel-notes-{_instance}-{ItemSuffix(item)}";

    private string RowNotesStatusId(VariableListItem item) => $"{RowNotesId(item)}-status";

    private NotesWrite? OpenNotesWrite =>
        _openId is { } variable && _shownList is { } list && _notesWritten.TryGetValue((list, variable), out var written)
            ? written
            : null;

    private string? NotesInvalid => OpenNotesWrite?.RefusedMax is null ? null : "true";

    // Said in the field: a refusal names the ceiling, and unsaved text kept from a failed save says so.
    private string? NotesRefusal => OpenNotesWrite switch
    {
        { RefusedMax: { } max } => T.NotesTooLong(max),
        { Failure: DesiredDataFailure.Throttled } => T.RateLimitError,
        { Saved: false, Pending: false } => T.NotesError,
        _ => null,
    };

    private string NotesStatusClass => NotesRefusal is null ? "caption" : "infobox infobox--bg-yellow";

    // A variable leaving the list takes its unsaved notes with it, so they cannot return on a re-add.
    // The write count moves on rather than restarting, so a save still out can never pass for a newer one.
    private void ForgetNotesFor(Guid list, VariableDatasamlingKey variable)
    {
        _notesWritten.Remove((list, variable));
        _notesQueued.Remove((list, variable));
        _notesWrites[(list, variable)] = _notesWrites.GetValueOrDefault((list, variable)) + 1;
    }

    // Steps aside while the failed row's own field is on screen, which says the same thing.
    private string? NotesMessage => _notesFailureKey is { } failed && failed == OpenNotesKey && _openTab == PanelTab.Notes
        ? null
        : _notesFailure switch
        {
            DesiredDataFailure.Throttled => T.RateLimitError,
            DesiredDataFailure.Failed => T.NotesError,
            _ => null,
        };

    private (Guid List, VariableDatasamlingKey Variable)? OpenNotesKey =>
        _openId is { } variable && _shownList is { } list ? (list, variable) : null;

    // A page read is what the API holds: saved text no longer needs bridging, and an open field
    // with nothing unsaved shows what the read brought, as Ønskede data's column does.
    private void ForgetSavedNotes()
    {
        foreach (var key in _notesWritten.Where(entry => entry.Value.Saved).Select(entry => entry.Key).ToList())
        {
            _notesWritten.Remove(key);
        }

        if (OpenNotesKey is { } open
            && !_notesWritten.ContainsKey(open)
            && _page?.Items.FirstOrDefault(item => VariableDatasamlingKey.Of(item) == open.Variable) is { } read)
        {
            _notesDraft = read.Notes ?? "";
        }
    }

    private async Task SaveNotesAsync(VariableListItem item, string? text)
    {
        if (_shownList is not { } list)
        {
            return;
        }

        var key = (list, VariableDatasamlingKey.Of(item));
        var next = text?.Trim() ?? "";

        _notesDraft = next;
        _notesWritten[key] = new NotesWrite(next, Saved: false, RefusedMax: null, Pending: true);
        ForgetFailures();

        if (!_notesInFlight.Add(key))
        {
            _notesQueued[key] = next;
            return;
        }

        try
        {
            while (true)
            {
                var sequence = _notesWrites.GetValueOrDefault(key) + 1;
                _notesWrites[key] = sequence;

                var (written, failure) = await WriteNotesAsync(list, item, next);

                // A removal, or a newer text waiting to go, makes this answer one about the past.
                if (_notesWrites.GetValueOrDefault(key) == sequence && !_notesQueued.ContainsKey(key))
                {
                    Land(key, written with { Failure = failure }, failure);
                }

                if (!_notesQueued.Remove(key, out var queued))
                {
                    break;
                }

                next = queued;
            }
        }
        finally
        {
            _notesInFlight.Remove(key);
        }
    }

    private async Task<(NotesWrite Written, DesiredDataFailure Failure)> WriteNotesAsync(Guid list, VariableListItem item, string text)
    {
        var variable = item.VariableId;

        try
        {
            // By item where the API names one: the variable's own route refuses once it is in the list twice.
            var result = item.ItemId is { } itemId
                ? await Client.SetMyListItemNotesAsync(list, itemId, text)
                : await Client.SetMyListNotesAsync(list, variable, text);

            return result switch
            {
                { Outcome: DesiredDataOutcome.Saved } => (new NotesWrite(text, Saved: true, RefusedMax: null), DesiredDataFailure.None),
                { Outcome: DesiredDataOutcome.Refused, MaxLength: { } maxLength } => (new NotesWrite(text, false, maxLength), DesiredDataFailure.None),
                _ => (new NotesWrite(text, false, null), DesiredDataFailure.Failed),
            };
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the notes on variable {VariableId} in list {ListId}", variable, list);
            return (new NotesWrite(text, false, null), DesiredDataFailure.Throttled);
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused the notes on variable {VariableId} in list {ListId} as unauthorised", variable, list);
            return (new NotesWrite(text, false, null), DesiredDataFailure.Failed);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not save the notes on variable {VariableId} in list {ListId}", variable, list);
            return (new NotesWrite(text, false, null), DesiredDataFailure.Failed);
        }
    }

    // Said under the field while it is on screen (Georgi, 2026-10-05); otherwise in the list's alert,
    // only ever set there, and only for the list the reader is still on.
    private void Land((Guid List, VariableDatasamlingKey Variable) key, NotesWrite written, DesiredDataFailure failure)
    {
        _notesWritten[key] = written;

        var fieldShown = _openId == key.Variable && _openTab == PanelTab.Notes;
        if (failure is not DesiredDataFailure.None && _shownList == key.List && !fieldShown)
        {
            _notesFailure = failure;
            _notesFailureKey = key;
        }
    }
}
