using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The open row's kilde or datasamling, shown in place of the list the way the search shows it (ADO 121586).</summary>
public sealed partial class VariableListView
{
    private SourceKind? _sourceKind;
    private KildeDetail? _kilde;
    private DatasamlingDetail? _datasamling;
    private bool _sourceLoading;
    private string? _sourceError;
    private int _sourceGeneration;

    private ElementReference _sourceRegion;
    private ElementReference _kildeButton;
    private ElementReference _datasamlingButton;
    private bool _focusSourceRegion;
    private SourceKind? _focusOwnerButton;
    private SourceKind? _openedFrom;

    private string SourceRegionId => $"munin-explorer-list-source-{_instance}";

    private string SourceHeadingId => $"munin-explorer-list-source-heading-{_instance}";

    private bool SourceShown => _sourceKind is not null && _openDetail is not null;

    private string? SourceStatus => _sourceLoading
        ? _sourceKind == SourceKind.Kilde ? T.KildeLoading : T.DatasamlingLoading
        : _sourceError;

    private string SourceStatusClass => _sourceError is null ? "caption" : "infobox infobox--bg-yellow";

    // Inline because Stiler has no rule for it: fixed tracks, so the group buttons and these two
    // line up in the same columns whatever the names, and a long name wraps inside its button.
    private const string OwnerButtonGrid =
        "display:grid;grid-template-columns:repeat(auto-fill,12rem);gap:0.5rem 1rem;margin-bottom:1rem";

    private const string OwnerButtonCell = "width:100%;white-space:normal";

    private string OwnerButtonText(SourceKind kind) => kind == SourceKind.Kilde ? T.ShowKilde : T.ShowDatasamling;

    // A double-click takes a word of the label rather than opening the view.
    private Task OpenSourceFromControlAsync(SourceKind kind, MouseEventArgs released) =>
        RowPress.WasSelectionStandingStill(released) ? Task.CompletedTask : OpenSourceAsync(kind);

    private async Task OpenSourceAsync(SourceKind kind)
    {
        if (_openDetail is not { } detail || VariableOwners.IdOf(detail, kind) is not { } id)
        {
            return;
        }

        _sourceKind = kind;
        _openedFrom = kind;
        _focusSourceRegion = true;
        await LoadSourceAsync(kind, id);
    }

    // The datasamling view's own way up to its kilde, as the search follows it.
    private async Task ShowDatasamlingKildeAsync(Guid id)
    {
        if (_datasamling?.ParentKildeId != id || id == Guid.Empty)
        {
            return;
        }

        _sourceKind = SourceKind.Kilde;
        _focusSourceRegion = true;
        await LoadSourceAsync(SourceKind.Kilde, id);
    }

    // Back to the button the reader pressed, even after the datasamling view led on to its kilde.
    private void CloseSource()
    {
        _focusOwnerButton = _openedFrom;
        ClearSource();
    }

    private void ClearSource()
    {
        _sourceKind = null;
        _kilde = null;
        _datasamling = null;
        _sourceError = null;
        _sourceLoading = false;
        _openedFrom = null;

        // Disowns a fetch still in flight, which would otherwise land in a view that has gone.
        _sourceGeneration++;
    }

    private async Task LoadSourceAsync(SourceKind kind, Guid id)
    {
        var generation = ++_sourceGeneration;

        _kilde = null;
        _datasamling = null;
        _sourceError = null;
        _sourceLoading = true;
        StateHasChanged();

        try
        {
            if (kind == SourceKind.Kilde)
            {
                var kilde = await Client.GetKildeAsync(id);

                if (_sourceGeneration == generation)
                {
                    _kilde = kilde;
                    _sourceError = kilde is null ? T.KildeMissing : null;
                }
            }
            else
            {
                var datasamling = await Client.GetDatasamlingAsync(id);

                if (_sourceGeneration == generation)
                {
                    _datasamling = datasamling;
                    _sourceError = datasamling is null ? T.DatasamlingMissing : null;
                }
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused {SourceKind} {SourceId}", kind, id);

            if (_sourceGeneration == generation)
            {
                _sourceError = T.RateLimitError;
            }
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not load {SourceKind} {SourceId}", kind, id);

            if (_sourceGeneration == generation)
            {
                _sourceError = kind == SourceKind.Kilde ? T.KildeError : T.DatasamlingError;
            }
        }
        finally
        {
            if (_sourceGeneration == generation)
            {
                _sourceLoading = false;
            }
        }
    }

    // The view takes the place of the button that opened it, and the way back takes the view away.
    private ElementReference? TakeSourceFocusTarget()
    {
        var toRegion = _focusSourceRegion && SourceShown;
        var toButton = _focusOwnerButton;
        _focusSourceRegion = false;
        _focusOwnerButton = null;

        return toRegion ? _sourceRegion
            : toButton == SourceKind.Kilde ? _kildeButton
            : toButton == SourceKind.Datasamling ? _datasamlingButton
            : null;
    }
}
