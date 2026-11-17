using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

// A list imported from the old helsedata: what did not come over, until the reader hides it (Fhi.Metadata-9huq2.37).
public sealed partial class VariableListView
{
    private ListImportReport? _importReport;

    // The list and reader the report was last asked for, so it is read once per list shown, not per page read.
    private Guid? _importReportFor;
    private int _importReportReader = -1;
    private int _importReportReads;
    private bool _importWriting;
    private ListActionFailure _importFailure;
    private readonly Dictionary<Guid, ElementReference> _importRemoveButtons = [];
    private ElementReference _importHideButton;

    // Where focus goes once a removal or the hide has landed; dropped if the reader moved on meanwhile.
    private (bool Pending, Guid? Entry, bool Hide, int Forms, int Moves, Guid? List) _importFocus;

    private string ImportHeadingId => $"munin-explorer-list-import-heading-{_instance}";

    private string ImportRemoveId(ListImportNotFound entry) =>
        $"munin-explorer-list-import-remove-{_instance}-{entry.EntryId:N}";

    private string ImportCodesId(ListImportNotFound entry) =>
        $"munin-explorer-list-import-codes-{_instance}-{entry.EntryId:N}";

    private string ImportPlaceId(ListImportNotFound entry) =>
        $"munin-explorer-list-import-place-{_instance}-{entry.EntryId:N}";

    private string ImportHideId => $"munin-explorer-list-import-hide-{_instance}";

    private string? ImportReportMessage => _importFailure switch
    {
        ListActionFailure.Throttled => T.RateLimitError,
        ListActionFailure.Failed => T.ListActionError,
        _ => null
    };

    /// <summary>The report to draw, or null. The markup is drawn only for the reader's own saved lists, signed in.</summary>
    private ListImportReport? ShownImportReport =>
        _importReport is { Dismissed: false } report
        && _importReportFor == _shownList
        && ShownList is { ImportReportDismissed: false }
        && (report.NotFound.Count > 0 || report.Skipped.Count > 0)
            ? report
            : null;

    // Both counts known and 0 is the one case with nothing to read: an API older than the skipped count sends none.
    private static bool ImportReportDue(VariableList list) =>
        list is { ImportedFrom.Length: > 0, ImportReportDismissed: false }
        && (list.ImportNotFoundCount > 0 || list.ImportSkippedCount is not 0);

    /// <summary>Starts the report's read when the list on screen is a new one, without holding up its rows.</summary>
    private void ReadImportReportIfDue()
    {
        if (State is null || (_importReportFor == _shownList && _importReportReader == State.Reader))
        {
            return;
        }

        _importReportFor = _shownList;
        _importReportReader = State.Reader;
        _importReport = null;
        _importRemoveButtons.Clear();
        var read = ++_importReportReads;

        if (ShownList is { } list && ImportReportDue(list))
        {
            _ = ReadImportReportAsync(list.Id, read);
        }
    }

    // A previous reader's notes are not kept in memory for the next one to be shown.
    private void ForgetImportReportOfPreviousReader()
    {
        if (State is not null && _importReportReader != State.Reader)
        {
            _importReport = null;
        }
    }

    private async Task ReadImportReportAsync(Guid list, int read)
    {
        ListImportReport? report;
        var reader = State?.Reader;

        // A failed read draws nothing, and the next page read asks again: the list itself is whole meanwhile.
        try
        {
            report = await Client.GetMyListImportReportAsync(list);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the import report of list {ListId}", list);
            AskAgainAfterFailedRead(read);
            return;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused the import report of list {ListId} as unauthorised", list);
            AskAgainAfterFailedRead(read);
            return;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not read the import report of list {ListId}", list);
            AskAgainAfterFailedRead(read);
            return;
        }

        // The reader switched list, or signed out and in, while it was out: the answer is another list's or reader's.
        if (read != _importReportReads || _shownList != list || State?.Reader != reader)
        {
            return;
        }

        _importReport = report;
        StateHasChanged();
    }

    private void AskAgainAfterFailedRead(int read)
    {
        if (read == _importReportReads)
        {
            _importReportFor = null;
        }
    }

    private static IEnumerable<ListImportNotFound> InOrder(ListImportReport report) =>
        report.NotFound.OrderBy(entry => entry.Index);

    // Entries whose codes read alike would share a «Fjern» name, so those also say their place in the notice.
    private HashSet<string> RepeatedImportCodes(ListImportReport report) =>
        [.. report.NotFound.GroupBy(e => ImportCodes(e.VariableCode, e.DatasamlingCode)).Where(g => g.Count() > 1).Select(g => g.Key)];

    private string? ImportReason(string status) => status switch
    {
        ListImportStatuses.UnknownVariable => T.ImportReasonUnknownVariable,
        ListImportStatuses.UnknownDatasamling => T.ImportReasonUnknownDatasamling,
        ListImportStatuses.Ambiguous => T.ImportReasonAmbiguous,
        _ => null
    };

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private string ImportCodes(string? variableCode, string? datasamlingCode) =>
        T.ImportReportCodes(Present(variableCode), Present(datasamlingCode));

