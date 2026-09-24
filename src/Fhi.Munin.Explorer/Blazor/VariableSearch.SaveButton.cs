using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The open panel's save action: puts the variable in the reader's list, and takes it out again.
/// </summary>
/// <remarks>
/// <para>
/// Drawn only for a signed-in reader. Signed out there is no button at all rather than a disabled
/// one — a control that can never do anything is worse than no control, and the state holder would
/// refuse the call anyway.
/// </para>
/// <para>
/// Whether a variable is saved is read from <see cref="VariableListState"/> on every render and
/// never remembered here. The rows are rebuilt whenever the facet counts change, so a button that
/// kept its own answer would forget it at the next refiltering and then show the wrong word for a
/// variable that is in the list.
/// </para>
/// </remarks>
public partial class VariableSearch
{
    private bool ShowSaveButton => ListState?.IsAuthenticated == true;

    /// <summary>How a row's last save attempt ended, when it ended badly.</summary>
    /// <remarks>
    /// Three failures, not two: throttled and unauthenticated both need words other than "prøv
    /// igjen om litt", but not the same words as each other — one reader should wait, the other
    /// cannot succeed by waiting at all.
    /// </remarks>
    private enum SaveFailure
    {
        /// <summary>Nothing has gone wrong for this row — the value a missing entry reads as.</summary>
        None = 0,

        /// <summary>The save threw for a reason the reader can only try again on.</summary>
        Failed,

        /// <summary>The API refused the save because too many requests arrived — HTTP 429.</summary>
        Throttled,

        /// <summary>The API refused the save as unauthenticated (HTTP 401/403), despite the host's own claim that the reader is signed in.</summary>
        SignInRequired
    }

    /// <summary>
    /// Rows whose last save attempt threw, against how it threw. Cleared when that row is tried
    /// again.
    /// </summary>
    /// <remarks>
    /// The condition and not the sentence, so the text is still resolved at render time and a host
    /// that switches language mid-session does not leave one row speaking the old one.
    /// </remarks>
    private readonly Dictionary<Guid, SaveFailure> _saveError = [];

    // In the open panel, as on helsedata's own list: a collapsed row keeps one Tab stop (35w0p.78).
    // Drawn from the row's summary, so it is there while the detail loads and after it fails (35w0p.83).
    private RenderFragment PanelSaveButton(VariableSummary v) => builder =>
    {
        if (!ShowSaveButton)
        {
            return;
        }

        var saved = ListState!.IsSaved(v.Id);

        // The panel's own button shape, so it reads as one of the actions beside it.
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "hd-button-square button-square--ghost margin-right margin-bottom");
        builder.AddAttribute(2, "type", "button");
        builder.AddAttribute(3, "id", SaveButtonId(v));

        // The pressed state is what a screen reader announces, and it is the same fact the word
        // shows sighted readers — one control in two states, not two controls.
        builder.AddAttribute(4, "aria-pressed", saved ? "true" : "false");

        // Our words, then Munin's name span (lang="no"), so each half is voiced in its own language
        // (WCAG 3.1.2). Visible text first for speech input (2.5.3); a blank term adds nothing.
        builder.AddAttribute(5, "aria-labelledby", $"{SaveButtonId(v)} {RowHeadingId(v)}");

        builder.AddAttribute(6, "onclick", EventCallback.Factory.Create(this, () => ToggleSavedAsync(v)));

        builder.AddContent(7, saved ? T.RemoveFromList : T.SaveToList);
        builder.CloseElement();
    };

    // Beside the button that failed. Always present while the button is, and empty until needed:
    // a role="alert" inserted and filled in one update is announced unreliably.
    private RenderFragment PanelSaveStatus(VariableSummary v) => builder =>
    {
        if (!ShowSaveButton)
        {
            return;
        }

        _saveError.TryGetValue(v.Id, out var failure);

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-data-list__save-status");
        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "role", "alert");
        builder.AddAttribute(4, "aria-live", "assertive");
        builder.AddAttribute(5, "aria-atomic", "true");
        builder.AddContent(6, failure switch
        {
            SaveFailure.Throttled => T.RateLimitError,
            SaveFailure.SignInRequired => T.SignInRequiredError,
            SaveFailure.Failed => T.SaveError,
            _ => null
        });
        builder.CloseElement();
        builder.CloseElement();
    };

    private async Task ToggleSavedAsync(VariableSummary v)
    {
        if (ListState is null)
        {
            return;
        }

        // Caught here the way every other await in this component catches: an unhandled exception
        // out of an EventCallback takes the whole circuit down, which is a far worse answer to a
        // failed save than a line of text beside the button.
        try
        {
            _saveError.Remove(v.Id);
            await ListState.ToggleSavedAsync(v.Id, T.FirstListName);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(
                ex, "the rate limiter refused the save of variable {VariableId}", v.Id);

            // The writes go through the same client as the reads and meet the same per-address
            // limiter, so this row's save can be refused while the catalogue is perfectly up.
            _saveError[v.Id] = SaveFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(
                ex, "the API refused the save of variable {VariableId} as unauthorised", v.Id);

            // The API's own answer, not IsAuthenticated read again — that is the host's claim the
            // API just contradicted, and asking it a second time would repeat the same wrong word.
            _saveError[v.Id] = SaveFailure.SignInRequired;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not save variable {VariableId}", v.Id);

            _saveError[v.Id] = SaveFailure.Failed;
        }

        StateHasChanged();
    }
}
