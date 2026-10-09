using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The two tabs beside the results, in the order they are drawn.</summary>
internal enum ExplorerTab
{
    /// <summary>What the reader is searching for.</summary>
    Search,

    /// <summary>What the reader has saved.</summary>
    VariableList,
}


/// <summary>The open row's tabs: what its data holds, then what the variable is. The RCL leads
/// this split now; Runa follows later or not at all (Fhi.Metadata-l9l2n.101).</summary>
// Declared in drawn order: Home/End and the arrow keys move by this enum's index.
internal enum PanelTab
{
    /// <summary>The kodeverk and the statistics the values are drawn from. Opens first.</summary>
    Data,

    /// <summary>Description, code, datatype and the latest year set.</summary>
    About,

    /// <summary>The reader's own notes, drawn only in a saved list's row.</summary>
    Notes,
}


/// <summary>
/// Search and browse published variables from the Munin Explorer API.
/// </summary>
/// <remarks>
/// <para>
/// This package ships no CSS, so the host stylesheet owns everything visual. Page furniture wears
/// <c>Fhi.Helsedata.Stiler</c>'s own names: <c>form-element__label</c>, <c>form-fieldset</c>, <c>searchbox__freetext*</c>,
/// <c>hd-button-square</c> with <c>button-square--primary</c>, <c>button-square--secondary</c>,
/// <c>button-square--ghost</c>, <c>button-square--ghost-blue</c>, <c>hd-button-reset</c>,
/// <c>margin-right</c>, <c>margin-bottom</c>,
/// <c>margin--bottom</c> and <c>margin--none</c>, <c>headline</c> with <c>headline-3</c>,
/// <c>headline-s</c> and <c>headline-xxs</c>, <c>caption</c>, <c>ingress</c>, <c>tag</c>,
/// <c>dot</c>, <c>infobox</c> with <c>infobox--bg-yellow</c>, and <c>screenreader-only</c>.
/// </para>
/// <para>
/// Everything else is under the <c>munin-explorer</c> prefix, whose rules ship in
/// <c>Fhi.Helsedata.Stiler</c> under <c>components/munin-explorer/</c>: the rows
/// (<c>munin-explorer-data-list*</c>, <c>munin-explorer-dataitem-*</c>), the list they sit in
/// (<c>munin-explorer-container</c>, <c>munin-explorer-results</c>), the opened panel
/// (<c>munin-explorer-meta*</c>), the pager (<c>munin-explorer-pagination</c>,
/// <c>munin-explorer-pagination-content</c>) and its skip link,
/// <c>munin-explorer-skiplink-pagination</c>, whose rule has to hide it until it is focused and
/// must not be scoped under <c>munin-explorer-header</c>, which the link sits outside.
/// <c>README.md</c> has the full inventory.
/// </para>
/// <para>
/// One pager is drawn at every width: a second, mobile copy would put two "Neste" buttons for one
/// list in the tab order and the accessibility tree.
/// </para>
/// <para>
/// The filter panel takes no class name from helsedata's stylesheets: a <c>&lt;details&gt;</c> per facet, a nested <c>&lt;ul&gt;</c> for the kilde/delkilde hierarchy and a checkbox per value,
/// dressed by Stiler under <c>munin-explorer-filters</c>. A host without Stiler must at least indent the lists, or the hierarchy nests in the accessibility tree but reads flat on screen.
/// Under Stiler <c>munin-explorer-filters</c> is a sidebar from 1024px; narrower, the fieldset (also <c>munin-explorer-filters__facets</c>) folds behind <c>munin-explorer-filters__toggle</c>.
/// A host drawing its own sidebar hides that toggle there and sets <c>.munin-explorer-filters__facets[hidden] { display: block }</c>,
/// keeping <c>display: none</c> on it below that width if a reset gives fieldsets a display.
/// </para>
/// <para>
/// The hierarchy trail over the results adds two names of ours — <c>munin-explorer-breadcrumb</c>
/// for the trail and <c>munin-explorer-crumb</c> for its steps. It is an <c>&lt;ol&gt;</c> of
/// <c>&lt;button&gt;</c>s whose steps narrow the filter rather than navigate, so Stiler's
/// <c>.breadcrumbs</c> names, worn by the detail pages' trail (<see cref="DetailTrail"/>), do not
/// apply. The chevrons are a host's to draw, and a host that draws nothing gets a numbered list
/// that still reads correctly, in order, with the right names.
/// </para>
/// <para>
/// The column picker sits in <c>munin-explorer-header</c> with its <c>__actions</c> and
/// <c>__actions-button</c>; its open list is Stiler's <c>dropdown-choicepicker</c> with
/// <c>--right</c> and <c>__item</c>, positioned against an inline <c>position: relative</c>, and
/// the disclosure wears <c>munin-explorer__dropdown</c> and <c>dropdown</c>. A host that has none
/// of them still gets a working <c>&lt;details&gt;</c>, drawn in the flow rather than over the
/// list. What it must supply either way is <c>screenreader-only</c>, or the sentence explaining
/// why the last column will not turn off is on screen for everyone — and rules taking the
/// browser's disclosure marker off the <c>&lt;summary&gt;</c>, which would otherwise draw a
/// triangle beside the button.
/// </para>
/// <para>
/// The detail panel adds no style name. It is a <c>&lt;dl&gt;</c> of labels and values under Om
/// variabelen and a <c>&lt;ul&gt;</c> for the kodeverk under Data, wearing Stiler's
/// <c>form-element__label</c>, <c>caption</c>, <c>infobox</c> and the ghost square button, so a
/// host supplies base styling for those two elements; one that supplies none still gets a panel
/// that reads correctly, just an unindented one. <c>munin-explorer-detail</c> is a handle that
/// Stiler also dresses, in <c>components/munin-explorer/_detail.scss</c>.
/// </para>
/// <para>
/// The kilde and datasamling do not open inside that panel: they take over the component's own
/// area as a drill-in, wearing the handle <c>munin-explorer-drilldown</c>. What it holds is a
/// heading in Stiler's <c>headline headline-s</c> and a <c>&lt;dl&gt;</c>, or — for a kilde — the
/// whole of <c>KildeView</c>. <c>munin-explorer-source</c> is not a class: it is the prefix of the
/// element id that names the region (<c>munin-explorer-source-{instance}</c>), so
/// <c>.munin-explorer-source</c> selects nothing.
/// </para>
/// <para>
/// <c>KildeView</c>, <c>VariableView</c> and <c>DatasamlingView</c> add handles of their own under
/// <c>munin-explorer-kilde*</c>, <c>munin-explorer-whole*</c> and <c>munin-explorer-datasamling*</c>.
/// Every element wearing them also wears a Stiler class or is dressed by its own browser default,
/// so a host that defines none of them loses no information. The README's inventory table is the
/// full list.
/// </para>
/// <para>
/// The panel's Data tab adds handles too — <c>munin-explorer-kodeverk</c> with its <c>__item</c>,
/// <c>__name</c> and <c>__reference</c> parts, and <c>munin-explorer-codes</c> with its
/// <c>__table</c>. The code list is a real <c>&lt;table&gt;</c>, because an element degrades to its
/// own browser default, which for a table is aligned columns, where a class name Stiler has never
/// heard of degrades to nothing at all.
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
/// </remarks>
public sealed partial class VariableSearch : ComponentBase
{
    /// <summary>
    /// Initial search text. Set by the host, typically from a URL query parameter — the
    /// component has no NavigationManager and no URL logic of its own, because the CMS
    /// host owns routing.
    /// </summary>
    /// <remarks>
    /// Read only at first render, and owned by the component afterwards: changing it on a mounted
    /// component has no effect. <see cref="SearchChanged"/> is how the host hears what it became.
    /// </remarks>
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
    /// Two-way, like <see cref="Page"/>, and for the same reason: the reader chooses between 10, 20
    /// and 50 beside the pager, so the value moves without the host touching it. A host that
    /// mirrors it into a URL keeps the choice on a shared link; a host that ignores
    /// <see cref="PageSizeChanged"/> still gets a working control and loses the choice on reload.
    /// </para>
    /// <para>
    /// Choosing a size returns the reader to page 1 and raises <see cref="PageChanged"/> with it.
    /// Page 7 of 12 is not page 7 of 5: the rows are renumbered, so the old number names a
    /// different part of the result, and the reader would lose their place without having asked to.
    /// </para>
    /// <para>
    /// Values outside 1–100 are clamped rather than rejected. The server clamps them anyway, and a
    /// zero or negative page size would otherwise make the page arithmetic on this side
    /// meaningless. The control's own values go through the same clamp, so it has no way past it.
    /// </para>
    /// <para>
    /// The default is 20, which is the middle of the three and Runa's own starting size. It was 25
    /// until the control arrived: a default outside the offered values would have left a host that
    /// never set this showing three buttons with none of them pressed, which is truthful and reads
    /// as broken. A host that had relied on 25 has to say so now.
    /// </para>
    /// <para>
    /// Read only at first render: changing it on a mounted component has no effect.
    /// </para>
    /// </remarks>
    [Parameter] public int PageSize { get; set; } = 20;

