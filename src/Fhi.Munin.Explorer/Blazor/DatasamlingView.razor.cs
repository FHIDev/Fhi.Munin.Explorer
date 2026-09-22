using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

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
/// <b>Register <see cref="IMuninExplorerClient"/> with <c>AddMuninExplorer</c> before mounting
/// this view.</b> The payload carries a variable count and no variables, so the table under the
/// collection's own section is a second request this view makes for itself — the arrangement
/// <see cref="KildeHierarchyView"/> already uses, and what keeps both explorers mounting this view
/// with the parameters they always passed.
/// </para>
/// <para>
/// Ships no CSS, like everything else in this package: it emits the host's class names so the
/// surrounding site styles it.
/// </para>
/// </remarks>
public sealed partial class DatasamlingView : ComponentBase, IDisposable
{
    [Inject] private IMuninExplorerClient Client { get; set; } = default!;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    private ILogger? _log;

    /// <summary>The host's logger, or none — see <see cref="ExplorerLog"/>.</summary>
    private ILogger? Log => _log ??= ExplorerLog.For<DatasamlingView>(Services);

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

    /// <summary>Rows per page in the variable table — the package's own default everywhere else.</summary>
    /// <remarks>
    /// <see cref="ExplorerUrlState.DefaultPageSize"/> rather than a number of this view's own, so a
    /// reader moving between the result list and a collection's page counts in the same pages. No
    /// size control beside it: this pager owns no query key, so a size the reader chose would be
    /// forgotten the moment they opened another collection.
    /// </remarks>
    private const int VariablesPageSize = ExplorerUrlState.DefaultPageSize;

    private Guid? _variablesFor;
    private CancellationTokenSource? _variablesRequest;
    private IReadOnlyList<VariableSummary> _variables = [];
    private int _variablesPage = 1;
    private int _variablesTotal;
    private int _variablesPages;
    private bool _variablesLoading;
    private bool _variablesFailed;
    private bool _variablesRateLimited;
    private bool _variablesRetryShown;
    private bool _disposed;

    /// <summary>Whether the retry on offer can do anything, which throttling is not.</summary>
    /// <remarks>Only waiting helps a 429, so pressing again is what the reader must not be invited to do.</remarks>
    private bool CanRetryVariables => _variablesFailed && !_variablesLoading && !_variablesRateLimited;

    /// <summary>How many pages the last answer said there are; none before one has arrived.</summary>
    private int VariablePageCount => _variablesPages;

    /// <summary>The fallback count, for an answer that left <c>totalPages</c> at zero.</summary>
    private static int PagesOver(int total, int size) => total <= 0 ? 0 : (total + size - 1) / size;

    /// <summary>
    /// What the alert region over the table says, and nothing once a first load has settled.
    /// </summary>
    /// <remarks>
    /// A successful empty answer says nothing here: its sentence is the paragraph that replaces the
    /// table, so a reader is not told twice — and a failure must never reach that paragraph, which
    /// is what the ordering of these arms is for.
    /// </remarks>
    private string VariablesStatus =>
        _variablesLoading ? T.VariablesLoading
        : _variablesRateLimited ? T.RateLimitError
        : _variablesFailed ? T.VariablesError
        : _variablesRetryShown ? T.VariablesLoaded : "";

    /// <summary>Whether the table itself is what the section draws.</summary>
    private bool AnyVariables => !_variablesLoading && !_variablesFailed && _variables.Count > 0;

    /// <summary>The paragraph's case: a load that came back with nothing at all to show.</summary>
    /// <remarks>
    /// On the total and not only on the rows: a page past the end of a collection that shrank under
    /// the reader also arrives empty, and this sentence claims the collection is, which would be a
    /// fact about the catalogue nobody checked. <see cref="RetreatFromEmptyVariablePageAsync"/>
    /// moves off such a page; until it has, the total is what tells the two apart.
    /// </remarks>
    private bool NoVariables =>
        !_variablesLoading && !_variablesFailed && !_variablesRateLimited
        && _variables.Count == 0 && _variablesTotal == 0;

