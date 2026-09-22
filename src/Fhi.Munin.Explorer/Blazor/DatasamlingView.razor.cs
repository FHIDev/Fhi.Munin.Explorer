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
/// the own values reports "Ingen" for a datasamling whose controller is perfectly well known
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

    /// <summary>
    /// Where the owning kilde can be opened, given its id. Null, the default, draws its name as
    /// plain text.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="KildeView.DatasamlingHref"/> uses, pointing the other way: this package
    /// has no router and helsedata's addresses are not ours, so a target is something the surface
    /// above hands down and a missing one is never a link that goes nowhere.
    /// </remarks>
    [Parameter]
    public Func<Guid, string>? KildeHref { get; set; }

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>
    /// The name the page's heading carries, resolved through the member the heading reads so the
    /// sticky bar and the name block cannot say it in different words.
    /// </summary>
    private (string Text, bool Norwegian) StickyNamed =>
        T.Named(Datasamling?.PreferredTerm, Datasamling?.Code);

    private string? StickyNameLang => CatalogueProperties.Foreign(StickyNamed.Norwegian, Reader);

    /// <summary>The code beside the bar's name, on the name block's own terms.</summary>
    /// <remarks>Nothing where the heading has already fallen back to the code. (Fhi.Metadata-w13lk)</remarks>
    private string? StickyCode => StickyNamed.Norwegian ? Datasamling?.Code : null;

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
                      (T.FieldCreatedInMunin, CatalogueDate.DayOrNothing(datasamling.Created, Language), false),
                  ]);

    /// <inheritdoc cref="Groups"/>
    private IReadOnlySet<string> DrawnElsewhere =>
        Description is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal) { CatalogueColumns.Description };

    /// <inheritdoc cref="CataloguePlacement.UnlessPlaced"/>
    private IReadOnlyList<TRow> UnlessPlaced<TRow>(string key, TRow row) => Placement.UnlessPlaced(key, row);

    private static (string Label, string? Value, bool Norwegian, string? Href) Unlinked(
        string label, string? value, bool norwegian) => (label, value, norwegian, null);

    /// <summary>
    /// The facts every datasamling has, labelled as the kilde view labels the same fields.
    /// </summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words, and the fourth is
    /// where the row goes. The kildetype and the identification level are vocabularies this package
    /// translates; the rest are stored once, in Norwegian, however the reader is reading.
    /// <para>
    /// Every row but Kilde, Kildetype and the two Munin timestamps is a column-backed property the
    /// catalogue can place in a section of its own, and each yields when it does — see
    /// <see cref="UnlessPlaced"/> and <see cref="ValidityRows"/>, which answers for Gyldig fra and
    /// Gyldig til apart although they share one row. The four are not among them: no section can
    /// draw a fact nothing merges into the renderable set, and the timestamps are Munin's own.
    /// </para>
    /// <para>
    /// Kilde is the one row with somewhere to go, and only where <see cref="KildeHref"/> was wired:
    /// the id is on the payload for exactly this, so the return path out of a collection costs no
    /// second request (Fhi.Metadata-35w0p.50).
    /// </para>
    /// <para>
    /// What is left after the yielding follows the yielded fields into their section rather than
    /// heading one of its own — see <see cref="BuildLayout"/>, and Fhi.Metadata-lr6yh for why a
    /// page carrying both Kildeinformasjon and Datakilde was the defect.
    /// </para>
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> SourceInformation =>
        Datasamling is not { } datasamling
            ? []
            : [
                (T.FieldSource, datasamling.ParentKildeName, true,
                 KildeHref?.Invoke(datasamling.ParentKildeId)),
                (T.FacetKildeType, KildetypeLabel, false, null),
                .. UnlessPlaced(CatalogueColumns.LegalBasis,
                                (T.FieldLegalBasis, CatalogueMarkdown.Words(datasamling.EffectiveLegalBasis),
                                 CatalogueMarkdown.Prose(datasamling.EffectiveLegalBasis),
                                 CatalogueMarkdown.Link(datasamling.EffectiveLegalBasis)?.Href)),
                .. UnlessPlaced(CatalogueColumns.DataController,
                                Unlinked(T.FieldDataController, datasamling.EffectiveDataController, true)),
                .. UnlessPlaced(CatalogueColumns.DataProcessor,
                                Unlinked(T.FieldDataProcessor, datasamling.EffectiveDataProcessor, true)),
                .. UnlessPlaced(CatalogueColumns.PersonIdentification,
                                Unlinked(T.FieldPersonIdentification, PersonIdentification, false)),
                .. ValidityRows.Select(row => (row.Label, row.Value, row.Norwegian, (string?)null)),
                (T.FieldLastUpdated, CatalogueDate.DayOrNothing(datasamling.LastUpdated, Language), false, null),
                (T.FieldCreatedInMunin, CatalogueDate.DayOrNothing(datasamling.Created, Language), false, null),
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
            && !string.IsNullOrWhiteSpace(datasamling.EffectiveKildetype)
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
                : string.IsNullOrWhiteSpace(datasamling.EffectivePersonIdentificationLevel)
                    ? null
                    : T.PersonIdentificationLabel(datasamling.EffectivePersonIdentificationLevel);
        }
    }

    /// <summary>The catalogue's statistikktype in this package's vocabulary, for the row.</summary>
    /// <remarks>
    /// Not one of <see cref="KildetypeLabel"/>'s four: the collection's own field, not an inherited
    /// one, and no hero fact draws it. <see cref="StatisticsHeading"/> resolves the code itself, so
    /// only the shared <see cref="Texts.StatisticsTypeLabel"/> keeps row and heading in one word.
    /// </remarks>
    private string? StatisticsTypeLabel =>
        Datasamling?.StatisticsType is { } type && !string.IsNullOrWhiteSpace(type)
            ? T.StatisticsTypeLabel(type)
            : null;

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
    /// has one, so it reads "Ingen" until the catalogue carries it. A count of nothing reads "Ingen"
    /// too rather than a zero, which is what lets a datasamling with no numbers at all draw no block.
    /// <para>
    /// Statistikktype, Frekvens and Telleenhet all yield to a section that has been given their
    /// key, as the fact box above does, and what is left follows them into it. The count is the
    /// collection's own number and has no property definition to be placed.
    /// </para>
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> Statistics =>
        Datasamling is not { } datasamling
            ? []
            : [
                .. UnlessPlaced(CatalogueColumns.StatisticsType,
                                (T.FieldStatisticsType, StatisticsTypeLabel, false)),
                .. UnlessPlaced(CatalogueColumns.Frequency, (T.FieldFrequency, datasamling.Frequency, true)),
                .. UnlessPlaced(CatalogueColumns.CountingUnit, (T.FieldCountingUnit, datasamling.CountingUnit, true)),
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
    /// catalogue carries one — so it stays in Statistikk, where an empty row reads "Ingen".
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
                new DetailFact(T.FieldLegalBasis, CatalogueMarkdown.Words(datasamling.EffectiveLegalBasis),
                               CatalogueMarkdown.Prose(datasamling.EffectiveLegalBasis)
                                   ? CatalogueProperties.Foreign("no", Reader)
                                   : null),
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
    /// findable. It heads the block only where the catalogue placed none of those keys — where it
    /// placed them the curator's own section name is the heading, which is the placement deciding
    /// the page rather than this view (Fhi.Metadata-lr6yh).
    /// </para>
    /// </remarks>
    private string StatisticsHeading =>
        StatisticsBlock.Heading(Datasamling?.StatisticsType, T);

    /// <summary>The sections this view draws, in the order it draws them.</summary>
    private IReadOnlyList<DetailLayoutSection> Layout { get; set; } = [];

    /// <summary>The contents nav, one entry per section this view drew, in that order.</summary>
    private IReadOnlyList<DetailTocEntry> Toc { get; set; } = [];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Layout = BuildLayout();
        Toc = BuildToc(Layout);
    }

    /// <summary>
    /// Every section this page draws, in the order the payload puts them.
    /// </summary>
    /// <remarks>
    /// No section list is written down here: the sections are Munin's placement rows to declare and
    /// a curator's to rename, and a group no row names keeps the Metadata block rather than being
    /// dropped (Fhi.Metadata-lr6yh).
    /// </remarks>
    private IReadOnlyList<DetailLayoutSection> BuildLayout()
    {
        if (Datasamling is not { } datasamling)
        {
            return [];
        }

        HashSet<string> named = new(
            datasamling.Sections.Select(section => section.Key).Where(key => !string.IsNullOrEmpty(key)),
            StringComparer.Ordinal);

        IReadOnlyList<PropertyGroup> ungrouped =
            [.. Groups.Where(group => group.Key is null || !named.Contains(group.Key))];

        var placement = Placement;
        var sourceFacts = SourceInformation;
        var source = DetailBlocks.AnyLinkedFacts(sourceFacts)
            ? placement.SectionOf(CatalogueColumns.LegalBasis, CatalogueColumns.DataController,
                                  CatalogueColumns.DataProcessor, CatalogueColumns.PersonIdentification)
            : null;

        var statistics = AnyStatistics
            ? placement.SectionOf(CatalogueColumns.StatisticsType, CatalogueColumns.Frequency,
                                  CatalogueColumns.CountingUnit)
            : null;

        List<DetailLayoutSection> groups = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        var sourceDrawn = false;
        var statisticsDrawn = false;

        foreach (var group in Groups.Where(group => group.Key is not null && named.Contains(group.Key)))
        {
            List<(string Label, string? Value, bool Norwegian, string? Href)> facts = [];

            if (string.Equals(group.Key, source, StringComparison.Ordinal))
            {
                facts.AddRange(sourceFacts);
                sourceDrawn = true;
            }

            if (string.Equals(group.Key, statistics, StringComparison.Ordinal))
            {
                facts.AddRange(Statistics.Select(row => (row.Label, row.Value, row.Norwegian, (string?)null)));
                statisticsDrawn = true;
            }

            var body = DetailBlocks.GroupBody(group, Language, CompleteRecordFacts);

            groups.Add(new(group.Key, DetailSectionIds.ReserveGroupId(group.Key!, ids), group.Name,
                           CatalogueProperties.Foreign(group.NameLanguage, Reader),
                           facts.Count == 0
                               ? body
                               : DetailBlocks.Both(body, DetailBlocks.LinkedFacts(facts, Language))));
        }

        return DetailLayout.Order(datasamling.Sections, groups,
                                  Blocks(datasamling, ungrouped, sourceFacts, sourceDrawn, statisticsDrawn));
    }

    /// <summary>
    /// This view's own sections, each under the key a placement row moves it by, in the order the
    /// view falls back to for whichever of them the payload places nowhere.
    /// </summary>
    /// <remarks>
    /// Asked of what was drawn and not of what was placed: a placed section whose every row came
    /// out empty is no section, and a box that had yielded to it would be on no surface at all.
    /// </remarks>
    private IReadOnlyList<DetailLayoutSection> Blocks(
        DatasamlingDetail datasamling,
        IReadOnlyList<PropertyGroup> ungrouped,
        IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> sourceFacts,
        bool sourceDrawn,
        bool statisticsDrawn)
    {
        List<DetailLayoutSection> blocks = [];

        if (ungrouped.Count > 0)
        {
            blocks.Add(new(null, DetailSectionIds.Metadata, T.HeadingMetadata, null,
                           DetailBlocks.Groups(ungrouped, GroupLevel, Language, CompleteRecordFacts)));
        }

        if (!string.IsNullOrWhiteSpace(datasamling.InclusionAndExclusionCriteria))
        {
            blocks.Add(new(SectionKeys.InclusionAndExclusionCriteria, DetailSectionIds.Criteria,
                           T.FieldInclusionCriteria, null,
                           DetailBlocks.Prose(datasamling.InclusionAndExclusionCriteria,
                                              "munin-explorer-datasamling__criteria",
                                              CatalogueProperties.Foreign("no", Reader))));
        }

        if (!sourceDrawn && DetailBlocks.AnyLinkedFacts(sourceFacts))
        {
            blocks.Add(new(SectionKeys.SourceInformation, DetailSectionIds.Source,
                           T.HeadingSourceInformation, null,
                           DetailBlocks.LinkedFacts(sourceFacts, Language)));
        }

        if (!statisticsDrawn && AnyStatistics)
        {
            blocks.Add(new(SectionKeys.Statistics, DetailSectionIds.Statistics, StatisticsHeading, null,
                           DetailBlocks.Facts(Statistics, Language)));
        }

        return blocks;
    }

    /// <summary>The nav, read off the drawn sections so a link cannot point at a block left out.</summary>
    private IReadOnlyList<DetailTocEntry> BuildToc(IReadOnlyList<DetailLayoutSection> layout)
    {
        DetailTocBuilder toc = new();

        if (Datasamling is null)
        {
            return toc.Entries;
        }

        foreach (var section in layout)
        {
            // The heading's own lang goes with it: the words are the curator's, and a nav link
            // repeating them unmarked is announced in the reader's phonetics (WCAG 3.1.2).
            toc.Always(section.Id, section.Heading, section.HeadingLanguage);
        }

        toc.AddNamed(NamedSections);

        return toc.Entries;
    }
}