    /// <inheritdoc cref="PageSize"/>
    [Parameter] public EventCallback<int> PageSizeChanged { get; set; }

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

    /// <summary>An address for this search narrowed to a collection. Null uses the filtering callback.</summary>
    /// <remarks>Supply from a fully interactive parent. Preserve the search's other facets in the address.</remarks>
    [Parameter] public Func<Guid, string>? DatasamlingVariablesHref { get; set; }

    /// <summary>
    /// The reader's own variable lists, drawn behind a second tab beside the results.
    /// </summary>
    /// <remarks>
    /// Left null and there are no tabs at all: the results render where they always did. Passed,
    /// and the tablist appears between the filters and the results — Runa's own placement, so the
    /// search box and the facets stay on screen whichever tab is open. <see cref="VariableExplorer"/>
    /// passes <see cref="VariableListView"/> here; a host composing its own page can pass anything.
    /// </remarks>
    [Parameter] public RenderFragment? VariableList { get; set; }

    /// <summary>
    /// The filter panel that belongs to <see cref="VariableList"/>, drawn in the filter column
    /// while that tab is open and in place of the search's own facets.
    /// </summary>
    /// <remarks>
    /// A second fragment rather than something the list fragment draws for itself, because the two
    /// land in different columns of Stiler's grid and only a child of this component's own section
    /// reaches the filter one. Ignored unless <see cref="VariableList"/> is passed too: without it
    /// there is no tab for this to belong to. <see cref="VariableExplorer"/> passes
    /// <see cref="VariableListFilters"/> here; a host composing its own page can pass anything.
    /// </remarks>
    [Parameter] public RenderFragment? VariableListFilters { get; set; }

    /// <summary>
    /// The code of a shared variable list being opened, or null. While one is present the
    /// <see cref="VariableList"/> tab is drawn, signed out too, and a new code opens on it.
    /// </summary>
    /// <remarks>
    /// Only what draws the tab: <see cref="VariableListView"/> opens the list itself.
    /// <see cref="VariableListFilters"/> is not drawn while one is present, since it narrows the
    /// reader's own list and a shared one is not theirs.
    /// </remarks>
    [Parameter] public string? ShareCode { get; set; }

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
    /// One paragraph under the title saying what the page is for, in the host's own words and
    /// language. Leave it null or blank and nothing is drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host's text rather than the package's, because the sentence is editorial: the site that
    /// links to this page owns its voice, and should be able to change it without a new release of
    /// this package. So there is no default, in either language, and nothing is translated — pass
    /// the text already in the language <see cref="Language"/> names.
    /// </para>
    /// <para>
    /// Drawn as plain text in a <c>&lt;p class="munin-explorer__lede"&gt;</c> directly after the
    /// title, as a direct child of <c>.munin-explorer</c>. With no text there is no element at all,
    /// not an empty one: Fhi.Helsedata.Stiler gives the lede its own grid row only when the element
    /// is there, so an empty paragraph would still move the filters and results down.
    /// </para>
    /// </remarks>
    [Parameter] public string? Lede { get; set; }

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

    /// <summary>
    /// The order the list is in, and the direction. Two-way. <see cref="SortField.Default"/> is the
    /// curated opening order, which has no active column.
    /// </summary>
    /// <remarks>
    /// Runa keeps both in the URL, so a colleague opening a shared link sees the same order. They
    /// are two parameters rather than one because a host binds each with <c>@bind-Sort</c> and
    /// <c>@bind-Direction</c>; they change together, and both callbacks fire on the same click.
    /// <para>
    /// Raised only after the reordered list has actually arrived. A failed fetch puts the old order
    /// back — see <see cref="SortAsync"/> — and telling the host about an order the API never
    /// delivered would leave a URL describing a list nobody can see.
    /// </para>
    /// <para>
    /// A <see cref="Sort"/> the host changes after the first render — its own back button, say — is
    /// followed: the list is fetched again in that order, from page one, with that <see cref="Direction"/>.
    /// If a fetch is already in flight it is followed once that one has landed. A <see cref="Direction"/>
    /// changed on its own is <b>not</b> followed, because a host re-rendering between the two
    /// callbacks of one press hands back a direction still one step behind; change it together with
    /// <see cref="Sort"/>, or leave ordering to the reader's presses.
    /// </para>
    /// <para>
    /// So the callbacks carry the order in force, which is not always the one the host just asked
    /// for: if the fetch for a host's new <see cref="Sort"/> fails, the old order stays and both
    /// <see cref="SortChanged"/> and <see cref="DirectionChanged"/> are raised with it.
    /// </para>
    /// </remarks>
    [Parameter] public SortField Sort { get; set; } = SortField.Default;

