using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Search and browse the catalogue's kilder — Kelda, the kildeutforsker, beside
/// <see cref="VariableExplorer"/> in the same package.
/// </summary>
/// <remarks>
/// <para>
/// The same host contract the variable explorer follows, for the same reasons: no <c>@page</c>, no
/// <c>@rendermode</c>, no router, no <c>HeadOutlet</c> and no CSS. One parameterised root
/// component that the host mounts wherever it likes, at whatever render mode it likes.
/// </para>
/// <para>
/// What it deliberately does <em>not</em> have is the machinery the variable explorer needs, and
/// that is a decision recorded under the Kelda epic rather than a gap. There is no paging and no
/// sorting: <see cref="IMuninExplorerClient.GetKilderAsync"/> answers with the whole list in one
/// array — 72 active kilder measured on 2026-08-25, against tens of thousands of variables — and
/// the API returns it ordered by name. So the list is fetched <em>once</em>, on initialisation,
/// with no search and no kildetype, and everything the reader does afterwards happens over the
/// list already in hand. That is also why the facets are counted client-side and are not
/// cross-filtered the way Runa's are — see <c>KildeExplorer.Filters.cs</c>.
/// </para>
/// <para>
/// Searching is therefore a filter over that list rather than a request: name, code and short
/// name, case-insensitively, which is the same three fields Munin's own Kelda matches. The field
/// still binds on <c>onchange</c> rather than <c>oninput</c>, and that is not about the API at
/// all — this renders inside helsedata's Blazor Server circuit, where <c>oninput</c> is one
/// round-trip per keystroke whatever the handler does with it, and each round-trip rewrites the
/// input while more input is still arriving. <c>Fhi.Metadata-l9l2n.26</c> is where that was paid
/// for once already; the same shape is used here from the start.
/// </para>
/// <para>
/// Selecting a kilde swaps the list for <see cref="KildeView"/> — the same component the variable
/// explorer drills into, so the two cannot render the same source differently. Kelda's own
/// sections reach it through <see cref="KildeView.Sections"/> and its own heading for the
/// datasamling table through <see cref="KildeView.DataCollectionsHeading"/>; nothing
/// Kelda-specific is added to that component itself, which is the whole reason it is a core with
/// slots rather than one view with flags.
/// </para>
/// <para>
/// The sections are the measured difference between the two explorers rather than an invention
/// here. On 2026-08-20 the same kilde in both drew the same name block, the same eight metadata
/// groups in the same order and the same two sidebar boxes; the datasamling section was the same
/// rows under a different word, and Kelda had three sections Runa has not — Variabler, Kriterier
/// for tilgang til data and Priser. Those three are markup in this component's own file, passed
/// into the shared core. What a host passes as <see cref="Sections"/> follows them.
/// </para>
/// <para>
/// Class names: the ordinary page furniture wears <c>Fhi.Helsedata.Stiler</c>'s own names —
/// <c>headline</c>, <c>caption</c>, <c>form-element__label</c>, <c>searchbox__freetext*</c>,
/// <c>hd-button-square</c> with its modifiers, <c>infobox</c> — and the structure wears the
/// <c>munin-explorer</c> prefix this package owns. <c>munin-explorer</c>,
/// <c>munin-explorer-container</c>, <c>munin-explorer-results</c>, <c>munin-explorer-filters</c>
/// and <c>munin-explorer-drilldown</c> are the explorer's existing ones, reused rather than
/// reinvented — two of which, <c>munin-explorer</c> and <c>munin-explorer-filters</c>, are handles
/// nothing defines a rule for, in this package or in Stiler, so a host that wants the panel placed
/// beside the results writes that rule itself. Five are new and belong to this view:
/// <c>munin-explorer-kilder</c> for the result table,
/// <c>munin-explorer-kilder__name</c> for the control that opens a kilde,
/// <c>munin-explorer-kilder__count</c> for the three columns that hold a number, and
/// <c>munin-explorer-filters__toggle</c> and <c>munin-explorer-filters__facets</c> for the facet
/// panel's disclosure — see <c>KildeExplorer.Filters.cs</c> for what those two are for. A host that
/// styles none of them still gets a usable list, which is why the results are a
/// <c>&lt;table&gt;</c> and the name is a <c>&lt;button&gt;</c>: an element degrades to its own
/// browser default — aligned columns, a control that visibly is one — where a class name no
/// stylesheet has heard of degrades to nothing at all.
/// </para>
/// </remarks>
public sealed partial class KildeExplorer : ComponentBase
{
    /// <summary>
    /// Initial search text. Set by the host; the component owns it afterwards.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, exactly as <see cref="VariableExplorer.Search"/> is. There is
    /// no <c>SearchChanged</c> beside it, and that is the Kelda parity decision rather than an
    /// omission: search, filters and column choices are component state that goes away on refresh,
    /// and the one thing worth putting in a host's URL is which kilde is open — which is what
    /// <see cref="SelectedKildeIdChanged"/> is for.
    /// </remarks>
    [Parameter] public string? Search { get; set; }

