using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A source, in the shape both explorers show it: what it is called, the catalogue's own metadata
/// about it, and the facts that are the same for every source.
/// </summary>
/// <remarks>
/// Shared deliberately, and built as a core with slots rather than one view with flags. Measured on
/// 2026-08-20, Runa and Kelda render the same source identically down to the heading order — same
/// name block, same eight metadata groups, same two fact boxes, and — since
/// Fhi.Metadata-rhybi — the same word over the datasamlinger. Kelda then adds sections Runa
/// does not have.
/// <para>
/// A boolean per Kelda section would make this the one place where both explorers leak into each
/// other, and every later difference would add another flag. Instead each explorer passes its own
/// sections in through <see cref="NamedSections"/>, and this component never learns which one is calling.
/// </para>
/// <para>
/// Ships no CSS, like everything else in this package: it emits the host's class names so the
/// surrounding site styles it.
/// </para>
/// <para>
/// Register IMuninExplorerClient through AddMuninExplorer before mounting. The collection
/// hierarchy is fetched separately and uses the same collapsed disclosures in every explorer.
/// Descriptions and validity tables remain available in a separate metadata disclosure.
/// </para>
/// </remarks>
public sealed partial class KildeView : ComponentBase
{
    /// <summary>The source to show. Nothing renders until this is set.</summary>
    [Parameter, EditorRequired]
    public KildeDetail? Kilde { get; set; }

    /// <inheritdoc cref="VariableSearch.Language"/>
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
    /// Sections to place after this view's own blocks, for the explorer that owns them. Each is drawn
    /// under a heading at the level of those blocks and listed in the contents nav.
    /// </summary>
    /// <remarks>
    /// Kelda passes its variables, access criteria and prices here. Runa passes nothing at all.
    /// </remarks>
    [Parameter]
    public IReadOnlyList<DetailNamedSection>? NamedSections { get; set; }

    /// <summary>
    /// Markup to place last, after <see cref="NamedSections"/>.
    /// </summary>
    /// <remarks>
    /// Drawn as given and not listed in the contents nav: this view cannot see an id or a heading
    /// inside a fragment. Kelda passes whatever its own host hung on the explorer here; a host that
    /// wants its section in the nav mounts this view and uses <see cref="NamedSections"/>.
    /// </remarks>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    /// <summary>
    /// Where this source sits, for the breadcrumb: the steps above it, outermost first. This view
    /// appends the source's own name as the last step, so a caller never has to say it twice and
    /// the current page can never end up drawn as a link.
    /// </summary>
    /// <remarks>
    /// Every step's target comes from here because this package has none to give: there is no
    /// router and helsedata's addresses are not ours. Pass nothing — which a caller with no
    /// addresses of its own has to — and no trail is drawn at all; pass a step with a null
    /// <see cref="DetailTrailStep.Href"/> and it is drawn as plain text rather than as a dead link.
    /// </remarks>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <summary>
    /// Page-level controls, gathered into a row above the name block.
    /// </summary>
    /// <remarks>
    /// For what acts on the source this page is about. The way out of whatever surface the view
    /// opened inside is not that: it belongs to the surface, has to be on screen while the payload
    /// is still in flight — which is before this view exists — and repeating it here would put the
    /// same control on the page twice.
    /// </remarks>
    [Parameter]
    public RenderFragment? Actions { get; set; }

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

    /// <summary>
    /// The name the page's heading carries, resolved through the member the heading reads so the
    /// sticky bar and the name block cannot say it in different words.
    /// </summary>
    private (string Text, bool Norwegian) StickyNamed => T.Named(Kilde?.PreferredTerm, Kilde?.Code);

    private string? StickyNameLang => CatalogueProperties.Foreign(StickyNamed.Norwegian, Reader);

    /// <summary>The identifiers beside the bar's name, on the name block's own terms.</summary>
    /// <remarks>Nothing where the heading has already fallen back to the code. (Fhi.Metadata-w13lk)</remarks>
    private string? StickyCode => StickyNamed.Norwegian ? Identifiers : null;

    /// <summary>The trail the chassis draws — see <see cref="DetailTrail.Append"/> for the rule.</summary>
    private IReadOnlyList<DetailTrailStep>? PageTrail =>
        Kilde is { } kilde
            ? DetailTrail.Append(Trail, T.Named(kilde.PreferredTerm, kilde.Code), Reader)
            : null;

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

