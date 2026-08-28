using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The panel's tabs. Runa splits the open row into what the variable IS and what its data holds;
/// this is that split, not helsedata's — their page is the one being replaced.
/// </summary>
internal enum PanelTab
{
    /// <summary>Description, placement and properties.</summary>
    Details,

    /// <summary>The kodeverk the values are drawn from.</summary>
    Data,
}


/// <summary>
/// Search and browse published variables from the Munin Explorer API.
/// </summary>
/// <remarks>
/// <para>
/// This package ships no CSS, so the host stylesheet owns everything visual. The class names
/// the markup emits are therefore not ours to invent: they are the ones
/// <c>Fhi.Helsedata.Stiler</c> already defines, so that on helsedata.no the component is
/// styled by the site it is embedded in rather than by whatever we guessed. The families used
/// are <c>form-element__label</c>, <c>form-fieldset</c>, <c>searchbox__freetext*</c>,
/// <c>hd-button-square</c> with <c>button-square--primary</c>, <c>button-square--secondary</c>,
/// <c>button-square--ghost</c>, <c>hd-button-reset</c>, <c>margin-right</c>, <c>margin-bottom</c>,
/// <c>margin--bottom</c> and <c>margin--none</c>, <c>headline</c> with <c>headline-3</c>,
/// <c>headline-s</c> and <c>headline-xxs</c>, <c>caption</c>, <c>ingress</c>, <c>tag</c>,
/// <c>dot</c>, <c>infobox</c> with <c>infobox--bg-yellow</c>, and <c>screenreader-only</c>.
/// </para>
/// <para>
/// That list used to include <c>datasourcecard*</c>, the card list helsedata's own
/// datakildeutforsker renders its results with. Since <c>Fhi.Metadata-zs56s</c> the results are
/// their variable page's own rows instead — see the paragraph on <c>variables.css</c> below — and
/// the markup emits no <c>datasourcecard</c> name at all.
/// </para>
/// <para>
/// The result vocabulary is not Stiler's at all, and that is a dependency rather than an oversight.
/// Since <c>Fhi.Metadata-zs56s</c> the component renders helsedata's own variable page rather than
/// a shape of its own, so the rows (<c>munin-explorer-data-list*</c>, <c>munin-explorer-dataitem-*</c>), the
/// list they sit in (<c>munin-explorer-container</c>, <c>munin-explorer-results</c>), the
/// opened panel (<c>munin-explorer-meta*</c>), the column picker's names listed below and the pager
/// (<c>munin-explorer-pagination</c>, <c>munin-explorer-pagination-content</c>) were read off the
/// page-specific <c>variables.css</c> that page carries rather than off the site-wide stylesheet.
/// The shape is still theirs; the names are not. The pager was the last of them to be renamed,
/// under <c>Fhi.Metadata-hyyxl</c>: Stiler defines no pagination rule of its own — its compiled
/// stylesheet has no <c>pagination</c>, <c>pager</c>, <c>paging</c>, <c>page-link</c> or
/// <c>page-item</c> — while <c>variables.css</c> does, and despite its name that stylesheet is
/// served on every page of helsedata.no, so borrowing cost nothing inside their estate and left
/// every host outside it drawing a pager at browser defaults. The rules for the whole prefix,
/// pager included, ship in <c>Fhi.Helsedata.Stiler</c> under <c>components/munin-explorer/</c> —
/// the pager's from 0.1.14, which is also where the skip link into it landed under
/// <c>Fhi.Metadata-ja2qu</c>, so on 0.1.13 the pager and nothing else renders at browser defaults.
/// The skip link was the last borrowed name of all, helsedata's <c>skiplink-pagination</c>, and it
/// failed backwards from every other missing rule: what was missing was the rule that HIDES the
/// link until it is focused, so a Stiler-only host drew a permanently visible "Hopp til
/// paginering" over every multi-page result list rather than an unstyled anything. It is
/// <c>munin-explorer-skiplink-pagination</c> now, and its Stiler rule is deliberately unscoped, so
/// it matches this anchor wherever the markup puts it — a rule scoped under
/// <c>munin-explorer-header</c> would not, since that header opens and closes entirely inside
/// <c>ColumnPicker()</c> while this anchor is rendered beside the result list. The gap survived
/// as long as it did because nothing here could see it: the two guards in this repository ask
/// whether a name has a rule in the capture of helsedata's live page or in the sample stylesheet,
/// and helsedata's <c>variables.css</c> styled the borrowed name while both samples styled it
/// themselves. Neither guard reads Stiler, so neither had anything to say about the one host that
/// has only Stiler. <c>README.md</c> has the full split.
/// </para>
/// <para>
/// Two parts of helsedata's own pager were deliberately not carried across when its shape was read
/// off <c>variables.css</c>, and the prefix has no equivalent of either.
/// <c>variables-pagination-mobile</c> is a second copy of the controls that their media queries
/// swap in; rendering it too would put two "Neste" buttons for one list in the tab order and in the
/// accessibility tree, so this renders the one pager at every width. The <c>__expired</c> modifiers
/// describe a state this component does not have — it never lists expired variables — and a
/// modifier whose meaning cannot be read back off the stylesheet is exactly the guess this package
/// exists to avoid.
/// </para>
/// <para>
/// The filter panel adds no class name to that list. Stiler has no accordion, no tree and no
/// checkbox whose names can be read back off its compiled stylesheet — and helsedata's own sidebar
/// is styled from <c>filter-search-explorer</c> in its page-specific <c>variables.css</c>, a rule
/// this repository has not read back — the result vocabulary comes from that same stylesheet, so
/// what is unverified here is the one name and not the file. So the panel is <c>&lt;details&gt;</c>
/// for the disclosure, a nested <c>&lt;ul&gt;</c> for the kilde/delkilde hierarchy and the square
/// button in its two states for the values, and what a host supplies is base styling for those
/// three elements rather than three more names. List indentation is the part that matters: without
/// it the hierarchy still nests in the accessibility tree but reads flat on screen.
/// <c>munin-explorer-filters</c> is a DOM handle for placing the panel, and carries no styling,
/// exactly like the <c>munin-explorer</c> root.
/// </para>
/// <para>
/// The hierarchy trail over the results adds two names of ours — <c>munin-explorer-breadcrumb</c>
/// and its <c>__clear</c> — and reuses <c>munin-explorer-crumb</c>, which the variable panel's
/// kilde trail already wears, for the steps themselves. It is an <c>&lt;ol&gt;</c> of
/// <c>&lt;button&gt;</c>s for the reason that trail is one: Stiler has no breadcrumb rule that can
/// be read back off its compiled stylesheet, so the chevrons between the steps are a host's to
/// draw and a host that draws nothing gets a numbered list that still reads correctly, in order,
/// with the right names.
/// </para>
/// <para>
/// The column picker adds eight names, all of them helsedata's own and none of them ours. They
/// come from the same <c>variables.css</c> as the rest of the result vocabulary.
/// <c>munin-explorer-header</c> with its <c>__actions</c> and <c>__actions-button</c> place
/// the control above the list; <c>dropdown-choicepicker</c> with its
/// <c>--right</c> and <c>__item</c> draw the open list, positioned against an inline
/// <c>position: relative</c> exactly as their own markup does it; and the disclosure wears both
/// <c>munin-explorer__dropdown</c>, which is the z-index, and the bare <c>dropdown</c>, which is
/// the width their own actions row gives a trigger
/// (<c>.munin-explorer-header__actions .dropdown { width: 100% }</c>). Each toggle's label is
/// the button's own text rather than a span wearing a name, which is one name fewer to have to
/// find in a stylesheet. A host that has none of them still
/// gets a working disclosure — the shape is <c>&lt;details&gt;</c>, a <c>&lt;ul&gt;</c> and the
/// square button in two states, the same three elements the filter panel leans on — it is drawn
/// in the flow rather than over the list. What it must supply either way is
/// <c>screenreader-only</c>, or the sentence explaining why the last column will not turn off is
/// on screen for everyone — and two rules that take the browser's disclosure marker off the
/// <c>&lt;summary&gt;</c>, which is <c>display: list-item</c> by default and would otherwise draw
/// a triangle beside a button that has none. helsedata's own trigger is a <c>&lt;button&gt;</c>,
/// so their stylesheet has no reason to carry those two.
/// </para>
/// <para>
/// The detail panel adds no class name either, and for the same reason. It is a
/// <c>&lt;dl&gt;</c> of labels and values, an <c>&lt;ol&gt;</c> for the kilde trail and a
/// <c>&lt;ul&gt;</c> for the variabelgrupper and kodeverk, wearing Stiler's
/// <c>form-element__label</c>, <c>caption</c>, <c>infobox</c> and the ghost square button for the
/// disclosure that opens it. Stiler has no definition list, no breadcrumb and no key/value block
/// that can be read back off its compiled stylesheet, so what a host supplies is base styling for
/// those three elements — a host that supplies none still gets a panel that reads correctly, just
/// an unindented one. <c>munin-explorer-detail</c> is a DOM handle like
/// <c>munin-explorer-filters</c>, and carries no styling.
/// </para>
/// <para>
/// The kilde and datasamling do not open inside that panel: they take over the component's own
/// area as a drill-in, wearing the handle <c>munin-explorer-drilldown</c> and again no style
/// name. What it holds is a heading in Stiler's <c>headline headline-s</c> and a
/// <c>&lt;dl&gt;</c>, or — for a kilde — the whole of <c>KildeView</c>, so what a host supplies
/// for it is the base <c>&lt;dl&gt;</c> styling the variable's own panel already needed.
/// <c>munin-explorer-source</c> is not a class: it is the prefix of the element id that names
/// the region (<c>munin-explorer-source-{instance}</c>), so a host or a test reaching for
/// <c>.munin-explorer-source</c> finds nothing. It was a class, back when the kilde opened
/// inside the variable's panel, and stopped being one when that panel became this drill-in.
/// </para>
/// <para>
/// <c>KildeView</c> adds nine handles of its own — <c>munin-explorer-kilde</c> with its
/// <c>__header</c>, <c>__identifiers</c>, <c>__kildetype</c>, <c>__description</c>, <c>__body</c>,
/// <c>__main</c>, <c>__aside</c> and <c>__datasamlinger</c> parts — and no style name, because
/// neither stylesheet has a kilde record to borrow one from. Every element wearing them also wears
/// a Stiler class or is dressed by its own browser default, so a host that defines none of them
/// loses no information.
/// </para>
/// <para>
/// The panel's Data tab adds handles of its own — <c>munin-explorer-kodeverk</c> with its
/// <c>__item</c>, <c>__name</c> and <c>__reference</c> parts, and <c>munin-explorer-codes</c>
/// with its <c>__table</c> — and no style name, because neither Stiler nor helsedata's own
/// variable page has a kodeverk section to borrow one from. What is worth spelling out is the
/// <c>&lt;table&gt;</c> inside it, one of the two this package emits — the other is the
/// datasamlinger list in <c>KildeView</c>. The results list is not one of them: it is helsedata's
/// own <c>munin-explorer-data-list</c>, a <c>&lt;ul&gt;</c> with a header row of <c>&lt;div&gt;</c>s,
/// because that is the shape their stylesheet dresses. Four columns of code values have no such
/// alternative shape. The rule that keeps it safe is the one the <c>&lt;dl&gt;</c>, the
/// <c>&lt;ol&gt;</c> and the <c>&lt;details&gt;</c> already rely on:
/// an element degrades to its own browser default, which for a table is aligned columns, where a
/// class name Stiler has never heard of degrades to nothing at all.
/// </para>
/// <para>
/// A host outside helsedata's estate has to provide equivalents for those names, and two
/// accessibility requirements the markup cannot meet on its own come with them. A host that
/// skips either fails WCAG whatever this component does:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A visible focus indicator on the search field and the Søk button. WCAG 2.4.7.
/// </description></item>
/// <item><description>
/// Text and non-text contrast, WCAG 1.4.3 and 1.4.11.
/// </description></item>
/// </list>
/// <para>
/// There is deliberately no visually-hidden helper in that list. Stiler has no global
/// screen-reader-only rule, so nothing here depends on one: the results list is named with
/// <c>aria-label</c> rather than a clipped <c>&lt;caption&gt;</c>, and a missing value is
/// written out as "Ikke oppgitt" for everyone rather than shown as an em dash and whispered to
/// assistive technology.
/// </para>
/// </remarks>
public partial class VariableExplorer : ComponentBase
{
    /// <summary>
    /// Initial search text. Set by the host, typically from a URL query parameter — the
    /// component has no NavigationManager and no URL logic of its own, because the CMS
    /// host owns routing.
    /// </summary>
    [Parameter] public string? Search { get; set; }