    /// <inheritdoc cref="Sort"/>
    [Parameter] public EventCallback<SortField> SortChanged { get; set; }

    /// <summary>The direction the list is ordered in. Two-way, alongside <see cref="Sort"/>.</summary>
    /// <remarks>
    /// Applied only together with a change to <see cref="Sort"/>: a <see cref="Direction"/> changed
    /// on its own after first render is ignored, deliberately. <see cref="Sort"/> says why.
    /// </remarks>
    [Parameter] public SortDirection Direction { get; set; } = SortDirection.Ascending;

    /// <inheritdoc cref="Sort"/>
    [Parameter] public EventCallback<SortDirection> DirectionChanged { get; set; }

    /// <summary>
    /// Whether the filter panel asks for guide lines down the levels of the facet tree. Two-way,
    /// <b>on by default</b>, which is what the <c>Nivålinjer</c> switch turns off.
    /// <b>The package draws no lines.</b> It puts
    /// <c>data-level-lines="true"</c> on the panel and the host's stylesheet draws them, so a host
    /// with no rule for that attribute sees nothing change when this is on.
    /// </summary>
    /// <remarks>
    /// The rule is one <c>border-left</c> on the nested lists — both sample hosts carry it. Give it
    /// at least 3:1 against whatever is behind the panel: a guide line is a non-text control under
    /// WCAG 1.4.11, and an ordinary light border grey does not reach it. The samples measured 1.16:1
    /// with theirs before this was settled, which is a line that is there and cannot be seen.
    /// <para>
    /// A way of drawing the panel rather than a filter, so it is deliberately not part of the
    /// shareable state: a link carries what the reader is looking at, not how they like to look at
    /// it. The <c>Nivålinjer</c> switch in the panel is what the reader presses — a native
    /// <c>&lt;button&gt;</c> carrying <c>role="switch"</c> and <c>aria-checked</c>, so it announces
    /// as on and off — and pressing it raises <see cref="LevelLinesChanged"/>.
    /// </para>
    /// <para>
    /// The package does not remember the choice, by decision rather than by omission. Reaching
    /// <c>localStorage</c> from a Blazor circuit is a JS interop call, and this package makes none —
    /// it has to run inside a static-SSR host as well as an interactive one, and what is remembered
    /// about a reader is the host's own policy to set. Like <see cref="Search"/> and <see cref="Filter"/>
    /// it is read once at mount and owned by the component afterwards, so a host that wants it remembered
    /// stores what this raises and supplies it at the next mount; a later change to the parameter on
    /// a mounted component does nothing. A host that stores nothing gets the lines on at every
    /// visit, so a reader who never finds the switch still sees the tree as a hierarchy.
    /// </para>
    /// </remarks>
    [Parameter] public bool LevelLines { get; set; } = true;

    /// <inheritdoc cref="LevelLines"/>
    [Parameter] public EventCallback<bool> LevelLinesChanged { get; set; }

    /// <summary>
    /// Whether the filter panel's kilde tree draws a node icon in front of each name — a folder on
    /// a kilde or a delkilde, one glyph per datakategori on a datasamling, and nothing on a
    /// variabelgruppe. Two-way, <b>on by default</b>, which is what the <c>Ikoner</c> switch turns
    /// off. It is passed on to the kilde a reader drills into, so one press decides the icons on
    /// both surfaces — <see cref="KildeHierarchyView.ShowNodeIcons"/> is the same choice there.
    /// </summary>
    /// <remarks>
    /// <b>It costs every reader the same thing.</b> The glyphs are <c>aria-hidden</c> and the
    /// <c>screenreader-only</c> words beside them are what says the same categories aloud, so the
    /// switch takes both or neither: a datasamling's datakategorier stop being stated on the row
    /// for a reader who can see the tree and for one who is listening to it alike. Keeping the
    /// words behind an <c>Ikoner</c> switch that is off would leave a control that does nothing
    /// for the reader pressing it, which is why <see cref="KildeHierarchyView.ShowNodeIcons"/>
    /// takes both too. <b>Nothing else moves.</b> The filter a row ticks, the counts beside it,
    /// the kildetype badge on a kilde and the level lines are all drawn exactly as before — the
    /// badge in particular is a fact rather than a picture, so it stays on the row and in the
    /// checkbox's accessible name whichever way this is set.
    /// <para>
    /// The <c>Ikonforklaring</c> legend under the facets goes with them. It names every datakategori
    /// the tree can draw, glyph and word, in the order a row draws several of them in, and with this
    /// off there is no picture left in the panel for it to explain. It is a
    /// <c>&lt;details&gt;</c> resting shut and folding with the facets under <c>Utvid alle</c> and
    /// <c>Skjul alle</c>, and it wears <c>munin-explorer-filters__legend</c> on the list with
    /// <c>munin-explorer-filters__legend-item</c> on each row.
    /// </para>
    /// <para>
    /// The glyphs are inline <c>&lt;svg&gt;</c> at <c>1em</c> in <c>currentColor</c>, each wearing
    /// <c>munin-explorer-filters__icon</c> and a <c>data-node-icon</c> naming its datakategori, so
    /// what a host stylesheet decides is whether two categories are told apart by colour as well as
    /// by shape. A host with no rule for them still gets icons, at text size and in the text colour.
    /// </para>
    /// <para>
    /// Remembered by the host and never by the package, exactly as <see cref="LevelLines"/> is:
    /// reaching <c>localStorage</c> from a circuit is a JS interop call this package does not make,
    /// and what is remembered about a reader is the host's own policy to set. It is read once at
    /// mount and owned by the component afterwards, so a host stores what
    /// <see cref="ShowNodeIconsChanged"/> raises and supplies it at the next mount; changing the
    /// parameter on a component that is already mounted does nothing. A host that stores nothing
    /// gets the icons at every visit.
    /// </para>
    /// </remarks>
    [Parameter] public bool ShowNodeIcons { get; set; } = true;