    private bool _resolved;
    private KildeDetail? _resolvedKilde;
    private string? _resolvedLanguage;
    private IReadOnlyList<PropertyGroup> _groups = [];
    private CataloguePlacement _placement = CataloguePlacement.None;

    /// <summary>The catalogue's metadata, grouped and ordered as the catalogue arranges it.</summary>
    private IReadOnlyList<PropertyGroup> Groups
    {
        get
        {
            Resolve();

            return _groups;
        }
    }

    /// <summary>
    /// The placement questions this page asks: the payload's curated bag with the source's own
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
    /// Fills both caches, once per (Kilde, Language) pair rather than per access.
    /// </summary>
    /// <remarks>
    /// The markup reads the groups twice per render — the empty check, then the loop — and each
    /// call was rebuilding DrawnElsewhere's set and re-walking every property (Fhi.Metadata-43jrq).
    /// The merged values are read once per hero fact on top of that, and are what the groups are
    /// built from, so the two belong to the same pair.
    /// </remarks>
    private void Resolve()
    {
        if (_resolved && ReferenceEquals(_resolvedKilde, Kilde) && _resolvedLanguage == Language)
        {
            return;
        }

        _resolved = true;
        _resolvedKilde = Kilde;
        _resolvedLanguage = Language;

        if (Kilde is not { } kilde)
        {
            _placement = CataloguePlacement.None;
            _groups = [];

            return;
        }

        _placement = new CataloguePlacement(kilde.PropertyMetadata,
                                            CatalogueColumns.Values(kilde, Language),
                                            Reader,
                                            DrawnElsewhere(kilde));

        _groups = CatalogueProperties.Groups(kilde.PropertyMetadata, _placement.Values, Reader,
                                             _placement.DrawnElsewhere,
                                             CompleteRecord.Values(kilde, _placement.Values));
    }

    /// <summary>
    /// The lead over the catch-all section, and the four facts it lists that no property definition
    /// carries.
    /// </summary>
    /// <remarks>
    /// Every one of them is drawn elsewhere on this page as well, which is the section's own rule:
    /// it is the complete record, so it repeats what the sections above already showed.
    /// </remarks>
    private CompleteRecordExtras CompleteRecordFacts =>
        Kilde is not { } kilde
            ? new(T.CompleteRecordLeadKilde, [])
            : new(T.CompleteRecordLeadKilde,
                  [
                      (T.FieldTotalVariables, TotalVariables, false),
                      (T.FieldDataCollections, DataCollections.Count.ToString(), false),
                      (T.FieldDataPeriod, DataPeriod, false),
                      (T.FieldLastUpdated, CatalogueDate.DayOrNothing(kilde.LastUpdated, Language), false),
                  ]);

