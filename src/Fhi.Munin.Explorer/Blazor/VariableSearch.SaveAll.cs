using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>«Lagre disse variablene»: the whole search result, every page, into the active list.</summary>
public partial class VariableSearch
{
    /// <summary>A result this large asks before it is saved, as ADO 118713 specifies.</summary>
    internal const int ConfirmSaveAllFrom = 200;

    /// <summary>How long each saved row shows its notice.</summary>
    internal static readonly TimeSpan SavedNoticeDuration = TimeSpan.FromSeconds(3);

    private readonly record struct SaveAllQuery(string? Search, VariableFilter Filter);

    // What the last press produced, and for whom: a sign-in or sign-out moves the reader, and an
    // outcome recorded for another reader is not drawn.
    private sealed record SaveAllOutcome(int Reader, SaveAllQuery Query, SaveFailure Failure, int? TooManyFrom, int? Saved, string? List, bool Empty = false);

    private SaveAllOutcome? _saveAll;
    private (int Reader, SaveAllQuery Query)? _confirmingFor;
    private bool _savingAll;
    private bool _focusSaveAll;
    private bool _disposed;
    private ElementReference _saveAllButton;

    private readonly HashSet<Guid> _noticeRows = [];
    private string _noticeList = "";
    private int _noticeReader;
    private CancellationTokenSource? _noticeTimer;

    private string SaveAllButtonId => $"munin-explorer-save-all-{_instance}";
    private string ConfirmSaveAllId => $"munin-explorer-save-all-confirm-{_instance}";

    private TimeProvider Clock => ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

    private SaveAllQuery CurrentQuery => new(_executedSearch, _filter);

    // While a fetch is out, _filter is already the next one and the count on screen is the last one's.
    private bool SaveAllReady => !_loading && !_savingAll;

    /// <summary>The outcome of the last press, when it was this reader's and this search's.</summary>
    private SaveAllOutcome? SaveAllShown =>
        _saveAll is { } outcome && ListState is { } state && outcome.Reader == state.Reader && outcome.Query == CurrentQuery
            ? outcome
            : null;

    private bool SavedAllForThisSearch => SaveAllShown is { List: not null };

    private bool ConfirmingSaveAll =>
        _confirmingFor is { } asked && asked.Reader == ListState?.Reader && asked.Query == CurrentQuery && !SavedAllForThisSearch;

    private bool SaveAllWouldAsk => TotalCount >= ConfirmSaveAllFrom && TotalCount <= IMuninExplorerClient.MaxVariablesPerBatch;

    private bool ShowSaveAll =>
        ShowSaveButton && _result is { Items.Count: > 0 } && _resultsTab == ExplorerTab.Search;

    private RenderFragment SaveAllControl() => builder =>
    {
        if (!ShowSaveAll)
        {
            return;
        }

        var done = SavedAllForThisSearch;
        var confirming = ConfirmingSaveAll;
        var shown = SaveAllShown;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-save-all");

        // A line of its own above the buttons, so a narrow page never breaks the question between them.
        if (confirming)
        {
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "id", ConfirmSaveAllId);
            builder.AddContent(4, T.ConfirmSaveAll(TotalCount));
            builder.CloseElement();
        }

        // aria-disabled rather than disabled: disabling the pressed button drops focus to <body>.
        builder.OpenElement(5, "button");
        builder.AddAttribute(6, "class", "hd-button-square button-square--ghost-blue margin-right");
        builder.AddAttribute(7, "type", "button");
        builder.AddAttribute(8, "id", SaveAllButtonId);
        builder.AddAttribute(9, "aria-disabled", done || !SaveAllReady ? "true" : null);
        builder.AddAttribute(10, "aria-expanded", SaveAllWouldAsk && !done ? (confirming ? "true" : "false") : null);
        builder.AddAttribute(11, "aria-describedby", confirming ? ConfirmSaveAllId : null);
        builder.AddAttribute(12, "onclick", EventCallback.Factory.Create(this, PressSaveAllAsync));
        builder.AddElementReferenceCapture(13, element => _saveAllButton = element);

        if (done)
        {
            builder.OpenElement(14, "span");
            builder.AddAttribute(15, "aria-hidden", "true");
            builder.AddContent(16, "✓ ");
            builder.CloseElement();
            builder.AddContent(17, T.SavedAllResults);
        }
        else
        {
            builder.AddContent(18, confirming ? T.ConfirmSaveAllNo : T.SaveAllResults);
        }

        builder.CloseElement();

        // After «Nei», which is the pressed button, so «Ja» is the next Tab stop.
        if (confirming)
        {
            builder.OpenElement(24, "button");
            builder.AddAttribute(25, "class", "hd-button-square button-square--ghost-blue");
            builder.AddAttribute(26, "type", "button");
            builder.AddAttribute(27, "aria-describedby", ConfirmSaveAllId);
            builder.AddAttribute(28, "aria-disabled", SaveAllReady ? null : "true");
            builder.AddAttribute(29, "onclick", EventCallback.Factory.Create(this, ConfirmSaveAllAsync));
            builder.AddContent(30, T.ConfirmSaveAllYes);
            builder.CloseElement();
        }

