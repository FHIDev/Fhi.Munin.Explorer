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
public partial class Variabelutforsker : ComponentBase
{
    /// <summary>
    /// Initial search text. Set by the host, typically from a URL query parameter — the
    /// component has no NavigationManager and no URL logic of its own, because the CMS
    /// host owns routing.
    /// </summary>
    [Parameter] public string? Sok { get; set; }

    /// <summary>
    /// Raised when the user searches, so the host can reflect it in its own URL.
    /// The Sok/SokChanged naming gives the host <c>@bind-Sok</c> for free.
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
    [Parameter] public EventCallback<string?> SokChanged { get; set; }

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
    [Parameter] public int SideStorrelse { get; set; } = 25;

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
    [Parameter] public string Sprak { get; set; } = "no";

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
    [Parameter] public int OverskriftNivaa { get; set; } = 2;

    [Inject] private IMuninExplorerClient Client { get; set; } = null!;

    private string? _sok;
    private bool _laster;
    private string? _feil;
    private Side<VariabelSammendrag>? _resultat;

    // The API's own default order, ascending, which is also where Runa starts — and the order the
    // API returns when it is asked for none, so the first render costs no extra query parameters.
    private SortField _sort = SortField.Default;
    private SortDirection _direction = SortDirection.Ascending;

    // The page being asked for, and the only piece of paging state there is. "Any change of search
    // or sort goes back to page one" is a rule about state — a result set reordered under someone
    // still looking at page 7 shows them rows from the middle of a sequence they never saw the
    // start of — so the resets live next to the field rather than at the call sites.
    //
    // Private, and reached only through GoToPageAsync. The host has no Side parameter and no
    // SideChanged callback, deliberately: the page number belongs in the host's URL alongside the
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
    // text in the box: @bind writes _sok on blur, so the box can hold an unsubmitted query
    // while the table below still shows the previous one. The announcement has to describe
    // what is on screen.
    private string? _utfortSok;

    // Unique per instance so two explorers on one page cannot collide on DOM ids,
    // which would be a WCAG 4.1.1 failure as well as breaking label association.
    private readonly string _instans = Guid.NewGuid().ToString("N")[..8];
    private string SokId => $"variabelutforsker-sok-{_instans}";
    private string TittelId => $"variabelutforsker-tittel-{_instans}";
    private string PaginationId => $"variabelutforsker-pagination-{_instans}";

    private Tekster T => Tekster.For(Sprak);

    private string Opptatt => _laster ? "true" : "false";

    /// <summary>Rows per page as actually requested — see <see cref="SideStorrelse"/>.</summary>
    private int PageSize => Math.Clamp(SideStorrelse, 1, 100);

    /// <summary>How many variables the search matched, not how many are on screen.</summary>
    private int TotalCount => _resultat?.TotalCount ?? 0;