    /// <inheritdoc cref="ShowNodeIcons"/>
    [Parameter] public EventCallback<bool> ShowNodeIconsChanged { get; set; }

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
    /// <para>
    /// Read only at first render: changing it on a mounted component has no effect.
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
    /// Read once, when the component initialises, and owned by the component afterwards, so changing
    /// it after first render has no effect. There is no navigation behind it: the detail is drawn
    /// inside the row it belongs to, so opening one costs a fetch and a render rather than a page.
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

    /// <summary>
    /// The datasamling of the open row, beside <see cref="SelectedVariableId"/>: a variable has a row
    /// per datasamling it is delivered from, so the variable alone does not say which row is open.
    /// </summary>
    /// <remarks>
    /// Read once, with <see cref="SelectedVariableId"/>. Null with a variable set opens that variable's
    /// first row on the page — what an older link carries — and the component then reports the row's
    /// datasamling through <see cref="SelectedDatasamlingIdChanged"/>.
    /// </remarks>
    [Parameter] public Guid? SelectedDatasamlingId { get; set; }

    /// <summary>
    /// Raised with the open row's datasamling whenever <see cref="SelectedVariableIdChanged"/> is, and
    /// just before it; null when the panel closes or the open row has no datasamling. Gives the host
    /// <c>@bind-SelectedDatasamlingId</c>, on the interactivity terms that callback states.
    /// </summary>
    [Parameter] public EventCallback<Guid?> SelectedDatasamlingIdChanged { get; set; }

    [Inject] private IMuninExplorerClient Client { get; set; } = null!;

    private string? _search;

    // Held so the clear control can hand focus back to the field it emptied. The control is drawn
    // only while there is something to clear, so pressing it is what takes it off the page.
    private ElementReference _searchField;

    private bool _loading;

    // The rows read alone. _loading also covers the facets read that follows, which is what holds every press
    // back; the line over the rows reads this one, so it reports them as soon as they land.
    private bool _rowsLoading;
    private string? _error;
    private Page<VariableSummary>? _result;

    // The request that failed and what pressing the button beside it would do. The request is the
    // control's presence too — one field rather than a flag that could only ever disagree with it —
    // and it outlives its own retry by one fetch, so the pressed button keeps its focus.
    private RowRequest? _failedRows;
    private bool _retryRowsEnabled;

    // The facet selection the visible rows were fetched with. Never null — VariableFilter.None is
    // "no narrowing" — so nothing downstream has to spell that case out twice.
    private VariableFilter _filter = VariableFilter.None;

    // The facets and counts as last reported for _executedSearch and _filter. Never set back to null:
    // the filter controls render from it, so a failed refresh keeps the old counts and says so
    // through _facetError rather than removing the control the reader just pressed.
    private FilterOptions? _facets;

    // Set when the facets could not be refreshed, which is a different failure from the search
    // failing: the rows on screen are the right rows, and it is the numbers beside the filters that
    // may now be stale. Reported separately for that reason.
    private string? _facetError;

    // Its own retry control, for its own failure. Shared state with the rows' would mean one button
    // answering for two messages, and the counts and the list are asked for separately.
    private bool _retryFacetsShown;
    private bool _retryFacetsEnabled;

    // The row whose detail panel is open — a variable from one datasamling — and what has been fetched
    // for it. Never a row that is not on screen: the panel is drawn inside its own row, so a selection
    // the current result does not contain is one nothing can render — see DropSelectionIfGoneAsync.
    private VariableDatasamlingKey? _selected;

    // A host named the variable and no datasamling, as a link from before rows were per datasamling
    // does: the first fetch resolves it to that variable's first row on the page.
    private bool _selectionNeedsRow;

    private VariableDetail? _detail;
    private bool _detailLoading;

    // Bumped by every open and close, so a detail fetch can tell whether its panel is still the one
    // it was started for: closing and reopening one row is two calls carrying one id.
    private int _detailGeneration;

    // Set when the detail could not be fetched or is not published. Not _error: the rows on screen
    // are unaffected, and what failed is one panel.
    private string? _detailError;

    // Which owner the open variable's panel discloses, and what was fetched for it; null until the
    // reader asks, so one press does not cost three requests. It lives only under an open variable
    // panel, which is why LoadDetailAsync and ClearSelection both clear it.
    private SourceKind? _sourceKind;
    private KildeDetail? _kilde;
    private DatasamlingDetail? _datasamling;
    private bool _sourceLoading;

    // Its own generation, as _detailGeneration: closing and reopening an owner is two calls with one
    // id. Separate, because an owner opened over a panel still on screen abandons nothing.
    private int _sourceGeneration;

    // Set when the owner could not be fetched, or when the API publishes no such kilde or
    // datasamling. Its own field for the same reason _detailError is: what failed is one panel
    // inside one panel, and neither the rows nor the variable above it are stale because of it.
    private string? _sourceError;

    // The code lists of the open variable, replaced with the variable so a late fetch writes nowhere.
    private KodeverkCodeLists? _codeLists;

    // The API's own default order, ascending, which is also where Runa starts — and the order the
    // API returns when it is asked for none, so the first render costs no extra query parameters.
    private SortField _sort = SortField.Default;
    private SortDirection _direction = SortDirection.Ascending;

    /// <summary>The <see cref="Sort"/> this component last followed, so a change can be told from an echo.</summary>
    private SortField _sortParameter;

    // The page and the size being asked for: the host's parameters, read once at mount and owned
    // here afterwards. Both send the reader back to page one when they change, because a renumbered
    // result leaves someone on page 7 in the middle of a sequence they never saw the start of.
    private int _page = 1;
    private int _pageSize = 20;

    // Whether the pager has been pressed since the last search or sort. A retreat can land on a
    // single-page result, and dropping the pager then would take Neste out from under focus.
    private bool _keepPager;

    // Whether the fetch running is the one the rows' retry button started, which nothing else can
    // report: _loading is raised by the facets too, and _failedRows outlives its own answer.
    private bool _retryingRows;

    // The search text the visible result came from. @bind writes _search on blur, so the box can
    // hold an unsubmitted query while the rows still show the previous one.
    private string? _executedSearch;

    // The term the last rows read asked for, trimmed as an answered one is, whether or not it was answered.
    // A host sort resends it: after a failed search it is the term that search sent; with rows on screen it is theirs.
    private string? _requestedSearch;