    /// <inheritdoc cref="VariableExplorer.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableExplorer.HeadingLevel"/>
    [Parameter] public int HeadingLevel { get; set; } = 2;

    /// <summary>
    /// The kilde whose view is open, or null when the list is showing. Set by the host, typically
    /// from its own URL.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, and owned by the component afterwards. An id the catalogue
    /// does not publish opens a view that says so rather than throwing — an id in a URL somebody
    /// edited is a normal event on a public page.
    /// </remarks>
    [Parameter] public Guid? SelectedKildeId { get; set; }

    /// <summary>
    /// Raised when the open kilde changes, so the host can reflect it in its own URL. The
    /// SelectedKildeId/SelectedKildeIdChanged naming gives the host
    /// <c>@bind-SelectedKildeId</c> for free.
    /// </summary>
    /// <remarks>
    /// A host mounting this component must make the mount point fully interactive. An
    /// <see cref="EventCallback"/> serialises to an empty delegate across a static-SSR to
    /// interactive-island boundary, and the callback then silently never fires.
    /// <para>
    /// It carries null when the reader goes back to the list. It is raised whether or not the
    /// kilde's detail could be fetched: the view is open either way, and a host whose URL only
    /// followed the successful fetches would hand out a link back to the list.
    /// </para>
    /// </remarks>
    [Parameter] public EventCallback<Guid?> SelectedKildeIdChanged { get; set; }

    /// <summary>
    /// The host's own sections for an open kilde, placed after Kelda's.
    /// </summary>
    /// <remarks>
    /// The seam that keeps <see cref="KildeView"/> a shared core: Kelda's own sections — its
    /// variables, its access criteria, its prices — are markup that goes <em>into</em> that
    /// component rather than markup added to it, and this parameter is the same door held open for
    /// whoever embedded the explorer. It is not passed straight through: what reaches
    /// <see cref="KildeView.Sections"/> is Kelda's three sections and then this, in that order,
    /// because a host's section is an addition to the page it embedded rather than a replacement
    /// for what the component is.
    /// </remarks>
    [Parameter] public RenderFragment? Sections { get; set; }

    [Inject] private IMuninExplorerClient Client { get; set; } = null!;

    // The whole list, fetched once. Never refetched: nothing the reader can do to this component
    // asks the API a different question, which is the point of an endpoint that is not paged.
    private IReadOnlyList<KildeSummary> _kilder = [];

    // The catalogue's own words for the coded properties the list carries, keyed by property. Two
    // of the facets draw their choices out of it — see KildeExplorer.Filters.cs — and it stays
    // empty when the fetch for it fails, which costs those choices their labels and nothing else.
    private IReadOnlyDictionary<string, PropertyMetadataEntry> _vocabulary =
        new Dictionary<string, PropertyMetadataEntry>(StringComparer.OrdinalIgnoreCase);

    // Whether that one fetch has answered, however it answered. The empty state and the count both
    // hang off it, so an empty list before the answer arrives does not read as "no kilder".
    private bool _loaded;
    private bool _loading;
    private string? _error;

