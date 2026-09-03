using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A source, in the shape both explorers show it: what it is called, the catalogue's own metadata
/// about it, and a sidebar of the facts that are the same for every source.
/// </summary>
/// <remarks>
/// Shared deliberately, and built as a core with slots rather than one view with flags. Measured on
/// 2026-08-20, Runa and Kelda render the same source identically down to the heading order — same
/// name block, same eight metadata groups, same two sidebar boxes, and — since
/// Fhi.Metadata-rhybi — the same word over the datasamlinger. Kelda then adds sections Runa
/// does not have.
/// <para>
/// A boolean per Kelda section would make this the one place where both explorers leak into each
/// other, and every later difference would add another flag. Instead each explorer passes its own
/// sections in through <see cref="Sections"/>, and this component never learns which one is calling.
/// </para>
/// <para>
/// Ships no CSS, like everything else in this package: it emits the host's class names so the
/// surrounding site styles it.
/// </para>
/// </remarks>
public sealed partial class KildeView : ComponentBase
{
    /// <summary>The source to show. Nothing renders until this is set.</summary>
    [Parameter, EditorRequired]
    public KildeDetail? Kilde { get; set; }

    /// <inheritdoc cref="VariableExplorer.Language"/>
    [Parameter]
    public string? Language { get; set; }

    /// <summary>
    /// The heading level this view's title should use.
    /// </summary>
    /// <remarks>
    /// Two on a page of its own, where the site's own <c>h1</c> is above it; deeper when it opens
    /// inside a result row, which already sits under headings of its own. A view that always
    /// emitted <c>h1</c> would be wrong in one of those places, and heading order is how a screen
    /// reader user navigates a page rather than decoration.
    /// </remarks>
    [Parameter]
    public int HeadingLevel { get; set; } = 2;

    /// <summary>
    /// Sections to place after the metadata, for the explorer that owns them.
    /// </summary>
    /// <remarks>
    /// Kelda passes its variables, access criteria and prices here, and after them whatever its own
    /// host hung on the explorer. Runa passes nothing at all. The datasamling hierarchy is in
    /// neither: this view draws that itself, from the source it was given. Neither explorer is named
    /// in this component.
    /// </remarks>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    /// <summary>
    /// An id for the name heading, so a surrounding region can label itself by it.
    /// </summary>
    /// <remarks>
    /// The drill-in is a landmark, and a landmark is only useful if a screen reader can say which
    /// source it just entered. That means the name it points at has to be this component's, not a
    /// second heading outside it saying the same thing.
    /// </remarks>
    [Parameter]
    public string? HeadingId { get; set; }

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The level for the two block headings, and for each metadata group under them.</summary>
    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>
    /// The identifier line under the name: <c>K_ALS (ALS)</c>, or just the code when there is no
    /// short name to put beside it.
    /// </summary>
    private string? Identifiers =>
        Kilde is { } kilde ? Identifier(kilde.Code, kilde.ShortName) : null;

    /// <inheritdoc cref="Identifiers"/>
    /// <remarks>
    /// Shared with the delkilder in the tree below, which have a code and a short name of the same
    /// shape — <c>K_TR.BIODATA</c> — and are looked up by them the same way.
    /// </remarks>
    private static string? Identifier(string? code, string? shortName) =>
        string.IsNullOrWhiteSpace(code) ? null
        : string.IsNullOrWhiteSpace(shortName) ? code
        : $"{code} ({shortName})";

    /// <summary>The catalogue's metadata, grouped and ordered as the catalogue arranges it.</summary>
    private IReadOnlyList<PropertyGroup> Groups =>
        Kilde is { } kilde
            ? CatalogueProperties.Groups(kilde.PropertyMetadata, kilde.AdditionalProperties, Reader,
                                         DrawnInTheHeader)
            : [];