    /// <summary>
    /// How many pages the result has. At least 1, so "Side 1 av 0" can never be written.
    /// </summary>
    /// <remarks>
    /// The server's own count is preferred over arithmetic here, because the server is the one that
    /// clamps the page size: counting the pages ourselves from a size it quietly changed would put
    /// a Neste button on screen for a page that does not exist. The arithmetic is kept as a fallback
    /// for a substituted <see cref="IMuninExplorerClient"/> that leaves the field at zero — claiming
    /// one page over three hundred rows would strand the reader on the first twenty-five of them.
    /// It divides by <see cref="ResultPageSize"/> and not by <see cref="PageSize"/> for the same
    /// reason: counting the pages against a size the rows were not built with would put the page
    /// count and the row range on screen describing two different pagings of one result.
    /// </remarks>
    private int TotalPages
    {
        get
        {
            if (_resultat is null || TotalCount <= 0)
            {
                return 1;
            }

            return _resultat.TotalPages > 0
                ? _resultat.TotalPages
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
    private bool ShowPager => _resultat is not null && (TotalPages > 1 || _keepPager);

    /// <summary>The 1-based position of the first row on screen, or 0 when there are no rows.</summary>
    /// <remarks>
    /// Guarded on the rows rather than on <see cref="TotalCount"/>, so that it agrees with
    /// <see cref="LastItemOnPage"/> without either of them relying on the markup to keep the pair
    /// off screen: a page with no rows on a non-zero total would otherwise read "Viser 26–0 av 312".
    /// </remarks>
    private int FirstItemOnPage =>
        _resultat is null || _resultat.Items.Count == 0 ? 0 : ((ResultPage - 1) * ResultPageSize) + 1;

    /// <summary>
    /// The 1-based position of the last row on screen, counted from the rows actually delivered.
    /// </summary>
    /// <remarks>
    /// Counted rather than calculated as <c>page × size</c>, so the last page says 312 and not 325,
    /// and so a server that returned a different page size than it was asked for still describes
    /// itself truthfully.
    /// </remarks>
    private int LastItemOnPage =>
        _resultat is null || _resultat.Items.Count == 0 ? 0 : FirstItemOnPage + _resultat.Items.Count - 1;

    /// <summary>
    /// The page size the visible result was actually built with, which is the server's answer when
    /// it gave one and what we asked for otherwise.
    /// </summary>
    private int ResultPageSize => _resultat is { Size: > 0 } page ? page.Size : PageSize;

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
    private int ResultPage => _resultat is { Page: > 0 } page ? page.Page : _page;

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
    private int TittelNivaa => Math.Clamp(OverskriftNivaa, 1, 6);

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
    private int RadNivaa => Math.Clamp(TittelNivaa + 1, 1, 6);

    /// <summary>
    /// One sentence describing the visible result, used both as the live announcement and
    /// as the list's accessible name so the two can never drift apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It names the ordering as well as the count. Without column headers there is no
    /// <c>aria-sort</c> to carry that, so it rides along on the status line the component already
    /// has: pressing a sort button changes this sentence, and the polite, atomic live region reads
    /// the whole of it back. The sentence is assembled inside <see cref="Tekster"/> rather than
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
    private string Sammendrag => _resultat is null
        ? ""
        : T.Treff(FirstItemOnPage, LastItemOnPage, TotalCount, _utfortSok,
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
        var stil = sort == _sort ? "button-square--secondary" : "button-square--ghost";

        return $"hd-button-square {stil} margin-right margin-bottom";
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
    private RenderFragment Overskrift => builder =>
    {
        builder.OpenElement(0, $"h{TittelNivaa}");
        builder.AddAttribute(1, "class", "headline headline-3");
        builder.AddAttribute(2, "id", TittelId);
        builder.AddContent(3, T.Tittel);
        builder.CloseElement();
    };

    /// <summary>
    /// A result card's heading — the variable's display name, at <see cref="RadNivaa"/>.
    /// </summary>
    /// <remarks>
    /// Giving every result a real heading is what lets a screen-reader user move between
    /// results with the heading rotor, which the table this replaced offered no equivalent of.
    /// The size comes from <c>datasourcecard__heading</c>, so it stays card-sized whatever
    /// level the element ends up being.
    /// </remarks>
    private RenderFragment RadOverskrift(VariabelSammendrag v) => builder =>
    {
        builder.OpenElement(0, $"h{RadNivaa}");
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
    private RenderFragment Infolinje(VariabelSammendrag v) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "datasourcecard__info");

        // Fixed, spread-out sequence numbers: each Felt call writes its own contiguous block,
        // so the renderer's diff sees a stable tree across renders.
        Felt(builder, 100, T.FieldCode, v.Code, forste: true);
        Felt(builder, 200, T.FieldSource, v.KildeName, forste: false);
        Felt(builder, 300, T.FieldDataCollection, v.DatasamlingName, forste: false);
        Felt(builder, 400, T.FieldPeriod, Perioden(v), forste: false);

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
    private void Felt(RenderTreeBuilder builder, int seq, string etikett, string? verdi, bool forste)
    {
        builder.OpenElement(seq, "span");
        builder.AddAttribute(seq + 1, "class", "datasourcecard__info--text");

        if (!forste)
        {
            // Stiler's dot separator between card metadata items. Purely decorative and empty,
            // so it is kept out of the accessibility tree rather than left as a nameless node.
            builder.OpenElement(seq + 2, "span");
            builder.AddAttribute(seq + 3, "class", "dot");
            builder.AddAttribute(seq + 4, "aria-hidden", "true");
            builder.CloseElement();
        }

        builder.AddContent(seq + 5, $"{etikett}: ");

        if (string.IsNullOrWhiteSpace(verdi))
        {
            builder.AddContent(seq + 6, T.IkkeOppgitt);
        }
        else
        {
            // The label follows Sprak; the value does not. Munin's metadata is Norwegian
            // whatever language the surrounding UI is in, and an English speech synthesiser
            // reading Norwegian variable names is unintelligible (WCAG 3.1.2).
            builder.OpenElement(seq + 7, "span");
            builder.AddAttribute(seq + 8, "lang", "no");
            builder.AddContent(seq + 9, verdi);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    protected override async Task OnInitializedAsync()
    {
        _sok = Sok;
        await SokAsync();
    }

    private async Task SokAsync()
    {
        // Nothing disables the submit button while a search runs — see the comment on it in
        // the markup — so a second submit is dropped here instead.
        if (_laster)
        {
            return;
        }

        // A different search is a different result set; page 7 of the old one means nothing in it.
        _page = 1;
        _keepPager = false;

        // The live contents of the box, which is what submitting means.
        await FetchAsync(_sok);

        await NotifySokChangedAsync();
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
        if (_laster)
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

        // _utfortSok, not _sok. Sorting is not searching: a click blurs the field first, so by the
        // time this runs the box's contents have already been written to _sok — text the user may
        // never have submitted. Fetching with it would run a search nobody asked for, quietly,
        // under a status line that then described the accidental search instead of saying anything
        // moved. It would also desynchronise the host, whose URL only follows SokChanged.
        if (!await FetchAsync(_utfortSok))
        {
            // The same invariant the _laster guard above protects, on the path that guard cannot
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
    /// Not a search, so <see cref="SokChanged"/> is not raised: the host's URL follows what was
    /// searched for, and turning a page did not change that.
    /// </para>
    /// </remarks>
    private async Task GoToPageAsync(int page)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit and a
        // sort click — and for the same reason the buttons carry aria-disabled instead of disabled:
        // neither is taken out of the document under the finger that pressed it, which is also why
        // a failed page turn below keeps the rows it already had.
        if (_laster)
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
        var previousResult = _resultat;

        // A pager button was pressed, so the pager stays until a search or a sort replaces the
        // result — including through a retreat that lands on a single-page answer.
        _keepPager = true;

        _page = target;

        // keepResult: the pressed button must survive the failure. The rest of the component
        // never removes a control the user just used, and the pager is the only pressable thing in
        // it that is rendered conditionally — so a page turn that cleared the rows would take
        // Forrige and Neste out of the document in the same render that reports the error, drop
        // focus to <body>, and leave a keyboard user restarting from the top of the host's page.
        if (!await FetchAsync(_utfortSok, keepResult: true))
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
    /// <see cref="IMuninExplorerClient.SokVariablerAsync"/> reports as an empty page rather than
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
    private async Task RetreatFromEmptyPageAsync(int previous, Side<VariabelSammendrag>? previousResult)
    {
        if (_page == 1 || _resultat is not { Items.Count: 0 })
        {
            return;
        }

        // TotalPages reads the answer that just arrived, so this is the new count and not the stale
        // one the clamp trusted. A server still claiming the page exists after sending nothing has
        // told us nothing usable, so page 1 is the only safe answer left.
        var last = TotalCount > 0 ? TotalPages : 1;
        _page = last < _page ? last : 1;

        if (await FetchAsync(_utfortSok, keepResult: true))
        {
            return;
        }

        // Nothing arrived, so — exactly as on the first fetch — the state has to go back to
        // describing the last answer that did. keepResult held on to the empty page that started
        // the retreat, which is the one result that must not be the one left on screen.
        _page = previous;
        _resultat = previousResult;
    }

    /// <summary>
    /// Tell the host what was searched for, so it can reflect it in its own URL.
    /// </summary>
    /// <remarks>
    /// Raised whether or not the fetch succeeded, which is what <see cref="SokChanged"/> documents:
    /// a host whose URL kept the previous query after a failed search would hand out a link that
    /// reloads into a different search than the box on screen is showing.
    /// </remarks>
    private async Task NotifySokChangedAsync()
    {
        if (!SokChanged.HasDelegate)
        {
            return;
        }

        try
        {
            await SokChanged.InvokeAsync(_sok);
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

    /// <summary>Fetch <paramref name="sok"/> at the current page and ordering. True when it succeeded.</summary>
    /// <remarks>
    /// <para>
    /// The search is a parameter rather than read from <c>_sok</c>, because the two callers do not
    /// mean the same thing by it: searching means the live contents of the box, sorting means the
    /// text the visible rows actually came from.
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
    private async Task<bool> FetchAsync(string? sok, bool keepResult = false)
    {
        _laster = true;
        _feil = null;
        StateHasChanged();

        try
        {
            _resultat = await Client.SokVariablerAsync(
                sok,
                side: _page,
                sideStorrelse: PageSize,
                sort: _sort,
                direction: _direction);
            _utfortSok = Renset(sok);

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
                _resultat = null;
            }

            _feil = T.Feil;

            return false;
        }
        finally
        {
            _laster = false;
        }
    }

    private static string? Renset(string? tekst) =>
        string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();

    private static string? Perioden(VariabelSammendrag v)
    {
        var fra = v.DataFrom?.Year.ToString();
        var til = v.DataTo?.Year.ToString();
        return (fra, til) switch
        {
            (null, null) => null,
            (not null, null) => $"{fra}–",
            (null, not null) => $"–{til}",
            _ => fra == til ? fra! : $"{fra}–{til}"
        };
    }

    /// <summary>
    /// Self-contained translations. Deliberately not IStringLocalizer — see <see cref="Sprak"/>.
    /// </summary>
    private sealed record Tekster(
        string Tittel,
        string SokLedetekst,
        string SokPlassholder,
        string SokKnapp,
        string SortBy,
        string Laster,
        string Feil,
        string IkkeOppgitt,
        string SortDefault,
        string FieldCode,
        string FieldSource,
        string FieldDataCollection,
        string FieldVariableGroup,
        string FieldPeriod,
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
        // (from, to, total, search, field, direction) — the whole result sentence. The ordering
        // clause is part of it rather than appended by the caller, so a language whose grammar puts
        // the ordering first can say it that way instead of inheriting Norwegian's clause order.
        Func<int, int, int, string?, string, string, string> Treff,
        Func<string?, string> IngenTreff)
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

        private static readonly Tekster No = new(
            Tittel: "Variabelutforsker",
            SokLedetekst: "Søk i variabler",
            SokPlassholder: "Søk etter variabelnavn eller kode",
            SokKnapp: "Søk",
            SortBy: "Sorter etter",
            Laster: "Henter variabler …",
            Feil: "Kunne ikke hente variabler nå. Prøv igjen om litt.",
            IkkeOppgitt: "Ikke oppgitt",
            SortDefault: "Standard",
            FieldCode: "Kode",
            FieldSource: "Datakilde",
            FieldDataCollection: "Datasamling",
            FieldVariableGroup: "Variabelgruppe",
            FieldPeriod: "Periode",
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
            Treff: (from, to, total, search, field, direction) =>
            {
                var antall = total == 1 ? "1 variabel" : $"{total} variabler";
                // One page of a longer list, so say which rows these are rather than captioning
                // rows 26 to 50 as though they were the first 25 of 312.
                var basis = from <= 1 && to >= total
                    ? $"{antall} funnet"
                    : $"Viser {from}–{to} av {antall} funnet";
                var forSok = search is null ? "" : $" for «{search}»";
                return $"{basis}{forSok}, sortert på {field}, {direction}";
            },
            IngenTreff: sok => sok is null
                ? "Ingen variabler passet søket."
                : $"Ingen variabler passet søket «{sok}».");

        private static readonly Tekster En = new(
            Tittel: "Variable explorer",
            SokLedetekst: "Search variables",
            SokPlassholder: "Search by variable name or code",
            SokKnapp: "Search",
            SortBy: "Sort by",
            Laster: "Loading variables …",
            Feil: "Could not load variables right now. Please try again shortly.",
            IkkeOppgitt: "Not specified",
            SortDefault: "Default",
            FieldCode: "Code",
            FieldSource: "Data source",
            FieldDataCollection: "Data collection",
            FieldVariableGroup: "Variable group",
            FieldPeriod: "Period",
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
            Treff: (from, to, total, search, field, direction) =>
            {
                var antall = total == 1 ? "1 variable" : $"{total} variables";
                var basis = from <= 1 && to >= total
                    ? $"{antall} found"
                    : $"Showing {from}–{to} of {antall} found";
                var forSok = search is null ? "" : $" for “{search}”";
                return $"{basis}{forSok}, sorted by {field}, {direction}";
            },
            IngenTreff: sok => sok is null
                ? "No variables matched your search."
                : $"No variables matched your search for “{sok}”.");

        public static Tekster For(string? sprak) =>
            string.Equals(sprak, "en", StringComparison.OrdinalIgnoreCase) ? En : No;
    }
}