    // The live contents of the search box. @bind writes it on change rather than on input, so it
    // holds a finished word rather than a prefix — see the remarks on the class and the comment on
    // the element itself. The filtering is done straight off it: with no request behind it there is
    // nothing for a separate "executed search" to be truer about.
    private string? _search;

    // The kilde whose view is open, what has been fetched for it, and the name the list already
    // knew — which is what the region can be labelled by while the fetch is still running.
    private Guid? _selectedId;
    private string? _selectedName;
    private KildeDetail? _kilde;
    private bool _detailLoading;
    private string? _detailError;

    // Bumped by every open and every close, so a detail fetch can tell whether the view it is
    // about to write into is still the one it was started for. The id alone cannot say that: it
    // names the kilde, not the call, and closing a kilde and opening the same one again is two
    // calls carrying one id.
    private int _detailGeneration;

    // Unique per instance so two explorers on one page cannot collide on DOM ids, which would be a
    // WCAG 4.1.1 failure as well as breaking label association.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    private string TitleId => $"munin-explorer-title-{_instance}";
    private string SearchId => $"munin-explorer-search-{_instance}";
    private string DetailId => $"munin-explorer-detail-{_instance}";
    private string DetailHeadingId => $"munin-explorer-heading-{_instance}";

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The component's own heading level, clamped into the range that is a heading.</summary>
    private int TitleLevel => Math.Clamp(HeadingLevel, 1, 6);

    /// <summary>
    /// The heading level for the open kilde: one step below the component's own title, so the
    /// outline stays unbroken however deep the host mounted us. Flattens at <c>h6</c> rather than
    /// breaking, for the reason <see cref="VariableExplorer.HeadingLevel"/> spells out.
    /// </summary>
    private int KildeLevel => Math.Clamp(TitleLevel + 1, 1, 6);

    /// <summary>
    /// The heading level for Kelda's own sections: one step below the open kilde's name, which is
    /// where <see cref="KildeView"/> puts the blocks it draws itself.
    /// </summary>
    /// <remarks>
    /// The two have to agree, and the value they agree on is private to that component — so this
    /// mirrors its arithmetic rather than reading it, and a test asserts that Kelda's sections come
    /// out on the same level as the core's own headings. Without that, "Variabler" would read as a
    /// part of the datasamlinger above it rather than as a section beside them, which is a claim
    /// about the document made to everyone navigating it by heading.
    /// </remarks>
    private int SectionLevel => Math.Clamp(KildeLevel + 1, 1, 6);

    private string DetailBusy => _detailLoading ? "true" : "false";

    /// <summary>
    /// The kilder the search and the facets leave, in the order the API sent them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Name, code and short name, which is the same three fields Munin's own Kelda matches on.
    /// Case-insensitive and by substring, so "als" finds both <c>K_ALS</c> and "Als registeret",
    /// and a reader who knows only the short name finds the row without knowing the long one.
    /// </para>
    /// <para>
    /// Ordinal rather than culture-aware comparison, deliberately: the culture a Blazor Server
    /// circuit runs under is the host's and not the reader's, so a culture-sensitive match would
    /// answer differently on two machines serving the same page. Every letter in these three
    /// fields is Latin, including æ, ø and å, which ordinal case folding handles.
    /// </para>
    /// <para>
    /// The facets narrow the same list, after the search and by the same client-side reading of it
    /// — see <c>KildeExplorer.Filters.cs</c> for what they are and why none of them is a request.
    /// Search and facets are AND: a reader who has typed a word and ticked a box is asking for
    /// kilder that answer both.
    /// </para>
    /// <para>
    /// No re-ordering. The API returns the list ordered by name and Kelda offers no sort control,
    /// so the sequence on screen is the sequence that arrived — which is what makes a row's
    /// position stable while the reader types.
    /// </para>
    /// </remarks>
    private IReadOnlyList<KildeSummary> Visible
    {
        get
        {
            var searched = Searched(SearchText);

            return _chosen.Values.All(values => values.Count == 0)
                ? searched
                : [.. searched.Where(MatchesFacets)];
        }
    }