    /// <summary>
    /// Raised when the user searches, so the host can reflect it in its own URL.
    /// The Search/SearchChanged naming gives the host <c>@bind-Search</c> for free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A host mounting this component must make the mount point fully interactive.
    /// An EventCallback serialises to an empty delegate across a static-SSR to
    /// interactive-island boundary, and the callback then silently never fires.
    /// </para>
    /// <para>
    /// Raised on every search, including one whose fetch failed and including the initial load:
    /// a URL that kept the previous query after a failed search would be a shared link that
    /// reloads into a different search than the box on screen is showing. Sorting is not a
    /// search and does not raise it. An exception out of the handler is swallowed rather than
    /// left to reach the host's circuit — see the catch in the component.
    /// </para>
    /// </remarks>
    [Parameter] public EventCallback<string?> SearchChanged { get; set; }

    /// <summary>Rows per page. Clamped to 1–100, the range the API itself accepts.</summary>
    /// <remarks>
    /// <para>
    /// The host owns this, and the reader is deliberately given no way to change it. Munin's own
    /// explorer offers a 10/20/50 picker; this one does not, and that is a decision rather than a
    /// gap. A picker is a <c>&lt;select&gt;</c>, and no class name for one can be read back off
    /// helsedata's stylesheets — their pager has no size control, so there is nothing to copy and
    /// anything we chose would be invented. An unstyled select inside an otherwise styled page is
    /// the failure this package exists to avoid, and the rule the rest of the component follows is
    /// to change the shape rather than to ship CSS. The host already knows how much room it gave
    /// us, which is the other reason this is a parameter in the first place.
    /// </para>
    /// <para>
    /// If a picker is wanted later it needs a verified class name first, and it belongs with the
    /// shareable-state work that puts the page number in the host's URL — page and size travel
    /// together there. Nothing in the current surface makes that harder: paging is private state
    /// behind one method, not an API.
    /// </para>
    /// <para>
    /// Values outside 1–100 are clamped rather than rejected. The server clamps them anyway, and a
    /// zero or negative page size would otherwise make the page arithmetic on this side meaningless.
    /// </para>
    /// </remarks>
    [Parameter] public int PageSize { get; set; } = 25;

    /// <summary>
    /// <c>"no"</c> or <c>"en"</c>. Matches helsedata's own culture tokens rather than
    /// <c>nb</c>. Translations are self-contained: no host in helsedata's estate calls
    /// <c>AddLocalization()</c>, so injecting IStringLocalizer would throw at render time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set this to match the host page's own <c>lang</c>. The component deliberately does
    /// not put a <c>lang</c> on its root: the UI strings follow this parameter, but the
    /// variable names and descriptions coming from Munin are Norwegian either way, and the
    /// result rows are marked as Norwegian for exactly that reason.
    /// </para>
    /// <para>
    /// A region is allowed and ignored: <c>en-GB</c> and <c>en-US</c> read as English, <c>nb-NO</c>
    /// as Norwegian. That is not decoration — helsedata's solution holds two representations of the
    /// same choice, the CMS branch name (<c>no</c>/<c>en</c>) and a full culture from
    /// <c>LanguageExtensions</c> (<c>nb-NO</c>/<c>en-GB</c>), and which one reaches the mount point
    /// is the host's to decide. Anything else, including nothing, is Norwegian.
    /// </para>
    /// <para>
    /// Read once per render rather than watched: changing it on a component already on screen
    /// re-renders every string this package owns, but not the datatype facet names, which the API
    /// resolves server side and this component only asks for when it fetches counts. A host that
    /// wants a live switch should re-mount the component rather than swap the parameter under it.
    /// That is not a limitation anyone in helsedata's estate meets today — both sample hosts set
    /// this once, and the CMS language switch is a full page load — and widening it means deciding
    /// where the mount point is first.
    /// </para>
    /// </remarks>
    [Parameter] public string Language { get; set; } = "no";

    /// <summary>
    /// Whether the host says this reader is signed in. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Told by the host rather than discovered by calling the API and reading a 401: probing spends
    /// a failed request per render on every signed-out reader, and cannot tell "no session" from
    /// "expired token" or "Munin is down".
    /// </para>
    /// <para>
    /// The default is signed out on purpose. A host that forgets this parameter gets no saved
    /// lists, which is a visible gap; the alternative default would send unauthorised calls on
    /// every render instead, which is not visible at all.
    /// </para>
    /// </remarks>
    [Parameter] public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Heading level for the component's own title, 1–6. Defaults to <c>2</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Heading level cannot be decided inside a component that does not know what surrounds
    /// it. Skipping a level — an <c>h2</c> under a page whose last heading was an <c>h4</c>,
    /// or on a page with no <c>h1</c> at all — breaks the outline screen-reader users
    /// navigate by, and fails WCAG 1.3.1.
    /// </para>
    /// <para>
    /// So the host decides. Pass the level that follows on from the heading above the mount
    /// point: <c>1</c> when the explorer is the page's own subject and nothing else supplies
    /// an <c>h1</c>, <c>3</c> when it sits inside an <c>h2</c> section, and so on. Values
    /// outside 1–6 are clamped rather than rejected, because an invalid heading tag would be
    /// a worse failure than an approximately-right one.
    /// </para>
    /// </remarks>
    [Parameter] public int HeadingLevel { get; set; } = 2;

    /// <summary>
    /// The facet selection to start from. Set by the host, typically from its own URL, the same way
    /// <see cref="Search"/> is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read once, when the component initialises, and owned by the component afterwards — again like
    /// <see cref="Search"/>. A host that rewrites this parameter later does not move the filters that
    /// are on screen; what it gets instead is <see cref="FilterChanged"/>, which fires whenever the
    /// reader moves them.
    /// </para>
    /// <para>
    /// <see cref="VariableFilter.ToQueryString"/> and <see cref="VariableFilter.Parse"/> are the two
    /// halves of putting this in a URL: parse the request's query string into this parameter on the
    /// way in, and write the callback's value back out on the way out. Both use the Explorer API's
    /// own parameter names, so a link built that way says what it filters on in terms anybody
    /// reading the URL — or the API's own documentation — can follow.
    /// </para>
    /// <para>
    /// Null is <see cref="VariableFilter.None"/>: no narrowing, every published variable the search
    /// matches.
    /// </para>
    /// </remarks>
    [Parameter] public VariableFilter? Filter { get; set; }

