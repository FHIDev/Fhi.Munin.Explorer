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
    private sealed record SaveAllOutcome(int Reader, SaveAllQuery Query, SaveFailure Failure, int? TooManyFrom, int? Saved, string? List);

    private SaveAllOutcome? _saveAll;
    private SaveAllQuery? _confirmingFor;
    private bool _savingAll;
    private bool _focusSaveAll;
    private bool _disposed;
    private ElementReference _saveAllButton;

    private readonly HashSet<Guid> _noticeRows = [];
    private string _noticeList = "";
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

    private bool ConfirmingSaveAll => _confirmingFor == CurrentQuery && !SavedAllForThisSearch;

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
        builder.AddAttribute(1, "class", "margin-bottom");

        // aria-disabled rather than disabled: disabling the pressed button drops focus to <body>.
        builder.OpenElement(2, "button");
        builder.AddAttribute(3, "class", "hd-button-square button-square--ghost-blue margin-right");
        builder.AddAttribute(4, "type", "button");
        builder.AddAttribute(5, "id", SaveAllButtonId);
        builder.AddAttribute(6, "aria-disabled", done || !SaveAllReady ? "true" : null);
        builder.AddAttribute(7, "aria-expanded", SaveAllWouldAsk && !done ? (confirming ? "true" : "false") : null);
        builder.AddAttribute(8, "onclick", EventCallback.Factory.Create(this, PressSaveAllAsync));
        builder.AddElementReferenceCapture(9, element => _saveAllButton = element);

        if (done)
        {
            builder.OpenElement(10, "span");
            builder.AddAttribute(11, "aria-hidden", "true");
            builder.AddContent(12, "✓ ");
            builder.CloseElement();
            builder.AddContent(13, T.SavedAllResults);
        }
        else
        {
            builder.AddContent(14, confirming ? T.ConfirmSaveAllNo : T.SaveAllResults);
        }

        builder.CloseElement();

        if (confirming)
        {
            builder.OpenElement(20, "span");
            builder.AddAttribute(21, "id", ConfirmSaveAllId);
            builder.AddAttribute(22, "class", "margin-right");
            builder.AddContent(23, T.ConfirmSaveAll(TotalCount));
            builder.CloseElement();

            builder.OpenElement(24, "button");
            builder.AddAttribute(25, "class", "hd-button-square button-square--ghost-blue");
            builder.AddAttribute(26, "type", "button");
            builder.AddAttribute(27, "aria-describedby", ConfirmSaveAllId);
            builder.AddAttribute(28, "onclick", EventCallback.Factory.Create(this, ConfirmSaveAllAsync));
            builder.AddContent(29, T.ConfirmSaveAllYes);
            builder.CloseElement();
        }

        // Both regions are always present and empty until needed: one inserted and filled at once is announced unreliably.
        builder.OpenElement(30, "p");
        builder.AddAttribute(31, "role", "status");
        builder.AddAttribute(32, "aria-live", "polite");
        builder.AddAttribute(33, "aria-atomic", "true");
        builder.AddContent(34, shown is { List: { } list } ? T.SavedAllStatus(shown.Saved, list) : null);
        builder.CloseElement();

        builder.OpenElement(35, "p");
        builder.AddAttribute(36, "role", "alert");
        builder.AddAttribute(37, "aria-live", "assertive");
        builder.AddAttribute(38, "aria-atomic", "true");
        builder.AddContent(39, shown switch
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
            _saveAll = null;
            _confirmingFor = CurrentQuery;
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
                return;
            }

            var result = await state.SaveAllAsync(ids.Ids, T.FirstListName);

            if (result is null)
            {
                _saveAll = Outcome(SaveFailure.Failed);
                return;
            }

            _saveAll = Outcome(saved: result.Added?.Count, list: result.ListName);
            ShowSavedNotices(result.Added ?? ids.Ids, result.ListName);
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

    private void ShowSavedNotices(IReadOnlyCollection<Guid> rows, string list)
    {
        StopSavedNotices();

        if (_disposed || rows.Count == 0)
        {
            return;
        }

        _noticeRows.UnionWith(rows);
        _noticeList = list;

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

    private bool ShowsSavedNotice(VariableSummary v) => _noticeRows.Contains(v.Id) && ListState?.IsSaved(v.Id) == true;

    private void StopSavedNotices()
    {
        _noticeTimer?.Cancel();
        _noticeTimer?.Dispose();
        _noticeTimer = null;
        _noticeRows.Clear();
    }
}
