using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

// The code picker under an opened row's «Ønskede data» (Fhi.Metadata-emsc0.3.7).
public sealed partial class VariableListView
{
    // What a save stored, until the next list read brings the API's own description of it.
    private readonly Dictionary<VariableDatasamlingKey, IReadOnlyList<DesiredCodeView>> _savedCodes = [];
    private VariableDatasamlingKey? _picking;
    // Bumped on every open and close, so an older picker's answer cannot touch a newer one; a list switch is _shownList's.
    private int _pickerGeneration;
    private VariableDatasamlingKey? _codesSaved;
    private ElementReference _pickerButton;
    private bool _focusPickerButton;

    // Only by item: the codes write has no variable route, and a list from an older API has no item ids.
    private static bool CanPickCodes(VariableListItem item, VariableDetail detail) =>
        item.ItemId is not null && detail.KodeverkLinks.Any(link => link.HasCodeValues);

    private bool IsPicking(VariableListItem item) => _picking == VariableDatasamlingKey.Of(item);

    private IReadOnlySet<KodeverkKey>? PickingLists(VariableListItem item, VariableDetail detail) =>
        IsPicking(item) ? detail.KodeverkLinks.Where(link => link.HasCodeValues).Select(KodeverkKey.Of).ToHashSet() : null;

    // Said by the row, not the picker: the picker closes on a save, so its own region would never be read.
    private string? CodesSavedText(VariableListItem item) =>
        _codesSaved == VariableDatasamlingKey.Of(item)
            ? $"{T.PickerSaved}: {T.PickerChoiceCount(CodesOf(item).Count, DesiredCodePicker.MaxChoices)}"
            : null;

    private IReadOnlyList<DesiredCodeView> CodesOf(VariableListItem item) =>
        _savedCodes.TryGetValue(VariableDatasamlingKey.Of(item), out var saved) ? saved : item.DesiredDataCodes;

    private string RowPickerId(VariableListItem item) => $"{RowDesiredDataId(item)}-picker";

    private void TogglePicker(MouseEventArgs press, VariableListItem item)
    {
        if (!RowPress.WasSelectionStandingStill(press))
        {
            _picking = IsPicking(item) ? null : VariableDatasamlingKey.Of(item);
            _codesSaved = null;
            _pickerGeneration++;
        }
    }

    // Focus goes back to the button that opened it, which the closing render draws again.
    private void ClosePicker(VariableListItem item, int generation)
    {
        if (IsPicking(item) && generation == _pickerGeneration)
        {
            _picking = null;
            _focusPickerButton = true;
        }
    }

    private async Task<DesiredDataResult> SaveCodesAsync(VariableListItem item, IReadOnlyList<DesiredCodeView> views, int generation)
    {
        if (_shownList is not { } list || item.ItemId is not { } itemId)
        {
            return new DesiredDataResult(DesiredDataOutcome.NotFound);
        }

        var result = await Client.SetMyListItemDesiredCodesAsync(
            list, itemId, DesiredDataOf(item), [.. views.Select(view => view.ToChoice())]);
        // As the text write: the answer stands for the list it named, but the row's cache and message are this list's.
        if (result.Outcome == DesiredDataOutcome.Saved && _shownList == list && generation == _pickerGeneration)
        {
            _savedCodes[VariableDatasamlingKey.Of(item)] = views;
            _codesSaved = VariableDatasamlingKey.Of(item);
            Log?.LogDebug("code choices saved for item {ItemId} in list {ListId}", itemId, list);
        }

        return result;
    }

    private async Task FocusPickerButtonAsync()
    {
        if (!_focusPickerButton)
        {
            return;
        }

        _focusPickerButton = false;
        try
        {
            await _pickerButton.FocusAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or TaskCanceledException)
        {
            // The row may have closed with the picker; there is then nothing to return focus to.
            Log?.LogDebug(ex, "focus could not go back to the code picker's button");
        }
    }
}
