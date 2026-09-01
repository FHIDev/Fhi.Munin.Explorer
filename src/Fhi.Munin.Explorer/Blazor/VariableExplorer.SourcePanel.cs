using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The drill-in view for the kilde or datasamling a variable belongs to.</summary>
public partial class VariableExplorer
{

    /// <summary>Which of a variable's two owners a panel is showing.</summary>
    /// <remarks>
    /// The two are one control each and one payload each, but one panel: they answer the same
    /// question about the same variable at two widths, and a reader comparing them side by side is
    /// not what the card has room for. One enum rather than two booleans, so "both open at once" is
    /// a state that cannot be written down.
    /// </remarks>
    private enum SourceKind
    {
        /// <summary>The kilde the variable's datasamling belongs to.</summary>
        Kilde,

        /// <summary>The datasamling the variable is pinned into.</summary>
        Datasamling
    }

    /// <summary>Whether <paramref name="kind"/> is the owner the panel is currently showing.</summary>
    private bool SourceOpen(SourceKind kind) => _sourceKind == kind;

    /// <summary>
    /// The id to fetch for an owner, or null when the variable does not name one.
    /// </summary>
    /// <remarks>
    /// <see cref="VariableDetail.KildeId"/> is a bare <c>Guid</c> rather than a nullable one, so
    /// "no kilde" arrives as <see cref="Guid.Empty"/> — a value the endpoint would answer 404 for.
    /// It is treated as absent here, which is what keeps a button off the screen that could only
    /// ever report "not found".
    /// </remarks>
    private static Guid? SourceIdOf(VariableDetail detail, SourceKind kind)
    {
        var id = kind == SourceKind.Kilde ? detail.KildeId : detail.DatasamlingId;

        return id is { } value && value != Guid.Empty ? value : null;
    }

    /// <summary>The owners this variable can actually be opened out into, in trail order.</summary>
    /// <remarks>
    /// Widest first, matching the kilde trail directly above the buttons: a reader following the
    /// path from kildetype to datasamling meets the two controls in the same order the trail names
    /// the two things.
    /// </remarks>
    private static IReadOnlyList<SourceKind> SourceTargets(VariableDetail detail) =>
        [.. new[] { SourceKind.Kilde, SourceKind.Datasamling }.Where(kind => SourceIdOf(detail, kind) is not null)];

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

    /// <summary>
    /// The panel's heading: the owner's name as the variable itself records it.
    /// </summary>
    /// <remarks>
    /// Taken from the variable's own detail rather than from the fetched payload, so the heading is
    /// on screen the moment the panel is — and does not change under the reader when the fetch
    /// lands. It is also what names the region, which a heading that only appeared with the payload
    /// could not do without leaving a dangling <c>aria-labelledby</c> while the panel loaded.
    /// </remarks>
    private RenderFragment SourceHeading(VariableDetail detail, SourceKind kind) => builder =>
    {
        var name = kind == SourceKind.Kilde ? detail.KildeName : detail.DatasamlingName;

        builder.OpenElement(0, $"h{SourceLevel}");
        builder.AddAttribute(1, "class", "headline headline-s margin--bottom");
        builder.AddAttribute(2, "id", SourceHeadingId);
        builder.AddAttribute(3, "lang", "no");
        builder.AddContent(4, Trimmed(name) ?? SourceFallbackName(kind));
        builder.CloseElement();
    };

    /// <summary>What to head the panel with when the variable records no name for its owner.</summary>
    /// <remarks>
    /// The field's own label — "Datakilde", "Datasamling" — rather than "Ikke oppgitt": the region
    /// still has to be named after what it holds, and a region called "Ikke oppgitt" says nothing
    /// about which of the two the reader opened.
    /// </remarks>
    private string SourceFallbackName(SourceKind kind) =>
        kind == SourceKind.Kilde ? T.FieldSource : T.FieldDataCollection;
}
