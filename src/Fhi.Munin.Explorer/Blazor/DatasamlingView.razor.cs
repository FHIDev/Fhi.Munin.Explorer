using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A datasamling in full: what it is called, the catalogue's own metadata about it, who it
/// includes, and who owns the data and how much of it there is.
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
    /// Sections to place after this view's own blocks, for the explorer that owns them. Each is drawn
    /// under a heading at the level of those blocks and listed in the contents nav.
    /// </summary>
    /// <remarks>
    /// The same slot <see cref="KildeView.NamedSections"/> is, and for the same reason: the explorers
    /// differ in what they add around a shared core, and a flag per difference would make this the
    /// one place they leak into each other. Neither explorer passes any.
    /// </remarks>
    [Parameter]
    public IReadOnlyList<DetailNamedSection>? NamedSections { get; set; }

    /// <inheritdoc cref="VariableView.Sections"/>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    /// <inheritdoc cref="KildeView.Trail"/>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <inheritdoc cref="KildeView.Actions"/>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The trail the chassis draws — see <see cref="DetailTrail.Append"/> for the rule.</summary>
    private IReadOnlyList<DetailTrailStep>? PageTrail =>
        Datasamling is { } datasamling
            ? DetailTrail.Append(Trail, T.Named(datasamling.PreferredTerm, datasamling.Code), Reader)
            : null;

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

    private bool _resolved;
    private DatasamlingDetail? _resolvedDatasamling;
    private string? _resolvedLanguage;
    private IReadOnlyList<PropertyGroup> _groups = [];
    private CataloguePlacement _placement = CataloguePlacement.None;

    /// <summary>The catalogue's metadata, grouped and ordered as the catalogue arranges it.</summary>
    /// <remarks>
    /// Beskrivelse is named as drawn elsewhere exactly when the ingress above draws it, which is
    /// this view's own rule: a description that only repeats the name is not drawn there, and
    /// suppressing the section's row as well would lose it altogether.
    /// </remarks>
    private IReadOnlyList<PropertyGroup> Groups
    {
        get
        {
            Resolve();

            return _groups;
        }
    }

    /// <summary>
    /// The placement questions this page asks: the payload's curated bag with the collection's own
    /// columns merged in — see <see cref="CatalogueColumns"/> for why a column-backed value has to
    /// be put there at all — and this view's own suppressions.
    /// </summary>
    /// <remarks>
    /// Read into a local wherever two questions are asked about one fact, so the pair is answered
    /// over one payload — see <see cref="CataloguePlacement"/>.
    /// </remarks>
    private CataloguePlacement Placement
    {
        get
        {
            Resolve();

            return _placement;
        }
    }

    /// <summary>
    /// Fills both caches, once per (Datasamling, Language) pair rather than per access.
    /// </summary>
    /// <remarks>
    /// The same cache <see cref="KildeView.Resolve"/> holds and for the same measurement
    /// (Fhi.Metadata-43jrq): the markup reads the groups twice per render, and a fact box asks about
    /// a dozen placement questions on top of that, each of which was merging the values and
    /// rebuilding the suppression set again.
    /// </remarks>
    private void Resolve()
    {
        if (_resolved && ReferenceEquals(_resolvedDatasamling, Datasamling) && _resolvedLanguage == Language)
        {
            return;
        }

        _resolved = true;
        _resolvedDatasamling = Datasamling;
        _resolvedLanguage = Language;

        if (Datasamling is not { } datasamling)
        {
            _placement = CataloguePlacement.None;
            _groups = [];

            return;
        }

        _placement = new CataloguePlacement(datasamling.PropertyMetadata,
                                            CatalogueColumns.Values(datasamling, Language),
                                            Reader,
                                            DrawnElsewhere);

        _groups = CatalogueProperties.Groups(datasamling.PropertyMetadata, _placement.Values, Reader,
                                             _placement.DrawnElsewhere,
                                             CompleteRecord.Values(datasamling, _placement.Values));
    }

    /// <inheritdoc cref="KildeView.CompleteRecordFacts"/>
    private CompleteRecordExtras CompleteRecordFacts =>
        Datasamling is not { } datasamling
            ? new(T.CompleteRecordLeadDatasamling, [])
            : new(T.CompleteRecordLeadDatasamling,
                  [
                      (T.FieldVariableCount, VariableCount, false),
                      (T.FieldLastUpdated, CatalogueDate.DayOrNothing(datasamling.LastUpdated, Language), false),
                  ]);

    /// <inheritdoc cref="Groups"/>
    private IReadOnlySet<string> DrawnElsewhere =>
        Description is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal) { CatalogueColumns.Description };

    /// <inheritdoc cref="CataloguePlacement.UnlessPlaced"/>
    private string? UnlessPlaced(string key, string? value) => Placement.UnlessPlaced(key, value);

    /// <summary>
    /// The facts every datasamling has, labelled as the kilde view labels the same fields.
    /// </summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words. The kildetype and the
    /// identification level are vocabularies this package translates; the rest are stored once, in
    /// Norwegian, however the reader is reading.
    /// <para>
    /// Six of them are column-backed properties the catalogue can place in a section of its own,
    /// and each yields when it does — see <see cref="UnlessPlaced"/> and
    /// <see cref="ValidityRows"/>. Kilde, Kildetype and Sist
    /// oppdatert are not among them: no section can draw a fact nothing merges into the renderable
    /// set.
    /// </para>
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> SourceInformation =>
        Datasamling is not { } datasamling
            ? []
            : [
                (T.FieldSource, datasamling.ParentKildeName, true),
                (T.FacetKildeType, KildetypeLabel, false),
                (T.FieldLegalBasis,
                 UnlessPlaced(CatalogueColumns.LegalBasis, datasamling.EffectiveLegalBasis), true),
                (T.FieldDataController,
                 UnlessPlaced(CatalogueColumns.DataController, datasamling.EffectiveDataController), true),
                (T.FieldDataProcessor,
                 UnlessPlaced(CatalogueColumns.DataProcessor, datasamling.EffectiveDataProcessor), true),
                (T.FieldPersonIdentification,
                 UnlessPlaced(CatalogueColumns.PersonIdentification, PersonIdentification), false),
                .. ValidityRows,
                (T.FieldLastUpdated, CatalogueDate.DayOrNothing(datasamling.LastUpdated, Language), false),
            ];

    /// <inheritdoc cref="CataloguePlacement.ValidityRows"/>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> ValidityRows =>
        Datasamling is { } datasamling
            ? Placement.ValidityRows(Validity,
                                     CatalogueDate.DayOrNothing(datasamling.EffectiveValidFrom, Language),
                                     CatalogueDate.DayOrNothing(datasamling.EffectiveValidTo, Language), T)
            : [];

    /// <summary>
    /// The four values the hero row and the fact boxes both draw, resolved once each.
    /// </summary>
    /// <remarks>
    /// One member per value rather than one expression per surface, for the reason
    /// <see cref="KildeView.KildetypeLabel"/> gives: the hero repeats what the sections show, and
    /// two resolutions of one field are how one fact ends up on one page under two different words.
    /// Every one of them is the inherited <c>Effective…</c> value, as this view's own remarks
    /// require.
    /// </remarks>
    private string? KildetypeLabel =>
        Datasamling is { } datasamling
            ? T.KildeTypeLabel(datasamling.EffectiveKildetype, datasamling.EffectiveKildetype)
            : null;

    /// <inheritdoc cref="KildetypeLabel"/>
    /// <remarks>
    /// The catalogue's own word once it has placed the key in a section, this package's vocabulary
    /// until then — the rule <c>KildeView.PersonIdentification</c> gives in full.
    /// </remarks>
    private string? PersonIdentification
    {
        get
        {
            if (Datasamling is not { } datasamling)
            {
                return null;
            }

            // Both questions over one snapshot, so the word drawn cannot belong to a payload other
            // than the one whose placement chose it.
            var placement = Placement;

            return placement.Placed(CatalogueColumns.PersonIdentification)
                ? placement.Curated(CatalogueColumns.PersonIdentification)
                : T.PersonIdentificationLabel(datasamling.EffectivePersonIdentificationLevel);
        }
    }

    /// <inheritdoc cref="KildetypeLabel"/>
    private string? Validity =>
        Datasamling is { } datasamling
            ? CatalogueDate.Period(datasamling.EffectiveValidFrom, datasamling.EffectiveValidTo, Language, T)
            : null;

    /// <inheritdoc cref="KildetypeLabel"/>
    private string? VariableCount =>
        Datasamling is { } datasamling && datasamling.VariableCount > 0
            ? datasamling.VariableCount.ToString()
            : null;

    /// <summary>
    /// How the data is collected and how much of it there is.
    /// </summary>
    /// <remarks>
    /// Frekvens is in the contract and in Runa's block, and no datasamling in the test catalogue
    /// has one — it draws when the catalogue starts carrying it and no row until then, which is
    /// what keeps it out of <see cref="AnyStatistics"/>. A count of nothing is left out rather than
    /// shown as a zero, for the same reason: both are what let a datasamling with no numbers at all
    /// draw no block.
    /// <para>
    /// Frekvens and Telleenhet yield to a section that has been given their key, as the fact box
    /// above does. The count is the collection's own number and has no property definition to be
    /// placed. Statistikktype is drawn by no row here at all — see <see cref="StatisticsHeading"/>,
    /// which is the surface that draws it and deliberately does not yield.
    /// </para>
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> Statistics =>
        Datasamling is not { } datasamling
            ? []
            : [
                (T.FieldFrequency, UnlessPlaced(CatalogueColumns.Frequency, datasamling.Frequency), true),
                (T.FieldCountingUnit, UnlessPlaced(CatalogueColumns.CountingUnit, datasamling.CountingUnit), true),
                (T.FieldVariableCount, VariableCount, false),
            ];

    /// <summary>
    /// The six facts a datasamling leads with — the source's own six, with Gyldighet where a source
    /// has Dataperiode and the collection's own variable count where a source has its total.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same six on purpose: a reader moving between a source and one of its collections is
    /// comparing them, and a row that reorders itself between the two pages is a row they have to
    /// read twice. Every value is the member the section below reads, so the two cannot come out in
    /// different words, and no key goes into <see cref="DrawnElsewhere"/> on account of being here —
    /// that set names Beskrivelse, which the ingress draws, and nothing else.
    /// </para>
    /// <para>
    /// Kilde is deliberately not among them although the fact box below shows it: the breadcrumb
    /// directly above already names the source, and a fact strip that repeats the chrome spends a
    /// slot on something the reader has just read. Telleenhet qualifies the count rather than
    /// taking a slot of its own, and Frekvens qualifies nothing today — no datasamling in the
    /// catalogue carries one — so it stays in Statistikk where an empty row draws nothing.
    /// </para>
    /// </remarks>
    private IReadOnlyList<DetailFact> HeroFacts =>
        Datasamling is not { } datasamling
            ? []
            : [
                new DetailFact(T.FacetKildeType, KildetypeLabel),
                new DetailFact(T.FieldDataController, datasamling.EffectiveDataController,
                               CatalogueProperties.Foreign("no", Reader)),
                new DetailFact(T.FieldPersonIdentification, PersonIdentification),
                new DetailFact(T.FieldValidity, Validity),
                new DetailFact(T.FieldVariableCount, VariableCount,
                               NoteLabel: T.FieldCountingUnit, Note: datasamling.CountingUnit,
                               NoteLang: CatalogueProperties.Foreign("no", Reader)),
                new DetailFact(T.FieldLegalBasis, datasamling.EffectiveLegalBasis,
                               CatalogueProperties.Foreign("no", Reader)),
            ];

    /// <summary>Whether the statistics block has a row to draw, heading and section included.</summary>
    /// <remarks>
    /// The fact-list flavour, over <see cref="Statistics"/>. <see cref="StatisticsBlock"/> spells
    /// the name too, for a variable's statistics table — a different question about a different
    /// type, so every <c>AnyStatistics</c> named in this file means this one.
    /// </remarks>
    private bool AnyStatistics => DetailBlocks.AnyFacts(Statistics);

    /// <summary>
    /// The statistics heading, naming the kind of statistics rather than just saying "Statistikk".
    /// </summary>
    /// <remarks>
    /// Shared with <see cref="StatisticsBlock"/>, which heads a variable's numbers the same way off
    /// the same field: a variable's statistikktype is the one belonging to the datasamling it is
    /// pinned into, so two spellings of that heading would be two spellings of one fact.
    /// <para>
    /// Read raw rather than through <see cref="UnlessPlaced"/>, alone among the merged keys: a
    /// heading naming what the numbers count is not the fact repeated, it is what makes the section
    /// findable, and a section placed StatistikkType and headed "Statistikk" would have lost it.
    /// </para>
    /// </remarks>
    private string StatisticsHeading =>
        StatisticsBlock.Heading(Datasamling?.StatisticsType, T);

    /// <summary>The sections this view draws, in the order it draws them.</summary>
    private IReadOnlyList<DetailTocEntry> Toc { get; set; } = [];

    private IReadOnlySet<string> DrawnIds { get; set; } = new HashSet<string>();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var toc = BuildToc();

        Toc = toc.Entries;
        DrawnIds = toc.Drawn;
    }

    /// <summary>This view's own predicates, which are what the nav and the blocks both read.</summary>
    private DetailTocBuilder BuildToc()
    {
        if (Datasamling is not { } datasamling)
        {
            return new();
        }

        DetailTocBuilder toc = new();

        toc.Add(Groups.Count > 0, DetailSectionIds.Metadata, T.HeadingMetadata);
        toc.Add(!string.IsNullOrWhiteSpace(datasamling.InclusionAndExclusionCriteria),
                DetailSectionIds.Criteria, T.FieldInclusionCriteria);
        toc.Add(DetailBlocks.AnyFacts(SourceInformation), DetailSectionIds.Source, T.HeadingSourceInformation);
        toc.Add(AnyStatistics, DetailSectionIds.Statistics, StatisticsHeading);
        toc.AddNamed(NamedSections);

        return toc;
    }

    /// <summary>Whether this view's own block is drawn; a named section never switches one on.</summary>
    private bool Drawn(string id) => DrawnIds.Contains(id);
}