    /// <summary>The reader's word for a stored datatype, never the stored value itself.</summary>
    /// <remarks>
    /// This surface fetches no filters, so the shipped table is the whole of what it has to go on —
    /// the bound <c>KildeView</c> and <c>VariableView</c> share (Fhi.Metadata-vcxoc).
    /// <see cref="Texts.DataTypeLabel"/> canonicalises first, so a legacy spelling and its code come
    /// out as one word rather than as two rows that look like two datatypes.
    /// </remarks>
    private string? DataTypeName(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? null : T.DataTypeLabel(stored);

    /// <summary>Written the way the result list writes it, so the pager reads the same on both.</summary>
    private static string AriaDisabled(bool enabled) => enabled ? "false" : "true";

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        Layout = BuildLayout();
        Toc = BuildToc(Layout);

        if (Datasamling?.Id is not { } id)
        {
            _variablesFor = null;
            _variablesRequest?.Cancel();
            _variables = [];
            _variablesTotal = 0;
            _variablesPages = 0;

            return Task.CompletedTask;
        }

        if (_variablesFor == id)
        {
            return Task.CompletedTask;
        }

        // A different collection is a different list: the page the reader was on says nothing about
        // this one, and the old total would size a pager over rows that are no longer there.
        _variablesFor = id;
        _variablesTotal = 0;
        _variablesPages = 0;
        _variablesRetryShown = false;

        return LoadVariablesAsync(1);
    }

    private Task RetryVariablesAsync() =>
        CanRetryVariables ? ShowVariablePageAsync(_variablesPage) : Task.CompletedTask;

    /// <summary>Fetch <paramref name="page"/>, then step off it if it turned out not to exist.</summary>
    /// <remarks>
    /// Every route but the first load goes through here, because every one of them names a page
    /// measured against a count that may since have changed.
    /// </remarks>
    private async Task ShowVariablePageAsync(int page)
    {
        await LoadVariablesAsync(page);
        await RetreatFromEmptyVariablePageAsync();
    }

    /// <summary>Step back to a page that has rows, when the page just fetched turned out not to.</summary>
    /// <remarks>
    /// <para>
    /// The clamp in <see cref="GoToVariablePageAsync"/> measures its target against the count the
    /// <em>previous</em> answer carried, so a collection losing rows between two requests leaves the
    /// reader past the end: a total saying rows exist, none to show, and — once the new count is
    /// under two pages — not even a pager to press back with.
    /// </para>
    /// <para>
    /// One step only, as <c>VariableSearch.RetreatFromEmptyPageAsync</c> takes it, and no rollback
    /// on failure: the state it would restore is the stranded page this exists to leave. Page 1 is
    /// the one page that can never be out of range, which is why the first load skips this.
    /// </para>
    /// </remarks>
    private Task RetreatFromEmptyVariablePageAsync()
    {
        // _variablesLoading says a newer load has already taken the state over, and this answer is
        // no longer the one on screen to retreat from.
        if (_disposed || _variablesLoading || _variablesFailed || _variablesRateLimited
            || _variablesPage == 1 || _variables.Count > 0 || _variablesTotal <= 0)
        {
            return Task.CompletedTask;
        }

        // A server still claiming the page exists after sending nothing has told us nothing usable,
        // so page 1 is the only safe answer left.
        return LoadVariablesAsync(_variablesPages < _variablesPage ? _variablesPages : 1);
    }

    /// <summary>
    /// Moves the table to <paramref name="page"/>, or does nothing where that page cannot exist.
    /// </summary>
    /// <remarks>
    /// The clamp is here rather than on each pager button, which is why both of them are
    /// <c>aria-disabled</c> and never <c>disabled</c>: disabling the control under the reader's
    /// focus drops that focus to <c>&lt;body&gt;</c>, with nothing on screen to say why.
    /// </remarks>
    private Task GoToVariablePageAsync(int page) =>
        page < 1 || page > VariablePageCount || page == _variablesPage || _variablesLoading
            ? Task.CompletedTask
            : ShowVariablePageAsync(page);

