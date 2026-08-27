using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The row's save action: puts the variable in the reader's list, and takes it out again.
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
public partial class VariableExplorer
{
    private bool ShowSaveButton => ListState?.IsAuthenticated == true;

    /// <summary>How a row's last save attempt ended, when it ended badly.</summary>
    /// <remarks>
    /// Worth telling the two failures apart: saving one row after another is the rhythm that meets
    /// the limiter, and "prøv igjen om litt" is the one piece of advice that cannot help a reader
    /// who is being throttled.
    /// </remarks>
    private enum SaveFailure
    {
        /// <summary>Nothing has gone wrong for this row — the value a missing entry reads as.</summary>
        None = 0,

        /// <summary>The save threw for a reason the reader can only try again on.</summary>
        Failed,

        /// <summary>The API refused the save because too many requests arrived — HTTP 429.</summary>
        Throttled
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

    private RenderFragment RowSaveButton(VariableSummary v) => builder =>
    {
        if (!ShowSaveButton)
        {
            return;
        }

        var saved = ListState!.IsSaved(v.Id);

        // A row with no entry reads as SaveFailure.None, which is what an untried — or a since
        // retried — row is.
        _saveError.TryGetValue(v.Id, out var failure);

        // Stiler's own square-button classes and nothing else, the same pair the detail panel's
        // toggles wear. No `munin-explorer-*` name of its own on purpose: the package ships no CSS,
        // so a new name here would be one with no rule behind it until somebody wrote one in Stiler,
        // and it would render unstyled in the host until they did.
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "hd-button-square button-square--ghost");
        builder.AddAttribute(2, "type", "button");

        // The pressed state is what a screen reader announces, and it is the same fact the word
        // shows sighted readers — one control in two states, not two controls.
        builder.AddAttribute(3, "aria-pressed", saved ? "true" : "false");

        // The accessible name says which variable, where the visible words cannot: a page of
        // results is 25 buttons all reading "Lagre i liste", and a screen reader moving down them
        // announces the same three words 25 times over. It tracks the pressed state for the same
        // reason the word does — one control in two states — and puts the name after the visible
        // text so that text stays a contiguous substring of it (WCAG 2.5.3, and 4.1.2 for the
        // name itself).
        builder.AddAttribute(4, "aria-label", saved
            ? T.RemoveFromListLabel(v.PreferredTerm)
            : T.SaveToListLabel(v.PreferredTerm));

        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => ToggleSavedAsync(v)));
        builder.AddContent(6, saved ? T.RemoveFromList : T.SaveToList);
        builder.CloseElement();

        // Said in the row rather than the component's alert region: the other rows are unaffected,
        // and the reader needs it beside the control that did not do what they asked.
        //
        // The container is always here, empty when nothing is wrong — the same shape the component's
        // own alert region uses (VariableExplorer.razor:286). A role="alert" element that is
        // inserted and filled in the same DOM update is announced unreliably; one that is already
        // there and gains text is announced.
        builder.OpenElement(7, "span");
        builder.AddAttribute(8, "role", "alert");
        builder.AddAttribute(9, "aria-live", "assertive");
        builder.AddAttribute(10, "aria-atomic", "true");
        builder.AddContent(11, failure switch
        {
            SaveFailure.Throttled => T.RateLimitError,
            SaveFailure.Failed => T.SaveError,
            _ => null
        });
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
        catch (MuninExplorerRateLimitedException)
        {
            // The writes go through the same client as the reads and meet the same per-address
            // limiter, so this row's save can be refused while the catalogue is perfectly up.
            _saveError[v.Id] = SaveFailure.Throttled;
        }
        catch (Exception)
        {
            _saveError[v.Id] = SaveFailure.Failed;
        }

        StateHasChanged();
    }
}