    // Unique per instance so two explorers on one page cannot collide on DOM ids,
    // which would be a WCAG 4.1.1 failure as well as breaking label association.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    private string SearchId => $"munin-explorer-search-{_instance}";
    private string TitleId => $"munin-explorer-title-{_instance}";
    private string PaginationId => $"munin-explorer-pagination-{_instance}";

    /// <summary>The size control, which its visible label points at with <c>for</c>.</summary>
    private string PageSizeSelectId => $"munin-explorer-pagination-size-{_instance}";

    // Per row as well as per instance: the detail panel is wired to its own row with
    // aria-controls and aria-labelledby, and two explorers listing the same variable would
    // otherwise mint the same id twice on one page.
    private string RowHeadingId(VariableSummary v) => $"munin-explorer-heading-{_instance}-{RowSuffix(v)}";
    private string RowNameId(VariableSummary v) => $"munin-explorer-name-{_instance}-{RowSuffix(v)}";
    private string DetailId(VariableSummary v) => $"munin-explorer-detail-{_instance}-{RowSuffix(v)}";
    private string SaveButtonId(VariableSummary v) => $"munin-explorer-save-{_instance}-{RowSuffix(v)}";

    // The row's own key: a variable repeats down the page once per datasamling (Fhi.Metadata-d07al.1).
    private static string RowKeyOf(VariableSummary v) => v.RowKey ?? v.Id.ToString();

    // The id half that tells two rows of one variable apart; just the variable from an API with one row per variable.
    private static string RowSuffix(VariableSummary v) =>
        v.RowKey is null ? v.Id.ToString("N") : $"{v.Id:N}-{(v.DatasamlingId is { } d ? d.ToString("N") : "none")}";

    // Per instance and not per row: the owner panel hangs inside the one open variable panel, so
    // there is never more than one of it in this component's DOM. The kind is in the toggle's id
    // because the two toggles are on screen together.
    private string SourceId => $"munin-explorer-source-{_instance}";
    private string SourceHeadingId => $"munin-explorer-source-heading-{_instance}";
    private string SourceToggleId(SourceKind kind) =>
        $"munin-explorer-source-toggle-{_instance}-{kind.ToString().ToLowerInvariant()}";

    private Texts T => Texts.For(Language);

    private string Busy => _rowsLoading ? "true" : "false";

    // The shared lock, as before: the panel is busy for any request out, its own counts or the rows.
    private string FiltersBusy => _loading ? "true" : "false";

    /// <summary>Whether the rows' failure box shows a retry in progress. Set by the retry itself:
    /// <c>_loading</c> is raised by the facets refresh too, and the offer outlives its answer.</summary>
    private bool RetryingRows => _retryingRows;

    /// <summary>What that box says: the failure, or that the offer beside it is being answered.</summary>
    private string? RowsAlert => RetryingRows ? T.Retrying : _error;

    /// <summary>The sizes the reader chooses between, which are Runa's own.</summary>
    private static readonly int[] PageSizeOptions = [10, 20, 50];

    /// <summary>The sizes the control offers: the three, and the host's own if it is not one, since a
    /// select with no option for the size in force shows the first.</summary>
    private IEnumerable<int> OfferedPageSizes =>
        PageSizeOptions.Contains(ClampedPageSize)
            ? PageSizeOptions
            : PageSizeOptions.Append(ClampedPageSize).Order();

    /// <summary>Bumped whenever a change is refused, to key the control below: the browser has already
    /// moved the select, and a render tree that did not change would not move it back.</summary>
    private int _sizeRefusals;

    /// <summary>Reads the size off the <c>&lt;select&gt;</c> and applies it. A value the control does
    /// not offer is dropped rather than clamped: only edited markup can send one.</summary>
    private async Task OnPageSizeChangedAsync(ChangeEventArgs args)
    {
        // Refused, and the select has already moved itself, so it has to be put back — see _sizeRefusals.
        if (_loading
            || !int.TryParse(args.Value?.ToString(), out var size)
            || !OfferedPageSizes.Contains(size))
        {
            _sizeRefusals++;

            return;
        }

        await SetPageSizeAsync(size);
    }

    /// <summary>Rows per page as actually requested — see <see cref="PageSize"/>.</summary>
    /// <remarks>
    /// The one clamp, so the reader's control cannot reach the API with a size the host's own
    /// parameter would have been held to.
    /// </remarks>
    private int ClampedPageSize =>
        Math.Clamp(_pageSize, ExplorerUrlState.MinPageSize, ExplorerUrlState.MaxPageSize);

    /// <summary>How many variables the search matched, not how many are on screen.</summary>
    private int TotalCount => _result?.TotalCount ?? 0;

    /// <summary>How many pages the result has; at least 1, so "Side 1 av 0" is never written.</summary>
    /// <remarks>
    /// The server's count first, since the server clamps the page size. The arithmetic is a fallback
    /// for a client that leaves it at zero, and divides by <see cref="ResultPageSize"/>, the size the rows were built with.
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

    /// <summary>Whether the pager belongs on screen: more than one page, or a reader already on it.</summary>
    /// <remarks>
    /// Removing the pressed control drops focus to <c>&lt;body&gt;</c> — see <see cref="_keepPager"/>.
    /// </remarks>
    private bool ShowPager => _result is not null && (TotalPages > 1 || _keepPager);

    /// <summary>The 1-based position of the first row on screen, or 0 when there are no rows.</summary>
    /// <remarks>
    /// Guarded on the rows, as <see cref="LastItemOnPage"/> is, or an empty page reads "Viser 26–0 av 312".
    /// </remarks>
    private int FirstItemOnPage =>
        _result is null || _result.Items.Count == 0 ? 0 : ((ResultPage - 1) * ResultPageSize) + 1;

    /// <summary>The 1-based position of the last row on screen, counted from the rows delivered.</summary>
    /// <remarks>
    /// Counted rather than <c>page × size</c>, so the last page says 312 and not 325.
    /// </remarks>
    private int LastItemOnPage =>
        _result is null || _result.Items.Count == 0 ? 0 : FirstItemOnPage + _result.Items.Count - 1;

    /// <summary>
    /// The page size the visible result was actually built with, which is the server's answer when
    /// it gave one and what we asked for otherwise.
    /// </summary>
    private int ResultPageSize => _result is { Size: > 0 } page ? page.Size : ClampedPageSize;

    /// <summary>The page the visible result is: the server's answer when it gave one, else the page asked for.</summary>
    /// <remarks>
    /// An API that clamps an out-of-range page would otherwise have the row range counted from the
    /// number asked for — "Viser 276–300 av 200".
    /// </remarks>
    private int ResultPage => _result is { PageNumber: > 0 } page ? page.PageNumber : _page;

