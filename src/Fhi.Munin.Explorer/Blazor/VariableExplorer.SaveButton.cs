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

    /// <summary>Rows whose last save attempt threw. Cleared when that row is tried again.</summary>
    private readonly HashSet<Guid> _saveError = [];

    private RenderFragment RowSaveButton(VariableSummary v) => builder =>
    {
        if (!ShowSaveButton)
        {
            return;
        }

        var saved = ListState!.IsSaved(v.Id);
        var failed = _saveError.Contains(v.Id);

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
        builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, () => ToggleSavedAsync(v)));
        builder.AddContent(5, saved ? T.RemoveFromList : T.SaveToList);
        builder.CloseElement();

        // Said in the row rather than the component's alert region: the other rows are unaffected,
        // and the reader needs it beside the control that did not do what they asked.
        //
        // The container is always here, empty when nothing is wrong — the same shape the component's
        // own alert region uses (VariableExplorer.razor:286). A role="alert" element that is
        // inserted and filled in the same DOM update is announced unreliably; one that is already
        // there and gains text is announced.
        builder.OpenElement(6, "span");
        builder.AddAttribute(7, "role", "alert");
        builder.AddAttribute(8, "aria-live", "assertive");
        builder.AddAttribute(9, "aria-atomic", "true");
        builder.AddContent(10, failed ? T.SaveError : null);
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
        catch (Exception)
        {
            _saveError.Add(v.Id);
        }

        StateHasChanged();
    }
}