    private string? ImportSkippedFields(IReadOnlyList<string> fields)
    {
        var note = fields.Contains(ListImportFields.Notes);
        var desiredData = fields.Contains(ListImportFields.DesiredData);
        return note || desiredData ? T.ImportSkippedFields(note, desiredData) : null;
    }

    /// <summary>
    /// The items left without a field, each named by its own codes, else by the row on the page the view holds; the
    /// rest are only counted, since naming them would mean reading the whole list.
    /// </summary>
    private (List<(ListImportSkipped Row, VariableListItem? Item)> Named, int Elsewhere) SkippedRows(ListImportReport report)
    {
        var named = new List<(ListImportSkipped, VariableListItem?)>();
        var elsewhere = 0;

        foreach (var row in report.Skipped)
        {
            var item = _page?.Items.FirstOrDefault(i => i.ItemId == row.ItemId);

            if (Present(row.VariableCode) is not null || item is not null)
            {
                named.Add((row, Present(row.VariableCode) is null ? item : null));
            }
            else
            {
                elsewhere++;
            }
        }

        return (named, elsewhere);
    }

    private async Task RemoveImportEntryAsync(ListImportNotFound entry)
    {
        if (_importWriting || State is null || _shownList is not { } list || _importReport is not { } report)
        {
            return;
        }

        var ordered = InOrder(report).ToList();
        var at = ordered.FindIndex(e => e.EntryId == entry.EntryId);
        Guid? next = at >= 0 && at + 1 < ordered.Count ? ordered[at + 1].EntryId : null;
        var since = (OpenForms(), _readerMoves);

        ForgetFailures();
        _importWriting = true;

        try
        {
            // False is Munin's 404: the entry is already gone, which is what the reader asked for.
            await Client.RemoveMyListImportEntryAsync(list, entry.EntryId);
            State.RecordImportEntryRemoved(list);

            if (_importReportFor != list || _importReport is not { } current)
            {
                return;
            }

            _importReport = current with { NotFound = [.. current.NotFound.Where(e => e.EntryId != entry.EntryId)] };
            _importRemoveButtons.Remove(entry.EntryId);

            var stillShown = _importReport.NotFound.Count > 0 || _importReport.Skipped.Count > 0;
            _importFocus = (true, next, stillShown && next is null, since.Item1, since.Item2, list);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused removing entry {EntryId} from the import report of list {ListId}", entry.EntryId, list);
            FailImportWrite(list, ListActionFailure.Throttled);
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused removing entry {EntryId} from the import report of list {ListId} as unauthorised", entry.EntryId, list);
            FailImportWrite(list, ListActionFailure.Failed);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not remove entry {EntryId} from the import report of list {ListId}", entry.EntryId, list);
            FailImportWrite(list, ListActionFailure.Failed);
        }
        finally
        {
            _importWriting = false;
        }
    }

    private async Task HideImportReportAsync()
    {
        if (_importWriting || State is null || _shownList is not { } list)
        {
            return;
        }

        var since = (OpenForms(), _readerMoves);

        ForgetFailures();
        _importWriting = true;

        try
        {
            // False is Munin's 404, already gone. On the holder's copy too, so switching back does not read it again.
            await Client.DismissMyListImportReportAsync(list);
            State.RecordImportReportDismissed(list);
            _importFocus = (true, null, false, since.Item1, since.Item2, list);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused hiding the import report of list {ListId}", list);
            FailImportWrite(list, ListActionFailure.Throttled);
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused hiding the import report of list {ListId} as unauthorised", list);
            FailImportWrite(list, ListActionFailure.Failed);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not hide the import report of list {ListId}", list);
            FailImportWrite(list, ListActionFailure.Failed);
        }
        finally
        {
            _importWriting = false;
        }
    }

    // A failure is said only on the list it happened to: the reader may have switched while the write was out.
    private void FailImportWrite(Guid list, ListActionFailure failure)
    {
        if (_shownList == list)
        {
            _importFailure = failure;
        }
    }

    /// <summary>
    /// The next entry's «Fjern», else «Skjul»; once the notice is gone, «Flere valg», where a removal leaving no row
    /// puts focus too. Nothing when the reader opened a form, moved a fold or left the list meanwhile.
    /// </summary>
    private ElementReference? TakeImportFocusTarget()
    {
        var focus = _importFocus;
        _importFocus = default;

        if (!focus.Pending || (OpenForms() & ~focus.Forms) != 0 || _readerMoves != focus.Moves || _shownList != focus.List)
        {
            return null;
        }

        if (ShownImportReport is null)
        {
            return _menuToggle;
        }

        return focus.Entry is { } entry && _importRemoveButtons.TryGetValue(entry, out var button) ? button
            : focus.Hide ? _importHideButton
            : null;
    }
}
