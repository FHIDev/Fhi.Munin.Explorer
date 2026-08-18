using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

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
/// <c>button-square--ghost</c>, <c>margin-right</c> and <c>margin-bottom</c>, <c>headline</c>,
/// <c>caption</c>, <c>infobox</c> and <c>datasourcecard*</c> — the last of these is the same card
/// list helsedata's own datakildeutforsker renders its results with.
/// </para>
/// <para>
/// The pager is the exception, and it is worth spelling out because it is a dependency rather
/// than an oversight. Stiler defines no pagination rule at all — its compiled stylesheet has no
/// <c>pagination</c>, <c>pager</c>, <c>paging</c>, <c>page-link</c> or <c>page-item</c> — while
/// helsedata's own variable page styles its pager from a page-specific <c>variables.css</c> that
/// is not part of the site-wide stylesheet. The markup therefore emits *their* names,
/// <c>variables-pagination</c>, <c>variables-pagination-content</c> and
/// <c>skiplink-pagination</c>, so that mounting the explorer on that page needs nothing new. A
/// host mounting it anywhere else has to supply those three itself — including the rule that
/// keeps <c>skiplink-pagination</c> out of sight until it is focused, which is the whole point
/// of a skip link. Where the component is mounted is not settled yet, so this is a known cost of
/// wearing helsedata's clothes rather than inventing our own.
/// </para>
/// <para>
/// Two names from that stylesheet are deliberately left unused. <c>variables-pagination-mobile</c>
/// is a second copy of the controls that helsedata's own media queries swap in; rendering it too
/// would put two "Neste" buttons for one list in the tab order and in the accessibility tree, so
/// this renders the one pager at every width. The <c>__expired</c> modifiers describe a state
/// this component does not have — it never lists expired variables — and a modifier whose meaning
/// cannot be read back off the stylesheet is exactly the guess this package exists to avoid.
/// </para>
/// <para>
/// The filter panel adds no class name to that list. Stiler has no accordion, no tree and no
/// checkbox whose names can be read back off its compiled stylesheet — and helsedata's own
/// sidebar is styled from <c>filter-search-explorer</c> in the same page-specific
/// <c>variables.css</c> the pager's names come from, which is not a stylesheet this repository
/// can read. So the panel is <c>&lt;details&gt;</c> for the disclosure, a nested
/// <c>&lt;ul&gt;</c> for the kilde/delkilde hierarchy and the square button in its two states for
/// the values, and what a host supplies is base styling for those three elements rather than
/// three more names. List indentation is the part that matters: without it the hierarchy still
/// nests in the accessibility tree but reads flat on screen. <c>variable-explorer-filters</c> is
/// a DOM handle for placing the panel, and carries no styling, exactly like the
/// <c>variable-explorer</c> root.
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
    /// Set this to match the host page's own <c>lang</c>. The component deliberately does
    /// not put a <c>lang</c> on its root: the UI strings follow this parameter, but the
    /// variable names and descriptions coming from Munin are Norwegian either way, and the
    /// result rows are marked as Norwegian for exactly that reason.
    /// </remarks>
    [Parameter] public string Language { get; set; } = "no";

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

    /// <summary>
    /// The orders offered for sorting, in the order the buttons appear.
    /// </summary>
    /// <remarks>
    /// Every member of <see cref="SortField"/>, in declaration order, rather than a list restating
    /// it: that enum is already the closed set of orders the API implements, and its own remarks are
    /// where the reason a field is missing from it is written down. Two copies of a list and of its
    /// reason drift apart independently — a member added there would otherwise leave the button row
    /// silently short.
    /// </remarks>
    private static readonly SortField[] Sortable = Enum.GetValues<SortField>();

    // The search text the visible result actually came from, which is not the same as the
    // text in the box: @bind writes _search on blur, so the box can hold an unsubmitted query
    // while the table below still shows the previous one. The announcement has to describe
    // what is on screen.
    private string? _executedSearch;

    // Unique per instance so two explorers on one page cannot collide on DOM ids,
    // which would be a WCAG 4.1.1 failure as well as breaking label association.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    private string SearchId => $"variable-explorer-search-{_instance}";
    private string TitleId => $"variable-explorer-title-{_instance}";
    private string PaginationId => $"variable-explorer-pagination-{_instance}";

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

    /// <summary>A sort button's label — the field, plus the direction when it is the active one.</summary>
    private string ButtonText(SortField sort) =>
        sort == _sort ? T.ActiveLabel(T.FieldLabel(sort), T.DirectionName(_direction)) : T.FieldLabel(sort);

    /// <summary>
    /// A sort button's classes. The active field is filled, the rest are ghosts; the trailing
    /// margins are Stiler's own modifiers, which the buttons need because nothing else separates
    /// them — Razor drops the whitespace between elements.
    /// </summary>
    private string ButtonClass(SortField sort)
    {
        var style = sort == _sort ? "button-square--secondary" : "button-square--ghost";

        return $"hd-button-square {style} margin-right margin-bottom";
    }

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
    /// A result card's heading — the variable's display name, at <see cref="RowLevel"/>.
    /// </summary>
    /// <remarks>
    /// Giving every result a real heading is what lets a screen-reader user move between
    /// results with the heading rotor, which the table this replaced offered no equivalent of.
    /// The size comes from <c>datasourcecard__heading</c>, so it stays card-sized whatever
    /// level the element ends up being.
    /// </remarks>
    private RenderFragment RowHeading(VariableSummary v) => builder =>
    {
        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "datasourcecard__heading");
        builder.AddAttribute(2, "lang", "no");
        builder.AddContent(3, v.PreferredTerm);
        builder.CloseElement();
    };

    /// <summary>
    /// The card's metadata line: code, source, data collection and period, in Stiler's
    /// <c>datasourcecard__info</c> shape.
    /// </summary>
    /// <remarks>
    /// Each value is labelled. helsedata's datakildeutforsker runs its values together
    /// unlabelled because there are only two of them and they are self-evident; ours are four,
    /// and once the column headers of a table are gone "Inklusjon" on its own says nothing
    /// about which field it is.
    /// </remarks>
    private RenderFragment InfoLine(VariableSummary v) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "datasourcecard__info");

        // Fixed, spread-out sequence numbers: each Field call writes its own contiguous block,
        // so the renderer's diff sees a stable tree across renders.
        Field(builder, 100, T.FieldCode, v.Code, first: true);
        Field(builder, 200, T.FieldSource, v.KildeName, first: false);
        Field(builder, 300, T.FieldDataCollection, v.DatasamlingName, first: false);
        Field(builder, 400, T.FieldPeriod, Period(v), first: false);

        builder.CloseElement();
    };

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
        builder.AddAttribute(seq + 1, "class", "datasourcecard__info--text");

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

    // ---------------------------------------------------------------------------- the filter panel

    /// <summary>One facet, as the panel draws it: a disclosure holding a list of values.</summary>
    /// <remarks>
    /// <c>Key</c> is stable across renders, so the disclosure's open state stays with its own facet.
    /// <c>EmptyText</c> is what to say when the facet has no values; null means the facet is left out
    /// instead, which is the right answer for most of them, because a facet the API returned nothing
    /// for is one there is nothing to choose from. Variabelgruppe is the exception: its emptiness is
    /// a message.
    /// </remarks>
    private sealed record FacetGroup(
        string Key,
        string Label,
        bool OpenByDefault,
        IReadOnlyList<FacetValue> Values,
        string? EmptyText = null)
    {
        /// <summary>How many values in this facet are selected, counting nested ones.</summary>
        public int SelectedCount => Selected(Values);

        private static int Selected(IReadOnlyList<FacetValue> values) =>
            values.Sum(value => (value.Selected ? 1 : 0) + Selected(value.Children));
    }

    /// <summary>
    /// One value inside a facet, and the values nested under it.
    /// </summary>
    /// <remarks>
    /// <c>Count</c> is how many variables the value would leave, or null where there is no count to
    /// show. <c>Toggle</c> is what pressing it does, or null for a value that is not selectable —
    /// the kildetype headings the kilder are grouped under are labels rather than filters, because
    /// kildetype has a facet of its own.
    /// </remarks>
    private sealed record FacetValue(
        string Key,
        string Label,
        int? Count,
        bool Selected,
        Func<Task>? Toggle,
        IReadOnlyList<FacetValue> Children);

    /// <summary>A node on the way to becoming a <see cref="FacetValue"/> tree.</summary>
    /// <remarks>
    /// The delkilde, variabelgruppe and saved-filter facets all arrive as a flat list carrying a
    /// parent id, and all three become a tree the same way. This is the shape <see cref="Tree"/>
    /// works in so that rule lives in one place.
    /// </remarks>
    private sealed record TreeNode(Guid Id, Guid? ParentId, string Label, int Count);

    /// <summary>The facets on screen, in the order they are drawn.</summary>
    /// <remarks>
    /// Built from the last answer rather than cached, so a facet's selected state and its count can
    /// never describe two different moments. It is a few hundred records per render, which is the
    /// same order as the rows the component already renders.
    /// </remarks>
    private IReadOnlyList<FacetGroup> FacetGroups
    {
        get
        {
            if (_facets is not { } facets)
            {
                return [];
            }

            // Kildetype first and kilde second, which is the order helsedata's own variable page
            // puts them in; the rest follow Munin's explorer.
            List<FacetGroup> groups =
            [
                KildeTypeGroup(facets),
                KildeGroup(facets),
                VariabelgruppeGroup(facets),
                SavedFilterGroup(facets),
                DataTypeGroup(facets),
                HelsefagligKodeverkGroup(facets),
                AdministrativtKodeverkGroup(facets),
                InstrumentGroup(facets),
                OtherGroup(facets)
            ];

            // A facet the API returned nothing for is left out rather than drawn as an empty
            // disclosure — except where the emptiness is itself the message.
            return [.. groups.Where(group => group.Values.Count > 0 || group.EmptyText is not null)];
        }
    }

    /// <summary>The kildetype facet — one value each, and only one of them can be chosen.</summary>
    private FacetGroup KildeTypeGroup(FilterOptions facets) =>
        new("kildetype", T.FacetKildeType, OpenByDefault: true, [.. facets.KildeTyper.Select(KildeTypeValue)]);

    private FacetValue KildeTypeValue(KildetypeFacet type) =>
        new($"kildetype:{type.Value}",
            // The facet's own displayName is the raw enum name (SentraltHelseregister), so the
            // prose comes from the component's own translations and falls back to what the API said.
            T.KildeTypeLabel(type.Value, type.DisplayName),
            type.Count,
            string.Equals(_filter.KildeType, type.Value, StringComparison.OrdinalIgnoreCase),
            () => SetKildeTypeAsync(type.Value),
            []);

    /// <summary>
    /// The kilde facet: kilder grouped under their kildetype, each with its own delkilde tree.
    /// </summary>
    /// <remarks>
    /// The whole tree is built from the facet payload alone — <see cref="DelkildeFacet"/> carries
    /// both its parent delkilde and its kilde precisely so this needs no second request. The level
    /// below it, datasamling, is not in that payload at all and is therefore not drawn; reaching it
    /// would mean a hierarchy request per kilde whose counts are the kilde's own totals rather than
    /// counts cross-filtered against the current selection, which would put two kinds of number in
    /// one tree. <see cref="VariableFilter.DatasamlingIds"/> still filters when a host sets it.
    /// </remarks>
    private FacetGroup KildeGroup(FilterOptions facets)
    {
        var delkilderByKilde = facets.Delkilder.ToLookup(delkilde => delkilde.KildeId);

        // The order the kildetype facet is in, so the headings here and the facet above agree.
        var kildeTypeOrder = facets.KildeTyper
            .Select((type, index) => (type.Value, Index: index))
            .ToDictionary(entry => entry.Value, entry => entry.Index, StringComparer.OrdinalIgnoreCase);

        var grouped = facets.Kilder
            .GroupBy(KildeTypeKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => kildeTypeOrder.TryGetValue(group.Key, out var index) ? index : int.MaxValue)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => KildeTypeHeading(group, delkilderByKilde))
            .ToList();

        // With one kildetype in the list its heading says nothing the facet above does not — and it
        // is exactly one whenever a kildetype has been chosen, which is when the panel is most
        // crowded. So the kilder are lifted out of it.
        if (grouped.Count == 1)
        {
            return new FacetGroup("kilde", T.FieldSource, OpenByDefault: true, grouped[0].Children);
        }

        return new FacetGroup("kilde", T.FieldSource, OpenByDefault: true, grouped);
    }

    /// <summary>A kilde's kildetype, or the empty string when it has none — never null, so it can be a key.</summary>
    private static string KildeTypeKey(KildeFacet kilde) =>
        string.IsNullOrWhiteSpace(kilde.KildeType) ? "" : kilde.KildeType;

    /// <summary>A kildetype heading: a label rather than a filter, because kildetype has its own facet.</summary>
    private FacetValue KildeTypeHeading(
        IGrouping<string, KildeFacet> kilder,
        ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        new($"kildetype-group:{kilder.Key}",
            T.KildeTypeLabel(kilder.Key, kilder.Key),
            Count: null,
            Selected: false,
            Toggle: null,
            [.. kilder.Select(kilde => KildeValue(kilde, delkilderByKilde))]);

    private FacetValue KildeValue(KildeFacet kilde, ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        new($"kilde:{kilde.Id}",
            kilde.Name,
            kilde.Count,
            _filter.KildeIds.Contains(kilde.Id),
            () => ToggleAsync(_filter.KildeIds, kilde.Id, ids => _filter with { KildeIds = ids }),
            DelkildeChildren(kilde.Id, delkilderByKilde));

    private IReadOnlyList<FacetValue> DelkildeChildren(Guid kildeId, ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        Tree(delkilderByKilde[kildeId].Select(d => new TreeNode(d.Id, d.ParentDelkildeId, d.Name, d.Count)),
             "delkilde:",
             IsDelkildeChosen,
             ToggleDelkilde);

    private bool IsDelkildeChosen(Guid id) => _filter.DelkildeIds.Contains(id);

    private Func<Task> ToggleDelkilde(Guid id) =>
        () => ToggleAsync(_filter.DelkildeIds, id, ids => _filter with { DelkildeIds = ids });

    /// <summary>
    /// The variabelgruppe facet, as a tree.
    /// </summary>
    /// <remarks>
    /// Its empty state is a message rather than an omission. With nothing chosen in the source
    /// hierarchy the API answers this facet with a curated shortlist — the whole catalogue is 930
    /// per-kilde groups and useless as a starting point — and that shortlist is empty in every
    /// environment probed so far. Saying "pick a datakilde" is what stops an empty list from
    /// reading as a broken one.
    /// </remarks>
    private FacetGroup VariabelgruppeGroup(FilterOptions facets) =>
        new("variabelgruppe",
            T.FieldVariableGroup,
            OpenByDefault: false,
            Tree(facets.Variabelgrupper.Select(g => new TreeNode(g.Id, g.ParentId, g.Name, g.Count)),
                 "variabelgruppe:",
                 IsGruppeChosen,
                 ToggleGruppe),
            T.NoVariabelgrupper);

    private bool IsGruppeChosen(Guid id) => _filter.VariabelgruppeIds.Contains(id);

    private Func<Task> ToggleGruppe(Guid id) =>
        () => ToggleAsync(_filter.VariabelgruppeIds, id, ids => _filter with { VariabelgruppeIds = ids });

    /// <summary>The saved catalogue filters — see <see cref="FilterOptions.Filters"/> for why this is usually empty.</summary>
    private FacetGroup SavedFilterGroup(FilterOptions facets) =>
        new("filter",
            T.FacetFilter,
            OpenByDefault: false,
            Tree(facets.Filters.Select(f => new TreeNode(f.Id, f.ParentId, f.Name, f.Count)),
                 "filter:",
                 IsSavedFilterChosen,
                 ToggleSavedFilter));

    private bool IsSavedFilterChosen(Guid id) => _filter.FilterIds.Contains(id);

    private Func<Task> ToggleSavedFilter(Guid id) =>
        () => ToggleAsync(_filter.FilterIds, id, ids => _filter with { FilterIds = ids });

    private FacetGroup DataTypeGroup(FilterOptions facets) =>
        new("datatype", T.FacetDataType, OpenByDefault: false, [.. facets.DataTypes.Select(DataTypeValue)]);

    private FacetValue DataTypeValue(DataTypeFacet dataType) =>
        new($"datatype:{dataType.Value}",
            // The API returns the code with no label at all, so the prose is the component's own.
            T.DataTypeLabel(dataType.Value),
            dataType.Count,
            _filter.DataTypes.Contains(dataType.Value),
            () => ToggleAsync(_filter.DataTypes, dataType.Value, values => _filter with { DataTypes = values }),
            []);

    private FacetGroup HelsefagligKodeverkGroup(FilterOptions facets) =>
        new("helsefaglig-kodeverk",
            T.FacetHelsefagligKodeverk,
            OpenByDefault: false,
            [.. facets.HelsefagligKodeverk.Select(HelsefagligKodeverkValue)]);

    private FacetValue HelsefagligKodeverkValue(HelsefagligKodeverkFacet kodeverk) =>
        new($"hk:{kodeverk.ShortName}",
            kodeverk.ShortName,
            kodeverk.Count,
            _filter.HelsefagligKodeverk.Contains(kodeverk.ShortName),
            () => ToggleAsync(_filter.HelsefagligKodeverk, kodeverk.ShortName,
                              values => _filter with { HelsefagligKodeverk = values }),
            []);

    private FacetGroup AdministrativtKodeverkGroup(FilterOptions facets) =>
        new("administrativt-kodeverk",
            T.FacetAdministrativtKodeverk,
            OpenByDefault: false,
            [.. facets.AdministrativtKodeverk.Select(AdministrativtKodeverkValue)]);

    private FacetValue AdministrativtKodeverkValue(AdministrativtKodeverkFacet kodeverk) =>
        new($"ak:{kodeverk.Oid}",
            // The OID when fhi.kodeverk could not be reached, because a nameless button is worse
            // than one labelled with the number the filter actually sends.
            string.IsNullOrWhiteSpace(kodeverk.Name) ? kodeverk.Oid : kodeverk.Name,
            kodeverk.Count,
            _filter.AdministrativtKodeverk.Contains(kodeverk.Oid),
            () => ToggleAsync(_filter.AdministrativtKodeverk, kodeverk.Oid,
                              values => _filter with { AdministrativtKodeverk = values }),
            []);

    private FacetGroup InstrumentGroup(FilterOptions facets) =>
        new("instrument", T.FacetInstrument, OpenByDefault: false, [.. facets.Instruments.Select(InstrumentValue)]);

    private FacetValue InstrumentValue(InstrumentFacet instrument) =>
        new($"instrument:{instrument.Id}",
            string.IsNullOrWhiteSpace(instrument.Name) ? instrument.Code : instrument.Name,
            instrument.Count,
            _filter.InstrumentIds.Contains(instrument.Id),
            () => ToggleAsync(_filter.InstrumentIds, instrument.Id, ids => _filter with { InstrumentIds = ids }),
            []);

    /// <summary>The two filters that are a yes/no rather than a choice of values.</summary>
    private FacetGroup OtherGroup(FilterOptions facets) =>
        new("other",
            T.FacetOther,
            OpenByDefault: false,
            [
                new FacetValue("has-kildekodeverk", T.HasKildekodeverk, facets.KildeKodeverkCount,
                               _filter.HasKildekodeverk == true, ToggleKildekodeverkAsync, []),

                // No count of its own: the API reports no facet for it, and the number it would
                // change is the total, which the status line already states.
                new FacetValue("include-historical", T.IncludeHistorical, null,
                               _filter.IncludeHistorical, ToggleHistoricalAsync, [])
            ]);

    /// <summary>
    /// Turn a flat list of parented nodes into the tree the panel draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A node whose parent is not in the list is treated as a root rather than dropped. That is not
    /// a defensive flourish: the API cross-filters each facet, so a parent with no matching
    /// variables of its own is genuinely absent from a payload its children are in, and a child
    /// hung off a missing parent would be a filter the reader can neither see nor clear.
    /// </para>
    /// <para>
    /// A parent chain that loops back on itself — a self-parented node, or two nodes naming each
    /// other, neither of which the catalogue should ever produce — has no root to be reached from,
    /// so the walk seeds itself with whatever the first pass did not reach. Without that second
    /// pass a cycle and everything hanging off it vanishes from the panel silently, which is the
    /// same failure the orphan rule above exists to prevent, arriving by the other door. The walk
    /// remembers what it has already placed, so entering a cycle stops at the repeat rather than
    /// recursing until the stack runs out; that memory also keeps a duplicated id from being drawn
    /// twice.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<FacetValue> Tree(
        IEnumerable<TreeNode> nodes,
        string keyPrefix,
        Func<Guid, bool> selected,
        Func<Guid, Func<Task>> toggle)
    {
        var all = nodes.ToList();

        if (all.Count == 0)
        {
            return [];
        }

        var known = all.Select(node => node.Id).ToHashSet();
        var byParent = all.Where(node => node.ParentId is not null).ToLookup(node => node.ParentId!.Value);
        HashSet<Guid> placed = [];

        var rooted = all.Where(node => node.ParentId is not { } parent || !known.Contains(parent));

        List<FacetValue> roots = [.. rooted.Select(Build)];

        // Whatever the first pass could not reach: every member of a cycle has its parent present,
        // so none of them is a root, and dropping them would take a filter off the panel with no
        // error anywhere. Each one that is still unplaced becomes a root of its own, which places
        // the rest of its cycle underneath it.
        roots.AddRange(all.Where(node => !placed.Contains(node.Id)).Select(Build));

        return roots;

        FacetValue Build(TreeNode node)
        {
            placed.Add(node.Id);

            var children = byParent[node.Id]
                .Where(child => !placed.Contains(child.Id))
                .Select(Build)
                .ToList();

            return new FacetValue($"{keyPrefix}{node.Id}", node.Label, node.Count, selected(node.Id), toggle(node.Id), children);
        }
    }

    /// <summary>The legend over the whole panel, saying how many filters are in force.</summary>
    private string FiltersLegend => _filter.IsEmpty ? T.FiltersTitle : $"{T.FiltersTitle} ({_filter.ActiveCount})";

    /// <summary>A facet's own label, saying how many of its values are chosen.</summary>
    /// <remarks>
    /// On the summary line, so a collapsed facet still says that something inside it is narrowing
    /// the list. Without it the only sign of a filter chosen three disclosures down is the number of
    /// results changing.
    /// </remarks>
    private static string GroupLabel(FacetGroup group) =>
        group.SelectedCount == 0 ? group.Label : $"{group.Label} ({group.SelectedCount})";

    /// <summary>
    /// A facet's values as a nested list of toggle buttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain <c>&lt;ul&gt;</c> with no class of its own, and buttons rather than checkboxes. Both
    /// follow the rule the rest of this component follows: no class name goes into the markup that
    /// cannot be read back off the host's stylesheet, and where there is nothing to read back the
    /// shape changes rather than a stylesheet appearing. Stiler has a square button and this
    /// component already renders one in two states, so a chosen value is a pressed button; a list
    /// is an element every base stylesheet styles, and its indentation is what draws the hierarchy
    /// without a class for a tree that nobody has verified.
    /// </para>
    /// <para>
    /// Every value is keyed. Counts move as the reader filters, so the values reorder between
    /// renders, and without keys the renderer would patch the button under the reader's finger into
    /// a different filter — leaving focus on a control that is no longer the one they pressed.
    /// </para>
    /// </remarks>
    private RenderFragment FacetList(IReadOnlyList<FacetValue> values) => builder =>
    {
        builder.OpenElement(0, "ul");

        foreach (var value in values)
        {
            builder.OpenElement(1, "li");
            builder.SetKey(value.Key);

            // Held in a local so the null check below is one the compiler can carry into the branch.
            var toggle = value.Toggle;

            if (toggle is null)
            {
                builder.AddContent(2, value.Label);
            }
            else
            {
                builder.OpenElement(3, "button");
                builder.AddAttribute(4, "class", FacetClass(value));
                builder.AddAttribute(5, "type", "button");

                // aria-pressed, and spelled out as "false" on the values that are not chosen —
                // unlike the sort buttons' aria-current, which is left off. The attribute is what
                // says these are toggles at all, so an unselected one carrying nothing would be
                // announced as an ordinary button that gives no sign of having two states.
                builder.AddAttribute(6, "aria-pressed", value.Selected ? "true" : "false");
                builder.AddAttribute(7, "onclick", EventCallback.Factory.Create(this, toggle));
                builder.AddContent(8, FacetText(value));
                builder.CloseElement();
            }

            if (value.Children.Count > 0)
            {
                builder.AddContent(9, FacetList(value.Children));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>A value's visible text — its label, and the count of what it would leave.</summary>
    /// <remarks>
    /// The count is in the button's own text rather than in a badge beside it, so it is part of the
    /// accessible name: "Dødsårsaksregisteret (1 234)" is announced whole, where a separate element
    /// would be read as a stray number or skipped.
    /// </remarks>
    private static string FacetText(FacetValue value) =>
        value.Count is { } count ? $"{value.Label} ({count})" : value.Label;

    /// <summary>A value's classes — filled when chosen, a ghost when not, the same pair the sort buttons use.</summary>
    private static string FacetClass(FacetValue value)
    {
        var style = value.Selected ? "button-square--secondary" : "button-square--ghost";

        return $"hd-button-square {style} margin-right margin-bottom";
    }

    /// <summary>Add or remove one value from a facet, and fetch what that leaves.</summary>
    /// <remarks>
    /// The type parameter is <c>TItem</c> and not <c>T</c>, which is the component's own
    /// translations accessor: a <c>T</c> here would shadow it, and the first string this body ever
    /// needs would fail to compile with an error pointing at the type parameter instead.
    /// </remarks>
    private Task ToggleAsync<TItem>(
        IReadOnlyList<TItem> selected, TItem value, Func<IReadOnlyList<TItem>, VariableFilter> apply)
    {
        if (selected.Contains(value))
        {
            return ApplyFilterAsync(
                apply([.. selected.Where(chosen => !EqualityComparer<TItem>.Default.Equals(chosen, value))]));
        }

        return ApplyFilterAsync(apply([.. selected, value]));
    }

    /// <summary>
    /// Choose a kildetype, or clear it by choosing the one already chosen.
    /// </summary>
    /// <remarks>
    /// One at a time, because the API takes one. Pressing the chosen one again clears it, which is
    /// what the button's own aria-pressed promises — a radio group would say the choice cannot be
    /// undone, and there is no "any kildetype" value to go back to.
    /// </remarks>
    private Task SetKildeTypeAsync(string value)
    {
        var chosen = string.Equals(_filter.KildeType, value, StringComparison.OrdinalIgnoreCase);

        return ApplyFilterAsync(_filter with { KildeType = chosen ? null : value });
    }

    /// <summary>
    /// Keep only variables that have a kildekodeverk link, or stop filtering on it.
    /// </summary>
    /// <remarks>
    /// Two states, not three. The API's <c>false</c> — only variables *without* one — is a question
    /// nobody asked of a catalogue browser, and offering it from one button would make a single
    /// press mean "yes", "no" or "either depending on where you are in the cycle".
    /// </remarks>
    private Task ToggleKildekodeverkAsync() =>
        ApplyFilterAsync(_filter with { HasKildekodeverk = _filter.HasKildekodeverk == true ? null : true });

    private Task ToggleHistoricalAsync() =>
        ApplyFilterAsync(_filter with { IncludeHistorical = !_filter.IncludeHistorical });

    /// <summary>Drop every filter and fetch the whole search again.</summary>
    /// <remarks>
    /// Always on screen, and inert rather than absent when there is nothing to clear — the same
    /// treatment the pager's buttons get, and for the same reason: taking the control the reader
    /// just pressed out of the document drops focus to <c>&lt;body&gt;</c>. Pressing it with no
    /// filters set asks for the filter already in force, which <see cref="ApplyFilterAsync"/>
    /// returns from without a request.
    /// </remarks>
    private Task ClearFiltersAsync() => ApplyFilterAsync(VariableFilter.None);

    /// <summary>
    /// Apply <paramref name="next"/>: fetch what it leaves, and refresh the counts beside it.
    /// </summary>
    /// <remarks>
    /// The one way the filter ever changes, so the rules that go with changing it — back to page
    /// one, roll back a fetch that failed, tell the host what is actually in force — are written
    /// once rather than once per facet.
    /// </remarks>
    private async Task ApplyFilterAsync(VariableFilter next)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit, a
        // sort click and a page turn.
        if (_loading)
        {
            return;
        }

        // Also what makes the clear button inert when there is nothing to clear. VariableFilter
        // compares by what it narrows, not by the identity of its lists — see the note on it.
        if (next == _filter)
        {
            return;
        }

        var previous = _filter;

        _filter = next;

        // Narrowing renumbers every page, so the page the reader is on is no longer the same rows.
        _page = 1;
        _keepPager = false;

        // _executedSearch, not _search: a click blurs the search field first, so the box's contents
        // have already been written to _search — text the reader may never have submitted. Same
        // reason the sort buttons fetch with it.
        if (await FetchAsync(_executedSearch))
        {
            // Only on success. The counts describe a selection, and after a rollback the selection
            // they already describe is the one back in force.
            await FetchFacetsAsync();
        }
        else
        {
            // The rows on screen are still the old ones, so the buttons have to say so — the same
            // invariant the sort rollback protects.
            _filter = previous;
        }

        // _filter and not next: what the host is told is what is in force, rolled back or not.
        await RaiseAsync(FilterChanged, _filter);
    }

    /// <summary>
    /// Refresh the facets and their counts for the current search and filter.
    /// </summary>
    /// <remarks>
    /// Its own request, and its own failure. The counts are cross-filtered against the whole
    /// selection, so they move whenever the search or the filter does — but not when the page or
    /// the ordering does, which is why turning a page does not re-ask for them.
    /// <para>
    /// A failure keeps the facets already on screen rather than clearing them. They are the controls
    /// the reader is using, and the numbers being briefly stale is a far smaller problem than the
    /// panel emptying under a press.
    /// </para>
    /// </remarks>
    private async Task FetchFacetsAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            _facets = await Client.GetFiltersAsync(_executedSearch, _filter);
            _facetError = null;
        }
        catch (Exception)
        {
            _facetError = T.FilterError;
        }
        finally
        {
            _loading = false;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _search = Search;
        _filter = Filter ?? VariableFilter.None;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        // Nothing disables the submit button while a search runs — see the comment on it in
        // the markup — so a second submit is dropped here instead.
        if (_loading)
        {
            return;
        }

        // A different search is a different result set; page 7 of the old one means nothing in it.
        _page = 1;
        _keepPager = false;

        // The live contents of the box, which is what submitting means.
        if (await FetchAsync(_search))
        {
            // The counts are cross-filtered against the search as well as the filter, so a new
            // search moves them; only on success, so a failed search leaves the numbers describing
            // the rows that are still on screen.
            await FetchFacetsAsync();
        }

        await NotifySearchChangedAsync();
    }

    /// <summary>
    /// Sort by <paramref name="sort"/>: the active field again reverses the direction, another
    /// field starts ascending. Runa's rule, moved off the column header it used to live on.
    /// </summary>
    private async Task SortAsync(SortField sort)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit. The
        // guard comes first on purpose: changing the state and then not fetching would leave a
        // button saying the list is ordered one way while it is still ordered the other.
        if (_loading)
        {
            return;
        }

        // Kept so a failed fetch can put them back — see below.
        var previousSort = _sort;
        var previousDirection = _direction;

        if (sort == _sort)
        {
            _direction = _direction == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _sort = sort;
            _direction = SortDirection.Ascending;
        }

        // Reordering renumbers every page, so the page the user is on is no longer the same rows.
        _page = 1;
        _keepPager = false;

        // _executedSearch, not _search. Sorting is not searching: a click blurs the field first, so
        // by the time this runs the box's contents have already been written to _search — text the
        // user may never have submitted. Fetching with it would run a search nobody asked for,
        // quietly, under a status line that then described the accidental search instead of saying
        // anything moved. It would also desynchronise the host, whose URL only follows SearchChanged.
        if (!await FetchAsync(_executedSearch))
        {
            // The same invariant the _loading guard above protects, on the path that guard cannot
            // see: the list is still in the old order, so the buttons have to say so. Left moved,
            // they would claim an order the API never delivered — and pressing the same button
            // again would take the reversal branch and ask for descending, with no way back to the
            // ascending fetch that just failed short of cycling twice.
            _sort = previousSort;
            _direction = previousDirection;
        }
    }

    /// <summary>
    /// Show page <paramref name="page"/> of the current result, keeping the search and the order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one way the page number ever changes, which is what the pager's two buttons, the clamp
    /// and a future URL-backed page all go through. Both buttons hand it an out-of-range number at
    /// the ends of the list rather than being guarded at the call site, so the boundary is enforced
    /// once, here, instead of once per caller.
    /// </para>
    /// <para>
    /// Not a search, so <see cref="SearchChanged"/> is not raised: the host's URL follows what was
    /// searched for, and turning a page did not change that.
    /// </para>
    /// </remarks>
    private async Task GoToPageAsync(int page)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit and a
        // sort click — and for the same reason the buttons carry aria-disabled instead of disabled:
        // neither is taken out of the document under the finger that pressed it, which is also why
        // a failed page turn below keeps the rows it already had.
        if (_loading)
        {
            return;
        }

        var target = Math.Clamp(page, 1, TotalPages);

        // Also the whole of what makes a click on an unavailable button inert: at either end the
        // clamped target is the page already on screen.
        if (target == _page)
        {
            return;
        }

        // Both kept so a failed fetch can put them back. The result as well as the number, because
        // the retreat below turns a second page and has to be able to undo both of them together.
        var previous = _page;
        var previousResult = _result;

        // A pager button was pressed, so the pager stays until a search or a sort replaces the
        // result — including through a retreat that lands on a single-page answer.
        _keepPager = true;

        _page = target;

        // keepResult: the pressed button must survive the failure. The rest of the component
        // never removes a control the user just used, and the pager is the only pressable thing in
        // it that is rendered conditionally — so a page turn that cleared the rows would take
        // Forrige and Neste out of the document in the same render that reports the error, drop
        // focus to <body>, and leave a keyboard user restarting from the top of the host's page.
        if (!await FetchAsync(_executedSearch, keepResult: true))
        {
            // Nothing arrived, so the state has to keep describing what did — and what did is
            // still on screen. Same invariant the sort rollback protects.
            _page = previous;

            return;
        }

        await RetreatFromEmptyPageAsync(previous, previousResult);
    }

    /// <summary>
    /// Step back to a page that has rows, when the page just fetched turned out not to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clamp in <see cref="GoToPageAsync"/> measures the target against the count the
    /// <em>previous</em> answer carried, so it can only ever ask for a page that existed when that
    /// answer was written. Two routes lead past it: the index shrinks between the two requests, and
    /// the API answers an out-of-range page with 404 — which
    /// <see cref="IMuninExplorerClient.SearchVariablesAsync"/> reports as an empty page rather than
    /// throwing, so no rollback runs.
    /// </para>
    /// <para>
    /// Left alone, either one strands the reader: the status line would say "Ingen variabler passet
    /// søket" over a search that matched hundreds, with no rows to show and nothing but a fresh
    /// search to get back from. So the component takes itself back to a page that exists — the last
    /// one the new answer admits to, or page 1, which is the one page that can never be out of
    /// range. One step only: a second empty answer is not retreated from again, so the reader is
    /// left on that page with the pager still under their finger rather than walking backwards
    /// through the result a page at a time.
    /// </para>
    /// <para>
    /// And its own fetch is checked like every other one. <paramref name="previous"/> and
    /// <paramref name="previousResult"/> are the page turn's starting point — a page that had rows
    /// on it — so a retreat that fails puts the reader back where they pressed the button instead
    /// of leaving <c>_page</c> naming one page while the empty answer for another is still on
    /// screen. That pairing is what would otherwise report "Ingen variabler passet søket" over a
    /// search that matched hundreds and take the pager with it, which is the exact state this
    /// method exists to prevent.
    /// </para>
    /// </remarks>
    private async Task RetreatFromEmptyPageAsync(int previous, Page<VariableSummary>? previousResult)
    {
        if (_page == 1 || _result is not { Items.Count: 0 })
        {
            return;
        }

        // TotalPages reads the answer that just arrived, so this is the new count and not the stale
        // one the clamp trusted. A server still claiming the page exists after sending nothing has
        // told us nothing usable, so page 1 is the only safe answer left.
        var last = TotalCount > 0 ? TotalPages : 1;
        _page = last < _page ? last : 1;

        if (await FetchAsync(_executedSearch, keepResult: true))
        {
            return;
        }

        // Nothing arrived, so — exactly as on the first fetch — the state has to go back to
        // describing the last answer that did. keepResult held on to the empty page that started
        // the retreat, which is the one result that must not be the one left on screen.
        _page = previous;
        _result = previousResult;
    }

    /// <summary>
    /// Tell the host what was searched for, so it can reflect it in its own URL.
    /// </summary>
    /// <remarks>
    /// Raised whether or not the fetch succeeded, which is what <see cref="SearchChanged"/>
    /// documents: a host whose URL kept the previous query after a failed search would hand out a
    /// link that reloads into a different search than the box on screen is showing.
    /// </remarks>
    private Task NotifySearchChangedAsync() => RaiseAsync(SearchChanged, _search);

    /// <summary>
    /// Hand a value to one of the host's callbacks without letting the host's own failure out.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="SearchChanged"/> and <see cref="FilterChanged"/>, because what has to be
    /// survived is the same for both: the handler is the host's, and what it most often does is
    /// rewrite a URL.
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
            // A host that navigates from its handler. During static SSR that is signalled by this
            // exception and the framework turns it into the redirect, so swallowing it would drop
            // the navigation on the floor.
            throw;
        }
        catch (Exception)
        {
            // The host's handler threw, and a NavigationManager call or a CMS URL rewrite is
            // exactly the kind that does. Left unhandled it would propagate out of Blazor's event
            // dispatch — and this same path runs from OnInitializedAsync, so during initial render
            // too. In helsedata's legacy Blazor Server host inside Optimizely that tears down the
            // circuit for the whole CMS page, not just this component.
            //
            // Nothing is said to the reader on top of what the search already reported for itself,
            // success or failure. What broke here is the host's own URL, which is the host's bug to
            // find in the host's logs — and reporting it as "Kunne ikke hente variabler" would
            // blame the API for a call the API was never part of.
        }
    }

    /// <summary>Fetch <paramref name="search"/> at the current page and ordering. True when it succeeded.</summary>
    /// <remarks>
    /// <para>
    /// The search is a parameter rather than read from <c>_search</c>, because the two callers do
    /// not mean the same thing by it: searching means the live contents of the box, sorting means
    /// the text the visible rows actually came from.
    /// </para>
    /// <para>
    /// <paramref name="keepResult"/> keeps the rows already on screen when the call fails,
    /// which is what a page turn wants and a search does not. A search that failed has no result
    /// to describe — the rows on screen came from a different query, and leaving them there under
    /// the new search's error message would say they answered it. A page turn's rows came from the
    /// query that is still on screen, so they stay, and with them the pager button the reader is
    /// standing on.
    /// </para>
    /// </remarks>
    private async Task<bool> FetchAsync(string? search, bool keepResult = false)
    {
        _loading = true;
        _error = null;
        StateHasChanged();

        try
        {
            _result = await Client.SearchVariablesAsync(
                search,
                _filter,
                page: _page,
                pageSize: ClampedPageSize,
                sort: _sort,
                direction: _direction);
            _executedSearch = Trimmed(search);

            // The page we are on is the page that arrived, not the page that was asked for. A
            // server that clamps page 12 to page 8 and says so has answered truthfully, and
            // ResultPage already counts the row range from its answer — leaving _page at 12 would
            // caption those rows "Side 12 av 8" and, worse, keep Neste enabled against a number
            // the server disowned, so every further press would walk the position further from the
            // rows without ever moving them. One page number for the caption, the two buttons and
            // the range, taken from the same place.
            _page = ResultPage;

            return true;
        }
        catch (Exception)
        {
            // Say what the reader can do about it; the detail belongs in the host's logs,
            // not on the page.
            if (!keepResult)
            {
                _result = null;
            }

            _error = T.Error;

            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    private static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Period(VariableSummary v)
    {
        var from = v.DataFrom?.Year.ToString();
        var to = v.DataTo?.Year.ToString();
        return (from, to) switch
        {
            (null, null) => null,
            (not null, null) => $"{from}–",
            (null, not null) => $"–{to}",
            _ => from == to ? from! : $"{from}–{to}"
        };
    }

    /// <summary>
    /// Self-contained translations. Deliberately not IStringLocalizer — see <see cref="Language"/>.
    /// </summary>
    private sealed record Texts(
        string Title,
        string SearchLabel,
        string SearchPlaceholder,
        string SearchButton,
        string SortBy,
        string Loading,
        string Error,
        string NotSpecified,
        string SortDefault,
        string FieldCode,
        string FieldSource,
        string FieldDataCollection,
        string FieldVariableGroup,
        string FieldPeriod,
        // The filter panel. FieldSource and FieldVariableGroup name two of the facets as well as two
        // of the card fields — deliberately the same word for the same thing in both places.
        string FiltersTitle,
        string ClearFilters,
        string FilterError,
        string FacetKildeType,
        string FacetFilter,
        string FacetDataType,
        string FacetHelsefagligKodeverk,
        string FacetAdministrativtKodeverk,
        string FacetInstrument,
        string FacetOther,
        string HasKildekodeverk,
        string IncludeHistorical,
        string NoVariabelgrupper,
        // Prose for the two facets the API reports as raw tokens: kildetype as its enum name, and
        // datatype as a bare code with no label at all. Both are Munin's own explorer wording, so
        // the two UIs name the same value the same way. A token missing from either falls back to
        // what the API sent rather than to nothing.
        IReadOnlyDictionary<string, string> KildeTypeNames,
        IReadOnlyDictionary<string, string> DataTypeNames,
        string Ascending,
        string Descending,
        string Pagination,
        string SkipToPagination,
        string Previous,
        string Next,
        // The buttons' accessible names. Longer than the words on them because "Forrige" on its own
        // does not say forrige what — and each one starts with the visible text, so a speech-input
        // user saying what they can see still hits the button (WCAG 2.5.3).
        string PreviousLabel,
        string NextLabel,
        // (page, totalPages) — the pager's own "Side 2 av 13".
        Func<int, int, string> PageOf,
        // (field, direction) — the active sort button's label.
        Func<string, string, string> ActiveLabel,
        // (from, to, total, search, filters, field, direction) — the whole result sentence. The
        // ordering clause is part of it rather than appended by the caller, so a language whose
        // grammar puts the ordering first can say it that way instead of inheriting Norwegian's
        // clause order. The filter count is in it for the same reason the ordering is: with the
        // facets collapsed, the sentence is the only place that says the list is narrowed at all.
        Func<int, int, int, string?, int, string, string, string> ResultSummary,
        // (search, filters) — the empty state. It names the filters because a search that matches
        // nothing *with three filters on* is a different thing to be told than one that matches
        // nothing at all, and the second reads as "this catalogue does not have it".
        Func<string?, int, string> NoResults)
    {
        /// <summary>
        /// The label for a sort order. The three that name one field use the same words the result
        /// cards label that value with, so the button and the line it orders say the same thing.
        /// </summary>
        /// <remarks>
        /// Every member has its own arm, and an unknown one throws rather than falling through to
        /// the default order's label: a member added to <see cref="SortField"/> without a label here
        /// would otherwise put a button on screen claiming an order it does not ask for.
        /// </remarks>
        public string FieldLabel(SortField sort) => sort switch
        {
            // Not "Navn". The API's default order leads with kilde, not the name — see the remarks
            // on SortField.Default — so a button labelled Navn would describe an order the list is
            // not in, which is the one thing the live-region announcement exists to get right.
            SortField.Default => SortDefault,
            SortField.Kilde => FieldSource,
            SortField.Datasamling => FieldDataCollection,
            SortField.Variabelgruppe => FieldVariableGroup,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "No label for this sort field.")
        };

        /// <summary>
        /// Prose for a kildetype token, falling back to what the API called it.
        /// </summary>
        /// <remarks>
        /// A fallback rather than a throw, unlike <see cref="FieldLabel"/>: the tokens are Munin's
        /// kildetype enum and a new member appearing there is a catalogue change, not a bug in this
        /// component. "SentraltHelseregister" on a button is poor prose but it is the truth, where
        /// dropping the value would take a filter off the screen that the API is still counting.
        /// </remarks>
        public string KildeTypeLabel(string? value, string? fallback)
        {
            if (value is not null && KildeTypeNames.TryGetValue(value, out var name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(fallback) ? NotSpecified : fallback;
        }

        /// <summary>Prose for a datatype code, falling back to the code — same reasoning as above.</summary>
        public string DataTypeLabel(string value) =>
            DataTypeNames.TryGetValue(value, out var name) ? name : value;

        /// <summary>The word for a direction, as the status line and the active button say it.</summary>
        /// <remarks>
        /// A switch with an arm per member rather than "descending, else ascending", for the same
        /// reason <see cref="FieldLabel"/> is one: a member added to <see cref="SortDirection"/>
        /// without a word here would be announced as ascending, and a list announced as ordered the
        /// opposite way to the order it is in is worse than one that fails loudly.
        /// </remarks>
        public string DirectionName(SortDirection direction) => direction switch
        {
            SortDirection.Ascending => Ascending,
            SortDirection.Descending => Descending,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction), direction, "No name for this sort direction.")
        };

        private static readonly Texts No = new(
            Title: "Variabelutforsker",
            SearchLabel: "Søk i variabler",
            SearchPlaceholder: "Søk etter variabelnavn eller kode",
            SearchButton: "Søk",
            SortBy: "Sorter etter",
            Loading: "Henter variabler …",
            Error: "Kunne ikke hente variabler nå. Prøv igjen om litt.",
            NotSpecified: "Ikke oppgitt",
            SortDefault: "Standard",
            FieldCode: "Kode",
            FieldSource: "Datakilde",
            FieldDataCollection: "Datasamling",
            FieldVariableGroup: "Variabelgruppe",
            FieldPeriod: "Periode",
            FiltersTitle: "Filtre",
            ClearFilters: "Fjern alle filtre",
            FilterError: "Kunne ikke oppdatere filtrene nå. Tallene kan være utdaterte.",
            // helsedata's own variable page calls it this, rather than Munin's "Kildetype".
            FacetKildeType: "Type datakilde",
            FacetFilter: "Filter",
            FacetDataType: "Datatype",
            FacetHelsefagligKodeverk: "Helsefaglig kodeverk",
            FacetAdministrativtKodeverk: "Administrativt kodeverk",
            FacetInstrument: "Instrument",
            FacetOther: "Andre filtre",
            HasKildekodeverk: "Har kildekodeverk",
            IncludeHistorical: "Vis historiske",
            NoVariabelgrupper: "Velg en datakilde for å se variabelgrupper.",
            KildeTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sentraltHelseregister"] = "Sentralt helseregister",
                ["nasjonaltMedisinskKvalitetsregister"] = "Nasjonalt medisinsk kvalitetsregister",
                ["annetMedisinskKvalitetsregister"] = "Annet medisinsk kvalitetsregister",
                ["befolkningsbasertHelseundersokelse"] = "Befolkningsbasert helseundersøkelse",
                ["biobank"] = "Biobank",
                ["annenDatakilde"] = "Annen datakilde",
                ["forskningsprosjekt"] = "Forskningsprosjekt",
                ["manueltOpprettet"] = "Manuelt opprettet"
            },
            DataTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Streng",
                ["2"] = "Heltall",
                ["3"] = "Desimaltall",
                ["4"] = "Boolsk",
                ["5"] = "Klokkeslett",
                ["6"] = "Dato",
                ["7"] = "Dato og tid",
                ["8"] = "URI",
                ["9"] = "Base64Binary",
                ["10"] = "Fødselsnummer (11 siffer)"
            },
            Ascending: "stigende",
            Descending: "synkende",
            Pagination: "Paginering",
            SkipToPagination: "Hopp til paginering",
            Previous: "Forrige",
            Next: "Neste",
            PreviousLabel: "Forrige side",
            NextLabel: "Neste side",
            PageOf: (page, totalPages) => $"Side {page} av {totalPages}",
            ActiveLabel: (field, direction) => $"{field} ({direction})",
            // The whole sentence, ordering clause included, because the comma and where the clause
            // sits are this language's grammar and not something to fix in C#.
            ResultSummary: (from, to, total, search, filters, field, direction) =>
            {
                var count = total == 1 ? "1 variabel" : $"{total} variabler";
                // One page of a longer list, so say which rows these are rather than captioning
                // rows 26 to 50 as though they were the first 25 of 312.
                var found = from <= 1 && to >= total
                    ? $"{count} funnet"
                    : $"Viser {from}–{to} av {count} funnet";
                var forSearch = search is null ? "" : $" for «{search}»";
                var narrowed = filters switch
                {
                    0 => "",
                    1 => ", avgrenset av 1 filter",
                    _ => $", avgrenset av {filters} filtre"
                };
                return $"{found}{forSearch}{narrowed}, sortert på {field}, {direction}";
            },
            NoResults: (search, filters) =>
            {
                var forSearch = search is null ? "Ingen variabler passet søket" : $"Ingen variabler passet søket «{search}»";
                return filters == 0 ? $"{forSearch}." : $"{forSearch} med filtrene som er valgt.";
            });

        private static readonly Texts En = new(
            Title: "Variable explorer",
            SearchLabel: "Search variables",
            SearchPlaceholder: "Search by variable name or code",
            SearchButton: "Search",
            SortBy: "Sort by",
            Loading: "Loading variables …",
            Error: "Could not load variables right now. Please try again shortly.",
            NotSpecified: "Not specified",
            SortDefault: "Default",
            FieldCode: "Code",
            FieldSource: "Data source",
            FieldDataCollection: "Data collection",
            FieldVariableGroup: "Variable group",
            FieldPeriod: "Period",
            FiltersTitle: "Filters",
            ClearFilters: "Clear all filters",
            FilterError: "Could not refresh the filters right now. The counts may be out of date.",
            FacetKildeType: "Type of data source",
            FacetFilter: "Filter",
            FacetDataType: "Data type",
            FacetHelsefagligKodeverk: "Clinical code system",
            FacetAdministrativtKodeverk: "Administrative code system",
            FacetInstrument: "Instrument",
            FacetOther: "Other filters",
            HasKildekodeverk: "Has source code system",
            IncludeHistorical: "Show historical",
            NoVariabelgrupper: "Select a data source to see variable groups.",
            KildeTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sentraltHelseregister"] = "Central health registry",
                ["nasjonaltMedisinskKvalitetsregister"] = "National medical quality registry",
                ["annetMedisinskKvalitetsregister"] = "Other medical quality registry",
                ["befolkningsbasertHelseundersokelse"] = "Population-based health survey",
                ["biobank"] = "Biobank",
                ["annenDatakilde"] = "Other data source",
                ["forskningsprosjekt"] = "Research project",
                ["manueltOpprettet"] = "Manually created"
            },
            DataTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "String",
                ["2"] = "Integer",
                ["3"] = "Decimal",
                ["4"] = "Boolean",
                ["5"] = "Time",
                ["6"] = "Date",
                ["7"] = "Datetime",
                ["8"] = "URI",
                ["9"] = "Base64Binary",
                ["10"] = "National ID (11 digits)"
            },
            Ascending: "ascending",
            Descending: "descending",
            Pagination: "Pagination",
            SkipToPagination: "Skip to pagination",
            Previous: "Previous",
            Next: "Next",
            PreviousLabel: "Previous page",
            NextLabel: "Next page",
            PageOf: (page, totalPages) => $"Page {page} of {totalPages}",
            ActiveLabel: (field, direction) => $"{field} ({direction})",
            ResultSummary: (from, to, total, search, filters, field, direction) =>
            {
                var count = total == 1 ? "1 variable" : $"{total} variables";
                var found = from <= 1 && to >= total
                    ? $"{count} found"
                    : $"Showing {from}–{to} of {count} found";
                var forSearch = search is null ? "" : $" for “{search}”";
                var narrowed = filters switch
                {
                    0 => "",
                    1 => ", narrowed by 1 filter",
                    _ => $", narrowed by {filters} filters"
                };
                return $"{found}{forSearch}{narrowed}, sorted by {field}, {direction}";
            },
            NoResults: (search, filters) =>
            {
                var forSearch = search is null ? "No variables matched your search" : $"No variables matched your search for “{search}”";
                return filters == 0 ? $"{forSearch}." : $"{forSearch} with the filters you have chosen.";
            });

        public static Texts For(string? language) =>
            string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? En : No;
    }
}
