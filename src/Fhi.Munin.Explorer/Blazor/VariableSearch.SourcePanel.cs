namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The drill-in view for the kilde or datasamling a variable belongs to.</summary>
public partial class VariableSearch
{

    /// <summary>Whether <paramref name="kind"/> is the owner the panel is currently showing.</summary>
    private bool SourceOpen(SourceKind kind) => _sourceKind == kind;

    private string SourceBusy => _sourceLoading ? "true" : "false";

    private string SourceToggleText(SourceKind kind) => kind switch
    {
        SourceKind.Kilde => SourceOpen(kind) ? T.HideKilde : T.ShowKilde,
        SourceKind.Datasamling => SourceOpen(kind) ? T.HideDatasamling : T.ShowDatasamling,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No label for this owner.")
    };

    private string SourceExpanded(SourceKind kind) => SourceOpen(kind) ? "true" : "false";

    /// <summary>
    /// What the narrowing button says, or null when no owner is open and it is not drawn.
    /// </summary>
    /// <remarks>
    /// Null rather than a throwing switch, unlike <see cref="SourceToggleText"/>: that one is only
    /// ever called with a kind the panel is already drawing, while this reads
    /// <see cref="_sourceKind"/> itself and so has a real null case — the list, where the button
    /// does not belong. Returning it lets the markup ask one question instead of two.
    /// </remarks>
    private string? SourceVariablesText => _sourceKind switch
    {
        SourceKind.Kilde => T.ShowKildeVariables,
        SourceKind.Datasamling => T.ShowDatasamlingVariables,
        _ => null
    };

    /// <summary>
    /// The panel's id on the toggle that opened it, and nothing on the other one.
    /// </summary>
    /// <remarks>
    /// The same rule <see cref="DetailControls"/> follows, with one addition: both toggles point at
    /// the same panel, so the closed one has to carry no <c>aria-controls</c> at all rather than
    /// point at a panel it did not open — two controls claiming one region is read as one region
    /// with two names.
    /// </remarks>
    private string? SourceControls(SourceKind kind) => SourceOpen(kind) ? SourceId : null;

    /// <summary>What the owner panel's status line says: that it is loading, or why it is empty.</summary>
    private string? SourceStatus => _sourceKind switch
    {
        null => null,
        SourceKind.Kilde => _sourceLoading ? T.KildeLoading : _sourceError,
        SourceKind.Datasamling => _sourceLoading ? T.DatasamlingLoading : _sourceError,
        _ => _sourceError
    };

    /// <summary>Muted while it is loading, Stiler's infobox when something went wrong.</summary>
    private string SourceStatusClass => _sourceError is null ? "caption" : "infobox infobox--bg-yellow";
}
