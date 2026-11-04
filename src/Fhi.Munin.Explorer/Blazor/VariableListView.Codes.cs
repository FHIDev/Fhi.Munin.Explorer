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
    private ElementReference _pickerButton;
    private bool _focusPickerButton;

    // Only by item: the codes write has no variable route, and a list from an older API has no item ids.
    private static bool CanPickCodes(VariableListItem item, VariableDetail detail) =>
        item.ItemId is not null && detail.KodeverkLinks.Any(link => link.HasCodeValues);

    private bool IsPicking(VariableListItem item) => _picking == VariableDatasamlingKey.Of(item);

    private IReadOnlyList<DesiredCodeView> CodesOf(VariableListItem item) =>
        _savedCodes.TryGetValue(VariableDatasamlingKey.Of(item), out var saved) ? saved : item.DesiredDataCodes;

    private string RowPickerId(VariableListItem item) => $"{RowDesiredDataId(item)}-picker";

    private void TogglePicker(MouseEventArgs press, VariableListItem item)
    {
        if (!RowPress.WasSelectionStandingStill(press))
        {
            _picking = IsPicking(item) ? null : VariableDatasamlingKey.Of(item);
        }
    }

    // Focus goes back to the button that opened it, which the closing render draws again.
    private void ClosePicker(VariableListItem item)
    {
        if (IsPicking(item))
        {
            _picking = null;
            _focusPickerButton = true;
        }
    }

    private async Task<DesiredDataResult> SaveCodesAsync(VariableListItem item, IReadOnlyList<DesiredCodeView> views)
    {
        if (_shownList is not { } list || item.ItemId is not { } itemId)
        {
            return new DesiredDataResult(DesiredDataOutcome.NotFound);
        }

        var result = await Client.SetMyListItemDesiredCodesAsync(
            list, itemId, DesiredDataOf(item), [.. views.Select(view => view.ToChoice())]);
        if (result.Outcome == DesiredDataOutcome.Saved)
        {
            _savedCodes[VariableDatasamlingKey.Of(item)] = views;
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