    /// <summary>
    /// One page of the collection's variables, from the search endpoint narrowed to this
    /// datasamling.
    /// </summary>
    /// <remarks>
    /// <c>IncludeHistorical</c> stays at its default: the payload's own variableCount counts the
    /// current ones, and the fact row saying so is drawn on this very page, so a total that counted
    /// history would contradict it (Fhi.Metadata-ivxpi).
    /// </remarks>
    private async Task LoadVariablesAsync(int page)
    {
        if (Datasamling?.Id is not { } id)
        {
            return;
        }

        _variablesRequest?.Cancel();
        using var request = new CancellationTokenSource();
        _variablesRequest = request;
        _variablesPage = page;
        _variables = [];
        _variablesLoading = true;
        _variablesFailed = false;
        _variablesRateLimited = false;

        try
        {
            var answer = await Client.SearchVariablesAsync(
                search: null,
                filter: new VariableFilter { DatasamlingIds = [id] },
                page: page,
                pageSize: VariablesPageSize,
                cancellationToken: request.Token);

            // A superseded call must not write over the list the reader is looking at, which is the
            // whole reason each load carries its own source rather than sharing one.
            if (_disposed || request.IsCancellationRequested)
            {
                return;
            }

            _variables = answer.Items;
            _variablesTotal = answer.TotalCount;

            // The server's own count wins, as VariableSearch.TotalPages explains: it is the side
            // that clamps the page size, so arithmetic over a size it quietly changed would offer
            // a Neste for a page that is not there.
            _variablesPages = answer.TotalPages > 0
                ? answer.TotalPages
                : PagesOver(answer.TotalCount, answer.Size > 0 ? answer.Size : VariablesPageSize);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // Inside the guard, not above it: a call this view cancelled on a new datasamling comes
            // back as a cancellation that nothing failed and nobody should read about.
            if (!_disposed && !request.IsCancellationRequested)
            {
                Log?.LogWarning(
                    ex, "the rate limiter refused the variables of datasamling {DatasamlingId} page {Page}",
                    id, page);

                _variablesRateLimited = true;
            }
        }
        catch (Exception ex)
        {
            // The same guard, and for the same reason: HttpClient's own timeout is a
            // TaskCanceledException worth logging, and this view's own Cancel is not.
            if (!_disposed && !request.IsCancellationRequested)
            {
                Log?.LogError(
                    ex, "could not load the variables of datasamling {DatasamlingId} page {Page}", id, page);

                _variablesFailed = true;
            }
        }
        finally
        {
            if (ReferenceEquals(_variablesRequest, request))
            {
                _variablesRequest = null;
                _variablesLoading = false;
                _variablesRetryShown |= _variablesFailed;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _variablesRequest?.Cancel();
        _variablesRequest?.Dispose();
        _variablesRequest = null;
    }

    /// <summary>
    /// Every section this page draws, in the order the payload puts them.
    /// </summary>
    /// <remarks>
    /// No section list is written down here: the sections are Munin's placement rows to declare and
    /// a curator's to rename, and a group no row names keeps the Metadata block rather than being
    /// dropped (Fhi.Metadata-lr6yh).
    /// <para>
    /// The variable table follows the count it enumerates, so the page gains no second heading and
    /// no key of this package's: it is drawn in whichever section the catalogue declares Antall
    /// variabler's neighbours for, whether or not this collection fills one of them in — a section
    /// the placement named and nothing filled is the table's, under the curator's own name. A
    /// payload predating the placement rows draws it beside the count in the view's own statistics
    /// block, and one with neither in the view's own Variabler block, because a table the view
    /// fetched and drew nowhere is the defect this arrangement exists to avoid (Fhi.Metadata-mg08i).
    /// </para>
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

        // Declared rather than placed, alone among these three: the table enumerates the count, and
        // a collection whose numbers are all blank draws no row for it to follow while still having
        // rows of its own to draw (Fhi.Metadata-mg08i).
        var variables = placement.DeclaredSectionOf(
            CatalogueColumns.StatisticsType, CatalogueColumns.Frequency, CatalogueColumns.CountingUnit);

        List<DetailLayoutSection> groups = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        var sourceDrawn = false;
        var statisticsDrawn = false;
        var variablesDrawn = false;

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

            var variablesHere = string.Equals(group.Key, variables?.Key, StringComparison.Ordinal);
            variablesDrawn |= variablesHere;

            var body = DetailBlocks.GroupBody(group, Language, CompleteRecordFacts);

            if (facts.Count > 0)
            {
                body = DetailBlocks.Both(body, DetailBlocks.LinkedFacts(facts, Language));
            }

            groups.Add(new(group.Key, DetailSectionIds.ReserveGroupId(group.Key!, ids), group.Name,
                           CatalogueProperties.Foreign(group.NameLanguage, Reader),
                           variablesHere ? DetailBlocks.Both(body, VariablesBlock) : body));
        }

        // The catalogue named a section for these and this payload filled none of it in, so the
        // section is the table's and keeps the curator's own name and place — where a block of this
        // view's would take a package word to the tail of the page.
        if (!variablesDrawn && variables is { } declared && named.Contains(declared.Key))
        {
            groups.Add(new(declared.Key, DetailSectionIds.ReserveGroupId(declared.Key, ids), declared.Name,
                           CatalogueProperties.Foreign(declared.Language, Reader), VariablesBlock));

            variablesDrawn = true;
        }

        return DetailLayout.Order(
            datasamling.Sections, groups,
            Blocks(datasamling, ungrouped, sourceFacts, sourceDrawn, statisticsDrawn, variablesDrawn));
    }