    /// <summary>
    /// Raised when the filter selection changes, so the host can reflect it in its own URL. The
    /// Filter/FilterChanged naming gives the host <c>@bind-Filter</c> for free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A host mounting this component must make the mount point fully interactive. An
    /// EventCallback serialises to an empty delegate across a static-SSR to interactive-island
    /// boundary, and the callback then silently never fires.
    /// </para>
    /// <para>
    /// It carries the filter that is actually in force, which is not always the one the reader just
    /// asked for: a selection whose fetch failed is rolled back, and this then reports the filter
    /// the rows on screen came from. A host that wrote the attempted filter to its URL instead would
    /// hand out a link that reloads into a different selection than the one the page is showing.
    /// Unlike <see cref="SearchChanged"/> it is not raised on the initial load — nothing has changed
    /// yet, and the value would be the one the host just passed in.
    /// </para>
    /// </remarks>
    [Parameter] public EventCallback<VariableFilter> FilterChanged { get; set; }

    /// <summary>The column the list is ordered by, and the direction. Two-way.</summary>
    /// <remarks>
    /// Runa keeps both in the URL, so a colleague opening a shared link sees the same order. They
    /// are two parameters rather than one because a host binds each with <c>@bind-Sort</c> and
    /// <c>@bind-Direction</c>; they change together, and both callbacks fire on the same click.
    /// <para>
    /// Raised only after the reordered list has actually arrived. A failed fetch puts the old order
    /// back — see <see cref="SortAsync"/> — and telling the host about an order the API never
    /// delivered would leave a URL describing a list nobody can see.
    /// </para>
    /// </remarks>
    [Parameter] public SortField Sort { get; set; } = SortField.Default;

    /// <inheritdoc cref="Sort"/>
    [Parameter] public EventCallback<SortField> SortChanged { get; set; }

    /// <inheritdoc cref="Sort"/>
    [Parameter] public SortDirection Direction { get; set; } = SortDirection.Ascending;

    /// <inheritdoc cref="Sort"/>
    [Parameter] public EventCallback<SortDirection> DirectionChanged { get; set; }

    /// <summary>Which page of results is showing. Two-way, one-based.</summary>
    /// <remarks>
    /// Restored on first render, so a shared link opens on the page it was shared from. A page past
    /// the end is not an error either: the API does not clamp — asked for a page it does not have it
    /// answers with that page and no rows — so the component moves to the last real page itself and
    /// reports it here. A host mirroring this into a URL therefore gets a corrected number back, and
    /// should write what it is told rather than what it sent.
    /// <para>
    /// Also raised when the page resets to 1 — a new search or a changed filter renumbers
    /// everything, and a host that only heard about page turns would keep <c>page=7</c> in a URL
    /// whose result set no longer has seven pages.
    /// </para>
    /// </remarks>
    [Parameter] public int Page { get; set; } = 1;

    /// <inheritdoc cref="Page"/>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>
    /// The variable whose detail panel is open, or null when none is. Set by the host, typically
    /// from its own URL, the same way <see cref="Search"/> and <see cref="Filter"/> are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read once, when the component initialises, and owned by the component afterwards. There is
    /// no navigation behind it: the detail is drawn inside the row it belongs to, so opening one
    /// costs a fetch and a render rather than a page.
    /// </para>
    /// <para>
    /// The selection is always a row that is on screen. An id the first page does not contain is
    /// dropped rather than fetched, because the panel has nowhere to be drawn — and that drop is
    /// the one occasion <see cref="SelectedVariableIdChanged"/> fires without the reader having
    /// done anything, so a host's URL is not left naming a variable the page is not showing.
    /// </para>
    /// </remarks>
    [Parameter] public Guid? SelectedVariableId { get; set; }

    /// <summary>
    /// Raised when the open detail panel changes, so the host can reflect it in its own URL. The
    /// SelectedVariableId/SelectedVariableIdChanged naming gives the host
    /// <c>@bind-SelectedVariableId</c> for free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A host mounting this component must make the mount point fully interactive. An
    /// EventCallback serialises to an empty delegate across a static-SSR to interactive-island
    /// boundary, and the callback then silently never fires.
    /// </para>
    /// <para>
    /// It carries null when the panel is closed — by the reader pressing the open row again, and
    /// also when a new search, filter, ordering or page leaves the selected variable off the
    /// screen. A selection whose detail could not be fetched is <em>not</em> rolled back, unlike a
    /// filter: the panel is open, it says why it is empty, and closing it under the reader would
    /// take the button they just pressed out of the document.
    /// </para>
    /// </remarks>
    [Parameter] public EventCallback<Guid?> SelectedVariableIdChanged { get; set; }

    [Inject] private IMuninExplorerClient Client { get; set; } = null!;

    private string? _search;
    private bool _loading;
    private string? _error;
    private Page<VariableSummary>? _result;

    // The facet selection the visible rows were fetched with. Never null — VariableFilter.None is
    // "no narrowing" — so nothing downstream has to spell that case out twice.
    private VariableFilter _filter = VariableFilter.None;

    // The facets and their counts, as the API last reported them for _executedSearch and _filter.
    // Null only until the first answer arrives, and never set back to null: the filter controls are
    // rendered from it, and taking them off the page after a failed refresh would remove the
    // control the reader just pressed — the same rule the pager and the Søk button follow. A
    // refresh that fails therefore leaves the previous counts on screen, and says so through
    // _facetError rather than by emptying the panel.
    private FilterOptions? _facets;

    // Set when the facets could not be refreshed, which is a different failure from the search
    // failing: the rows on screen are the right rows, and it is the numbers beside the filters that
    // may now be stale. Reported separately for that reason.
    private string? _facetError;

    // The variable whose detail panel is open, and what has been fetched for it. Never a variable
    // that is not among the rows on screen: the panel is drawn inside its own row, so a selection
    // the current result does not contain is one nothing can render — see DropSelectionIfGoneAsync.
    private Guid? _selectedId;
    private VariableDetail? _detail;
    private bool _detailLoading;

    // Bumped by every open and every close, so a detail fetch can tell whether the panel it is
    // about to write into is still the one it was started for. The id alone cannot say that: it
    // names the variable, not the call, and closing a row and opening the same row again is two
    // calls carrying one id — the abandoned first would otherwise be read as the answer to the
    // second and report its failure into a panel that is still waiting.
    private int _detailGeneration;

    // Set when the detail could not be fetched, or when the API says there is no such published
    // variable. Its own field rather than _error, because the rows on screen are unaffected: what
    // failed is one panel inside one card, and reporting it in the component's own alert region
    // would say the whole list was stale.
    private string? _detailError;

    // Which of the two owners the open variable's panel is currently disclosing, and what has been
    // fetched for it. Null is "neither", which is where every variable's panel starts: the owners
    // are a second fetch each, and asking for them before the reader has said they want them would
    // put three requests behind one press on a public page.
    //
    // One at a time, and only ever under an open variable panel — the kilde and the datasamling are
    // reached *through* a variable, which is what the bead asks for, so there is no state here that
    // can outlive the panel it hangs in. LoadDetailAsync and ClearSelection both clear it for that
    // reason.
    private SourceKind? _sourceKind;
    private KildeDetail? _kilde;
    private DatasamlingDetail? _datasamling;
    private bool _sourceLoading;

    // Its own generation, for the reason the detail panel has one: closing an owner and opening it
    // again is two calls carrying one id, and the abandoned first must not report itself into the
    // second one's panel. Separate from _detailGeneration because the two fetches are independent —
    // an owner opened over a variable panel that is still on screen has not been abandoned by
    // anything.
    private int _sourceGeneration;

    // Set when the owner could not be fetched, or when the API publishes no such kilde or
    // datasamling. Its own field for the same reason _detailError is: what failed is one panel
    // inside one panel, and neither the rows nor the variable above it are stale because of it.
    private string? _sourceError;

    // The kodeverk whose code lists the reader has opened, and what has been fetched for each.
    //
    // Keyed rather than single, unlike the owner panel above: the kodeverk are a list of peers
    // under one heading and a reader comparing two of them is a thing the panel has room for,
    // where the kilde and the datasamling answer the same question twice and do not.
    //
    // Nothing here is fetched with the variable. A kodeverk can run to hundreds of codes —
    // Kommunenummer is 885 — and most readers open none of them, so putting the codes in the
    // detail payload would make every opened row pay for a list almost nobody reads.
    private readonly HashSet<KodeverkKey> _openCodes = [];

    // What came back, kept after a list is collapsed so opening it again costs no second request.
    // Emptied with the panel it hangs in, not before: a variable's codes are only ever drawn under
    // that variable, so there is nothing for a cache that outlives it to be right about.
    private readonly Dictionary<KodeverkKey, IReadOnlyList<KodeverkCode>> _codes = [];
    private readonly HashSet<KodeverkKey> _codesLoading = [];

    // Per link, for the reason the owner panel's error is its own field: one code list that could
    // not be fetched leaves every other line on the panel describing exactly what it did before.
    private readonly Dictionary<KodeverkKey, string> _codesError = [];

    // One generation for all of them rather than one each, because they are only ever abandoned
    // together: what disowns a code fetch is the variable panel closing, and that closes every list
    // in it at once. Two lists open on one variable are not racing each other — they ask different
    // questions of different references.
    private int _codesGeneration;