        // Both regions are always present and empty until needed: one inserted and filled at once is announced unreliably.
        // A <div>, not a <p>: an empty paragraph keeps its margins. Stiler's caption, as the result count above wears.
        builder.OpenElement(40, "div");
        builder.AddAttribute(41, "class", "caption");
        builder.AddAttribute(42, "role", "status");
        builder.AddAttribute(43, "aria-live", "polite");
        builder.AddAttribute(44, "aria-atomic", "true");
        builder.AddContent(45, shown switch
        {
            { List: { } list } => T.SavedAllStatus(shown.Saved, list),
            { Empty: true } => T.NothingToSave,
            _ => null
        });
        builder.CloseElement();

        builder.OpenElement(46, "div");
        builder.AddAttribute(47, "class", "caption");
        builder.AddAttribute(48, "role", "alert");
        builder.AddAttribute(49, "aria-live", "assertive");
        builder.AddAttribute(50, "aria-atomic", "true");
        builder.AddContent(51, shown switch
        {
            { TooManyFrom: { } max } => T.SaveAllTooMany(max),
            { Failure: SaveFailure.Throttled } => T.RateLimitError,
            { Failure: SaveFailure.SignInRequired } => T.SignInRequiredError,
            { Failure: SaveFailure.Failed } => T.SaveError,
            _ => null
        });
        builder.CloseElement();

        builder.CloseElement();
    };

    private async Task PressSaveAllAsync()
    {
        if (!SaveAllReady || SavedAllForThisSearch || ListState is null)
        {
            return;
        }

        if (ConfirmingSaveAll)
        {
            _confirmingFor = null;
            return;
        }

        // Known from the count already, so the reader is neither asked nor sent a request the API would refuse.
        if (TotalCount > IMuninExplorerClient.MaxVariablesPerBatch)
        {
            _saveAll = new SaveAllOutcome(ListState.Reader, CurrentQuery, SaveFailure.None, IMuninExplorerClient.MaxVariablesPerBatch, null, null);
            return;
        }

        if (SaveAllWouldAsk)
        {
            // Asking clears only this search's old message; another search's outcome stays until the next save.
            if (SaveAllShown is not null)
            {
                _saveAll = null;
            }

            _confirmingFor = (ListState.Reader, CurrentQuery);
            return;
        }

        await SaveAllAsync();
    }

    private Task ConfirmSaveAllAsync()
    {
        if (!SaveAllReady || !ConfirmingSaveAll)
        {
            return Task.CompletedTask;
        }

        // The Ja button leaves the document with the question, so focus goes back to the one it answered.
        _confirmingFor = null;
        _focusSaveAll = true;

        return SaveAllAsync();
    }

    private async Task SaveAllAsync()
    {
        if (ListState is not { } state)
        {
            return;
        }

        var reader = state.Reader;
        var query = CurrentQuery;

        SaveAllOutcome Outcome(SaveFailure failure = SaveFailure.None, int? tooManyFrom = null, int? saved = null, string? list = null) =>
            new(reader, query, failure, tooManyFrom, saved, list);

        _saveAll = null;
        _savingAll = true;
        StateHasChanged();

        try
        {
            var ids = await Client.GetVariableIdsAsync(query.Search, query.Filter);

            if (ids.TooMany)
            {
                _saveAll = Outcome(tooManyFrom: ids.MaxIds);
                return;
            }

            // The result emptied between the count on screen and the press: nothing to save, and no list to make.
            if (ids.Ids.Count == 0)
            {
                _saveAll = Outcome() with { Empty = true };
                return;
            }

            var result = await state.SaveAllAsync(ids.Ids, T.FirstListName);

            if (result is null)
            {
                _saveAll = Outcome(SaveFailure.Failed);
                return;
            }

            _saveAll = Outcome(saved: result.Added?.Count, list: result.ListName);
            // Unknown when the list could not be read: rows it already held must not read as new.
            ShowSavedNotices(result.Added ?? [], result.ListName, reader);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused saving the whole result");
            _saveAll = Outcome(SaveFailure.Throttled);
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused saving the whole result as unauthorised");
            _saveAll = Outcome(SaveFailure.SignInRequired);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not save the whole result");
            _saveAll = Outcome(SaveFailure.Failed);
        }
        finally
        {
            _savingAll = false;
        }
    }

    private void ShowSavedNotices(IReadOnlyCollection<Guid> rows, string list, int reader)
    {
        StopSavedNotices();

        if (_disposed || rows.Count == 0)
        {
            return;
        }

        _noticeRows.UnionWith(rows);
        _noticeList = list;
        _noticeReader = reader;

        var timer = _noticeTimer = new CancellationTokenSource();
        _ = HideSavedNoticesAsync(timer.Token);
    }

    private async Task HideSavedNoticesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SavedNoticeDuration, Clock, cancellationToken);
            await InvokeAsync(() =>
            {
                // A later save may have put its own rows in after this delay ended.
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _noticeRows.Clear();
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later save, or the component was disposed.
        }
        catch (ObjectDisposedException)
        {
            // The renderer went away while the timer ran.
        }
    }

    private bool ShowsSavedNotice(VariableSummary v) =>
        _noticeRows.Contains(v.Id) && ListState is { } state && state.Reader == _noticeReader && state.IsSaved(v.Id);

    private void StopSavedNotices()
    {
        _noticeTimer?.Cancel();
        _noticeTimer?.Dispose();
        _noticeTimer = null;
        _noticeRows.Clear();
    }
}