    /// <summary>The kilder the search leaves, before the facets have had their turn.</summary>
    /// <remarks>
    /// It takes <see cref="SearchText"/> rather than trimming <c>_search</c> itself, so there is
    /// one definition of the search as it counts: a field holding only spaces is no search, and two
    /// places deciding that separately is how they come to disagree.
    /// </remarks>
    private IReadOnlyList<KildeSummary> Searched(string? term) =>
        string.IsNullOrEmpty(term) ? _kilder : [.. _kilder.Where(kilde => Matches(kilde, term))];

    private static bool Matches(KildeSummary kilde, string term) =>
        Contains(kilde.Name, term) || Contains(kilde.Code, term) || Contains(kilde.ShortName, term);

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One sentence describing the visible result — "72 kilder" — used both as the live
    /// announcement and as the table's accessible name, so the two cannot drift apart.
    /// </summary>
    /// <remarks>
    /// It is a count and nothing else. The variable explorer's equivalent names the row range and
    /// the ordering as well, because it has a pager and sortable headers; this list has neither, so
    /// there is nothing further to say and a sentence claiming otherwise would be furniture.
    /// <para>
    /// It takes the list rather than reading <see cref="Visible"/> itself, so that the sentence and
    /// the rows underneath it are counted off one read of the filter — see the capture at the top
    /// of the markup's list branch.
    /// </para>
    /// </remarks>
    private string Summary(IReadOnlyList<KildeSummary> visible) => T.KildeCount(visible.Count);

    /// <summary>The search text as it is worth reporting back, which is nothing when it is blank.</summary>
    private string? SearchText => string.IsNullOrWhiteSpace(_search) ? null : _search.Trim();

    protected override async Task OnInitializedAsync()
    {
        _search = Search;
        _selectedId = SelectedKildeId;

        // Raised here rather than in LoadKildeAsync, which cannot start until the list has
        // answered. The drilldown is on screen from the first render, and ComponentBase draws it
        // as soon as the await below yields — so without this the reader arrives at a view whose
        // aria-busy says false, whose status line is empty and whose heading reports a finished,
        // empty fetch that has not been made.
        _detailLoading = _selectedId is not null;

        await LoadAsync();

        // After the list, not before: the list is what knows the kilde's name, which is what the
        // open view's region is labelled by while its own fetch is still running.
        if (_selectedId is { } id)
        {
            _selectedName = _kilder.FirstOrDefault(kilde => kilde.Id == id)?.Name;
            await LoadKildeAsync(id);
        }
    }

    /// <summary>
    /// The whole list, unfiltered, and the vocabulary its coded properties are read with.
    /// </summary>
    /// <remarks>
    /// No search parameter and no kildetype, though the endpoint takes both. Everything the reader
    /// narrows with is applied to the list already in hand — see the remarks on the class — so
    /// sending either would fetch a second, smaller list that the client-side filter would then
    /// filter again, and the counts beside the facets would be counted over a set the reader cannot
    /// get back to without another request.
    /// <para>
    /// Two calls rather than one, in flight together and awaited apart. They answer different
    /// questions and fail independently: the list is what the component is for, while the
    /// vocabulary only decides whether two facets read as words or as CURIEs. Started together so
    /// the two round trips overlap rather than queue; awaited apart because a vocabulary that never
    /// came back must not leave the page saying the catalogue could not be reached.
    /// </para>
    /// <para>
    /// The render between the two awaits is what makes a reader waiting for the list not also wait
    /// for a round trip that has nothing to do with them, and it is not optional. This component
    /// asks for its own renders nowhere else: <see cref="ComponentBase"/> draws when
    /// <see cref="OnInitializedAsync"/> first yields and again when it returns, and nothing in
    /// between — so without it, starting both together would overlap the network and nothing else,
    /// and the finished list would sit behind <c>Laster kilder …</c> until the slower of the two
    /// landed, up to <c>HttpClient</c>'s hundred-second default.
    /// </para>
    /// </remarks>
    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        // Started first and awaited last, so both calls are in flight at once. It cannot throw
        // where the list can — it catches its own — which is why the list's own call stays inside
        // the try: a client implementation is free to throw from the call rather than the await.
        var vocabulary = LoadVocabularyAsync();