    // The API's own default order, ascending, which is also where Runa starts — and the order the
    // API returns when it is asked for none, so the first render costs no extra query parameters.
    private SortField _sort = SortField.Default;
    private SortDirection _direction = SortDirection.Ascending;

    // The page being asked for, and the only piece of paging state there is. "Any change of search
    // or sort goes back to page one" is a rule about state — a result set reordered under someone
    // still looking at page 7 shows them rows from the middle of a sequence they never saw the
    // start of — so the resets live next to the field rather than at the call sites.
    //
    // Private, and reached only through GoToPageAsync. The host has no Page parameter and no
    // PageChanged callback, deliberately: the page number belongs in the host's URL alongside the
    // search text, and that contract is still being designed. One field and one method is the
    // smallest thing for it to hook into when it arrives; a public parameter now would be a shape
    // it had to keep.
    private int _page = 1;

    // Whether the pager has been pressed since the last search or reordering, which is the one
    // thing "there is more than one page" cannot tell the markup on its own. A retreat can land on
    // a result that legitimately has a single page — the index shrank to one page's worth between
    // two requests — and dropping the pager in that render would take Neste out of the document
    // under the finger that pressed it, which is the failure the retreat exists to avoid rather
    // than a new one to introduce. Reset by a search and by a sort, neither of which is started
    // from a pager button, so a single-page result reached that way still costs no furniture.
    private bool _keepPager;

    // The search text the visible result actually came from, which is not the same as the
    // text in the box: @bind writes _search on blur, so the box can hold an unsubmitted query
    // while the table below still shows the previous one. The announcement has to describe
    // what is on screen.
    private string? _executedSearch;

    // Unique per instance so two explorers on one page cannot collide on DOM ids,
    // which would be a WCAG 4.1.1 failure as well as breaking label association.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    private string SearchId => $"munin-explorer-search-{_instance}";
    private string TitleId => $"munin-explorer-title-{_instance}";
    private string PaginationId => $"munin-explorer-pagination-{_instance}";

    // Per row as well as per instance: the detail panel is wired to its own row with
    // aria-controls and aria-labelledby, and two explorers listing the same variable would
    // otherwise mint the same id twice on one page.
    private string RowHeadingId(VariableSummary v) => $"munin-explorer-heading-{_instance}-{v.Id:N}";
    private string DetailToggleId(VariableSummary v) => $"munin-explorer-toggle-{_instance}-{v.Id:N}";
    private string DetailId(VariableSummary v) => $"munin-explorer-detail-{_instance}-{v.Id:N}";
    private string SaveButtonId(VariableSummary v) => $"munin-explorer-save-{_instance}-{v.Id:N}";

    // Per instance and not per row: the owner panel hangs inside the one open variable panel, so
    // there is never more than one of it in this component's DOM. The kind is in the toggle's id
    // because the two toggles are on screen together.
    private string SourceId => $"munin-explorer-source-{_instance}";
    private string SourceHeadingId => $"munin-explorer-source-heading-{_instance}";
    private string SourceToggleId(SourceKind kind) =>
        $"munin-explorer-source-toggle-{_instance}-{kind.ToString().ToLowerInvariant()}";

    // Per instance and per link. There is one open variable panel, so the variable does not need to
    // be in the id, but the links inside it do: several code lists can be open together, and the
    // table in each is named from the line above it.
    //
    // The link's position in the payload rather than its type and reference, which read better and
    // cannot safely be used: a reference is the catalogue's own text — dotted OIDs for V-AK,
    // hyphenated acronyms like NCMP-NCSP-NCRP for V-HK — and punctuation stripped to make it an id
    // would let two different references mint the same one, which is a duplicate-id WCAG failure
    // and an aria-controls naming the wrong table. The position is unique by construction.
    private string KodeverkNameId(int index) => $"munin-explorer-kodeverk-{_instance}-{index}";
    private string KodeverkCodesId(int index) => $"munin-explorer-codes-{_instance}-{index}";

    private Texts T => Texts.For(Language);

    private string Busy => _loading ? "true" : "false";

    /// <summary>Rows per page as actually requested — see <see cref="PageSize"/>.</summary>
    private int ClampedPageSize => Math.Clamp(PageSize, 1, 100);

    /// <summary>How many variables the search matched, not how many are on screen.</summary>
    private int TotalCount => _result?.TotalCount ?? 0;

    /// <summary>
    /// How many pages the result has. At least 1, so "Side 1 av 0" can never be written.
    /// </summary>
    /// <remarks>
    /// The server's own count is preferred over arithmetic here, because the server is the one that
    /// clamps the page size: counting the pages ourselves from a size it quietly changed would put
    /// a Neste button on screen for a page that does not exist. The arithmetic is kept as a fallback
    /// for a substituted <see cref="IMuninExplorerClient"/> that leaves the field at zero — claiming
    /// one page over three hundred rows would strand the reader on the first twenty-five of them.
    /// It divides by <see cref="ResultPageSize"/> and not by <see cref="ClampedPageSize"/> for the
    /// same reason: counting the pages against a size the rows were not built with would put the
    /// page count and the row range on screen describing two different pagings of one result.
    /// </remarks>
    private int TotalPages
    {
        get
        {
            if (_result is null || TotalCount <= 0)
            {
                return 1;
            }

            return _result.TotalPages > 0
                ? _result.TotalPages
                : (int)Math.Ceiling(TotalCount / (double)ResultPageSize);
        }
    }

    private bool CanGoPrevious => _page > 1;

    private bool CanGoNext => _page < TotalPages;

    /// <summary>Whether the pager belongs on screen at all.</summary>
    /// <remarks>
    /// More than one page, or a pager the reader is already standing on. "Side 1 av 1" between two
    /// buttons that can never do anything is furniture and is left out — but only when the reader
    /// did not arrive at that single page by pressing one of those two buttons, because taking the
    /// pressed control out of the document drops focus to <c>&lt;body&gt;</c>. See
    /// <see cref="_keepPager"/> for the path that reaches a single page from a pager button.
    /// </remarks>
    private bool ShowPager => _result is not null && (TotalPages > 1 || _keepPager);

    /// <summary>The 1-based position of the first row on screen, or 0 when there are no rows.</summary>
    /// <remarks>
    /// Guarded on the rows rather than on <see cref="TotalCount"/>, so that it agrees with
    /// <see cref="LastItemOnPage"/> without either of them relying on the markup to keep the pair
    /// off screen: a page with no rows on a non-zero total would otherwise read "Viser 26–0 av 312".
    /// </remarks>
    private int FirstItemOnPage =>
        _result is null || _result.Items.Count == 0 ? 0 : ((ResultPage - 1) * ResultPageSize) + 1;

    /// <summary>
    /// The 1-based position of the last row on screen, counted from the rows actually delivered.
    /// </summary>
    /// <remarks>
    /// Counted rather than calculated as <c>page × size</c>, so the last page says 312 and not 325,
    /// and so a server that returned a different page size than it was asked for still describes
    /// itself truthfully.
    /// </remarks>
    private int LastItemOnPage =>
        _result is null || _result.Items.Count == 0 ? 0 : FirstItemOnPage + _result.Items.Count - 1;

    /// <summary>
    /// The page size the visible result was actually built with, which is the server's answer when
    /// it gave one and what we asked for otherwise.
    /// </summary>
    private int ResultPageSize => _result is { Size: > 0 } page ? page.Size : ClampedPageSize;

    /// <summary>
    /// The page the visible result actually is, which is the server's answer when it gave one and
    /// the page we asked for otherwise.
    /// </summary>
    /// <remarks>
    /// The same treatment <see cref="ResultPageSize"/> gives the size, for the same reason. An API
    /// that clamps an out-of-range page — answering page 8 of 8 to a request for page 12 — would
    /// otherwise have the row range counted from the number that was asked for, so the status line
    /// would offer "Viser 276–300 av 200" over rows the reader is not looking at.
    /// </remarks>
    private int ResultPage => _result is { PageNumber: > 0 } page ? page.PageNumber : _page;

    /// <summary>
    /// <c>"true"</c> on a pager button that would do nothing, and nothing at all on one that works.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>aria-disabled</c> rather than the <c>disabled</c> attribute, for the same reason the Søk
    /// button is never disabled: disabling the element that currently has focus drops focus to
    /// <c>&lt;body&gt;</c>. Pressing Neste until the last page, or Forrige back to the first, is the
    /// ordinary way to use a pager, and both end with the pressed button becoming unavailable — so
    /// with a real <c>disabled</c> attribute the reward for reaching the end of the list is to start
    /// tabbing from the top of the host's page again.
    /// </para>
    /// <para>
    /// The button is genuinely inert either way: <see cref="GoToPageAsync"/> clamps, so a click at
    /// the boundary asks for the page it is already on and returns without a request. This is the
    /// ARIA Authoring Practices' own recommendation for a control that must stay focusable.
    /// </para>
    /// </remarks>
    private static string? AriaDisabled(bool enabled) => enabled ? null : "true";

    /// <summary>The component's own heading level, clamped into the range that is a heading.</summary>
    private int TitleLevel => Math.Clamp(HeadingLevel, 1, 6);

