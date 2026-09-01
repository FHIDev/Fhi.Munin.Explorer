using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A datasamling in full: what it is called, the catalogue's own metadata about it, who it
/// includes, and a sidebar of who owns the data and how much of it there is.
/// </summary>
/// <remarks>
/// The sibling of <see cref="KildeView"/> and built the same way — a page-shaped view that opens
/// inside the component rather than at a route of its own, because this package has no router. A
/// source and a datasamling are the only two things an explorer drills into, and until this
/// existed only one of them had a view: the other was a flat definition list of eleven fields,
/// drawing none of the curated metadata the payload carries (Fhi.Metadata-jgfum).
/// <para>
/// Every inherited field is drawn from its <c>Effective…</c> value. Munin lets a datasamling
/// inherit dataansvarlig, databehandler, lovverk, identification level and validity from its
/// delkilde or its kilde, and the own value is null when nothing is set at that level — so drawing
/// the own values reports "Ikke oppgitt" for a datasamling whose controller is perfectly well known
/// one level up. What applies is what the reader is asking about; where it was written down is a
/// curation detail.
/// </para>
/// <para>
/// Ships no CSS, like everything else in this package: it emits the host's class names so the
/// surrounding site styles it.
/// </para>
/// </remarks>
public sealed partial class DatasamlingView : ComponentBase
{
    /// <summary>The datasamling to show. Nothing renders until this is set.</summary>
    [Parameter, EditorRequired]
    public DatasamlingDetail? Datasamling { get; set; }

    /// <inheritdoc cref="VariableExplorer.Language"/>
    [Parameter]
    public string? Language { get; set; }

    /// <inheritdoc cref="KildeView.HeadingLevel"/>
    [Parameter]
    public int HeadingLevel { get; set; } = 2;

    /// <inheritdoc cref="KildeView.HeadingId"/>
    [Parameter]
    public string? HeadingId { get; set; }

    /// <summary>
    /// Sections to place after the metadata, for the explorer that owns them.
    /// </summary>
    /// <remarks>
    /// The same slot <see cref="KildeView.Sections"/> is, and for the same reason: the explorers
    /// differ in what they add around a shared core, and a flag per difference would make this the
    /// one place they leak into each other. Runa passes none.
    /// </remarks>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The level for the block headings, and for each metadata group under them.</summary>
    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>
    /// The description, unless it only repeats the name.
    /// </summary>
    /// <remarks>
    /// A quarter of the datasamlinger in the test catalogue store the name again as the
    /// beskrivelse, and an ingress restating the heading above it reads as a rendering fault.
    /// </remarks>
    private string? Description =>
        Datasamling is { } datasamling
        && !string.IsNullOrWhiteSpace(datasamling.Description)
        && !string.Equals(datasamling.Description.Trim(), datasamling.PreferredTerm.Trim(),
                          StringComparison.Ordinal)
            ? datasamling.Description
            : null;

    /// <summary>The catalogue's metadata, grouped and ordered as the catalogue arranges it.</summary>
    /// <remarks>
    /// No key is named as drawn elsewhere: the fields the sidebar shows are ungrouped in the
    /// catalogue's own metadata, and an ungrouped key never reaches a group to begin with.
    /// </remarks>
    private IReadOnlyList<PropertyGroup> Groups =>
        Datasamling is { } datasamling
            ? CatalogueProperties.Groups(datasamling.PropertyMetadata, datasamling.AdditionalProperties, Reader)
            : [];

    /// <summary>
    /// The facts every datasamling has, labelled as the kilde view labels the same fields.
    /// </summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words. The kildetype and the
    /// identification level are vocabularies this package translates; the rest are stored once, in
    /// Norwegian, however the reader is reading.
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> SourceInformation =>
        Datasamling is not { } datasamling
            ? []
            : [
                (T.FieldSource, datasamling.ParentKildeName, true),
                (T.FacetKildeType, T.KildeTypeLabel(datasamling.EffectiveKildetype, datasamling.EffectiveKildetype), false),
                (T.FieldLegalBasis, datasamling.EffectiveLegalBasis, true),
                (T.FieldDataController, datasamling.EffectiveDataController, true),
                (T.FieldDataProcessor, datasamling.EffectiveDataProcessor, true),
                (T.FieldPersonIdentification, T.PersonIdentificationLabel(datasamling.EffectivePersonIdentificationLevel), false),
                (T.FieldValidity, Period(datasamling.EffectiveValidFrom, datasamling.EffectiveValidTo), false),
                (T.FieldLastUpdated, Day(datasamling.LastUpdated), false),
            ];