    /// <summary>
    /// This view's own sections, each under the key a placement row moves it by, in the order the
    /// view falls back to for whichever of them the payload places nowhere.
    /// </summary>
    /// <remarks>
    /// Asked of what was drawn and not of what was placed: a placed section whose every row came
    /// out empty is no section, and a box that had yielded to it would be on no surface at all. The
    /// criteria block carries no key because no built-in row is seeded for this surface yet.
    /// </remarks>
    private IReadOnlyList<DetailLayoutSection> Blocks(
        DatasamlingDetail datasamling,
        IReadOnlyList<PropertyGroup> ungrouped,
        IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> sourceFacts,
        bool sourceDrawn,
        bool statisticsDrawn,
        bool variablesDrawn)
    {
        List<DetailLayoutSection> blocks = [];

        if (ungrouped.Count > 0)
        {
            blocks.Add(new(null, DetailSectionIds.Metadata, T.HeadingMetadata, null,
                           DetailBlocks.Groups(ungrouped, GroupLevel, Language, CompleteRecordFacts)));
        }

        if (!string.IsNullOrWhiteSpace(datasamling.InclusionAndExclusionCriteria))
        {
            blocks.Add(new(null, DetailSectionIds.Criteria, T.FieldInclusionCriteria, null,
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
            var facts = DetailBlocks.Facts(Statistics, Language);

            blocks.Add(new(SectionKeys.Statistics, DetailSectionIds.Statistics, StatisticsHeading, null,
                           variablesDrawn ? facts : DetailBlocks.Both(facts, VariablesBlock)));

            variablesDrawn = true;
        }

        // The one place this view heads the table itself, and the last resort: a collection with no
        // numbers at all draws neither the placed section nor the statistics block, and the table
        // still has a page to be on (Fhi.Metadata-mg08i).
        if (!variablesDrawn)
        {
            blocks.Add(new(null, DetailSectionIds.Variables, T.HeadingVariables, null, VariablesBlock));
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
