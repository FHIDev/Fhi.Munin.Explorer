using System.Collections.ObjectModel;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A variable in full: the catalogue's metadata about it, what its data looks like, and a sidebar
/// saying where it lives.
/// </summary>
/// <remarks>
/// The sibling of <see cref="KildeView"/> and built the same way, for the same reason — a page-shaped
/// view that opens inside the component rather than at a route of its own, because this package has
/// no router and never will.
/// <para>
/// That was an open question on Fhi.Metadata-xbynn until both halves turned out to exist already:
/// <c>SelectedVariableId</c> has always been two-way, so a host mirrors it into its URL exactly as it
/// does search and sorting, and the drill-in pattern was proven by the kilde view. Neither was built
/// for this.
/// </para>
/// </remarks>
public sealed partial class VariableView : ComponentBase
{
    /// <summary>The variable to show. Nothing renders until this is set.</summary>
    [Parameter, EditorRequired]
    public VariableDetail? Variable { get; set; }

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
    /// Sections to place between the metadata and the statistics.
    /// </summary>
    /// <remarks>
    /// The kodeverk section arrives this way rather than being rebuilt here: the panel already draws
    /// it, and one section drawn twice is one section to fix twice.
    /// </remarks>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    // Unique per instance so two of these views on one page cannot collide on DOM ids, the same
    // reason VariableExplorer carries one. A host mounting a variable beside the one it replaced
    // is the case that makes it real: both views hold the same version ids.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>
    /// The catalogue's metadata about this variable, grouped as the catalogue groups it.
    /// </summary>
    /// <remarks>
    /// The same call the kilde view makes, unchanged. A variable's groups happen to be Beskrivelse,
    /// Personvern, Skjema, Teknisk and Synlighet where a source's are Datainnsamling, Juridisk and so
    /// on — but nothing here knows either list, which is the point of resolving them from the payload.
    /// </remarks>
    private IReadOnlyList<PropertyGroup> Groups =>
        Variable is { } variable
            ? CatalogueProperties.Groups(variable.PropertyMetadata, variable.AdditionalProperties, Reader,
                                         DrawnInTheSidebar)
            : [];

    /// <summary>Keys this view renders itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Just the one, and it earns its place: DataType is the only filled-in key in its group on a
    /// typical variable, so dropping it drops the group and leaves the five Runa shows.
    /// </remarks>
    private static readonly HashSet<string> DrawnInTheSidebar = new(StringComparer.Ordinal) { "DataType" };

    /// <summary>Where the variable lives: which source, under which name.</summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words, the same as the kilde
    /// view. The source's name and short name are stored once, in Norwegian; the kildetype is a
    /// vocabulary this package translates. Marking all three, or none, would be wrong either way.
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> SourceInformation =>
        Variable is not { } variable
            ? []
            : [
                (T.FieldKildeName, variable.KildeName, true),
                (T.FieldKildeShortName, variable.KildeShortName, true),
                (T.FacetKildeType, T.KildeTypeLabel(variable.KildeType, variable.KildeType), false),
            ];

    /// <summary>
    /// The statistics heading, which names the kind of statistics rather than just saying
    /// "Statistikk".
    /// </summary>
    /// <remarks>
    /// Runa writes "Statistikk (Årsbasert)". The kind matters to a reader deciding what the numbers
    /// mean: a yearly set is one row per year, and an accumulated one is a running total that only
    /// its last row describes.
    /// </remarks>
    private string StatisticsHeading =>
        Variable?.DatasamlingStatisticsType is { } type && !string.IsNullOrWhiteSpace(type)
            ? $"{T.HeadingStatistics} ({T.StatisticsTypeLabel(type)})"
            : T.HeadingStatistics;

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

    /// <summary>A definition list of label and value, skipping what the variable has not filled in.</summary>
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

