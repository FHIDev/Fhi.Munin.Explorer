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

    private bool _confirmingSaveAll;
    private bool _savingAll;
    private bool _focusSaveAll;
    private ElementReference _saveAllButton;

    private SaveFailure _saveAllFailure;
    private int? _saveAllTooManyFrom;
    private (int Count, string List)? _savedAll;

    // The search the last save covered: a new search or filter offers the button again.
    private (string? Search, VariableFilter Filter)? _savedAllQuery;

    private readonly HashSet<Guid> _noticeRows = [];
    private string _noticeList = "";
    private CancellationTokenSource? _noticeTimer;

    private string SaveAllButtonId => $"munin-explorer-save-all-{_instance}";
    private string ConfirmSaveAllId => $"munin-explorer-save-all-confirm-{_instance}";

    private TimeProvider Clock => ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

    private bool SavedAllForThisSearch =>
        _savedAllQuery is { } saved
        && string.Equals(saved.Search, _executedSearch, StringComparison.Ordinal)
        && saved.Filter.Equals(_filter);

    private bool ShowSaveAll =>
        ShowSaveButton && _result is { Items.Count: > 0 } && _resultsTab == ExplorerTab.Search;

    private RenderFragment SaveAllControl() => builder =>
    {
        if (!ShowSaveAll)
        {
            return;
        }

        var done = SavedAllForThisSearch;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "margin-bottom");

        // aria-disabled rather than disabled while saving or done: disabling the pressed button drops focus to <body>.
        builder.OpenElement(2, "button");
        builder.AddAttribute(3, "class", "hd-button-square button-square--ghost-blue margin-right");
        builder.AddAttribute(4, "type", "button");
        builder.AddAttribute(5, "id", SaveAllButtonId);
        builder.AddAttribute(6, "aria-disabled", done || _savingAll ? "true" : null);
        builder.AddAttribute(7, "aria-expanded", _confirmingSaveAll ? "true" : null);
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
            builder.AddContent(14, _confirmingSaveAll ? T.ConfirmSaveAllNo : T.SaveAllResults);
        }

        builder.CloseElement();

        if (_confirmingSaveAll)
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
        builder.AddContent(34, _savedAll is { } saved && done ? T.SavedAllStatus(saved.Count, saved.List) : null);
        builder.CloseElement();

        builder.OpenElement(35, "p");
        builder.AddAttribute(36, "role", "alert");
        builder.AddAttribute(37, "aria-live", "assertive");
        builder.AddAttribute(38, "aria-atomic", "true");
        builder.AddContent(39, _saveAllTooManyFrom is { } max
            ? T.SaveAllTooMany(max)
            : _saveAllFailure switch
            {
                SaveFailure.Throttled => T.RateLimitError,
                SaveFailure.SignInRequired => T.SignInRequiredError,
                SaveFailure.Failed => T.SaveError,
                _ => null
            });
        builder.CloseElement();

        builder.CloseElement();
    };

    private async Task PressSaveAllAsync()
    {
        if (_savingAll || SavedAllForThisSearch)
        {
            return;
        }

        if (_confirmingSaveAll)
        {
            _confirmingSaveAll = false;
            return;
        }

        if (TotalCount >= ConfirmSaveAllFrom)
        {
            ForgetSaveAllOutcome();
            _confirmingSaveAll = true;
            return;
        }

        await SaveAllAsync();
    }

    private Task ConfirmSaveAllAsync()
    {
        // The Ja button leaves the document with the question, so focus goes back to the one it answered.
        _confirmingSaveAll = false;
        _focusSaveAll = true;

        return SaveAllAsync();
    }

    private void ForgetSaveAllOutcome()
    {
        _saveAllFailure = SaveFailure.None;
        _saveAllTooManyFrom = null;
        _savedAll = null;
    }

    private async Task SaveAllAsync()
    {
        if (ListState is null)
        {
            return;
        }

        var search = _executedSearch;
        var filter = _filter;

        ForgetSaveAllOutcome();

        // Known from the count already, so the reader is told without a request the API would refuse.
        if (TotalCount > IMuninExplorerClient.MaxVariablesPerBatch)
        {
            _saveAllTooManyFrom = IMuninExplorerClient.MaxVariablesPerBatch;
            return;
        }

        _savingAll = true;
        StateHasChanged();

        try
        {
            var ids = await Client.GetVariableIdsAsync(search, filter);

            if (ids.TooMany)
            {
                _saveAllTooManyFrom = ids.MaxIds;
                return;
            }

            var added = await ListState.SaveAllAsync(ids.Ids, T.FirstListName);

            if (added is null)
            {
                _saveAllFailure = SaveFailure.Failed;
                return;
            }

            var list = ListState.Lists.FirstOrDefault(l => l.Id == ListState.ActiveListId)?.Name ?? "";

            _savedAllQuery = (search, filter);
            _savedAll = (added.Count, list);
            ShowSavedNotices(added, list);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused saving the whole result");
            _saveAllFailure = SaveFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused saving the whole result as unauthorised");
            _saveAllFailure = SaveFailure.SignInRequired;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not save the whole result");
            _saveAllFailure = SaveFailure.Failed;
        }
        finally
        {
            _savingAll = false;
        }
    }

    private void ShowSavedNotices(IReadOnlyCollection<Guid> rows, string list)
    {
        _noticeTimer?.Cancel();
        _noticeTimer?.Dispose();

        _noticeRows.Clear();
        _noticeRows.UnionWith(rows);
        _noticeList = list;

        if (_noticeRows.Count == 0)
        {
            _noticeTimer = null;
            return;
        }

        var timer = _noticeTimer = new CancellationTokenSource();
        _ = HideSavedNoticesAsync(timer.Token);
    }

    private async Task HideSavedNoticesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SavedNoticeDuration, Clock, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _noticeRows.Clear();
            StateHasChanged();
        });
    }

    private bool ShowsSavedNotice(VariableSummary v) => _noticeRows.Contains(v.Id) && ListState?.IsSaved(v.Id) == true;

    private void StopSavedNotices()
    {
        _noticeTimer?.Cancel();
        _noticeTimer?.Dispose();
        _noticeTimer = null;
    }
}