        try
        {
            _kilder = await Client.GetKilderAsync();
        }
        catch (Exception)
        {
            // What went wrong is the API's business and the host's logs'; what the reader needs is
            // a sentence saying the list is not there and that trying again is worth doing. The
            // list is left as it was, which on the first load is empty.
            _error = T.KildeListError;
        }
        finally
        {
            _loading = false;
            _loaded = true;
        }

        // The render that puts the list on screen. Asked for here rather than left to the one
        // ComponentBase performs when OnInitializedAsync returns, because that one comes after the
        // await below — see the remarks: without this the list is held off screen for exactly as
        // long as the vocabulary takes.
        StateHasChanged();

        // Awaited here and not left running: a task nobody awaits would write the vocabulary in
        // after the render that needed it, with nothing to redraw the panel it labels.
        await vocabulary;

        // And the render that labels the panel. Redundant as this is called today — the sole caller
        // is OnInitializedAsync, which returns straight afterwards and gets ComponentBase's own
        // render — but the arrival of a vocabulary is what draws the words for it, and leaving that
        // to whatever the caller happens to do next is how a second caller loses them silently.
        // A render nobody needed costs a diff over some tens of rows.
        StateHasChanged();
    }

    /// <summary>
    /// The catalogue's own vocabulary for the curated properties the list sends as bare codes.
    /// </summary>
    /// <remarks>
    /// A sibling of the list rather than part of it, because the vocabulary is one definition per
    /// property and not one per kilde — see <see cref="IMuninExplorerClient.GetKildePropertyMetadataAsync"/>.
    /// It is fetched at all so that the facets and the kilde view a click away cannot disagree
    /// about what a token is called: both read the words the catalogue holds now, rather than one
    /// of them reading a table transcribed into this package on some earlier day.
    /// <para>
    /// A failure costs labels, not the list, so it is caught here rather than reported: the facets
    /// fall back to showing the catalogue's own tokens, which is what they show for a value the
    /// vocabulary does not list either way. A sentence about a vocabulary is not something a reader
    /// of a kilde list can act on, and it would sit beside a panel that is still usable.
    /// </para>
    /// <para>
    /// One entry per key is what the endpoint promises; the grouping is what keeps a second entry
    /// for one key from throwing at the reader instead of losing a label. The blank-key filter is
    /// the same guard by another route — a key that is not a key looks up nothing either way, but
    /// two of them collide as surely as two real ones. Both matter more than the usual defensive
    /// line because of the catch below: a throw here is swallowed whole and leaves the vocabulary
    /// empty, so one repeated key would cost every facet its words rather than one.
    /// </para>
    /// <para>
    /// No cancellation token, and the omission is deliberate rather than overlooked: this component
    /// holds none to pass — it is not disposable and opens no token source — so like every other
    /// call it makes, the fetch runs to completion and its result is dropped when the reader has
    /// already left. That is one abandoned request per abandoned circuit, for a call made once per
    /// component. It follows that the catch below never sees a disposal cancellation; what it can
    /// see is <c>HttpClient</c>'s own timeout, which arrives as a cancellation and is a vocabulary
    /// that did not answer, which is exactly how it is treated. Should a token ever be threaded
    /// through here, the two stop being the same thing and the cancellation has to be let out.
    /// </para>
    /// </remarks>
    private async Task LoadVocabularyAsync()
    {
        try
        {
            var entries = await Client.GetKildePropertyMetadataAsync();

            _vocabulary = entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Left as it was, which on the first load is empty.
        }
    }

    /// <summary>
    /// What submitting the search form does, which is nothing.
    /// </summary>
    /// <remarks>
    /// Deliberately empty, and deliberately here rather than left off the element. The list is
    /// already in hand and the field's own <c>onchange</c> is what applies the search — pressing
    /// Enter, or clicking Søk from inside the field, fires it before the submit arrives. What the
    /// form is for is that Enter keystroke: without it the reader has to blur the field to search,
    /// and without the handler and its <c>preventDefault</c> the browser would reload the host's
    /// whole page instead. A named method rather than an inline lambda so the emptiness reads as a
    /// decision.
    /// </remarks>
    private static void Submit()
    {
    }

    /// <summary>Open <paramref name="kilde"/>'s view, in place of the list.</summary>
    private async Task SelectAsync(KildeSummary kilde)
    {
        _selectedId = kilde.Id;
        _selectedName = kilde.Name;

        await RaiseAsync(SelectedKildeIdChanged, _selectedId);
        await LoadKildeAsync(kilde.Id);
    }

    /// <summary>Go back to the list.</summary>
    /// <remarks>
    /// The generation is bumped here as well as on opening, so a fetch still in flight for the
    /// kilde being closed cannot write its answer into a component that is showing the list again.
    /// </remarks>
    private async Task CloseAsync()
    {
        _detailGeneration++;
        _selectedId = null;
        _selectedName = null;
        _kilde = null;
        _detailError = null;
        _detailLoading = false;

        await RaiseAsync(SelectedKildeIdChanged, null);
    }

    private async Task LoadKildeAsync(Guid id)
    {
        var generation = ++_detailGeneration;

        _kilde = null;
        _detailError = null;
        _detailLoading = true;

        try
        {
            var detail = await Client.GetKildeAsync(id);

            if (generation != _detailGeneration)
            {
                return;
            }

            _kilde = detail;

            // Null is the answer for a kilde the catalogue does not publish, which is not a fault —
            // see the remarks on IMuninExplorerClient — so it says so rather than reporting an error
            // the API never had.
            _detailError = detail is null ? T.KildeMissing : null;
        }
        catch (Exception)
        {
            if (generation != _detailGeneration)
            {
                return;
            }

            _detailError = T.KildeError;
        }
        finally
        {
            if (generation == _detailGeneration)
            {
                _detailLoading = false;
            }
        }
    }

    /// <summary>
    /// The one message the open view's status line holds at a time: loading, or why it is empty.
    /// </summary>
    private string? DetailStatus => _detailLoading ? T.KildeLoading : _detailError;

    /// <summary>
    /// <c>caption</c> normally, and the warning box when the line is carrying a failure — the same
    /// treatment the variable explorer's panel gives its own status line.
    /// </summary>
    private string DetailStatusClass =>
        !_detailLoading && _detailError is not null ? "infobox infobox--bg-yellow" : "caption";

    /// <summary>
    /// The title, at the level the host asked for. Razor has no syntax for a computed element
    /// name, so this is built by hand.
    /// </summary>
    /// <remarks>
    /// The visual size is pinned with Stiler's <c>headline-3</c> rather than left to the element,
    /// because the element is the host's choice: without it, mounting the explorer one level
    /// deeper would silently shrink its title.
    /// </remarks>
    private RenderFragment Heading => builder =>
    {
        builder.OpenElement(0, $"h{TitleLevel}");
        builder.AddAttribute(1, "class", "headline headline-3");
        builder.AddAttribute(2, "id", TitleId);
        builder.AddContent(3, T.KildeTitle);
        builder.CloseElement();
    };

    /// <summary>
    /// The open view's own heading, drawn only until <see cref="KildeView"/> arrives with one of
    /// its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The region points at this id, and a landmark whose label does not exist yet is worse than a
    /// plain one — so something has to carry it from the first render. The list already knew the
    /// kilde's name, so the reader is told which kilde they opened while it loads rather than
    /// after.
    /// </para>
    /// <para>
    /// When it did not, the heading follows the load state instead of standing on "Henter
    /// datakilden …" forever: the list cannot name a kilde whose id the catalogue does not publish,
    /// nor any kilde at all when the list itself failed to load, and both are states this heading
    /// outlives. It falls back to the status line's own sentence rather than a second wording of
    /// it, so the landmark's name cannot contradict the status underneath it — which is exactly
    /// what a permanent "loading" over a finished, failed fetch did.
    /// </para>
    /// <para>
    /// The third fallback is defensive only, and its wording is chosen for that:
    /// <see cref="DetailStatus"/> is null exactly when nothing is loading and nothing went wrong,
    /// which for an open view means the detail arrived — and then <see cref="KildeView"/> owns the
    /// heading and this fragment is not drawn at all. The one state that did reach it was the very
    /// first render of a host-named kilde, before the list had answered and the detail fetch had
    /// begun, where it announced "no details found" for a fetch nobody had made yet;
    /// <see cref="OnInitializedAsync"/> raises the loading flag there instead, so the status line
    /// now carries the same sentence this would.
    /// </para>
    /// </remarks>
    private RenderFragment DrilldownHeading => builder =>
    {
        builder.OpenElement(0, $"h{KildeLevel}");
        builder.AddAttribute(1, "class", "headline headline-s");
        builder.AddAttribute(2, "id", DetailHeadingId);
        builder.AddAttribute(3, "lang", CatalogueLang(_selectedName));
        builder.AddContent(4, _selectedName ?? DetailStatus ?? T.KildeLoading);
        builder.CloseElement();
    };

    /// <summary>
    /// The heading over one of Kelda's own sections, at <see cref="SectionLevel"/>.
    /// </summary>
    /// <remarks>
    /// Built by hand for the reason <see cref="Heading"/> is: Razor has no syntax for a computed
    /// element name, and the level is the host's choice rather than this component's. It wears
    /// <c>headline-s</c>, which is what <see cref="KildeView"/> gives the blocks it draws itself,
    /// so a reader cannot see which side of the seam a section came from.
    /// </remarks>
    private RenderFragment SectionHeading(string text) => builder =>
    {
        builder.OpenElement(0, $"h{SectionLevel}");
        builder.AddAttribute(1, "class", "headline headline-s");
        builder.AddContent(2, text);
        builder.CloseElement();
    };

    /// <summary>
    /// The catalogue's own language for a value that really is the catalogue's, and nothing at all
    /// for one this package supplied.
    /// </summary>
    /// <remarks>
    /// A <c>lang</c> the content is not in is worse than none: it switches a screen reader to a
    /// Norwegian voice for an English sentence, which is WCAG 3.1.2. So the empty values — where
    /// what is on screen is <see cref="Texts.NotSpecified"/>, in the reader's language — are
    /// marked as nothing, and <see cref="CatalogueProperties.Foreign(string, string)"/> answers
    /// null for a reader already reading Norwegian.
    /// </remarks>
    private string? CatalogueLang(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : CatalogueProperties.Foreign("no", Reader);

    /// <summary>A cell's value, with the package's own words for one the catalogue left empty.</summary>
    private string Value(string? value) => string.IsNullOrWhiteSpace(value) ? T.NotSpecified : value;

    /// <summary>
    /// Invoke a host callback without letting the host's own exception out.
    /// </summary>
    /// <remarks>
    /// The reasoning is spelled out once, on <c>VariableExplorer.RaiseAsync</c>: a handler that
    /// navigates throws <see cref="NavigationException"/> during static SSR and the framework needs
    /// it, while anything else escaping here would tear down the circuit for the whole CMS page
    /// rather than for this component.
    /// </remarks>
    private static async Task RaiseAsync<TValue>(EventCallback<TValue> callback, TValue value)
    {
        if (!callback.HasDelegate)
        {
            return;
        }

        try
        {
            await callback.InvokeAsync(value);
        }
        catch (NavigationException)
        {
            throw;
        }
        catch (Exception)
        {
            // Nothing is said to the reader: what broke is the host's own URL handling, which is
            // the host's bug to find in the host's logs.
        }
    }
}
