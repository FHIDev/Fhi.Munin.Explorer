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
/// sections reach it through <see cref="KildeView.Sections"/>; nothing Kelda-specific is added to
/// that component itself, which is the whole reason it is a core with slots rather than one view
/// with flags. The datasamling section's heading is not passed at all — it follows the source,
/// which is a fact about the source rather than about who is rendering it.
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
/// beside the results writes that rule itself. Seven are new and belong to this view:
/// <c>munin-explorer-kilder</c> for the result table,
/// <c>munin-explorer-kilder__name</c> for the control that opens a kilde,
/// <c>munin-explorer-kilder__count</c> for the two columns that hold a number,
/// <c>munin-explorer-kilder__select</c> for the checkbox column a host that wired
/// <see cref="ExploreVariablesRequested"/> gets in front of them, and
/// <c>munin-explorer-filters__toggle</c> and <c>munin-explorer-filters__facets</c> for the facet
/// panel's disclosure, and <c>munin-explorer-filters__count</c> for the number beside a facet
/// value — see <c>KildeExplorer.Filters.cs</c> for what those three are for. A host that
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
    /// Raised when the reader asks to explore variables for the kilder they have chosen, carrying
    /// the ids that go with them. Wire it, or no selection column is drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handover between the two explorers, and the reason it is a callback rather than a link:
    /// this component has no <c>NavigationManager</c> and no idea where the host mounted a
    /// <see cref="VariableExplorer"/>, so it says what the reader asked for and the host decides
    /// where that goes. <c>new VariableFilter { KildeIds = ids }.ToQueryString()</c> is the pairing
    /// that lands the ids in <see cref="VariableExplorer.Filter"/> — see
    /// <c>KildeExplorer.Selection.cs</c> for the whole of the reasoning, and both sample hosts for
    /// it written out.
    /// </para>
    /// <para>
    /// What it carries is not always what is ticked. Ticked rows win; with nothing ticked but a
    /// search or a facet in force it carries the rows the reader is looking at; with neither it
    /// carries an empty list, which means the whole catalogue rather than a selection of none.
    /// </para>
    /// <para>
    /// A host that leaves this unwired gets no checkbox column, no count and no button — the ticks
    /// exist to reach a destination this component cannot reach on its own, and a control that
    /// leads nowhere is worse than one that is not there.
    /// </para>
    /// <para>
    /// "Unwired" includes wiring it from the wrong place, and that trap is worth stating exactly,
    /// because it cost this repository a sample that looked right. An
    /// <see cref="EventCallback"/> does not survive being passed from a statically-rendered parent
    /// into an interactive island. It is not rejected either — Blazor throws for a bare delegate
    /// parameter, but <see cref="EventCallback"/> is a struct, so it serialises as
    /// <c>{"HasDelegate":true}</c> and is read back inside the circuit as empty. Putting
    /// <c>@rendermode</c> on this component's own tag does NOT fix it: that makes the mount point
    /// interactive while the parent creating the callback stays static. What fixes it is the
    /// callback being created inside an interactive component — a wrapper the host renders
    /// interactively, which is what both sample hosts do. Where it is wrong the column is simply
    /// absent, which is quieter than a dead button but still a puzzle; the same applies to
    /// <see cref="SelectedKildeIdChanged"/>, where it has no visible symptom at all.
    /// </para>
    /// </remarks>
    [Parameter] public EventCallback<IReadOnlyList<Guid>> ExploreVariablesRequested { get; set; }

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

    /// <summary>
    /// Everything the view is initialised from: the list, the vocabulary its coded properties are
    /// read with, and — when the host mounts with a kilde already chosen — that kilde as well.
    /// </summary>
    /// <remarks>
    /// Three round trips, and the order they are started, awaited and drawn in is the whole of what
    /// the reader spends waiting. Two of them are what somebody is here for: the list is what the
    /// component is for, and an open kilde's detail is why a deep link was followed at all. The
    /// third is not: the vocabulary only decides whether two facets read as words or as CURIEs, and
    /// it fails silently into the second of those. So it is started first, awaited last, and
    /// nothing above that await waits on it.
    /// <para>
    /// The renders in between are not optional, and each one is a state somebody would otherwise
    /// sit in front of. This component asks for its own renders nowhere else, and
    /// <see cref="ComponentBase"/> draws when this method first yields and again when it returns
    /// and nothing in between — so without them the finished list sits behind
    /// <c>Laster kilder …</c>, and a landed kilde behind <c>Henter datakilden …</c>, until the
    /// vocabulary's round trip ends, up to <c>HttpClient</c>'s hundred-second default.
    /// </para>
    /// <para>
    /// Both halves of that have been wrong here, in the same way and one after the other: first the
    /// list was awaited before the vocabulary but drawn after it, and then the deep-linked kilde's
    /// fetch was not merely left undrawn but never issued at all until the vocabulary landed,
    /// because the await sat inside <see cref="LoadAsync"/> and this method could not reach
    /// <see cref="LoadKildeAsync"/> until it returned.
    /// </para>
    /// </remarks>
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

        // Started here and awaited at the bottom, so its round trip overlaps the two below rather
        // than queueing with them. Starting it cannot throw where the list's call can — it catches
        // its own, and an implementation that throws from the call rather than the await is caught
        // there too — which is why there is no try around this line and there is one around that.
        var vocabulary = LoadVocabularyAsync();

        await LoadAsync();

        // After the list, not before: the list is what knows the kilde's name, which is what the
        // open view's region is labelled by while its own fetch is still running. Read before the
        // render below rather than after it, so that render is the one that carries the name.
        _selectedName = _selectedId is { } named
            ? _kilder.FirstOrDefault(kilde => kilde.Id == named)?.Name
            : null;

        // The render that puts the list on screen — or, on a deep link, the named drilldown that
        // has replaced it.
        StateHasChanged();

        if (_selectedId is { } id)
        {
            await LoadKildeAsync(id);

            // And the render that puts the kilde on screen. The drilldown draws from the detail
            // record alone, so it owes the vocabulary nothing and must not wait behind it.
            StateHasChanged();
        }

        // Awaited here and not left running: a task nobody awaits would write the vocabulary in
        // after the render that needed it, with nothing to redraw the panel it labels.
        await vocabulary;

        // And the render that labels the panel. This is the last statement of the method, so
        // ComponentBase's own post-initialisation render draws the same thing today and deleting
        // this line breaks no test. It is kept because what the words depend on is the vocabulary
        // arriving, not this method ending: anything awaited below it — a second fetch, a callback
        // raised at the host — would take them off the panel again with nothing on screen saying
        // so. A render nobody needed costs a diff over some tens of rows.
        StateHasChanged();
    }

    /// <summary>
    /// The whole list, unfiltered.
    /// </summary>
    /// <remarks>
    /// No search parameter and no kildetype, though the endpoint takes both. Everything the reader
    /// narrows with is applied to the list already in hand — see the remarks on the class — so
    /// sending either would fetch a second, smaller list that the client-side filter would then
    /// filter again, and the counts beside the facets would be counted over a set the reader cannot
    /// get back to without another request.
    /// <para>
    /// The list and nothing else: the vocabulary its coded properties are read with is fetched
    /// beside it rather than in it, and neither the render that draws the list nor the one that
    /// draws an opened kilde belongs to this method. Both of those are orderings between the three
    /// calls rather than steps of any one of them — see <see cref="OnInitializedAsync"/>, where
    /// they are, and where they can be read in one place.
    /// </para>
    /// </remarks>
    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        try
        {
            _kilder = await Client.GetKilderAsync();
        }
        catch (MuninExplorerRateLimitedException)
        {
            // Throttled, not down — and the difference is the whole point of saying so: the text
            // below invites the reader to try again, which is what the limiter is counting.
            _error = T.RateLimitError;
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
    /// for one key from throwing at the reader instead of losing a label. It matters more than the
    /// usual defensive line because of the catch below: a throw here is swallowed whole and leaves
    /// the vocabulary empty, so one repeated key would cost every facet its words rather than one.
    /// The blank-key filter is not that guard by another route, and reading it as one overstates
    /// it: the grouping runs first and collapses two blank keys as readily as two real ones, so
    /// nothing there can throw whether the filter is present or not. What it does is keep a key
    /// that is not a key out of the dictionary at all — every lookup here is by a property name, so
    /// such an entry could only ever sit unread. Tidiness, and no test can tell it apart from its
    /// own absence.
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

    /// <summary>Empty the search box and put the whole list back.</summary>
    private void ClearSearch()
    {
        // Nothing is fetched — the search was only ever a filter over a list already in hand. The
        // facets are left alone: one control must not undo another. aria-disabled stops no click,
        // so the refusal lives here. (Fhi.Metadata-5ghur)
        if (SearchText is null)
        {
            return;
        }

        _search = null;
    }

    /// <summary>Open <paramref name="kilde"/>'s view, in place of the list.</summary>
    private async Task SelectAsync(KildeSummary kilde)
    {
        _selectedId = kilde.Id;
        _selectedName = kilde.Name;

        // Before the callback rather than inside LoadKildeAsync after it: an asynchronous host —
        // one writing the URL, say — yields, and ComponentBase draws the open view in that gap.
        // Without this that frame says aria-busy "false" for a fetch that has not been issued.
        _detailLoading = true;

        await RaiseAsync(SelectedKildeIdChanged, _selectedId);

        // _selectedId rather than the captured id — the rule ToggleDetailAsync follows for what the
        // host is told, here for what is fetched: the callback above yields with Back drawn and
        // clickable, and a fetch for a kilde the reader has left would undo the close.
        if (_selectedId == kilde.Id)
        {
            await LoadKildeAsync(kilde.Id);
        }
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
        catch (Exception ex)
        {
            // One branch for both failures, so the stale-fetch guard is written once: a fetch the
            // reader has already moved on from must not paint its answer — of either kind — into the
            // panel now showing something else.
            if (generation != _detailGeneration)
            {
                return;
            }

            // Only the sentence differs. A kilde that did not arrive because we asked too often is
            // neither a kilde that is missing nor a catalogue that is down, and the generic text
            // invites the retry the limiter is counting.
            _detailError = ex is MuninExplorerRateLimitedException ? T.RateLimitError : T.KildeError;
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

    /// <summary>The year the kilde was founded, as the import file states it.</summary>
    /// <remarks>
    /// Not <see cref="KildeSummary.Created"/>, which is when Munin's own row was written — Kelda
    /// draws that as Importert and keeps it off by default. Handed on verbatim because the source
    /// holds "2916", "1900" and "0", and a formatter asked to read those hides a fault at source.
    /// <para>
    /// The lookup is ordinal, so the key's spelling is the whole contract: get it wrong and every
    /// row reads "Ikke oppgitt" with nothing failing. It is pinned to a captured payload rather
    /// than to a test's own bag — see <c>Testdata/kilder.json</c> and the test named for it.
    /// </para>
    /// </remarks>
    private static string? Established(KildeSummary kilde) => Property(kilde, "Opprettet");

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
