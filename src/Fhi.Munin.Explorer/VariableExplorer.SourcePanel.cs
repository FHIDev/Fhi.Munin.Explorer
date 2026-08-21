using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

    /// <summary>
    /// The owner's record, as a definition list, once it has arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two shapes of the same list, because a kilde and a datasamling are not the same record: the
    /// kilde carries the kildetype and the scale of the whole catalogue entry, the datasamling
    /// carries what one row of its data counts and who it includes. What they share — dataansvarlig,
    /// databehandler, identification level, lovverk, validity — is labelled identically in both, so
    /// moving between them compares like with like.
    /// </para>
    /// <para>
    /// The datasamling is drawn from its <c>Effective…</c> values throughout. Munin lets a
    /// datasamling inherit those from its delkilde or its kilde, and the own value is null when
    /// nothing is set at that level — so drawing the own values would report "Ikke oppgitt" for a
    /// datasamling whose data controller is perfectly well known, one level up. What applies is
    /// what the reader is asking about; where it was written down is a curation detail.
    /// </para>
    /// </remarks>
    private RenderFragment SourceFields => builder =>
    {
        if (_kilde is null && _datasamling is null)
        {
            return;
        }

        builder.OpenElement(0, "dl");

        // Fixed, spread-out sequence numbers, for the reason InfoLine has them: each field writes
        // its own contiguous block, so the renderer's diff sees a stable tree across renders.
        if (_kilde is { } kilde)
        {
            SourceField(builder, 100, T.FieldDescription, kilde.Description);
            SourceField(builder, 200, T.FacetKildeType, T.KildeTypeLabel(kilde.Kildetype, kilde.Kildetype), norwegian: false);
            SourceField(builder, 300, T.FieldDataController, kilde.DataController);
            SourceField(builder, 400, T.FieldDataProcessor, kilde.DataProcessor);
            SourceField(builder, 500, T.FieldPersonIdentification,
                        T.PersonIdentificationLabel(kilde.PersonIdentificationLevel), norwegian: false);
            SourceField(builder, 600, T.FieldLegalBasis, kilde.LegalBasis);
            SourceField(builder, 700, T.FieldValidity, Period(kilde.ValidFrom, kilde.ValidTo));
            SourceField(builder, 800, T.FieldPeriod, Period(kilde.DataFrom, kilde.DataTo));
            SourceField(builder, 900, T.FieldDataCollections, DatasamlingCount(kilde).ToString(), norwegian: false);
            SourceField(builder, 1000, T.FieldVariableCount, kilde.TotalVariables.ToString(), norwegian: false);
        }
        else if (_datasamling is { } datasamling)
        {
            SourceField(builder, 100, T.FieldDescription, datasamling.Description);
            SourceField(builder, 200, T.FieldSource, datasamling.ParentKildeName);
            SourceField(builder, 300, T.FieldInclusionCriteria, datasamling.InclusionAndExclusionCriteria);
            SourceField(builder, 400, T.FieldDataController, datasamling.EffectiveDataController);
            SourceField(builder, 500, T.FieldDataProcessor, datasamling.EffectiveDataProcessor);
            SourceField(builder, 600, T.FieldPersonIdentification,
                        T.PersonIdentificationLabel(datasamling.EffectivePersonIdentificationLevel), norwegian: false);
            SourceField(builder, 700, T.FieldLegalBasis, datasamling.EffectiveLegalBasis);
            SourceField(builder, 800, T.FieldValidity,
                        Period(datasamling.EffectiveValidFrom, datasamling.EffectiveValidTo));
            SourceField(builder, 900, T.FieldFrequency, datasamling.Frequency);
            SourceField(builder, 1000, T.FieldCountingUnit, datasamling.CountingUnit);
            SourceField(builder, 1100, T.FieldVariableCount, datasamling.VariableCount.ToString(), norwegian: false);
        }

        builder.CloseElement();
    };

    /// <summary>
    /// How many datasamlinger the kilde holds, everywhere in its tree.
    /// </summary>
    /// <remarks>
    /// Counted rather than listed. A large kilde has dozens, and a wall of names inside a result
    /// card answers a question nobody asked while burying the one the panel is for; the number is
    /// what says how big the thing the variable came out of actually is. The delkilde tree is
    /// walked recursively because it can be deeper than one level — a study series has one delkilde
    /// per wave, and counting only the top of it would report a fraction of the catalogue entry.
    /// </remarks>
    private static int DatasamlingCount(KildeDetail kilde) =>
        kilde.Datasamlinger.Count + kilde.Delkilder.Sum(DatasamlingCount);

    private static int DatasamlingCount(KildeDelkilde delkilde) =>
        delkilde.Datasamlinger.Count + delkilde.Children.Sum(DatasamlingCount);

    /// <summary>
    /// One label and its value in the owner panel.
    /// </summary>
    /// <remarks>
    /// <c>norwegian</c> says whether the value is the catalogue's own words. False for ours — the
    /// kildetype and the identification level are prose that follows <see cref="Language"/>, and
    /// marking those as Norwegian would hand an English page's synthesiser a language it is not
    /// reading. A missing value is written out as "Ikke oppgitt" either way, which is the rule the
    /// cards and the variable's own panel already follow.
    /// </remarks>
    private void SourceField(RenderTreeBuilder builder, int seq, string label, string? value, bool norwegian = true)
    {
        builder.OpenElement(seq, "dt");
        builder.AddAttribute(seq + 1, "class", "form-element__label");
        builder.AddContent(seq + 2, label);
        builder.CloseElement();

        builder.OpenElement(seq + 3, "dd");

        if (norwegian)
        {
            builder.AddContent(seq + 4, DetailValue(value));
        }
        else
        {
            builder.AddContent(seq + 5, string.IsNullOrWhiteSpace(value) ? T.NotSpecified : value);
        }

        builder.CloseElement();
    }
}