    /// <summary>
    /// The heading level for a result card: one step below the component's own title, so the
    /// outline stays unbroken however deep the host mounted us.
    /// </summary>
    /// <remarks>
    /// With the title already at <c>h6</c> there is no level below, so the cards sit at
    /// <c>h6</c> alongside it. That flattens the outline rather than breaking it, which is the
    /// better of the two available answers — HTML has no <c>h7</c>, and dropping the headings
    /// altogether would cost the heading rotor these cards were given for.
    /// </remarks>
    private int RowLevel => Math.Clamp(TitleLevel + 1, 1, 6);

    /// <summary>
    /// The heading level for the kilde or datasamling panel: one step below the result card it
    /// opens inside, so the owner reads as part of the variable rather than as a sibling of it.
    /// </summary>
    /// <remarks>
    /// Clamped the same way <see cref="RowLevel"/> is, and flattens against the card's own level
    /// for the same reason: HTML stops at <c>h6</c>, and a flattened outline is a smaller loss than
    /// a missing heading.
    /// </remarks>
    private int SourceLevel => Math.Clamp(RowLevel + 1, 1, 6);

    /// <summary>
    /// One sentence describing the visible result, used both as the live announcement and
    /// as the list's accessible name so the two can never drift apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It names the ordering as well as the count. Without column headers there is no
    /// <c>aria-sort</c> to carry that, so it rides along on the status line the component already
    /// has: pressing a sort button changes this sentence, and the polite, atomic live region reads
    /// the whole of it back. The sentence is assembled inside <see cref="Texts"/> rather than
    /// glued together here, so a language that has to state the ordering first can say it that way.
    /// </para>
    /// <para>
    /// It names <em>which</em> rows are on screen — "Viser 26–50 av 312" — rather than only how
    /// many, and that is also what announces a page change: turning a page rewrites this sentence,
    /// and the live region reads it. The range is not repeated inside the pager, where Munin's own
    /// explorer puts it, because saying it twice on one screen is the duplication the empty state
    /// already avoids, and because only one of the two copies would be announced.
    /// </para>
    /// </remarks>
    private string Summary => _result is null
        ? ""
        : T.ResultSummary(FirstItemOnPage, LastItemOnPage, TotalCount, _executedSearch, _filter.ActiveCount,
                          T.FieldLabel(_sort), T.DirectionName(_direction));

    /// <summary><c>"true"</c> on the active field, and nothing at all on the others.</summary>
    /// <remarks>
    /// Null rather than <c>"false"</c>: Blazor leaves an attribute out when its value is null, and
    /// three buttons carrying <c>aria-current="false"</c> is noise in the accessibility tree.
    /// </remarks>
    private string? AriaCurrent(SortField sort) => sort == _sort ? "true" : null;

    /// <summary>
    /// The title, at the level the host asked for. Razor has no syntax for a computed
    /// element name, so this is built by hand.
    /// </summary>
    /// <remarks>
    /// The visual size is pinned with Stiler's <c>headline-3</c> rather than left to the
    /// element, because the element is the host's choice: without it, mounting the explorer
    /// one level deeper would silently shrink its title.
    /// </remarks>
    private RenderFragment Heading => builder =>
    {
        builder.OpenElement(0, $"h{TitleLevel}");
        builder.AddAttribute(1, "class", "headline headline-3");
        builder.AddAttribute(2, "id", TitleId);
        builder.AddContent(3, T.Title);
        builder.CloseElement();
    };

    /// <summary>
    /// The first column: the variable's name, which is also the control that opens its panel.
    /// </summary>
    /// <remarks>
    /// No heading element. An earlier version wrapped this in one so results could be walked with
    /// a screen reader's heading rotor, but helsedata's row is <c>display: flex</c> and
    /// <c>munin-explorer-dataitem-main__name</c> sizes the flex ITEM — a heading in between becomes the
    /// item and the name column falls out of line with its header. Neither reference wraps it:
    /// helsedata puts the button straight in the row, and Runa's rows are table rows. The results
    /// are a list of list items, each with a named disclosure carrying <c>aria-expanded</c>.
    /// <para>
    /// There is a wrapper, though, and it is not a heading: a <c>div</c> carrying
    /// <c>role="rowheader"</c> and the same class the button wears. A row owns nothing but cells,
    /// and this column's content is a <c>button</c> that cannot be one without ceasing to be a
    /// button — see the comment on the wrapper for why the class is on both elements.
    /// </para>
    /// </remarks>
    private RenderFragment RowHeading(VariableSummary v) => builder =>
    {
        // No heading wrapper. An earlier version wrapped this button in an h-element so results
        // could be walked with a screen reader's heading rotor, having checked that none of
        // helsedata's selectors for these names uses a child combinator — descendant styling
        // survives an extra element in between. But flex sizing does not: their row is
        // `display: flex` and `.munin-explorer-dataitem-main__name` sizes the NAME CELL, so a heading
        // in between becomes the flex item and the column collapses to its content, throwing every
        // row out of line with the header. Neither reference wraps it — helsedata puts the button
        // straight in the row, and Runa's rows are table rows with no per-row heading either.
        //
        // The rows are a list of list items, each with a named disclosure carrying aria-expanded,
        // which is the pattern this is supposed to be.

        // A cell around the button, and rowheader rather than cell: the variable's name is what
        // the rest of the row is about, which is the same call Kelda's table makes with
        // <th scope="row">. It exists at all because a row owns nothing but cells and this column
        // is a <button> — the one element in the row that cannot carry a cell role itself without
        // giving up being a button.
        //
        // It wears `munin-explorer-dataitem-main__name`, the class the button already had, and the
        // button keeps it too. That is deliberate rather than sloppy: the class is what sizes this
        // column (`flex: 210 1 0`, in Stiler and in both sample stylesheets), so the wrapper has to
        // carry it or the name column stops lining up with its header — and the button has to keep
        // it or it draws as a browser default button. Every rule on the name is either inherited
        // or harmless twice; the only one that repeats is an empty `::after` overlay with
        // `pointer-events: none`, which draws nothing.
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-dataitem-main__name");
        builder.AddAttribute(2, "role", "rowheader");

        // The name IS the disclosure — helsedata's own pattern, and the APG accordion pattern.
        // It replaces a separate "Vis detaljer" button that sat under the metadata line, and with
        // it the dead affordance the old card shape had: .datasourcecard carried a pointer cursor,
        // because on their datakilde page the whole card is a link, which ours never was.
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "class", "munin-explorer-dataitem-main__name");
        builder.AddAttribute(5, "type", "button");
        builder.AddAttribute(6, "id", DetailToggleId(v));
        builder.AddAttribute(7, "aria-expanded", DetailExpanded(v));
        builder.AddAttribute(8, "aria-controls", DetailControls(v));

        // A name for the shape where the button's own content cannot give it one. PreferredTerm
        // defaults to "" (Contracts/VariableSummary.cs) and the row draws it blank, so the span
        // below is empty and this button — whose only content IS that span — announces as
        // "button, collapsed" with nothing in front of it. WCAG 4.1.2. The save button beside it
        // survives the same row for free, because its own words are the first half of its name;
        // this one has no second source, so it needs a written fallback.
        //
        // An aria-label, and not the two-element aria-labelledby the rest of this row uses. That
        // rule exists because a Munin name interpolated into our prose is one unmarked string in
        // two languages — here there is no Munin half at all, only our own sentence, which follows
        // Language like every other string this component says. And it is not written into the
        // span, because the save button borrows the span: putting it there would name that button
        // "Lagre i liste Vis hele variabelen" for a variable neither control can actually name.
        //
        // Null while the term is there, so the visible words stay the name and a speech-input user
        // saying what they can see still reaches the control (WCAG 2.5.3).
        builder.AddAttribute(9, "aria-label",
            string.IsNullOrWhiteSpace(v.PreferredTerm) ? T.ShowWholeVariable : null);

        // Never disabled, including while its own fetch runs: pressing it again is how the panel
        // is closed, and disabling the element that has focus drops focus to <body>.
        builder.AddAttribute(10, "onclick", EventCallback.Factory.Create(this, () => ToggleDetailAsync(v)));

        builder.OpenElement(11, "span");
        builder.AddAttribute(12, "class", "munin-explorer-dataitem-main__column__text");
        // Named, because the save button beside it borrows these words for its own accessible
        // name — see RowSaveButton. The id is on the span holding the name rather than on the
        // button around it, so what gets borrowed is the variable and not the whole cell.
        //
        // Written here and nowhere else: this is the only element that carries RowHeadingId, and
        // it is drawn for every row whether that row's panel is open or shut. Both matter to the
        // save button, which points at it in either state — a second emitter would make every row
        // a duplicate-id failure (WCAG 4.1.1) and aim the button at whichever came first.
        builder.AddAttribute(13, "id", RowHeadingId(v));
        // Munin's variable names are Norwegian whatever language the surrounding UI is in.
        builder.AddAttribute(14, "lang", "no");
        builder.AddContent(15, v.PreferredTerm);
        builder.CloseElement();

        builder.CloseElement();

