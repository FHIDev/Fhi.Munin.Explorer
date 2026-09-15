using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A variable in full: the catalogue's metadata about it, what its data looks like, and the blocks
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

    /// <inheritdoc cref="VariableSearch.Language"/>
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

    /// <inheritdoc cref="KildeView.Trail"/>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <inheritdoc cref="KildeView.Actions"/>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    // Unique per instance so two of these views on one page cannot collide on DOM ids, the same
    // reason VariableSearch carries one. A host mounting a variable beside the one it replaced
    // is the case that makes it real: both views hold the same version ids.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The trail the chassis draws — see <see cref="DetailTrail.Append"/> for the rule.</summary>
    private IReadOnlyList<DetailTrailStep>? PageTrail =>
        Variable is { } variable
            ? DetailTrail.Append(Trail, T.Named(variable.PreferredTerm, variable.Code), Reader)
            : null;

    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>The month this view abbreviates, because the version list's date columns are narrow.</summary>
    /// <remarks>
    /// One width for both places a date is written, and the version list is the one that needs it:
    /// two dates as two columns beside a name and a badge. The data period was narrowed for the
    /// 320px rail it used to sit in and keeps the form (Fhi.Metadata-n39ea).
    /// </remarks>
    private const DateWidth Dates = DateWidth.Narrow;

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
                                         DrawnElsewhere)
            : [];

    /// <summary>Keys this view renders itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Just the one, and it earns its place: DataType is the only filled-in key in its group on a
    /// typical variable, so dropping it drops the group and leaves the five Runa shows.
    /// </remarks>
    private static readonly HashSet<string> DrawnElsewhere = new(StringComparer.Ordinal) { "DataType" };

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

    /// <summary>The years this variable's data covers, in words, or null when the catalogue has neither end.</summary>
    /// <remarks>
    /// A property rather than a pattern match in the markup, because the contents nav asks the same
    /// question: the block is drawn exactly when there is a period to put in it.
    /// </remarks>
    private string? DataPeriod =>
        Variable is { } variable
            ? CatalogueDate.Period(variable.DataFrom, variable.DataTo, Language, T, Dates)
            : null;

    /// <summary>This variable's data type in the reader's language, or null when the catalogue names none.</summary>
    /// <remarks>
    /// The same shape as <see cref="DataPeriod"/>, and for the same reason: the block is drawn
    /// exactly when there is a label to put in it, and the contents nav asks that same question.
    /// </remarks>
    private string? DataTypeLabel =>
        Variable is { DataType: { } dataType } && !string.IsNullOrWhiteSpace(dataType)
            ? T.DataTypeLabel(dataType)
            : null;

    // The catalogue's own keys for the three curated properties the hero row leads with. Named here
    // because a hero fact has to be chosen; their labels, words and order are still the payload's.
    private const string OriginKey = "Opprinnelse";
    private const string IdentificationKey = "Identifiseringsgrad";
    private const string DatabaseReferenceKey = "DatabaseReferanse";

    /// <summary>
    /// The six facts a variable leads with, in the order the strip reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kilde and Datasamling are deliberately not among them although the strip this replaces led
    /// with both: the breadcrumb directly above names them, and a fact strip that repeats the
    /// chrome spends two of six slots on facts the reader has just read (Fhi.Metadata-l9l2n.92).
    /// Kodeverk, Statistikk, Opprinnelse, Identifiseringsgrad and Databasereferanse are that bead's
    /// own five; Dataperiode is the sixth, and it passes the same test — it is in no crumb.
    /// </para>
    /// <para>
    /// Every one is drawn again below, and none of the three curated keys joins
    /// <see cref="DrawnElsewhere"/> on account of being here: that set exists for the same fact
    /// drawn twice in the body under two labels, which is what DataType was, and a hero row is a
    /// different register. What must not differ is the wording, so the three keys resolve through
    /// <see cref="CatalogueProperties.Row"/> — the call their groups are built from — rather than
    /// off the bag.
    /// </para>
    /// <para>
    /// A variable with several kodeverk leads with the first and the kind it is; the section below
    /// is what lists them all.
    /// </para>
    /// </remarks>
    private IReadOnlyList<DetailFact> HeroFacts =>
        Variable is null
            ? []
            : [.. new DetailFact?[]
                {
                    KodeverkFact,
                    StatisticsFact,
                    Curated(OriginKey),
                    Curated(IdentificationKey),
                    Curated(DatabaseReferenceKey),
                    new DetailFact(T.FieldDataPeriod, DataPeriod),
                }.OfType<DetailFact>()];

    /// <summary>One curated property as a hero fact, or null where the catalogue holds none.</summary>
    /// <remarks>
    /// Label and value both come off the resolved row, so a curated property renamed or
    /// re-translated in Munin moves here and in the group below it together.
    /// </remarks>
    private DetailFact? Curated(string key) =>
        Variable is { } variable
        && CatalogueProperties.Row(variable.PropertyMetadata, variable.AdditionalProperties, Reader, key)
            is { Values: [var first, ..] } row
            ? new DetailFact(row.Label, first.Text, CatalogueProperties.Foreign(first.Language, Reader))
            : null;

    /// <summary>The first kodeverk the variable draws its values from, and the kind of list it is.</summary>
    private DetailFact? KodeverkFact =>
        Variable?.KodeverkLinks is [{ } link, ..]
            ? new DetailFact(
                T.HeadingKodeverk,
                string.IsNullOrWhiteSpace(link.DisplayName) ? T.KodeverkUnnamed : link.DisplayName,
                CatalogueProperties.Foreign(!string.IsNullOrWhiteSpace(link.DisplayName), Reader),
                Note: T.KodeverkTypeLabel(link.KodeverkType))
            : null;

    /// <summary>
    /// Which kind of statistics the variable has, or null where it has none to have a kind of.
    /// </summary>
    /// <remarks>
    /// The label and the word are the two halves <see cref="StatisticsBlock.Heading"/> joins into
    /// the heading below, read off the same two members rather than spelled again.
    /// </remarks>
    private DetailFact? StatisticsFact =>
        Variable is { DatasamlingStatisticsType: { } type }
        && !string.IsNullOrWhiteSpace(type)
        && StatisticsBlock.AnyStatistics(Variable)
            ? new DetailFact(T.HeadingStatistics, T.StatisticsTypeLabel(type))
            : null;

    /// <summary>The heading over the statistics block, which the nav has to name without drawing it.</summary>
    /// <remarks>
    /// Read off <see cref="StatisticsBlock"/> rather than rebuilt here, the same way
    /// <see cref="DatasamlingView.StatisticsHeading"/> is: the block emits this exact string, and
    /// two spellings of it would be two spellings of one fact.
    /// </remarks>
    private string StatisticsHeading => StatisticsBlock.HeadingFor(Variable, T);

    /// <summary>The sections this view draws, in the order it draws them.</summary>
    private IReadOnlyList<DetailTocEntry> Toc { get; set; } = [];

    /// <inheritdoc />
    protected override void OnParametersSet() => Toc = BuildToc();

    /// <summary>This view's own predicates, which are what the nav and the blocks both read.</summary>
    /// <remarks>
    /// The kodeverk section arrives through <see cref="Sections"/> and carries no id of its own, so
    /// the nav does not offer it — this view never learns what the explorer put there.
    /// </remarks>
    private IReadOnlyList<DetailTocEntry> BuildToc()
    {
        if (Variable is not { } variable)
        {
            return [];
        }

        DetailTocBuilder toc = new();

        toc.Add(Groups.Count > 0, DetailSectionIds.Metadata, T.HeadingMetadata);
        toc.Add(Versions.Count > 0, DetailSectionIds.Versions, T.HeadingVersionHistory);
        toc.Add(StatisticsBlock.AnyStatistics(variable), DetailSectionIds.Statistics, StatisticsHeading);
        toc.Add(DetailBlocks.AnyFacts(SourceInformation), DetailSectionIds.Source, T.HeadingSourceInformation);
        toc.Add(DataPeriod is not null, DetailSectionIds.DataPeriod, T.FieldDataPeriod);
        toc.Add(DataTypeLabel is not null, DetailSectionIds.DataType, T.FieldDataType);
        toc.Add(variable.AllVariabelgrupper.Count > 0, DetailSectionIds.VariableGroups, T.FieldVariableGroups);
        toc.Add(variable.AllDatasamlinger.Count > 0, DetailSectionIds.DataCollections, T.HeadingDataCollections);

        return toc.Entries;
    }

    /// <summary>Whether the section with this id is drawn, which is whether the nav names it.</summary>
    private bool Drawn(string id) => Toc.Contains(id);
}