    /// <summary><c>"true"</c> on a pager button that would do nothing, and nothing on one that works.</summary>
    /// <remarks>
    /// Not <c>disabled</c>: pressing Neste to the last page would then drop focus to <c>&lt;body&gt;</c>.
    /// The button is inert either way, since <see cref="GoToPageAsync"/> clamps.
    /// </remarks>
    private static string? AriaDisabled(bool enabled) => enabled ? null : "true";

    /// <summary>The component's own heading level, clamped into the range that is a heading.</summary>
    private int TitleLevel => Math.Clamp(HeadingLevel, 1, 6);

    /// <summary>The heading level for a result card: one below the component's own title.</summary>
    /// <remarks>
    /// Under an <c>h6</c> title the cards sit at <c>h6</c> too: HTML has no <c>h7</c>, and a flat
    /// outline beats dropping the headings.
    /// </remarks>
    private int RowLevel => Math.Clamp(TitleLevel + 1, 1, 6);

    /// <summary>The visible result in one sentence: the live announcement and the list's name.</summary>
    /// <remarks>
    /// It names the ordering and which rows are showing, so a sort or a page turn is announced by
    /// rewriting it. Assembled in <see cref="Texts"/> so a language can order the clauses its own way.
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

    /// <summary>The title, at the level the host asked for; Razor cannot compute an element name.</summary>
    /// <remarks>
    /// Stiler's <c>headline-3</c> pins the size, so mounting one level deeper does not shrink it.
    /// </remarks>
    private RenderFragment Heading => builder =>
    {
        builder.OpenElement(0, $"h{TitleLevel}");
        builder.AddAttribute(1, "class", "headline headline-3");
        builder.AddAttribute(2, "id", TitleId);
        builder.AddContent(3, T.Title);
        builder.CloseElement();
    };

    // Named for the variable: "Vis detaljer" repeated down a column says nothing about which row it
    // opens. The w13lk rule applies, so a blank PreferredTerm is named by its code.
    private string ExpandLabel(VariableSummary v)
    {
        var name = T.Named(v.PreferredTerm, v.Code).Text;

        return IsSelected(v) ? T.CollapseVariableDetail(name) : T.ExpandVariableDetail(name);
    }

    /// <summary>The first column: the variable's name, the row's disclosure and its one Tab stop.</summary>
    /// <remarks>
    /// No heading element: <c>munin-explorer-dataitem-main__name</c> sizes the flex item, and a
    /// heading between would pull the column out of line (Fhi.Metadata-35w0p.78).
    /// </remarks>
    private RenderFragment RowHeading(VariableSummary v) => builder =>
    {
        // A row owns only cells and a <button> cannot be one, so the wrapper is the rowheader. It
        // wears the button's class as well, because that class carries the column's flex weight;
        // the one rule that repeats is Stiler's empty ::after overlay, which draws nothing here.
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-dataitem-main__name");
        builder.AddAttribute(2, "role", "rowheader");

        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "class", "munin-explorer-dataitem-main__name");
        builder.AddAttribute(5, "type", "button");
        builder.AddAttribute(6, "id", RowNameId(v));

        // Load-bearing beyond assistive tech: Stiler keys both the chevron's picture and the row
        // ring on this button's aria-expanded (Fhi.Metadata-35w0p.80).
        builder.AddAttribute(7, "aria-expanded", DetailExpanded(v));
        builder.AddAttribute(8, "aria-controls", DetailControls(v));
        builder.AddAttribute(9, "aria-label", ExpandLabel(v));

        // Never disabled, including while its own fetch runs: pressing it again is how the panel
        // is closed, and disabling the element that has focus drops focus to <body>.
        builder.AddAttribute(10, "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, e => ToggleDetailFromNameAsync(v, e)));

        // The click stops here, or the strip behind toggles too and one press opens and shuts the
        // panel. The mousedown does not: a drag begun on the name is measured by the strip, which
        // has to have seen where it went down. (Fhi.Metadata-l9l2n.81)
        builder.AddEventStopPropagationAttribute(11, "onclick", true);

        // First child and a descendant of the button: Stiler draws the chevron through the
        // button's aria-expanded, so the span carries no state of its own beyond the glyph class.
        builder.OpenElement(12, "span");
        builder.AddAttribute(13, "class",
            IsSelected(v)
                ? "icon icon-keyboard-arrow-up munin-explorer-dataitem-main__expand-icon"
                : "icon icon-keyboard-arrow-down munin-explorer-dataitem-main__expand-icon");
        builder.AddAttribute(14, "aria-hidden", "true");
        builder.CloseElement();

        builder.OpenElement(15, "span");
        builder.AddAttribute(16, "class", "munin-explorer-dataitem-main__column__text");
        // The save button in the open panel borrows these words for its name, so this id is on
        // the span holding the name alone and is written nowhere else (WCAG 4.1.1).
        builder.AddAttribute(17, "id", RowHeadingId(v));
        // Munin's variable names are Norwegian whatever language the surrounding UI is in.
        builder.AddAttribute(18, "lang", "no");
        // No title: Stiler wraps the name since 0.1.88, so a tooltip would repeat what is on screen.
        builder.AddContent(19, v.PreferredTerm);
        builder.CloseElement();

        builder.CloseElement();

        // The rowheader cell.
        builder.CloseElement();
    };

