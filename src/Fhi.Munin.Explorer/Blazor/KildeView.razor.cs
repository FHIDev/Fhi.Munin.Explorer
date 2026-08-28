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
/// name block, same eight metadata groups, same two sidebar boxes. Kelda then adds sections Runa
/// does not have, and calls its hierarchy section something else.
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
    /// Kelda passes its datasamling hierarchy, variables, access criteria and prices here. Runa
    /// passes its own datasamling section. Neither is named in this component.
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
            ? CatalogueProperties.Groups(kilde.PropertyMetadata, kilde.AdditionalProperties, Reader)
            : [];

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
                (T.FieldValidity, Period(kilde.ValidFrom, kilde.ValidTo), false),
                (T.FieldLastUpdated, Day(kilde.LastUpdated), false),
            ];

    /// <summary>Counts and dates, which belong to no language.</summary>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> Statistics =>
        Kilde is not { } kilde
            ? []
            : [
                (T.FieldTotalVariables, kilde.TotalVariables.ToString(), false),
                (T.FieldDataPeriod, Period(kilde.DataFrom, kilde.DataTo), false),
            ];

    /// <summary>
    /// The heading for the datasamling section, when the explorer using this view wants a word of
    /// its own over it.
    /// </summary>
    /// <remarks>
    /// Kelda passes "Delkilder og datasamlinger" unconditionally, which is what Munin's own Kelda
    /// says. Runa passes nothing and takes <see cref="DefaultDataCollectionsHeading"/>, which now
    /// follows the source rather than the explorer.
    /// </remarks>
    [Parameter]
    public string? DataCollectionsHeading { get; set; }

    /// <summary>
    /// What the section is called when the caller does not say: "Delkilder og datasamlinger" over a
    /// source that has delkilder, "Datasamlinger" over one that has not.
    /// </summary>
    /// <remarks>
    /// It followed the explorer until the section started drawing the delkilde tree — Runa said
    /// "Datasamlinger" and Kelda said "Delkilder og datasamlinger" over identical rows, which was a
    /// difference of one word over one flat table and not worth a parameter's worth of argument.
    /// <para>
    /// It is not that any more. The section now draws the delkilder themselves, so on Tromsø the
    /// Runa wording headed five waves and promised none of them — the heading was describing the
    /// old section. Which of the two words is right is a question about the SOURCE, not about who
    /// is rendering it, so the answer comes from the source: a kilde with no delkilder still says
    /// "Datasamlinger", because there is nothing else under it to name.
    /// </para>
    /// </remarks>
    private string DefaultDataCollectionsHeading =>
        Delkilder.Count > 0 ? T.HeadingDelkilderAndDataCollections : T.HeadingDataCollections;

    /// <summary>
    /// Every datasamling the source holds, including those hanging off a delkilde.
    /// </summary>
    /// <remarks>
    /// Not what the view draws — that is <see cref="DataCollectionStructure"/>, which keeps each one
    /// under the delkilde it belongs to. This is the count behind the section's heading: a source
    /// whose datasamlinger all hang under delkilder still has some, and a section that drew no
    /// heading for them would be wrong about it.
    /// </remarks>
    private IReadOnlyList<KildeDatasamling> DataCollections =>
        Kilde is { } kilde ? Ordered([.. Flatten(kilde)]) : [];

    /// <summary>The datasamlinger hanging directly off the source, in catalogue order.</summary>
    private IReadOnlyList<KildeDatasamling> DirectDataCollections =>
        Kilde is { } kilde ? Ordered(kilde.Datasamlinger) : [];

    /// <summary>The source's own delkilder, in catalogue order. Most sources have none.</summary>
    private IReadOnlyList<KildeDelkilde> Delkilder =>
        Kilde is { } kilde ? Ordered(kilde.Delkilder) : [];

    /// <summary>
    /// The level a top-level delkilde's name lands on — under the section heading above it, and one
    /// deeper for every level of the tree, so navigating the page by heading walks the same tree the
    /// list draws. Both stop at 6, which is where the outline stops.
    /// </summary>
    private int DelkildeLevel => Math.Min(BlockLevel + 1, 6);

    /// <summary>
    /// Catalogue order: the curated order first for whatever has one, then the Norwegian alphabet
    /// for the rest — the same two rules, and the same comparer, at every level of the tree.
    /// </summary>
    /// <remarks>
    /// Two overloads rather than one generic. The two records share the pair of fields but no
    /// interface declaring it, so a generic would need a selector argument at each call site, and a
    /// selector argument is a place for one call site to sort by something else.
    /// </remarks>
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
    /// The datasamlinger, arranged the way the catalogue arranges them rather than flattened into
    /// one table.
    /// </summary>
    /// <remarks>
    /// A source with no delkilder has nothing to nest, and gets the single table this view has
    /// always drawn — which is most sources, and the case where the arranged view and the flat one
    /// are the same picture. A study series gets that table for its own datasamlinger and then a
    /// list, one item per delkilde, each carrying what hangs off it and any delkilder below it.
    /// Tromsø's organising fact is its waves: a flat table of all fourteen answers what the study
    /// holds while destroying how it is arranged, which is usually the question a study series is
    /// asked.
    /// <para>
    /// A real <c>&lt;ul&gt;</c> of real <c>&lt;li&gt;</c>, and deliberately not indented
    /// <c>&lt;div&gt;</c>s. A list carries the parent/child relationship to a screen reader by
    /// itself; indentation carries it to a sighted reader only, and an automated accessibility
    /// check is blind to structure that was never marked up, so it would call the second one green.
    /// <c>role="tree"</c> would carry it as well and would oblige this component to implement the
    /// full arrow-key contract that goes with it — a different piece of work, not a spelling of
    /// this one. The browser's own list indentation is the other half of the choice: the structure
    /// still reads on a host that has no rule for any of the names below, where a stack of divs
    /// would collapse into one undifferentiated column.
    /// </para>
    /// </remarks>
    private RenderFragment DataCollectionStructure => builder =>
    {
        var seq = 0;
        var delkilder = Delkilder;

        if (delkilder.Count == 0)
        {
            CollectionTable(builder, ref seq, DataCollections);
            return;
        }

        CollectionTable(builder, ref seq, DirectDataCollections);
        DelkildeList(builder, ref seq, delkilder, DelkildeLevel);
    };

    /// <summary>The datasamlinger of one level of the tree, as the table Runa shows.</summary>
    /// <remarks>
    /// Drawn once per level, and each one keeps its own <c>thead</c>. A table is what associates a
    /// cell with its column heading for a screen reader, so a second table borrowing the first
    /// one's headings is a table with none — the repetition is what buys every level being
    /// readable on its own.
    /// </remarks>
    private void CollectionTable(RenderTreeBuilder builder, ref int seq, IReadOnlyList<KildeDatasamling> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        builder.OpenElement(seq++, "table");
        builder.AddAttribute(seq++, "class", "munin-explorer-kilde__datasamlinger");

        builder.OpenElement(seq++, "thead");
        builder.OpenElement(seq++, "tr");
        HeaderCell(builder, ref seq, T.FieldName);
        HeaderCell(builder, ref seq, T.FieldDescription);
        HeaderCell(builder, ref seq, T.FieldValidity);
        HeaderCell(builder, ref seq, T.FieldTotalVariables);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(seq++, "tbody");

        foreach (var row in rows)
        {
            builder.OpenElement(seq++, "tr");

            // The name is a th, not a td: it is what the rest of the row is about, and a screen
            // reader reading a cell out of context should hear which datasamling it belongs to.
            builder.OpenElement(seq++, "th");
            builder.AddAttribute(seq++, "scope", "row");
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", Reader));
            builder.AddContent(seq++, string.IsNullOrWhiteSpace(row.ShortName)
                ? row.Name
                : $"{row.Name} ({row.ShortName})");
            builder.CloseElement();

            Cell(builder, ref seq, row.Description, norwegian: true);
            Cell(builder, ref seq, Period(row.EffectiveValidFrom, row.EffectiveValidTo), norwegian: false);
            Cell(builder, ref seq, $"{row.VariableCount} {T.VariableCountSuffix}", norwegian: false);

            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>One level of the delkilde tree: a list item per delkilde, recursing into its own.</summary>
    /// <remarks>
    /// The name wears <c>headline-xxs</c> rather than a size between it and the <c>headline-s</c>
    /// of the section heading above it, because Stiler's scale has nothing verified in that gap —
    /// the same reason the block headings are the size they are. The heading LEVEL carries the
    /// depth instead, which is what a screen reader navigates the page by anyway.
    /// </remarks>
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

            // The same line the kilde's own name block carries, wearing the same name, because it is
            // the same thing one level down: what a reader looks this level up by elsewhere.
            if (Identifier(delkilde.Code, delkilde.ShortName) is { } identifiers)
            {
                builder.OpenElement(seq++, "p");
                builder.AddAttribute(seq++, "class", "caption margin--none munin-explorer-kilde__identifiers");
                builder.AddContent(seq++, identifiers);
                builder.CloseElement();
            }

            // The delkilde's own beskrivelse is deliberately not drawn, though the contract carries
            // one. Read what the catalogue actually stores there before adding it: in the Tromsø
            // payload it is the name again for K_TR.BIODATA, and a bare markdown link whose text is
            // the name again for each of the five waves. This view renders text rather than
            // markdown — the kilde's own description already shows its <br> tags as words — so a
            // description here would put "[Tromsø4 - The Fourth Tromsø Study](https://uit.no/...)"
            // beside every wave, which is a visible fault rather than information.

            CollectionTable(builder, ref seq, Ordered(delkilde.Datasamlinger));
            DelkildeList(builder, ref seq, Ordered(delkilde.Children), Math.Min(level + 1, 6));

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private static void HeaderCell(RenderTreeBuilder builder, ref int seq, string label)
    {
        builder.OpenElement(seq++, "th");
        builder.AddAttribute(seq++, "scope", "col");
        builder.AddContent(seq++, label);
        builder.CloseElement();
    }

    private void Cell(RenderTreeBuilder builder, ref int seq, string? value, bool norwegian)
    {
        builder.OpenElement(seq++, "td");

        if (norwegian)
        {
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", Reader));
        }

        builder.AddContent(seq++, value);
        builder.CloseElement();
    }

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

    /// <summary>A definition list of label and value, skipping anything the source has not filled in.</summary>
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
    /// The dot is not a separator, it is what makes the number an ordinal in Norwegian — "1." is
    /// "first". English writes the same date "1 January 2026" with no dot at all, so the pattern
    /// has to follow the reader rather than the culture merely supplying month names to a Norwegian
    /// skeleton. The culture's own long pattern is not usable here either: for English it leads with
    /// the weekday, which is more than a metadata field needs.
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
