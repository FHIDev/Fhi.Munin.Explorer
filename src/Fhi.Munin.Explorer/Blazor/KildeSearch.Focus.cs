using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

// Opening a kilde replaces the list, the pressed button with it, so focus fell to <body> and the next Tab
// went past the component to the host's footer; going back removes the Back button the same way.
// Both move focus once the new view is drawn (Fhi.Metadata-l9l2n.140).
public sealed partial class KildeSearch
{
    private enum FocusAfterRender { None, Detail, Row }

    private ElementReference _detailRegion;
    private readonly Dictionary<Guid, ElementReference> _nameButtons = [];
    private FocusAfterRender _focus;
    private Guid? _focusRowOf;

    /// <summary>Called first in <see cref="OnAfterRenderAsync"/>; an arrival from the address takes no focus.</summary>
    private async Task MoveFocusAsync()
    {
        var focus = _focus;
        if (focus == FocusAfterRender.None)
        {
            return;
        }

        if (focus == FocusAfterRender.Detail && _selectedId is not null && _detailRegion.Context is not null)
        {
            _focus = FocusAfterRender.None;
            try
            {
                await _detailRegion.FocusAsync();
            }
            catch (JSException)
            {
                // A lost focus is not worth the rest of the render pass; the reader is no worse off than before.
            }
        }
        else if (focus == FocusAfterRender.Row && _selectedId is null)
        {
            _focus = FocusAfterRender.None;
            // The row the reader came from, or the search field if the list no longer holds it.
            if (_focusRowOf is not { } id || !Visible.Any(k => k.Id == id) || !await TryFocusAsync(id))
            {
                await _searchField.FocusAsync();
            }

            _focusRowOf = null;
        }
    }

    // A reference kept from an earlier render can name an element that is gone; the browser then refuses it.
    private async Task<bool> TryFocusAsync(Guid id)
    {
        if (!_nameButtons.TryGetValue(id, out var row) || row.Context is null)
        {
            return false;
        }

        try
        {
            await row.FocusAsync();
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}