    /// <summary>Keys the header renders itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Both spellings, since a kilde curates one or the other. Not <c>BeskrivelseEngelsk</c>: the
    /// ingress is the Norwegian one, so excluding it would delete a fact. (Fhi.Metadata-8yqoz)
    /// </remarks>
    private static readonly IReadOnlySet<string> DrawnInTheHeader =
        new HashSet<string>(StringComparer.Ordinal) { "Beskrivelse", "BeskrivelseFlerspraklig" };

    /// <summary>
    /// The facts every source has, which is why they are typed fields rather than curated properties.
    /// </summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words. Two of these are ours
    /// — the kildetype and the identification level are vocabularies this package translates — and
    /// the rest are stored once, in Norwegian, however the reader is reading.
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> SourceInformation =>
        Kilde is not { } kilde
            ? []
            : [
                (T.FacetKildeType, T.KildeTypeLabel(kilde.Kildetype, kilde.Kildetype), false),
                (T.FieldLegalBasis, kilde.LegalBasis, true),
                (T.FieldDataController, kilde.DataController, true),
                (T.FieldDataProcessor, kilde.DataProcessor, true),
                (T.FieldPersonIdentification, T.PersonIdentificationLabel(kilde.PersonIdentificationLevel), false),
                (T.FieldValidity, CatalogueDate.Period(kilde.ValidFrom, kilde.ValidTo, Language, T), false),
                (T.FieldLastUpdated, CatalogueDate.Day(kilde.LastUpdated, Language), false),
            ];

    /// <summary>Counts and dates, which belong to no language.</summary>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> Statistics =>
        Kilde is not { } kilde
            ? []
            : [
                (T.FieldTotalVariables, kilde.TotalVariables.ToString(), false),
                (T.FieldDataPeriod, CatalogueDate.Period(kilde.DataFrom, kilde.DataTo, Language, T), false),
            ];

    /// <summary>
    /// The heading for the datasamling section, when the explorer using this view wants a word of
    /// its own over it.
    /// </summary>
    /// <remarks>
    /// Neither explorer passes one: both take <see cref="DefaultDataCollectionsHeading"/>, which
    /// follows the source rather than the explorer. Kept for a host rendering this view directly
    /// with a word of its own.
    /// </remarks>
    [Parameter]
    public string? DataCollectionsHeading { get; set; }

    /// <summary>
    /// The section's own word for itself, which the source decides rather than the explorer: it
    /// draws the delkilder now, so the wording for a flat table promised none of them
    /// (Fhi.Metadata-wtz80).
    /// </summary>
    private string DefaultDataCollectionsHeading =>
        Delkilder.Count > 0 ? T.HeadingDelkilderAndDataCollections : T.HeadingDataCollections;

    /// <summary>
    /// Every datasamling the source holds, delkilder included — the count behind the heading, not
    /// what the view draws. <see cref="DataCollectionStructure"/> keeps each under its own delkilde.
    /// </summary>
    private IReadOnlyList<KildeDatasamling> DataCollections =>
        Kilde is { } kilde ? Ordered([.. Flatten(kilde)]) : [];

    /// <summary>The datasamlinger hanging directly off the source, in catalogue order.</summary>
    private IReadOnlyList<KildeDatasamling> DirectDataCollections =>
        Kilde is { } kilde ? Ordered(kilde.Datasamlinger) : [];

    /// <summary>The source's own delkilder, in catalogue order. Most sources have none.</summary>
    private IReadOnlyList<KildeDelkilde> Delkilder =>
        Kilde is { } kilde ? Ordered(kilde.Delkilder) : [];

    /// <summary>
    /// Where a top-level delkilde's name sits, so the heading outline walks the same tree the list
    /// draws. Stops at 6 with the outline.
    /// </summary>
    private int DelkildeLevel => Math.Min(BlockLevel + 1, 6);

    /// <summary>
    /// Catalogue order at every level: curated first, then the Norwegian alphabet. Two overloads
    /// because the records share the fields but no interface, and a selector argument is a place
    /// for one call site to sort by something else.
    /// </summary>
    private static IReadOnlyList<KildeDatasamling> Ordered(IReadOnlyList<KildeDatasamling> datasamlinger) =>
        [.. datasamlinger.OrderBy(d => d.PresentationOrder ?? int.MaxValue)
                         .ThenBy(d => d.Name, CatalogueProperties.CatalogueOrder)];