        // The rowheader cell.
        builder.CloseElement();
    };

    /// <summary>
    /// The column header row, in helsedata's own shape: a row wearing the <c>--header</c> modifier,
    /// with one <c>sortable-header</c> cell per column.
    /// </summary>
    /// <remarks>
    /// This replaces the "Sorter etter" fieldset. The fieldset existed because there was no header
    /// to put the ordering in; now there is, and leaving both would give the same choice two
    /// controls.
    /// <para>
    /// Four of the eight columns map to a real <see cref="SortField"/>. Kode, Datatype, Status and
    /// Dataperiode have none, so their headers are plain text rather than buttons that would
    /// promise an ordering the API does not offer. The variable column maps to
    /// <see cref="SortField.Default"/>, which is honest rather than convenient: that member is
    /// documented as the API's own order and its wire token is literally <c>name</c>.
    /// </para>
    /// <para>
    /// aria-current, not aria-pressed, for the same reason the old buttons used it: a pressed
    /// toggle promises that pressing again releases it, and this one flips the direction instead.
    /// </para>
    /// <para>
    /// Every cell but the first is drawn only while its column is on screen — see
    /// <see cref="ColumnVisible"/>. The header and the rows read the same predicate, because a
    /// header cell without the values under it puts every row out of line with its own column.
    /// That is also why a sorted column does not keep its header when it is hidden: leaving the
    /// cell behind for its <c>aria-sort</c> would be the misalignment this rule exists to prevent.
    /// The ordering itself survives, deliberately and announced — see <see cref="ToggleColumn"/>.
    /// </para>
    /// </remarks>
    private RenderFragment ResultHeader() => builder =>
    {
        // The table's header rowgroup — what a <thead> is. The row itself is two elements further
        // down, on the flex container that actually holds the cells; the box in between is
        // helsedata's row wrapper and lays nothing out that the tree needs to hear about, so it
        // steps aside with role="none". A row owns nothing but cells, and an anonymous group
        // sitting between a row and its columns is what breaks that.
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-data-list__header");
        builder.AddAttribute(2, "role", "rowgroup");

        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "munin-explorer-data-list__item__row munin-explorer-data-list__item__row--header");
        builder.AddAttribute(5, "role", "none");

        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "class", "munin-explorer-dataitem-header");
        builder.AddAttribute(8, "role", "row");

        // Navn is not in the picker and has no condition here: it is the row's disclosure as well
        // as its first column.
        HeaderCell(builder, 100, "name", T.ColumnVariable, SortField.Default);

        if (ColumnVisible(ResultColumn.Code))
        {
            HeaderCell(builder, 200, "code", T.FieldCode, sort: null);
        }

        if (ColumnVisible(ResultColumn.Kilde))
        {
            HeaderCell(builder, 300, "source", T.FieldSource, SortField.Kilde);
        }

        if (ColumnVisible(ResultColumn.Datasamling))
        {
            HeaderCell(builder, 400, "dataCollection", T.FieldDataCollection, SortField.Datasamling);
        }

        if (ColumnVisible(ResultColumn.Variabelgruppe))
        {
            HeaderCell(builder, 500, "theme", T.FieldVariableGroup, SortField.Variabelgruppe);
        }

        if (ColumnVisible(ResultColumn.DataType))
        {
            HeaderCell(builder, 600, "dataType", T.FieldDataType, sort: null);
        }

        if (ColumnVisible(ResultColumn.Status))
        {
            HeaderCell(builder, 700, "status", T.FieldStatus, sort: null);
        }

        if (ColumnVisible(ResultColumn.DataPeriod))
        {
            HeaderCell(builder, 800, "period", T.FieldDataPeriod, sort: null);
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    };

    /// <summary>One header cell, sortable when the column maps to a field the API can order by.</summary>
    private void HeaderCell(RenderTreeBuilder builder, int seq, string? key, string label, SortField? sort)
    {
        builder.OpenElement(seq, "div");
        builder.AddAttribute(seq + 1, "class",
            key is null ? "sortable-header" : $"sortable-header munin-explorer-dataitem-header__{key}");

        // columnheader, which is the role aria-sort below is only allowed on — and, more to the
        // point, the role that gives the cells under this one a column to belong to. Without it
        // the whole header resolved to a run of anonymous nodes and a reader had no way to hear
        // "kolonne 3 av 7, Kilde" (WCAG 1.3.1).
        builder.AddAttribute(seq + 2, "role", "columnheader");

        if (sort is not { } field)
        {
            builder.AddContent(seq + 3, label);
            builder.CloseElement();
            return;
        }

        // aria-sort on the cell rather than the button: it describes the COLUMN's state, and it is
        // what a screen reader reads when moving across the header. Only the active column carries
        // it — "none" on every other column is noise a reader has to listen through.
        if (IsActiveSort(field))
        {
            builder.AddAttribute(seq + 4, "aria-sort", AriaSort());
        }

        builder.OpenElement(seq + 5, "button");
        // hd-button-reset is Stiler's own "this is a button but draw nothing" class, which is what
        // their header buttons wear — 12 rules, in the site-wide stylesheet.
        builder.AddAttribute(seq + 6, "class", "hd-button-reset munin-explorer-dataitem-header__button");
        builder.AddAttribute(seq + 7, "type", "button");
        builder.AddAttribute(seq + 8, "aria-current", AriaCurrent(field));
        builder.AddAttribute(seq + 9, "onclick", EventCallback.Factory.Create(this, () => SortAsync(field)));

        // The button says what the COLUMN is, not what the ordering is. It used to render the sort
        // field's own label, so the first column read "Standard (stigende)" where it should read
        // "Navn" — the name of the thing in the column. The ordering is shown by the arrow beside
        // it and announced by aria-sort above, which is how a column header carries both.
        builder.AddContent(seq + 10, label);

        if (IsActiveSort(field))
        {
            builder.OpenElement(seq + 11, "span");
            builder.AddAttribute(seq + 12, "aria-hidden", "true");
            builder.AddContent(seq + 13, Ascending ? " \u2191" : " \u2193");
            builder.CloseElement();
        }

        builder.CloseElement();

        builder.CloseElement();
    }

    /// <summary>
    /// The chevron helsedata draws at the head of every row, pointing down once the row is open.
    /// </summary>
    /// <remarks>
    /// Their icon font, from the site-wide stylesheet: <c>.icon</c> alone carries 466 rules across
    /// five bundles. Purely decorative — the button beside it already announces the state through
    /// <c>aria-expanded</c>, so a second announcement here would be noise.
    /// </remarks>
    private RenderFragment RowChevron(VariableSummary v) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class",
            IsSelected(v)
                ? "icon icon-keyboard-arrow-down munin-explorer-dataitem-main__expand-icon"
                : "icon icon-keyboard-arrow-right munin-explorer-dataitem-main__expand-icon");
        builder.AddAttribute(2, "aria-hidden", "true");
        builder.CloseElement();
    };

    /// <summary>
    /// The data period, drawn as Runa draws it: the two dates, and a bar beneath them.
    /// </summary>
    /// <remarks>
    /// The bar's width is the share of the variable's own lifetime that its data covers —
    /// <c>(to - from) / (now - from)</c> — so a register that stopped collecting years ago reads as
    /// visibly short, and one still collecting fills the bar. That is Runa's rule, not an
    /// invention: a period with no end is drawn full and in a different colour rather than as an
    /// unknown, because "no end date" means still running.
    /// <para>
    /// Floored at 5% so a period of days is still a mark rather than nothing at all, and capped at
    /// 100% because a <c>to</c> in the future would otherwise overflow the track.
    /// </para>
    /// <para>
    /// <c>munin-explorer-period</c> is a handle of ours, like the other four: helsedata has no
    /// period bar to borrow a name from. The host draws it; without a rule it degrades to the two
    /// dates, which is the information, with the bar as the illustration.
    /// </para>
    /// </remarks>
    private RenderFragment PeriodBar(DateTimeOffset? from, DateTimeOffset? to) => builder =>
    {
        if (from is null && to is null)
        {
            builder.AddContent(0, T.NotSpecified);
            return;
        }

        var ongoing = to is null;

        builder.OpenElement(1, "div");
        builder.AddAttribute(2, "class", "munin-explorer-period");

        builder.OpenElement(3, "p");
        builder.AddAttribute(4, "class", "munin-explorer-period__range");
        builder.AddContent(5, from is { } f ? MonthYear(f) : "?");
        builder.AddContent(6, " – ");
        builder.AddContent(7, to is { } t ? MonthYear(t) : T.Ongoing);
        builder.CloseElement();

        builder.OpenElement(8, "div");
        builder.AddAttribute(9, "class",
            ongoing
                ? "munin-explorer-period__track munin-explorer-period__track--ongoing"
                : "munin-explorer-period__track");
        // Decorative: the dates above say the same thing, and a bar a screen reader announces as
        // "94 percent" would describe a proportion nobody asked about.
        builder.AddAttribute(10, "aria-hidden", "true");

        builder.OpenElement(11, "div");
        builder.AddAttribute(12, "class", "munin-explorer-period__fill");
        builder.AddAttribute(13, "style", $"width:{PeriodShare(from, to)}%");
        builder.CloseElement();

        builder.CloseElement();
        builder.CloseElement();
    };

    /// <summary>The share of the variable's lifetime its data covers, as a whole percent.</summary>
    private static int PeriodShare(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from is not { } start || to is not { } end)
        {
            return 100;
        }

        var lifetime = DateTimeOffset.UtcNow - start;
        var covered = end - start;

        if (lifetime <= TimeSpan.Zero)
        {
            return 100;
        }

        return Math.Clamp((int)Math.Round(covered / lifetime * 100), 5, 100);
    }

    /// <summary>A date as month and year, in the reader's language.</summary>
    private string MonthYear(DateTimeOffset date) =>
        date.ToString("MMM yyyy", CatalogueProperties.Culture(Language));

    /// <summary>
    /// The variable's curated properties, in the order the catalogue puts them.
    /// </summary>
    /// <remarks>
    /// Nothing here is known to this component. The keys, their labels, their order and the
    /// vocabularies their coded values are drawn from all arrive with the payload, because they are
    /// editable master data — a property added or renamed in Munin appears here without this
    /// package being touched, and a copy of any of it would be stale the first time someone edited
    /// a definition.
    /// <para>
    /// Runa gathers these under one heading in the inline panel and only splits them by their own
    /// groups on the full detail page. This follows that: one group, catalogue order.
    /// </para>
    /// </remarks>
    private RenderFragment PropertiesGroup(VariableDetail detail) => builder =>
    {
        var rows = PropertyRows(detail);

        if (rows.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none munin-explorer-group");
        builder.AddContent(2, T.GroupProperties);
        builder.CloseElement();

        builder.OpenElement(3, "dl");
        builder.AddAttribute(4, "class", "munin-explorer-meta__grid");

        var seq = 10;

        foreach (var row in rows)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddAttribute(seq + 3, "lang", Foreign(row.LabelLanguage));
            builder.AddContent(seq + 4, row.Label);
            builder.CloseElement();

            builder.OpenElement(seq + 5, "dd");
            builder.AddAttribute(seq + 6, "lang", Foreign(row.ValueLanguage));
            builder.AddContent(seq + 7, row.Value);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
    };

    /// <summary>The reader's language as a tag, and the marker for text that is not in it.</summary>
    /// <remarks>
    /// <c>Reader</c> rather than <c>ReaderLanguage</c>: the type that resolves it is
    /// <see cref="ReaderLanguage"/>, and a member of that name shadows the type inside every
    /// <c>VariableExplorer</c> partial, so <c>ReaderLanguage.Of(...)</c> would not compile in any
    /// of them. <see cref="VariableView"/> and <see cref="KildeView"/> already call it
    /// <c>Reader</c>; all three agree now.
    /// <para>
    /// The marking and the property resolution below delegate to
    /// <see cref="CatalogueProperties"/>, which is where the catalogue's own properties are
    /// resolved for every explorer in this package rather than once per component. The
    /// kildeutforsker draws properties the same way, and a second copy of this would drift from
    /// the first the moment either was edited.
    /// </para>
    /// </remarks>
    private string Reader => ReaderLanguage.Of(Language);

    private string? Foreign(string language) => CatalogueProperties.Foreign(language, Reader);

    /// <summary>The variable's curated properties, resolved for this reader.</summary>
    private List<PropertyRow> PropertyRows(VariableDetail detail) =>
        CatalogueProperties.Rows(detail.PropertyMetadata, detail.AdditionalProperties, Reader);

    /// <summary>Which tab of the open panel is showing.</summary>
    /// <remarks>
    /// Runa's panel has two: the metadata, and the data behind the variable. Reset whenever a
    /// different row is opened — a reader who was on Data for one variable has not asked to be on
    /// Data for the next, and arriving on a tab you did not choose is disorienting.
    /// </remarks>
    private PanelTab _tab = PanelTab.Details;

    /// <summary>The panel's tabs, in the order they are drawn.</summary>
    private static readonly PanelTab[] Tabs = Enum.GetValues<PanelTab>();

    private string TabId(PanelTab tab) => $"munin-explorer-tab-{_instance}-{tab}";

    /// <summary>
    /// The one tab panel. Its id does not vary by tab: there is a single panel whose contents
    /// change, so every tab points at it. An id per tab would leave the unselected tab's
    /// aria-controls naming an element that is not rendered.
    /// </summary>
    private string TabPanelId() => $"munin-explorer-tabpanel-{_instance}";

    private string TabLabel(PanelTab tab) => tab switch
    {
        PanelTab.Details => T.TabDetails,
        PanelTab.Data => T.TabData,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "No label for this tab."),
    };

    private string TabClass(PanelTab tab) =>
        tab == _tab
            ? "munin-explorer-meta__tab munin-explorer-meta__tab--active"
            : "munin-explorer-meta__tab";

    private void SelectTab(PanelTab tab) => _tab = tab;

    /// <summary>
    /// Arrow-key movement between the tabs, as the APG tabs pattern prescribes.
    /// </summary>
    /// <remarks>
    /// Without this a keyboard user tabs into the tablist and cannot change tab: the buttons carry
    /// <c>tabindex="-1"</c> when not selected, which is what stops the tablist costing one tab stop
    /// per tab. Arrow keys are what replaces those stops, so leaving them out makes the panel
    /// unreachable rather than merely awkward.
    /// </remarks>
    private void TabKey(KeyboardEventArgs e)
    {
        var i = Array.IndexOf(Tabs, _tab);

        var next = e.Key switch
        {
            "ArrowRight" or "ArrowDown" => (i + 1) % Tabs.Length,
            "ArrowLeft" or "ArrowUp" => (i - 1 + Tabs.Length) % Tabs.Length,
            "Home" => 0,
            "End" => Tabs.Length - 1,
            _ => i,
        };

        _tab = Tabs[next];
    }

    /// <summary>
    /// A datatype code as its name, from the facets the filter panel has already loaded.
    /// </summary>
    /// <remarks>
    /// The row endpoint sends the code — "2" — and nothing else. The filters endpoint sends the
    /// same codes WITH their names, and the component fetches those anyway to draw the filter
    /// panel, so the name is already in memory and costs no second request.
    /// <para>
    /// Falls back to the raw code when the facets have not arrived yet, or against an API that
    /// predates the names. A code is poor, but it is true; a lookup table here would freeze a copy
    /// of editable master data inside a package that ships to other people.
    /// </para>
    /// </remarks>
    private string? DataTypeName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        var named = _facets?.DataTypes.FirstOrDefault(d => d.Value == code)?.DisplayName;

        return string.IsNullOrWhiteSpace(named) ? code : named;
    }

    /// <summary>The heading of the drill-in view, named after whatever was opened.</summary>
    private RenderFragment DrilldownHeading => builder =>
    {
        var name = _kilde?.PreferredTerm ?? _datasamling?.PreferredTerm;

        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "headline headline-s margin--bottom");
        builder.AddAttribute(2, "id", SourceHeadingId);

        if (!string.IsNullOrWhiteSpace(name))
        {
            // The catalogue's own name, so it stays Norwegian whatever the UI language is.
            builder.AddAttribute(3, "lang", "no");
            builder.AddContent(4, name);
        }
        else
        {
            builder.AddContent(5, _sourceKind == SourceKind.Kilde ? T.ShowKilde : T.ShowDatasamling);
        }

        builder.CloseElement();
    };

    /// <summary>Leaves the drill-in view and returns to the list, which was never torn down.</summary>
    private Task CloseSourceAsync()
    {
        _sourceKind = null;
        _kilde = null;
        _datasamling = null;

        return Task.CompletedTask;
    }

    /// <summary>Whether this field is the one the list is currently ordered by.</summary>
    private bool IsActiveSort(SortField field) => _sort == field;

    /// <summary>The ordering, in the words aria-sort uses.</summary>
    private string AriaSort() => Ascending ? "ascending" : "descending";

    /// <summary>Whether the current ordering runs ascending.</summary>
    private bool Ascending => _direction == SortDirection.Ascending;

    /// <summary>
    /// Whether the Status column is worth drawing — that is, whether a row could say anything
    /// other than "Active".
    /// </summary>
    /// <remarks>
    /// The API computes VersjonStatus from GyldigTil and filters expired versions out unless
    /// IncludeHistorical is asked for, so in the default view the column is a constant.
    /// <para>
    /// This is the column's default rather than the last word on it. The picker can turn Status on
    /// against this and off with it, and once it has been pressed the reader's choice is what
    /// counts — see <see cref="_statusColumnChosen"/>.
    /// </para>
    /// </remarks>
    private bool ShowStatusColumn => _filter.IncludeHistorical;

    /// <summary>The list item's class, carrying helsedata's expanded state.</summary>
    private string RowItemClass(VariableSummary v) =>
        IsSelected(v)
            ? "munin-explorer-data-list__item munin-explorer-data-list__item--expanded"
            : "munin-explorer-data-list__item";

    /// <summary>
    /// The row's metadata line: code, source, data collection and period, in helsedata's own
    /// <c>munin-explorer-dataitem-main__column</c> shape.
    /// </summary>
    /// <remarks>
    /// Each value is labelled. helsedata's datakildeutforsker runs its values together
    /// unlabelled because there are only two of them and they are self-evident; ours are up to
    /// seven, and a reader who has turned half of them off has no header for the ones that are
    /// left to line up against, so "Inklusjon" on its own would say nothing about which field it is.
    /// </remarks>
    private RenderFragment InfoLine(VariableSummary v) => builder =>
    {
        // One div per column, each holding a span, which is exactly helsedata's shape. Their grid
        // is on .munin-explorer-dataitem-main, so the columns line up only if they are its direct
        // children — the row's own layout comes from CSS we do not own.
        // Runa's columns, in Runa's order. Runa is what this replaces helsedata's variable page
        // WITH, so it decides what a row says; helsedata decides what a row looks like. Taking the
        // column set from the page being retired would be copying the thing we are replacing.
        //
        // Four of the eight modifiers exist in helsedata's stylesheet today. __code, __dataType,
        // __status and __period do not, and they are emitted anyway — deliberately. The arrangement
        // with helsedata is that we supply class names and they write the rules, so these four ARE
        // the request, and the sample host carries the widths they should be given. A column with
        // no width rule sizes by content, which is what put Kode on two lines: a variable code is
        // one unbreakable token and cannot give way, so everything else must. Their header row is
        // further along: `variable-dataitem-header__period` is already in helsedata's variables.css, because
        // their own variable page has had a period column all along — it is only the cell modifier
        // that is missing, since theirs draws a bar sized inline rather than a column of text.
        //
        // Each one is drawn only while its column is on screen. What decides that is the reader,
        // through the column picker above the list — see ColumnVisible — except for Status, which
        // follows the filter until they say otherwise.
        if (ColumnVisible(ResultColumn.Code))
        {
            Column(builder, 100, T.FieldCode, v.Code, "code");
        }

        // The short name, which is what Runa shows — "ALS" rather than "Als registeret" — with the
        // full name on hover, also as Runa does. A kilde name is long and repeats down every row of
        // a single register's variables, so the short form is what makes the column readable. It
        // falls back to the full name where a kilde has no short one.
        if (ColumnVisible(ResultColumn.Kilde))
        {
            Column(builder, 200, T.FieldSource, v.KildeShortName ?? v.KildeName, "source", tooltip: v.KildeName);
        }

        if (ColumnVisible(ResultColumn.Datasamling))
        {
            Column(builder, 300, T.FieldDataCollection, v.DatasamlingName, "dataCollection");
        }

        if (ColumnVisible(ResultColumn.Variabelgruppe))
        {
            Column(builder, 400, T.FieldVariableGroup, v.VariabelgruppeName, "theme");
        }

        if (ColumnVisible(ResultColumn.DataType))
        {
            Column(builder, 500, T.FieldDataType, DataTypeName(v.DataType), "dataType");
        }

        // Status starts hidden unless historical variables can be in the list at all. The API
        // computes it from GyldigTil — Active unless the version has expired — and excludes
        // expired versions unless IncludeHistorical is set. In the default view every row is
        // therefore Active, and a column that says the same word on every row is not a column,
        // it is furniture. Verified against the live API: 100 rows sampled across five pages of
        // the catalogue, all Active. That is now a default rather than the whole rule: a reader
        // who wants the column anyway can press it in the picker, and their choice sticks.
        if (ColumnVisible(ResultColumn.Status))
        {
            Column(builder, 600, T.FieldStatus, v.VersionStatus, "status");
        }

        // The dataperiode as text — the same two dates the panel draws under its bar, from the
        // same fields. helsedata's own period cell is a bar and nothing else, with the dates on
        // hover, but a bar is drawn entirely by rules this package does not ship: in a host that
        // has not styled `munin-explorer-dataitem-period` the cell would be empty rather than plain, and
        // an empty column is indistinguishable from a variable with no period recorded. The panel
        // is where the bar is worth its dependency, because the row beside it says the dates.
        // The only column whose value is not the catalogue's own words: the dates are formatted for
        // the reader and the word between them is this component's, so it follows Language like a
        // label rather than staying Norwegian like a variable name. Hence `catalogue: false` — an
        // English reader hearing "Jan 2010 – Ongoing" announced by a Norwegian voice is the very
        // thing lang="no" is there to prevent, applied backwards.
        if (ColumnVisible(ResultColumn.DataPeriod))
        {
            Column(builder, 700, T.FieldDataPeriod, PeriodText(v.DataFrom, v.DataTo), "period", catalogue: false);
        }
    };

    /// <summary>The dataperiode in one line, or null where the catalogue has neither date.</summary>
    /// <remarks>
    /// Word for word what <see cref="PeriodBar"/> writes above its bar, including "?" for a missing
    /// start and the word for a period still running, so the column and the open panel never
    /// describe one variable's period two ways. Null rather than a dash when both dates are
    /// missing: the cell then says "Ikke oppgitt" in plain sight, which is what every other column
    /// does with a value the catalogue does not have.
    /// </remarks>
    private string? PeriodText(DateTimeOffset? from, DateTimeOffset? to) =>
        from is null && to is null
            ? null
            : $"{(from is { } f ? MonthYear(f) : "?")} – {(to is { } t ? MonthYear(t) : T.Ongoing)}";

    /// <summary>
    /// One column of a result row, in <c>munin-explorer-dataitem-main__column</c> shape.
    /// </summary>
    /// <remarks>
    /// The field name is not shown in the cell — the column header names it. It is still emitted
    /// for assistive technology, because a screen reader moving down a column has no header to
    /// glance up at.
    /// <para>
    /// <paramref name="catalogue"/> says whose words the value is. Nearly always the catalogue's,
    /// which are Norwegian whatever the reader's language is, so they are marked <c>lang="no"</c>.
    /// A column the component composes itself — the dataperiode — is in the reader's language
    /// already and is left unmarked, exactly like the "Ikke oppgitt" beside it, so it inherits the
    /// host page's language rather than claiming a language it is not in.
    /// </para>
    /// </remarks>
    private void Column(
        RenderTreeBuilder builder,
        int seq,
        string label,
        string? value,
        string? key,
        string? tooltip = null,
        bool catalogue = true)
    {
        // Sequence numbers ascend without gaps or repeats through every path below. Blazor uses
        // them positionally to diff one render against the next, so a number that goes backwards
        // makes the renderer compare the wrong nodes — an earlier version emitted seq+15 before
        // seq+2 and would have diffed the label span against the value span.
        builder.OpenElement(seq, "div");
        builder.AddAttribute(seq + 1, "class",
            key is null
                ? "munin-explorer-dataitem-main__column"
                : $"munin-explorer-dataitem-main__column munin-explorer-dataitem-main__{key}");

        // The cell, which is what this element has always been called in the comments here and is
        // now what it is. Without the role the value and the header above it were two unrelated
        // runs of text, so nothing said which column a value belonged to (WCAG 1.3.1).
        builder.AddAttribute(seq + 2, "role", "cell");

        // The full value as a tooltip on the CELL, because a cell can be clipped — the code column
        // truncates rather than wraps, since a broken identifier is neither readable nor copyable.
        // A column may show a shorter form than the value it holds: kilde shows the short name.
        var hoverText = string.IsNullOrWhiteSpace(tooltip) ? value : tooltip;

        if (!string.IsNullOrWhiteSpace(hoverText))
        {
            builder.AddAttribute(seq + 3, "title", hoverText);
        }

        // The field name, for assistive technology only. The column header names it on screen, so
        // showing it in every cell as well would undo what the header is for — but a screen reader
        // moving down a column has no header to glance up at, so the name has to travel with the
        // value or "Inklusjon" means nothing.
        //
        // NOT an aria-label on the value: aria-label REPLACES the text it labels, so a reader would
        // hear the field name instead of the value. screenreader-only is Stiler's own class for
        // this, 16 rules in the site-wide stylesheet.
        builder.OpenElement(seq + 4, "span");
        builder.AddAttribute(seq + 5, "class", "screenreader-only");
        builder.AddContent(seq + 6, $"{label}: ");
        builder.CloseElement();

        builder.OpenElement(seq + 7, "span");
        builder.AddAttribute(seq + 8, "class", "munin-explorer-dataitem-main__column__text");

        if (string.IsNullOrWhiteSpace(value))
        {
            builder.AddContent(seq + 9, T.NotSpecified);
        }
        else if (catalogue)
        {
            // The label follows Language; the value does not. Munin's metadata is Norwegian
            // whatever language the surrounding UI is in, and an English speech synthesiser
            // reading Norwegian variable names is unintelligible (WCAG 3.1.2).
            builder.OpenElement(seq + 10, "span");
            builder.AddAttribute(seq + 11, "lang", "no");
            builder.AddContent(seq + 12, value);
            builder.CloseElement();
        }
        else
        {
            // The component's own words, in the reader's language. Unmarked, so it inherits the
            // host page's language the same way every other string this component composes does.
            builder.AddContent(seq + 13, value);
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// One labelled item in the metadata line.
    /// </summary>
    /// <remarks>
    /// A missing value is written out as "Ikke oppgitt" in plain sight rather than drawn as an
    /// em dash with the words hidden behind a visually-hidden class. An em dash is either read
    /// as "em dash" or skipped in silence depending on the reader's punctuation setting, and
    /// neither says "we do not know" — but saying it out loud to everyone is better than saying
    /// it to assistive technology alone, and it means this markup needs no screen-reader-only
    /// rule from the host, which is just as well: Stiler has none.
    /// </remarks>
    private void Field(RenderTreeBuilder builder, int seq, string label, string? value, bool first)
    {
        builder.OpenElement(seq, "span");
        builder.AddAttribute(seq + 1, "class", "munin-explorer-dataitem-main__column__text");

        if (!first)
        {
            // Stiler's dot separator between card metadata items. Purely decorative and empty,
            // so it is kept out of the accessibility tree rather than left as a nameless node.
            builder.OpenElement(seq + 2, "span");
            builder.AddAttribute(seq + 3, "class", "dot");
            builder.AddAttribute(seq + 4, "aria-hidden", "true");
            builder.CloseElement();
        }

        builder.AddContent(seq + 5, $"{label}: ");

        if (string.IsNullOrWhiteSpace(value))
        {
            builder.AddContent(seq + 6, T.NotSpecified);
        }
        else
        {
            // The label follows Language; the value does not. Munin's metadata is Norwegian
            // whatever language the surrounding UI is in, and an English speech synthesiser
            // reading Norwegian variable names is unintelligible (WCAG 3.1.2).
            builder.OpenElement(seq + 7, "span");
            builder.AddAttribute(seq + 8, "lang", "no");
            builder.AddContent(seq + 9, value);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
