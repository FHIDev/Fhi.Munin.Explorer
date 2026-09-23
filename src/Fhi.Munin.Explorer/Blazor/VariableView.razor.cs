using System.Collections.ObjectModel;
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
    /// Sections to place between the metadata and the version history. Each is drawn under a heading
    /// at the level of this view's own blocks and listed in the contents nav.
    /// </summary>
    /// <remarks>
    /// The kodeverk section arrives this way rather than being rebuilt here: the panel already draws
    /// it, and one section drawn twice is one section to fix twice.
    /// </remarks>
    [Parameter]
    public IReadOnlyList<DetailNamedSection>? NamedSections { get; set; }

    /// <summary>
    /// Markup to place after <see cref="NamedSections"/>, drawn as given.
    /// </summary>
    /// <remarks>
    /// Not listed in the contents nav, because this view cannot see an id or a heading inside a
    /// fragment. Use <see cref="NamedSections"/> for a section the nav should offer.
    /// </remarks>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    /// <inheritdoc cref="KildeView.Trail"/>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <inheritdoc cref="VariableSearch.InstrumentHref"/>
    [Parameter]
    public Func<Guid, string>? InstrumentHref { get; set; }

    /// <inheritdoc cref="KildeView.Actions"/>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    // Unique per instance so two of these views on one page cannot collide on DOM ids, the same
    // reason VariableSearch carries one. A host mounting a variable beside the one it replaced
    // is the case that makes it real: both views hold the same version ids.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>
    /// The name the page's heading carries, resolved through the member the heading reads so the
    /// sticky bar and the name block cannot say it in different words.
    /// </summary>
    private (string Text, bool Norwegian) StickyNamed => T.Named(Variable?.PreferredTerm, Variable?.Code);

    private string? StickyNameLang => CatalogueProperties.Foreign(StickyNamed.Norwegian, Reader);

    /// <summary>The code beside the bar's name, on the name block's own terms.</summary>
    /// <remarks>Nothing where the heading has already fallen back to the code. (Fhi.Metadata-w13lk)</remarks>
    private string? StickyCode => StickyNamed.Norwegian ? Variable?.Code : null;

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
            ? CatalogueProperties.Groups(variable.PropertyMetadata, Values, Reader, DrawnElsewhere,
                                         CompleteRecord.Values(variable, Values))
            : [];

    /// <inheritdoc cref="KildeView.CompleteRecordFacts"/>
    private CompleteRecordExtras CompleteRecordFacts =>
        new(T.CompleteRecordLeadVariable, [(T.FieldDataPeriod, DataPeriod, false)]);

    /// <summary>
    /// The payload's curated bag with the variable's own columns merged in — see
    /// <see cref="CatalogueColumns"/> for why a column-backed value has to be put there at all.
    /// </summary>
    private IReadOnlyDictionary<string, string?> Values =>
        Variable is { } variable
            ? CatalogueColumns.Values(variable)
            : ReadOnlyDictionary<string, string?>.Empty;

    /// <summary>Keys this view renders itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// DataType earns its place twice over: it is the only filled-in key in its group on a typical
    /// variable, so dropping it drops the group and leaves the five Runa shows. Beskrivelse is the
    /// ingress under the name, which is where a variable's description has always been read — the
    /// same call <see cref="KildeView"/> makes for the same field (Fhi.Metadata-bct95). PreferredTerm
    /// is the page title (Fhi.Metadata-zg89n).
    /// </remarks>
    private static readonly IReadOnlySet<string> DrawnElsewhere =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "DataType", CatalogueColumns.Description, CatalogueColumns.PreferredTerm,
        };

    /// <summary>Where the variable sits in the catalogue, as the open row's panel draws it.</summary>
    /// <remarks>
    /// Derived once in <see cref="OnParametersSet"/> and held, because the contents nav asks the
    /// same question as the block: the entry and the section it points at have to be answered by
    /// one value rather than by two builds of it.
    /// </remarks>
    private IReadOnlyList<KildeTrailBlock.Crumb> Placement { get; set; } = [];

    /// <summary>The datasamlinger this view lists, which is the set the trail's last step counts.</summary>
    /// <remarks>
    /// <see cref="KildeTrailBlock.NamedDatasamlinger"/> rather than the payload's own list, because
    /// the trail counts what that predicate answers: a list built off anything else stands under a
    /// count of some other number, and an unnamed datasamling draws an empty bullet besides.
    /// </remarks>
    private IReadOnlyList<DatasamlingReference> Datasamlinger { get; set; } = [];

    /// <summary>The variabelgrupper this view lists, which is the set its contents entry answers to.</summary>
    /// <remarks>
    /// <see cref="KildeTrailBlock.NamedVariabelgrupper"/> rather than the payload's own list, the
    /// same bargain <see cref="Datasamlinger"/> makes: an unnamed group drew an empty bullet, and a
    /// payload naming none of them drew a heading and a contents entry over nothing.
    /// </remarks>
    private IReadOnlyList<VariabelgruppeReference> Variabelgrupper { get; set; } = [];

    /// <summary>The instruments this view lists, which is what its contents entry answers to.</summary>
    /// <remarks>
    /// Read straight off the payload, unlike <see cref="Datasamlinger"/> and
    /// <see cref="Variabelgrupper"/> beside it, which are derived.
    /// </remarks>
    private IReadOnlyList<InstrumentReference> Instruments => Variable?.Instruments ?? [];

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
                (T.FacetKildeType,
                 string.IsNullOrWhiteSpace(variable.KildeType)
                     ? null
                     : T.KildeTypeLabel(variable.KildeType, variable.KildeType),
                 false),
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
        && CatalogueProperties.Row(variable.PropertyMetadata, Values, Reader, key)
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

    private IReadOnlySet<string> DrawnIds { get; set; } = new HashSet<string>();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var variable = Variable;

        // The kildetype is passed no facet name, and that is this view's answer rather than an
        // omission: it holds no facet payload, so the trail falls back to the shipped table — the
        // reading Kildeinformasjon below already uses, so the page cannot name one kildetype twice.
        Placement = variable is null ? [] : KildeTrailBlock.Steps(variable, T, kildeTypeApiName: null);
        Datasamlinger = variable is null ? [] : KildeTrailBlock.NamedDatasamlinger(variable);
        Variabelgrupper = variable is null ? [] : KildeTrailBlock.NamedVariabelgrupper(variable);

        var toc = BuildToc();

        Toc = toc.Entries;
        DrawnIds = toc.Drawn;
    }

    /// <summary>This view's own predicates, which are what the nav and the blocks both read.</summary>
    private DetailTocBuilder BuildToc()
    {
        if (Variable is not { } variable)
        {
            return new();
        }

        DetailTocBuilder toc = new();

        toc.Add(Groups.Count > 0, DetailSectionIds.Metadata, T.HeadingMetadata);
        toc.AddNamed(NamedSections);
        toc.Add(Versions.Count > 0, DetailSectionIds.Versions, T.HeadingVersionHistory);
        toc.Add(StatisticsBlock.AnyStatistics(variable), DetailSectionIds.Statistics, StatisticsHeading);
        toc.Add(Placement.Count > 0, DetailSectionIds.Placement, T.GroupPlacement);
        toc.Add(DetailBlocks.AnyFacts(SourceInformation), DetailSectionIds.Source, T.HeadingSourceInformation);
        toc.Add(DataPeriod is not null, DetailSectionIds.DataPeriod, T.FieldDataPeriod);
        toc.Add(DataTypeLabel is not null, DetailSectionIds.DataType, T.FieldDataType);
        toc.Add(Variabelgrupper.Count > 0, DetailSectionIds.VariableGroups, T.FieldVariableGroups);
        toc.Add(Instruments.Count > 0, DetailSectionIds.Instruments, T.FieldInstruments);
        toc.Add(Datasamlinger.Count > 0, DetailSectionIds.DataCollections, T.HeadingDataCollections);

        return toc;
    }

    /// <summary>Whether this view's own block is drawn; a named section never switches one on.</summary>
    private bool Drawn(string id) => DrawnIds.Contains(id);
}
