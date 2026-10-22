using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

// «Vis hele variabelen» and the kilde/datasamling drill-ins replace the list, the pressed button with it, so focus
// fell to <body>; Back removes itself the same way. Each moves focus once the new view is drawn, as KildeSearch does
// (Fhi.Metadata-l9l2n.143, after l9l2n.140).
public partial class VariableSearch
{
    private enum FocusAfter { None, WholeVariable, WholeVariableOpener, Source, SourceOpener }

    private ElementReference _wholeRegion;
    private ElementReference _wholeToggle;
    private readonly Dictionary<SourceKind, ElementReference> _sourceToggles = [];
    private FocusAfter _focusAfter;
    private SourceKind? _focusSourceOpener;

    private void ReturnFocusToSourceOpener()
    {
        _focusSourceOpener = _sourceKind;
        _focusAfter = FocusAfter.SourceOpener;
    }

    /// <summary>Called first in <see cref="OnAfterRenderAsync"/>; an arrival from the address takes no focus.</summary>
    private async Task MoveFocusAsync()
    {
        var target = _focusAfter switch
        {
            FocusAfter.WholeVariable when _wholeVariable => _wholeRegion,
            FocusAfter.Source when _sourceKind is not null => _sourceRegion,
            // The opener, back in the row's panel, or the search field if the row has left the page.
            FocusAfter.WholeVariableOpener when !_wholeVariable => _selected is null ? _searchField : _wholeToggle,
            FocusAfter.SourceOpener when _sourceKind is null => _focusSourceOpener is { } kind
                && _selected is not null && _sourceToggles.TryGetValue(kind, out var toggle) ? toggle : _searchField,
            _ => default,
        };

        if (target.Context is null)
        {
            return;
        }

        _focusAfter = FocusAfter.None;
        _focusSourceOpener = null;
        try
        {
            await target.FocusAsync();
        }
        catch (JSException)
        {
            // A refused focus is not worth the rest of the render pass; the reader is no worse off than before.
        }
    }
}