    /// <inheritdoc cref="Ordered(IReadOnlyList{KildeDatasamling})"/>
    private static IReadOnlyList<KildeDelkilde> Ordered(IReadOnlyList<KildeDelkilde> delkilder) =>
        [.. delkilder.OrderBy(d => d.PresentationOrder ?? int.MaxValue)
                     .ThenBy(d => d.Name, CatalogueProperties.CatalogueOrder)];

    private static IEnumerable<KildeDatasamling> Flatten(KildeDetail kilde) =>
        kilde.Datasamlinger.Concat(kilde.Delkilder.SelectMany(Flatten));

    private static IEnumerable<KildeDatasamling> Flatten(KildeDelkilde delkilde) =>
        delkilde.Datasamlinger.Concat(delkilde.Children.SelectMany(Flatten));

    /// <summary>
    /// The datasamlinger under the delkilde each belongs to. A real &lt;ul&gt;/&lt;li&gt;: it
    /// carries the relationship to a screen reader, where indentation carries it only to a sighted
    /// one, and a browser indents it unasked so it survives a host with no rule (Fhi.Metadata-wtz80).
    /// </summary>
    private RenderFragment DataCollectionStructure => builder =>
    {
        var seq = 0;
        var delkilder = Delkilder;

        if (delkilder.Count == 0)
        {
            DatasamlingTable.Render(builder, ref seq, DataCollections, T, Language, Reader);
            return;
        }

        DatasamlingTable.Render(builder, ref seq, DirectDataCollections, T, Language, Reader);
        DelkildeList(builder, ref seq, delkilder, DelkildeLevel);
    };

    /// <summary>
    /// One level of the tree. The name wears <c>headline-xxs</c> because Stiler's scale has nothing
    /// verified between it and the <c>headline-s</c> above; the heading LEVEL carries the depth.
    /// </summary>
    private void DelkildeList(RenderTreeBuilder builder, ref int seq,
                              IReadOnlyList<KildeDelkilde> delkilder, int level)
    {
        if (delkilder.Count == 0)
        {
            return;
        }

        builder.OpenElement(seq++, "ul");
        builder.AddAttribute(seq++, "class", "munin-explorer-kilde__delkilder");

        foreach (var delkilde in delkilder)
        {
            builder.OpenElement(seq++, "li");
            builder.AddAttribute(seq++, "class", "munin-explorer-kilde__delkilde");

            builder.OpenElement(seq++, $"h{level}");
            builder.AddAttribute(seq++, "class",
                                 "headline headline-xxs margin--none munin-explorer-kilde__delkilde-name");
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", Reader));
            builder.AddContent(seq++, delkilde.Name);
            builder.CloseElement();

            // The kilde's own identifier line, one level down and wearing the same name.
            if (Identifier(delkilde.Code, delkilde.ShortName) is { } identifiers)
            {
                builder.OpenElement(seq++, "p");
                builder.AddAttribute(seq++, "class", "caption margin--none munin-explorer-kilde__identifiers");
                builder.AddContent(seq++, identifiers);
                builder.CloseElement();
            }

            // The delkilde's own words, under its name line. Norwegian whatever the reader's
            // language, and authored with markdown links more often than any other field
            // (Fhi.Metadata-3osk6), which is what CatalogueMarkdown is here to draw.
            if (!string.IsNullOrWhiteSpace(delkilde.Description))
            {
                builder.OpenElement(seq++, "p");
                builder.AddAttribute(seq++, "class", "munin-explorer-kilde__delkilde-description");
                builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", Reader));
                builder.AddContent(seq++, CatalogueMarkdown.Render(delkilde.Description));
                builder.CloseElement();
            }

            DatasamlingTable.Render(builder, ref seq, Ordered(delkilde.Datasamlinger), T, Language, Reader);
            DelkildeList(builder, ref seq, Ordered(delkilde.Children), Math.Min(level + 1, 6));

            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
