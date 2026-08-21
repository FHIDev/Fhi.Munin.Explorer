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

    private string Reader => CatalogueProperties.Reader(Language);

    /// <summary>The level for the two block headings, and for each metadata group under them.</summary>
    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>
    /// The identifier line under the name: <c>K_ALS (ALS)</c>, or just the code when there is no
    /// short name to put beside it.
    /// </summary>
    private string? Identifiers
    {
        get
        {
            if (Kilde is not { } kilde || string.IsNullOrWhiteSpace(kilde.Code))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(kilde.ShortName)
                ? kilde.Code
                : $"{kilde.Code} ({kilde.ShortName})";
        }
    }

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
    /// The heading for the datasamling section, when the explorer using this view calls it
    /// something else.
    /// </summary>
    /// <remarks>
    /// Runa says "Datasamlinger"; Kelda says "Delkilder og datasamlinger" over the same data. That
    /// is a difference of one word, and one word is not worth a second table.
    /// </remarks>
    [Parameter]
    public string? DataCollectionsHeading { get; set; }

    /// <summary>
    /// Every datasamling the source holds, including those hanging off a delkilde.
    /// </summary>
    /// <remarks>
    /// A source's datasamlinger are not a flat list. They hang off the source directly and off each
    /// delkilde, nested to any depth, and a reader asking what data a source holds wants all of them
    /// — a delkilde is how the catalogue is organised, not a reason to hide what is inside it.
    /// </remarks>
    private IReadOnlyList<KildeDatasamling> DataCollections =>
        Kilde is { } kilde
            ? [.. Flatten(kilde).OrderBy(d => d.PresentationOrder ?? int.MaxValue)
                                .ThenBy(d => d.Name, CatalogueProperties.CatalogueOrder)]
            : [];

    private static IEnumerable<KildeDatasamling> Flatten(KildeDetail kilde) =>
        kilde.Datasamlinger.Concat(kilde.Delkilder.SelectMany(Flatten));

    private static IEnumerable<KildeDatasamling> Flatten(KildeDelkilde delkilde) =>
        delkilde.Datasamlinger.Concat(delkilde.Children.SelectMany(Flatten));

    /// <summary>The datasamlinger, as the table Runa shows.</summary>
    private RenderFragment DataCollectionTable => builder =>
    {
        var rows = DataCollections;

        if (rows.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "table");
        builder.AddAttribute(1, "class", "variable-explorer-kilde__datasamlinger");

        builder.OpenElement(2, "thead");
        builder.OpenElement(3, "tr");
        HeaderCell(builder, 10, T.FieldName);
        HeaderCell(builder, 20, T.FieldDescription);
        HeaderCell(builder, 30, T.FieldValidity);
        HeaderCell(builder, 40, T.FieldTotalVariables);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(4, "tbody");

        var seq = 100;

        foreach (var row in rows)
        {
            builder.OpenElement(seq, "tr");

            // The name is a th, not a td: it is what the rest of the row is about, and a screen
            // reader reading a cell out of context should hear which datasamling it belongs to.
            builder.OpenElement(seq + 1, "th");
            builder.AddAttribute(seq + 2, "scope", "row");
            builder.AddAttribute(seq + 3, "lang", CatalogueProperties.Foreign("no", Reader));
            builder.AddContent(seq + 4, string.IsNullOrWhiteSpace(row.ShortName)
                ? row.Name
                : $"{row.Name} ({row.ShortName})");
            builder.CloseElement();

            Cell(builder, seq + 10, row.Description, norwegian: true);
            Cell(builder, seq + 20, Period(row.EffectiveValidFrom, row.EffectiveValidTo), norwegian: false);
            Cell(builder, seq + 30, $"{row.VariableCount} {T.VariableCountSuffix}", norwegian: false);

            builder.CloseElement();
            seq += 100;
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    private static void HeaderCell(RenderTreeBuilder builder, int seq, string label)
    {
        builder.OpenElement(seq, "th");
        builder.AddAttribute(seq + 1, "scope", "col");
        builder.AddContent(seq + 2, label);
        builder.CloseElement();
    }

    private void Cell(RenderTreeBuilder builder, int seq, string? value, bool norwegian)
    {
        builder.OpenElement(seq, "td");

        if (norwegian)
        {
            builder.AddAttribute(seq + 1, "lang", CatalogueProperties.Foreign("no", Reader));
        }

        builder.AddContent(seq + 2, value);
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
        builder.AddAttribute(1, "class", "variable-meta__grid");

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
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none variable-explorer-group");
        builder.AddAttribute(2, "lang", CatalogueProperties.Foreign(group.NameLanguage, Reader));
        builder.AddContent(3, group.Name);
        builder.CloseElement();

        builder.OpenElement(4, "dl");
        builder.AddAttribute(5, "class", "variable-meta__grid");

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
