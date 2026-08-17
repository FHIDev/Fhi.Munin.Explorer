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
    /// A host mounting this component must make the mount point fully interactive.
    /// An EventCallback serialises to an empty delegate across a static-SSR to
    /// interactive-island boundary, and the callback then silently never fires.
    /// </remarks>
    [Parameter] public EventCallback<string?> SokChanged { get; set; }

    /// <summary>Rows per page.</summary>
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

    // Name ascending, the same default Runa starts on — and the order the API returns when it is
    // asked for none, so the first render costs no extra query parameters.
    private Sorteringsfelt _sortering = Sorteringsfelt.Navn;
    private Sorteringsretning _retning = Sorteringsretning.Stigende;

    // The page being asked for. There is no pager yet — that is bead Fhi.Metadata-l9l2n.12 — so
    // this only ever holds 1 today. It is here rather than written inline at the call site because
    // "any change of search or sort goes back to page one" is a rule about state: a result set
    // reordered under someone still looking at page 7 shows them rows from the middle of a
    // sequence they never saw the start of. Keeping the reset next to the state it resets is what
    // stops the pager from landing without it.
    private int _side = 1;

    /// <summary>
    /// The fields offered for sorting, in the order the buttons appear.
    /// </summary>
    /// <remarks>
    /// The same four Runa offers. Code, datatype, status and data period are absent because the
    /// API does not sort on them — a fifth button would order the list by name and claim otherwise.
    /// </remarks>
    private static readonly Sorteringsfelt[] Sorterbare =
    [
        Sorteringsfelt.Navn,
        Sorteringsfelt.Kilde,
        Sorteringsfelt.Datasamling,
        Sorteringsfelt.Variabelgruppe
    ];

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

    private Tekster T => Tekster.For(Sprak);

    private string Opptatt => _laster ? "true" : "false";

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
    /// It names the ordering as well as the count. Without column headers there is no
    /// <c>aria-sort</c> to carry that, so it rides along on the status line the component already
    /// has: pressing a sort button changes this sentence, and the polite, atomic live region reads
    /// the whole of it back.
    /// </remarks>
    private string Sammendrag =>
        _resultat is null ? "" : T.Treff(_resultat.Items.Count, _resultat.TotalCount, _utfortSok) + Sortert;

    /// <summary>The trailing clause of <see cref="Sammendrag"/>: what the list is ordered by.</summary>
    private string Sortert => T.Sortert(T.Feltetikett(_sortering), T.Retningsnavn(_retning));

    /// <summary>A sort button's label — the field, plus the direction when it is the active one.</summary>
    private string Knappetekst(Sorteringsfelt felt) =>
        felt == _sortering ? T.AktivEtikett(T.Feltetikett(felt), T.Retningsnavn(_retning)) : T.Feltetikett(felt);

    /// <summary>
    /// A sort button's classes. The active field is filled, the rest are ghosts; the trailing
    /// margins are Stiler's own modifiers, which the buttons need because nothing else separates
    /// them — Razor drops the whitespace between elements.
    /// </summary>
    private string Knappeklasse(Sorteringsfelt felt)
    {
        var stil = felt == _sortering ? "button-square--secondary" : "button-square--ghost";

        return $"hd-button-square {stil} margin-right margin-bottom";
    }

    /// <summary><c>"true"</c> on the active field, and nothing at all on the others.</summary>
    /// <remarks>
    /// Null rather than <c>"false"</c>: Blazor leaves an attribute out when its value is null, and
    /// three buttons carrying <c>aria-current="false"</c> is noise in the accessibility tree.
    /// </remarks>
    private string? Valgt(Sorteringsfelt felt) => felt == _sortering ? "true" : null;

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
        Felt(builder, 100, T.FeltKode, v.Code, forste: true);
        Felt(builder, 200, T.FeltKilde, v.KildeName, forste: false);
        Felt(builder, 300, T.FeltDatasamling, v.DatasamlingName, forste: false);
        Felt(builder, 400, T.FeltPeriode, Perioden(v), forste: false);

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
        _side = 1;

        if (await HentAsync() && SokChanged.HasDelegate)
        {
            await SokChanged.InvokeAsync(_sok);
        }
    }

    /// <summary>
    /// Order by <paramref name="felt"/>: the active field again reverses the direction, another
    /// field starts ascending. Runa's rule, moved off the column header it used to live on.
    /// </summary>
    private async Task SorterAsync(Sorteringsfelt felt)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit. The
        // guard comes first on purpose: changing the state and then not fetching would leave a
        // button saying the list is ordered one way while it is still ordered the other.
        if (_laster)
        {
            return;
        }

        if (felt == _sortering)
        {
            _retning = _retning == Sorteringsretning.Stigende
                ? Sorteringsretning.Synkende
                : Sorteringsretning.Stigende;
        }
        else
        {
            _sortering = felt;
            _retning = Sorteringsretning.Stigende;
        }

        // Reordering renumbers every page, so the page the user is on is no longer the same rows.
        _side = 1;

        await HentAsync();
    }

    /// <summary>Fetch the current search, page and ordering. True when it succeeded.</summary>
    private async Task<bool> HentAsync()
    {
        _laster = true;
        _feil = null;
        StateHasChanged();

        try
        {
            _resultat = await Client.SokVariablerAsync(
                _sok,
                side: _side,
                sideStorrelse: SideStorrelse,
                sortering: _sortering,
                retning: _retning);
            _utfortSok = Renset(_sok);

            return true;
        }
        catch (Exception)
        {
            // Say what the reader can do about it; the detail belongs in the host's logs,
            // not on the page.
            _resultat = null;
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
        string SorterEtter,
        string Laster,
        string Feil,
        string IkkeOppgitt,
        string FeltNavn,
        string FeltKode,
        string FeltKilde,
        string FeltDatasamling,
        string FeltVariabelgruppe,
        string FeltPeriode,
        string Stigende,
        string Synkende,
        Func<string, string, string> AktivEtikett,
        Func<string, string, string> Sortert,
        Func<int, int, string?, string> Treff,
        Func<string?, string> IngenTreff)
    {
        /// <summary>
        /// The label for a sortable field. The same words the result cards label their values
        /// with, so the button and the line it orders say the same thing.
        /// </summary>
        public string Feltetikett(Sorteringsfelt felt) => felt switch
        {
            Sorteringsfelt.Kilde => FeltKilde,
            Sorteringsfelt.Datasamling => FeltDatasamling,
            Sorteringsfelt.Variabelgruppe => FeltVariabelgruppe,
            _ => FeltNavn
        };

        public string Retningsnavn(Sorteringsretning retning) =>
            retning == Sorteringsretning.Synkende ? Synkende : Stigende;

        private static readonly Tekster No = new(
            Tittel: "Variabelutforsker",
            SokLedetekst: "Søk i variabler",
            SokPlassholder: "Søk etter variabelnavn eller kode",
            SokKnapp: "Søk",
            SorterEtter: "Sorter etter",
            Laster: "Henter variabler …",
            Feil: "Kunne ikke hente variabler nå. Prøv igjen om litt.",
            IkkeOppgitt: "Ikke oppgitt",
            FeltNavn: "Navn",
            FeltKode: "Kode",
            FeltKilde: "Datakilde",
            FeltDatasamling: "Datasamling",
            FeltVariabelgruppe: "Variabelgruppe",
            FeltPeriode: "Periode",
            Stigende: "stigende",
            Synkende: "synkende",
            AktivEtikett: (felt, retning) => $"{felt} ({retning})",
            Sortert: (felt, retning) => $", sortert på {felt}, {retning}",
            Treff: (vist, total, sok) =>
            {
                var antall = total == 1 ? "1 variabel" : $"{total} variabler";
                // Only the first page is fetched, so say so rather than captioning 25 rows
                // with a count of 312.
                var basis = vist < total ? $"Viser {vist} av {antall} funnet" : $"{antall} funnet";
                return sok is null ? basis : $"{basis} for «{sok}»";
            },
            IngenTreff: sok => sok is null
                ? "Ingen variabler passet søket."
                : $"Ingen variabler passet søket «{sok}».");

        private static readonly Tekster En = new(
            Tittel: "Variable explorer",
            SokLedetekst: "Search variables",
            SokPlassholder: "Search by variable name or code",
            SokKnapp: "Search",
            SorterEtter: "Sort by",
            Laster: "Loading variables …",
            Feil: "Could not load variables right now. Please try again shortly.",
            IkkeOppgitt: "Not specified",
            FeltNavn: "Name",
            FeltKode: "Code",
            FeltKilde: "Data source",
            FeltDatasamling: "Data collection",
            FeltVariabelgruppe: "Variable group",
            FeltPeriode: "Period",
            Stigende: "ascending",
            Synkende: "descending",
            AktivEtikett: (felt, retning) => $"{felt} ({retning})",
            Sortert: (felt, retning) => $", sorted by {felt}, {retning}",
            Treff: (vist, total, sok) =>
            {
                var antall = total == 1 ? "1 variable" : $"{total} variables";
                var basis = vist < total ? $"Showing {vist} of {antall} found" : $"{antall} found";
                return sok is null ? basis : $"{basis} for “{sok}”";
            },
            IngenTreff: sok => sok is null
                ? "No variables matched your search."
                : $"No variables matched your search for “{sok}”.");

        public static Tekster For(string? sprak) =>
            string.Equals(sprak, "en", StringComparison.OrdinalIgnoreCase) ? En : No;
    }
}