    /// <summary>Keys whose value already appears elsewhere on the page, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Beskrivelse always duplicates the ingress (Fhi.Metadata-8yqoz). Formaal is safely dropped
    /// when FormaalFlerspraklig also holds a value; Tittel and hasLegalBasis are not, since their
    /// EHDS mirrors can hold content PreferredTerm and Lovverk lack (Fhi.Metadata-43jrq).
    /// </remarks>
    private static IReadOnlySet<string> DrawnElsewhere(KildeDetail kilde)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            CatalogueColumns.Description, "BeskrivelseFlerspraklig",
        };

        if (Filled(kilde, "Formaal") && Filled(kilde, "FormaalFlerspraklig"))
        {
            keys.Add("Formaal");
        }

        return keys;
    }

    // AdditionalProperties is declared non-nullable but System.Text.Json writes an explicit JSON
    // null straight over it (see Rows_WhenTheBagIsNull_ThenThereAreNoRowsRatherThanAThrow), so a
    // host substituting its own client can still hand this a null dictionary.
    private static bool Filled(KildeDetail kilde, string key) =>
        kilde.AdditionalProperties?.TryGetValue(key, out var value) is true && !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// The five values the hero row and the fact boxes both draw, resolved once each.
    /// </summary>
    /// <remarks>
    /// One member per value rather than one expression per surface: the hero repeats what the
    /// sections show on purpose, and two resolutions of one field are how the same fact ends up on
    /// one page under two different words.
    /// </remarks>
    private string? KildetypeLabel =>
        Kilde is { Kildetype: { } type } && !string.IsNullOrWhiteSpace(type) ? T.KildeTypeLabel(type, type) : null;

    /// <inheritdoc cref="KildetypeLabel"/>
    /// <remarks>
    /// The catalogue's own word once it has placed the key in a section, this package's vocabulary
    /// until then. Both spell the same code — <c>Direkte personidentifiserbare data</c> against
    /// <c>Direkte identifiserbar</c> — so a hero reading one while the section reads the other is
    /// the DataType collision again (Fhi.Metadata-bct95).
    /// </remarks>
    private string? PersonIdentification
    {
        get
        {
            if (Kilde is not { } kilde)
            {
                return null;
            }

            // Both questions over one snapshot, so the word drawn cannot belong to a payload other
            // than the one whose placement chose it.
            var placement = Placement;

            return placement.Placed(CatalogueColumns.PersonIdentification)
                ? placement.Curated(CatalogueColumns.PersonIdentification)
                : string.IsNullOrWhiteSpace(kilde.PersonIdentificationLevel)
                    ? null
                    : T.PersonIdentificationLabel(kilde.PersonIdentificationLevel);
        }
    }

    /// <inheritdoc cref="KildetypeLabel"/>
    private string? Validity =>
        Kilde is { } kilde ? CatalogueDate.Period(kilde.ValidFrom, kilde.ValidTo, Language, T) : null;

    /// <inheritdoc cref="CataloguePlacement.UnlessPlaced"/>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> UnlessPlaced(
        string key, (string Label, string? Value, bool Norwegian) row) => Placement.UnlessPlaced(key, row);

    /// <inheritdoc cref="KildetypeLabel"/>
    private string? DataPeriod =>
        Kilde is { } kilde ? CatalogueDate.Period(kilde.DataFrom, kilde.DataTo, Language, T) : null;

    /// <inheritdoc cref="KildetypeLabel"/>
    /// <remarks>
    /// A zero is drawn, not dropped: <see cref="KildeSortOrder"/> and KildeSearch's zero-weight
    /// count class both read nought off this non-nullable int as a count of none, and KildeSearch
    /// prints it in words a section below this one.
    /// </remarks>
    private string? TotalVariables => Kilde?.TotalVariables.ToString();

    /// <summary>
    /// The facts every source has, which is why they are typed fields rather than curated properties.
    /// </summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words. Two of these are ours
    /// — the kildetype and the identification level are vocabularies this package translates — and
    /// the rest are stored once, in Norwegian, however the reader is reading.
    /// <para>
    /// Six of them are column-backed properties the catalogue can place in a section of its own,
    /// and each yields when it does — see <see cref="UnlessPlaced"/> and <see cref="ValidityRows"/>,
    /// which yields one end at a time because the catalogue places two keys where this shows one row.
    /// Kildetype is not among them: nothing merges that column into the renderable set, so no section
    /// can draw it. Sist oppdatert has no property definition at all.
    /// </para>
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> SourceInformation =>
        Kilde is not { } kilde
            ? []
            : [
                (T.FacetKildeType, KildetypeLabel, false),
                .. UnlessPlaced(CatalogueColumns.LegalBasis, (T.FieldLegalBasis, kilde.LegalBasis, true)),
                .. UnlessPlaced(CatalogueColumns.DataController, (T.FieldDataController, kilde.DataController, true)),
                .. UnlessPlaced(CatalogueColumns.DataProcessor, (T.FieldDataProcessor, kilde.DataProcessor, true)),
                .. UnlessPlaced(CatalogueColumns.PersonIdentification,
                                (T.FieldPersonIdentification, PersonIdentification, false)),
                .. ValidityRows,
                (T.FieldLastUpdated, CatalogueDate.DayOrNothing(kilde.LastUpdated, Language), false),
            ];

    /// <inheritdoc cref="CataloguePlacement.ValidityRows"/>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> ValidityRows =>
        Kilde is { } kilde
            ? Placement.ValidityRows(Validity,
                                     CatalogueDate.DayOrNothing(kilde.ValidFrom, Language),
                                     CatalogueDate.DayOrNothing(kilde.ValidTo, Language), T)
            : [];

    /// <summary>Counts and dates, which belong to no language.</summary>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> Statistics =>
        Kilde is not { } kilde
            ? []
            : [
                (T.FieldTotalVariables, TotalVariables, false),
                (T.FieldDataPeriod, DataPeriod, false),
            ];

    /// <summary>
    /// The six facts a source leads with, in the order the mockup puts them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of them is still drawn below — the data period and the variable count in
    /// Statistikk, the rest in Kildeinformasjon or, once the catalogue places the key, in the
    /// section it placed it in — and every value here is the member that section reads, so the two
    /// cannot come out in different words. None of these keys goes into
    /// <see cref="DrawnElsewhere"/>: a hero row is a summary in a different register and is meant
    /// to repeat.
    /// </para>
    /// <para>
    /// The mockup's sixth is Tilgang, and Munin's catalogue holds no access field for a source, so
    /// Lovverk stands in it — the nearest thing a reader deciding whether they can have the data
    /// actually has. The mockup's note under Dataansvarlig is a contact role the payload does not
    /// carry either, and a note with nothing behind it is left off rather than invented.
    /// </para>
    /// </remarks>
    private IReadOnlyList<DetailFact> HeroFacts =>
        Kilde is not { } kilde
            ? []
            : [
                new DetailFact(T.FacetKildeType, KildetypeLabel),
                new DetailFact(T.FieldDataController, kilde.DataController,
                               CatalogueProperties.Foreign("no", Reader)),
                new DetailFact(T.FieldPersonIdentification, PersonIdentification),
                new DetailFact(T.FieldDataPeriod, DataPeriod,
                               NoteLabel: T.FieldValidity, Note: Validity),
                new DetailFact(T.FieldTotalVariables, TotalVariables,
                               Note: DataCollections.Count > 0
                                   ? T.DatasamlingCountCrumb(DataCollections.Count)
                                   : null),
                new DetailFact(T.FieldLegalBasis, kilde.LegalBasis,
                               CatalogueProperties.Foreign("no", Reader)),
            ];

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
        if (Kilde is null)
        {
            return new();
        }

        DetailTocBuilder toc = new();

        toc.Add(Groups.Count > 0, DetailSectionIds.Metadata, T.HeadingMetadata);

        // The tree's own status line carries the loading, empty and error states, so this block has
        // no empty state to suppress it on.
        toc.Always(DetailSectionIds.DataCollections, DataCollectionsHeading ?? DefaultDataCollectionsHeading);

        toc.Add(DetailBlocks.AnyFacts(SourceInformation), DetailSectionIds.Source, T.HeadingSourceInformation);
        toc.Add(DetailBlocks.AnyFacts(Statistics), DetailSectionIds.Statistics, T.HeadingStatistics);
        toc.AddNamed(NamedSections);

        return toc;
    }

    /// <summary>Whether this view's own block is drawn; a named section never switches one on.</summary>
    private bool Drawn(string id) => DrawnIds.Contains(id);

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
    /// Whether the delkilde and datasamling tree draws its node icons — see
    /// <see cref="KildeHierarchyView.ShowNodeIcons"/>, which this is passed straight to.
    /// </summary>
    /// <remarks>
    /// <see cref="VariableSearch"/> offers it further up, as the <c>Ikoner</c> switch in its filter
    /// panel, and hands the reader's own choice to this view when they drill into a kilde.
    /// <see cref="KildeSearch"/> offers nothing of the kind, so a host that wants the icons off in
    /// the kildeutforsker mounts this view, or <see cref="KildeHierarchyView"/>, itself.
    /// </remarks>
    [Parameter]
    public bool ShowNodeIcons { get; set; } = true;

    /// <summary>
    /// Where a datasamling in the tree can be opened, given its id — see
    /// <see cref="KildeHierarchyView.DatasamlingHref"/>, which this is passed straight to. Null, the
    /// default, draws no link.
    /// </summary>
    /// <remarks>
    /// Only <see cref="KildeSearch"/> sets it. <see cref="VariableSearch"/> reaches a datasamling
    /// through the variable that named one, so its tree leaves this null and the two explorers show
    /// the same view with one link each has a route for.
    /// </remarks>
    [Parameter]
    public Func<Guid, string>? DatasamlingHref { get; set; }

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

            var named = T.Named(delkilde.Name, delkilde.Code);

            builder.OpenElement(seq++, $"h{level}");
            builder.AddAttribute(seq++, "class",
                                 "headline headline-xxs margin--none munin-explorer-kilde__delkilde-name");
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign(named.Norwegian, Reader));
            builder.AddContent(seq++, named.Text);
            builder.CloseElement();

            // The kilde's own identifier line, one level down and wearing the same name - unless the
            // heading above has already fallen back to it (Fhi.Metadata-w13lk).
            if (named.Norwegian && Identifier(delkilde.Code, delkilde.ShortName) is { } identifiers)
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