    /// <summary>
    /// The statistics, as the table Runa draws: one row per set, described by the year it covers.
    /// </summary>
    /// <remarks>
    /// Only the summary columns, and deliberately: measured against Runa on 2026-08-21, it shows
    /// Minimum, Maksimum, Gjennomsnitt and Standardavvik and nothing else. The payload also carries
    /// MED, a median, and counts of valid and missing cases — Runa draws none of them, so neither
    /// does this. Adding a column Runa has not got would make the two disagree about the same data.
    /// <para>
    /// An absent number is a dash rather than a blank, so a reader can tell "not measured" from a
    /// cell that failed to draw.
    /// </para>
    /// </remarks>
    private RenderFragment StatisticsTable => builder =>
    {
        if (Variable is not { Statistics.Count: > 0 } variable)
        {
            return;
        }

        builder.OpenElement(0, "table");
        builder.AddAttribute(1, "class", "munin-explorer-statistics");

        builder.OpenElement(2, "thead");
        builder.OpenElement(3, "tr");
        HeaderCell(builder, 10, T.FieldYear);
        HeaderCell(builder, 20, T.FieldMinimum);
        HeaderCell(builder, 30, T.FieldMaximum);
        HeaderCell(builder, 40, T.FieldMean);
        HeaderCell(builder, 50, T.FieldStandardDeviation);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(4, "tbody");

        var seq = 100;

        foreach (var statistic in variable.Statistics)
        {
            // Null-coalesced although Statistic.AdditionalProperties is declared non-nullable — see
            // that declaration for how a null gets in, and NullAsEmptyCollections for what stops it
            // arriving from this package's own client. A host can substitute that client, and
            // unguarded one such statistic throws while rendering, past the try/catch around the
            // fetch, which on a Blazor Server host takes the circuit and the page it is mounted in
            // down. Read as the empty bag it means, the row draws the dash Value already gives a
            // key the catalogue holds no number for.
            var props = statistic.AdditionalProperties ?? ReadOnlyDictionary<string, string?>.Empty;

            builder.OpenElement(seq, "tr");

            // The year heads its own row: every other cell is a number about that year, and a
            // screen reader reading one out of context should hear which year it belongs to.
            builder.OpenElement(seq + 1, "th");
            builder.AddAttribute(seq + 2, "scope", "row");
            builder.AddContent(seq + 3, Value(props, "SisteOppdaterteAarssett"));
            builder.CloseElement();

            Cell(builder, seq + 10, Value(props, "MIN"));
            Cell(builder, seq + 20, Value(props, "MAX"));
            Cell(builder, seq + 30, Value(props, "AVG"));
            Cell(builder, seq + 40, Value(props, "STD"));

            builder.CloseElement();
            seq += 100;
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    /// <summary>A statistic's value, or a dash where the catalogue holds none.</summary>
    private static string Value(IReadOnlyDictionary<string, string?> properties, string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "—";

    private static void HeaderCell(RenderTreeBuilder builder, int seq, string label)
    {
        builder.OpenElement(seq, "th");
        builder.AddAttribute(seq + 1, "scope", "col");
        builder.AddContent(seq + 2, label);
        builder.CloseElement();
    }

    private static void Cell(RenderTreeBuilder builder, int seq, string? value)
    {
        builder.OpenElement(seq, "td");
        builder.AddContent(seq + 1, value);
        builder.CloseElement();
    }

    /// <summary>A date as the day it fell on, with the month abbreviated.</summary>
    /// <remarks>
    /// Not the panel's <c>MonthYear</c>, whose name this borrowed while changing the format under
    /// it. The two are answering different questions and Runa writes them differently: the panel's
    /// period bar spans years and is drawn to be compared across rows, so a month and a year is as
    /// much as it can use; this is one variable's validity window, where the day is the point.
    /// <para>
    /// The month is abbreviated rather than spelled out as the kilde view spells it, which is what
    /// Runa does here too — this sidebar is 320px, and "20. september 2022 – 9. november 2022" wraps
    /// in it where the short form does not.
    /// </para>
    /// </remarks>
    private string Day(DateTimeOffset date) =>
        date.ToString("d. MMM yyyy", CatalogueProperties.Culture(Language));

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
