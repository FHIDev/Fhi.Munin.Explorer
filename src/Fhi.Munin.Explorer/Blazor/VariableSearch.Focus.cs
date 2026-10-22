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

    // The button pressed in the row's panel. «Vis datakilden» inside a datasamling turns the view into its kilde,
    // so the view's own kind is not the way back (Copilot on #643).
    private SourceKind? _sourceOpenedFrom;

    private void FocusSourceOpenedFrom(SourceKind kind)
    {
        _sourceOpenedFrom = kind;
        _focusAfter = FocusAfter.Source;
    }

    private void ReturnFocusToSourceOpener()
    {
        _focusSourceOpener = _sourceOpenedFrom ?? _sourceKind;
        _sourceOpenedFrom = null;
        _focusAfter = FocusAfter.SourceOpener;
    }

    /// <summary>Called first in <see cref="OnAfterRenderAsync"/>; an arrival from the address takes no focus.</summary>
    private async Task MoveFocusAsync()
    {
        if (_focusAfter == FocusAfter.None)
        {
            return;
        }

        var (wait, target) = FocusTarget();
        if (wait || (target is { } drawn && drawn.Context is null))
        {
            return; // not drawn yet; the render that draws it comes next
        }

        _focusAfter = FocusAfter.None;
        _focusSourceOpener = null;
        if (target is not { } element)
        {
            return; // the view this was for has gone again, so a later render must not take focus
        }

        try
        {
            await element.FocusAsync();
        }
        catch (JSException)
        {
            // A refused focus is not worth the rest of the render pass; the reader is no worse off than before.
        }
    }

    // Wait while a restored row's detail is loading: its buttons are not drawn, and a reference to the last ones is stale.
    private (bool Wait, ElementReference? Target) FocusTarget() => _focusAfter switch
    {
        FocusAfter.WholeVariable => (false, _wholeVariable ? _wholeRegion : null),
        FocusAfter.Source => (false, _sourceKind is not null ? _sourceRegion : null),
        FocusAfter.WholeVariableOpener when _wholeVariable => (false, null),
        FocusAfter.SourceOpener when _sourceKind is not null => (false, null),
        FocusAfter.WholeVariableOpener or FocusAfter.SourceOpener when _selected is null => (false, _searchField),
        FocusAfter.WholeVariableOpener or FocusAfter.SourceOpener when _detailLoading || _detail is null => (true, null),
        FocusAfter.WholeVariableOpener => (false, _wholeToggle),
        FocusAfter.SourceOpener => (false, _focusSourceOpener is { } kind && _sourceToggles.TryGetValue(kind, out var toggle)
            ? toggle : _searchField),
        _ => (false, null),
    };
}