    /// <summary>
    /// How the data is collected and how much of it there is.
    /// </summary>
    /// <remarks>
    /// Frekvens is in the contract and in Runa's block, and no datasamling in the test catalogue
    /// has one — it draws when the catalogue starts carrying it and no row until then, which is
    /// what keeps it out of <see cref="AnyStatistics"/>. A count of nothing is left out rather than
    /// shown as a zero, for the same reason: both are what let a datasamling with no numbers at all
    /// draw no block.
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> Statistics =>
        Datasamling is not { } datasamling
            ? []
            : [
                (T.FieldFrequency, datasamling.Frequency, true),
                (T.FieldCountingUnit, datasamling.CountingUnit, true),
                (T.FieldVariableCount, datasamling.VariableCount > 0 ? datasamling.VariableCount.ToString() : null, false),
            ];

    /// <summary>Whether the statistics block has a row to draw, heading included.</summary>
    private bool AnyStatistics => Statistics.Any(fact => !string.IsNullOrWhiteSpace(fact.Value));

    /// <summary>
    /// The statistics heading, naming the kind of statistics rather than just saying "Statistikk".
    /// </summary>
    /// <remarks>
    /// Shared with <see cref="StatisticsBlock"/>, which heads a variable's numbers the same way off
    /// the same field: a variable's statistikktype is the one belonging to the datasamling it is
    /// pinned into, so two spellings of that heading would be two spellings of one fact.
    /// </remarks>
    private string StatisticsHeading =>
        StatisticsBlock.Heading(Datasamling?.StatisticsType, T);

    /// <summary>A heading at the given level, so this view nests wherever it is put.</summary>
    private static RenderFragment Heading(int level, string text, string cssClass,
                                          string? id = null, string? language = null) => builder =>
    {
        builder.OpenElement(0, $"h{level}");
        builder.AddAttribute(1, "class", cssClass);
        builder.AddAttribute(2, "id", id);
        builder.AddAttribute(3, "lang", language);
        builder.AddContent(4, text);
        builder.CloseElement();
    };

    /// <summary>A definition list of label and value, skipping anything the catalogue has not filled in.</summary>
    private RenderFragment Facts(IReadOnlyList<(string Label, string? Value, bool Norwegian)> facts) => builder =>
    {
        var shown = facts.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();

        if (shown.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", "munin-explorer-meta__grid");

        var seq = 10;

        foreach (var (label, value, norwegian) in shown)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddContent(seq + 3, label);
            builder.CloseElement();

            builder.OpenElement(seq + 4, "dd");
            builder.AddAttribute(seq + 5, "lang", norwegian ? CatalogueProperties.Foreign("no", Reader) : null);
            builder.AddContent(seq + 6, value);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
    };

    /// <summary>One metadata group: its name, then its rows.</summary>
    private RenderFragment Group(PropertyGroup group) => builder =>
    {
        builder.OpenElement(0, $"h{GroupLevel}");
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none munin-explorer-group");
        builder.AddAttribute(2, "lang", CatalogueProperties.Foreign(group.NameLanguage, Reader));
        builder.AddContent(3, group.Name);
        builder.CloseElement();

        builder.OpenElement(4, "dl");
        builder.AddAttribute(5, "class", "munin-explorer-meta__grid");

        var seq = 10;

        foreach (var row in group.Rows)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddAttribute(seq + 3, "lang", CatalogueProperties.Foreign(row.LabelLanguage, Reader));
            builder.AddContent(seq + 4, row.Label);
            builder.CloseElement();

            builder.OpenElement(seq + 5, "dd");
            builder.AddAttribute(seq + 6, "lang", CatalogueProperties.Foreign(row.ValueLanguage, Reader));
            builder.AddContent(seq + 7, row.Value);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
    };

    /// <summary>A date as the day it fell on, in the reader's language.</summary>
    /// <remarks>
    /// The dot is not a separator, it is what makes the number an ordinal in Norwegian. English
    /// writes the same date "1 January 2026" with no dot, so the pattern follows the reader.
    /// </remarks>
    private string Day(DateTimeOffset value) =>
        value.ToString(
            string.Equals(Reader, "en", StringComparison.Ordinal) ? "d MMMM yyyy" : "d. MMMM yyyy",
            CatalogueProperties.Culture(Language));

    /// <summary>
    /// A period, with an open end shown as ongoing rather than as a blank or a guessed date.
    /// </summary>
    private string? Period(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from is null && to is null)
        {
            return null;
        }

        var start = from is { } f ? Day(f) : "";
        var end = to is { } t ? Day(t) : T.Ongoing;

        return string.IsNullOrEmpty(start) ? end : $"{start} – {end}";
    }
}