    /// <summary>The column header row, in helsedata's shape: one <c>sortable-header</c> cell per column.</summary>
    /// <remarks>
    /// aria-current, not aria-pressed: pressing again flips the direction. Cells follow
    /// <see cref="ColumnVisible"/> as the rows do, so a hidden sorted column loses its header too.
    /// </remarks>
    private RenderFragment ResultHeader() => builder =>
    {
        // The header rowgroup, the <thead>. The row is the flex container two elements down; the
        // wrapper between is role="none", since a row may own nothing but cells.
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-data-list__header");
        builder.AddAttribute(2, "role", "rowgroup");

        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "munin-explorer-data-list__item__row munin-explorer-data-list__item__row--header");
        builder.AddAttribute(5, "role", "none");

        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "class", "munin-explorer-dataitem-header");
        builder.AddAttribute(8, "role", "row");

        // Navn is not in the picker and has no condition here. It is the first column: the chevron
        // lives inside the name button, so there is no control column to name (Fhi.Metadata-35w0p.78).
        HeaderCell(builder, 100, "name", T.ColumnVariable, SortField.Name);

        if (ColumnVisible(ResultColumn.Code))
        {
            HeaderCell(builder, 200, "code", T.FieldCode, SortField.Code);
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
            HeaderCell(builder, 600, "dataType", T.FieldDataType, SortField.DataType);
        }

        if (ColumnVisible(ResultColumn.Status))
        {
            HeaderCell(builder, 700, "status", T.FieldStatus, SortField.Status);
        }

        if (ColumnVisible(ResultColumn.DataPeriod))
        {
            HeaderCell(builder, 800, "period", T.FieldDataPeriod, SortField.DataPeriod);
        }

        // «Valg», as on helsedata's own page and the saved list's action column; the tab's word read as a tab.
        if (ColumnVisible(ResultColumn.SaveToList))
        {
            HeaderCell(builder, 900, "save", T.ColumnActions, null);
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    };

    /// <summary>One header cell, sortable when the column maps to a field the API can order by.</summary>
    private void HeaderCell(RenderTreeBuilder builder, int seq, string? key, string label, SortField? sort)
    {
        builder.OpenElement(seq, "div");
        // Built from the key, so the class inventory cannot see them: munin-explorer-dataitem-header__code,
        // munin-explorer-dataitem-header__status and munin-explorer-dataitem-header__save are named here for it.
        builder.AddAttribute(seq + 1, "class",
            key is null ? "sortable-header" : $"sortable-header munin-explorer-dataitem-header__{key}");

        // columnheader gives the cells below a column to belong to — "kolonne 3 av 7, Kilde" — and
        // is the role aria-sort is allowed on (WCAG 1.3.1).
        builder.AddAttribute(seq + 2, "role", "columnheader");

        if (sort is not { } field)
        {
            Label(builder, seq + 3, label);
            builder.CloseElement();
            return;
        }

        // aria-sort on the cell rather than the button: it describes the COLUMN's state, and it is
        // what a screen reader reads when moving across the header. Only the active column carries
        // it — "none" on every other column is noise a reader has to listen through.
        if (IsActiveSort(field))
        {
            builder.AddAttribute(seq + 5, "aria-sort", AriaSort());
        }

        builder.OpenElement(seq + 6, "button");
        // hd-button-reset is Stiler's own "this is a button but draw nothing" class, which is what
        // their header buttons wear — 12 rules, in the site-wide stylesheet.
        builder.AddAttribute(seq + 7, "class", "hd-button-reset munin-explorer-dataitem-header__button");
        builder.AddAttribute(seq + 8, "type", "button");
        builder.AddAttribute(seq + 9, "aria-current", AriaCurrent(field));
        builder.AddAttribute(seq + 10, "onclick", EventCallback.Factory.Create(this, () => SortAsync(field)));

        // The column's name, not the ordering's: the arrow and aria-sort carry the ordering.
        Label(builder, seq + 11, label);

        if (IsActiveSort(field))
        {
            builder.OpenElement(seq + 14, "span");
            builder.AddAttribute(seq + 15, "aria-hidden", "true");
            builder.AddContent(seq + 16, Ascending ? " \u2191" : " \u2193");
            builder.CloseElement();
        }

        builder.CloseElement();

        builder.CloseElement();
    }

    // Stiler clips a header that outgrows its column, so the label carries itself as a tooltip. On
    // the span: on the sorted column, Edge reads a title on the header or its button as a repeated name.
    private static void Label(RenderTreeBuilder builder, int seq, string label)
    {
        builder.OpenElement(seq, "span");
        builder.AddAttribute(seq + 1, "title", label);
        builder.AddContent(seq + 2, label);
        builder.CloseElement();
    }

    /// <summary>The reader's language as a tag, and the marker for text that is not in it.</summary>
    /// <remarks>
    /// Not <c>ReaderLanguage</c>: a member of that name would shadow the type in every partial.
    /// </remarks>
    private string Reader => ReaderLanguage.Of(Language);

    private string? Foreign(string language) => CatalogueProperties.Foreign(language, Reader);

    /// <summary>Which tab of the open panel is showing.</summary>
    /// <remarks>
    /// Reset to Data whenever a different row opens, so nobody lands on a tab they did not choose
    /// (Fhi.Metadata-l9l2n.101).
    /// </remarks>
    private PanelTab _tab = PanelTab.Data;

    /// <summary>A datatype code as its name, from the facets the filter panel has already loaded.</summary>
    /// <remarks>
    /// Not the row's <c>dataTypeDisplayName</c>, which is in the API's default language. AGENTS.md,
    /// "The API names a datatype, not this package".
    /// </remarks>
    private string? DataTypeName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        // Before the first facets the code would read as the value; after a failed read it is all there is.
        if (_facets is null && _facetError is null)
        {
            return T.DataTypeLoading;
        }

        var canonical = T.CanonicalDataTypeCode(code);
        var named = _facets?.DataTypes.FirstOrDefault(d => d.Value == canonical)?.DisplayName;

        return string.IsNullOrWhiteSpace(named) ? canonical : named;
    }

    /// <summary><see cref="Texts.KildeTypeNameFromApi"/> for the kilde facet's group headings.</summary>
    /// <remarks>
    /// Takes the payload rather than the field, so a heading resolves out of the very object its
    /// facet button did and the two cannot fall back apart (Fhi.Metadata-1b0ag).
    /// </remarks>
    private string KildeTypeNameFromApi(FilterOptions? facets, string? value) =>
        T.KildeTypeNameFromApi(value, FacetKildeTypeName(facets, value));

    /// <summary>The API's own word for a kildetype, out of the facets the filter panel loaded.</summary>
    private static string? FacetKildeTypeName(FilterOptions? facets, string? value) =>
        facets?.KildeTyper
            .FirstOrDefault(type => string.Equals(type.Value, value, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName;

    /// <summary>The drill-in view's heading while it is still empty, named for what was opened.</summary>
    /// <remarks>
    /// Only the placeholder: an arrived payload brings its own heading (Fhi.Metadata-jgfum). It keeps
    /// the id the region is labelled by.
    /// </remarks>
    private RenderFragment DrilldownHeading => builder =>
    {
        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "headline headline-s margin--bottom");
        builder.AddAttribute(2, "id", SourceHeadingId);
        builder.AddContent(3, _sourceKind == SourceKind.Kilde ? T.ShowKilde : T.ShowDatasamling);
        builder.CloseElement();
    };

    // The name the reader pressed, from the row it was on: the detail it opens has not arrived yet.
    private RenderFragment WholeVariableHeading => builder =>
    {
        var row = _result?.Items.FirstOrDefault(v => VariableDatasamlingKey.Of(v) == _selected);
        var named = T.Named(row?.PreferredTerm, row?.Code);

        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "headline headline-s margin--bottom");
        builder.AddAttribute(2, "id", WholeVariableHeadingId);
        builder.AddAttribute(3, "lang", named.Norwegian ? Foreign("no") : null);
        builder.AddContent(4, named.Text);
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

    /// <summary>Whether the Status column shows by default: whether a row could say other than "Active".</summary>
    /// <remarks>
    /// The API filters expired versions out unless IncludeHistorical is set. Once pressed, the
    /// picker overrides this — see <see cref="_statusColumnChosen"/>.
    /// </remarks>
    private bool ShowStatusColumn => _filter.IncludeHistorical;

    /// <summary>The list item's class, carrying helsedata's expanded state.</summary>
    private string RowItemClass(VariableSummary v) =>
        "munin-explorer-data-list__item"
        + (IsSelected(v) ? " munin-explorer-data-list__item--expanded" : "")
        + (ShowsSavedNotice(v) ? " munin-explorer-data-list__item--saved" : "");

    /// <summary>The row's metadata cells, in helsedata's <c>munin-explorer-dataitem-main__column</c> shape.</summary>
    /// <remarks>
    /// Each value is labelled: with columns turned off there may be no header to line up against.
    /// </remarks>
    private RenderFragment InfoLine(VariableSummary v) => builder =>
    {
        // Runa's columns in Runa's order, in helsedata's shape: one div per column, direct children of
        // .munin-explorer-dataitem-main, whose grid lines them up. Each is drawn only while
        // ColumnVisible says so; Kode starts off, as the widest column that buys the least.
        if (ColumnVisible(ResultColumn.Code))
        {
            RowCell.Write(builder, 100, T.FieldCode, v.Code, "code", T.NotSpecified);
        }

        // Runa's short name, with the full name on hover. Trimmed rather than `??`, since an omitted
        // kortnavn is null or "".
        if (ColumnVisible(ResultColumn.Kilde))
        {
            RowCell.Write(builder, 200, T.FieldSource, DisplayText.Trimmed(v.KildeShortName) ?? v.KildeName, "source", T.NotSpecified, tooltip: v.KildeName);
        }

        if (ColumnVisible(ResultColumn.Datasamling))
        {
            RowCell.Write(builder, 300, T.FieldDataCollection, v.DatasamlingName, "dataCollection", T.NotSpecified);
        }

        if (ColumnVisible(ResultColumn.Variabelgruppe))
        {
            RowCell.Write(builder, 400, T.FieldVariableGroup, v.VariabelgruppeName, "theme", T.NotSpecified);
        }

        if (ColumnVisible(ResultColumn.DataType))
        {
            // Unmarked: the API resolves the name in the reader's language (Fhi.Metadata-13xf8).
            RowCell.Write(builder, 500, T.FieldDataType, DataTypeName(v.DataType), "dataType", T.NotSpecified, catalogue: false);
        }

        // Hidden by default unless historical variables can be listed, since otherwise every row
        // reads Active. A reader can still turn it on in the picker.
        if (ColumnVisible(ResultColumn.Status))
        {
            // Translated through the same map the variable page uses, and unmarked for that
            // reason: the label is our own word in the reader's language rather than the
            // catalogue's Norwegian (Fhi.Metadata-hq0b6).
            var status = v.VersionStatus is { } token ? T.VersionStatusLabel(token) : null;
            RowCell.Write(builder, 600, T.FieldStatus, status, "status", T.NotSpecified, catalogue: false);
        }

        // Text, not helsedata's hover-only bar: the package ships no rules to draw a bar with, and
        // an unstyled one is an empty cell, which reads as no period recorded.
        // `catalogue: false`: the dates are formatted for the reader, so follow Language.
        if (ColumnVisible(ResultColumn.DataPeriod))
        {
            RowCell.Write(builder, 700, T.FieldDataPeriod, PeriodText(v.DataFrom, v.DataTo), "period", T.NotSpecified, catalogue: false);
        }
    };

    /// <summary>The dataperiode in one line, or null where the catalogue has neither date.</summary>
    /// <remarks>
    /// The one helper every surface joins a period with (Fhi.Metadata-msax9).
    /// </remarks>
    private string? PeriodText(DateTimeOffset? from, DateTimeOffset? to) =>
        CatalogueDate.Period(from, to, Language, T, DateWidth.Narrow);

    /// <summary>The two tabs, in the order they are drawn.</summary>
    private static readonly ExplorerTab[] ResultTabs = Enum.GetValues<ExplorerTab>();

    private ExplorerTab _resultsTab = ExplorerTab.Search;

    /// <summary>
    /// Whether there is a second tab worth drawing. A signed-out reader has no lists, so the
    /// tablist would name a panel with nothing in it — unless a shared list is being opened.
    /// </summary>
    private bool ShowTabs => VariableList is not null && (IsAuthenticated || ShareCode is not null);

    private string ResultTabId(ExplorerTab tab) => $"munin-explorer-tab-{_instance}-{tab}";

    /// <summary>One id per panel: both are in the DOM at once, so a shared id would be two
    /// elements answering to one <c>aria-controls</c>.</summary>
    private string ResultTabPanelId(ExplorerTab tab) => $"munin-explorer-tabpanel-{_instance}-{tab}";

    private string ResultTabLabel(ExplorerTab tab) => tab switch
    {
        ExplorerTab.Search => T.TabSearchResults,
        ExplorerTab.VariableList => T.TabVariableList,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "No label for this tab."),
    };

    private string ResultTabClass(ExplorerTab tab) =>
        tab == _resultsTab
            ? "munin-explorer-meta__tab munin-explorer-meta__tab--active"
            : "munin-explorer-meta__tab";

    /// <summary>
    /// Arrow-key movement, as the APG tabs pattern prescribes: the unselected tab carries
    /// <c>tabindex="-1"</c>, so without this it is unreachable rather than merely awkward.
    /// </summary>
    private void ResultTabKey(KeyboardEventArgs e)
    {
        var i = Array.IndexOf(ResultTabs, _resultsTab);

        var next = e.Key switch
        {
            "ArrowRight" or "ArrowDown" => (i + 1) % ResultTabs.Length,
            "ArrowLeft" or "ArrowUp" => (i - 1 + ResultTabs.Length) % ResultTabs.Length,
            "Home" => 0,
            "End" => ResultTabs.Length - 1,
            _ => i,
        };

        _resultsTab = ResultTabs[next];
    }
}
