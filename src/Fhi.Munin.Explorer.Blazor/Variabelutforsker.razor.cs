using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Search and browse published variables from the Munin Explorer API.
/// </summary>
/// <remarks>
/// <para>
/// This package ships no CSS, so the host stylesheet — <c>Fhi.Helsedata.Stiler</c> on
/// helsedata.no — owns everything visual. Three of those are accessibility requirements the
/// markup here cannot meet on its own, and a host that skips them fails WCAG whatever this
/// component does:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A visible focus indicator on the search field, the Søk button and the scrollable table
/// wrapper (<c>variabelutforsker-tabell-omslag</c>, which is deliberately focusable so it can
/// be scrolled from the keyboard). WCAG 2.4.7.
/// </description></item>
/// <item><description>
/// Text and non-text contrast, WCAG 1.4.3 and 1.4.11 — including the em dash that stands in
/// for a missing value.
/// </description></item>
/// <item><description>
/// A <c>variabelutforsker-visuelt-skjult</c> rule that takes an element out of the visual
/// layout while leaving it readable by assistive technology (the usual clip-rect recipe, not
/// <c>display: none</c>, which hides it from screen readers too). Without it the table caption
/// and the "Ikke oppgitt" stand-in for empty cells simply appear on screen.
/// </description></item>
/// </list>
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

    /// <summary>
    /// Applied to text that exists for assistive technology only — the table's caption and
    /// the stand-in for an empty cell. The host stylesheet has to provide the usual
    /// clip-rect rule for it (see <c>Fhi.Helsedata.Stiler</c>); without it the text simply
    /// shows up on screen. This package ships no CSS of its own by design.
    /// </summary>
    private const string VisueltSkjult = "variabelutforsker-visuelt-skjult";

    private string? _sok;
    private bool _laster;
    private string? _feil;
    private Side<VariabelSammendrag>? _resultat;

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
    private string SammendragId => $"variabelutforsker-sammendrag-{_instans}";

    private Tekster T => Tekster.For(Sprak);

    private string Opptatt => _laster ? "true" : "false";

    /// <summary>
    /// One sentence describing the visible result, used both as the live announcement and
    /// as the table's caption so the two can never drift apart.
    /// </summary>
    private string Sammendrag =>
        _resultat is null ? "" : T.Treff(_resultat.Items.Count, _resultat.TotalCount, _utfortSok);

    /// <summary>
    /// The title, at the level the host asked for. Razor has no syntax for a computed
    /// element name, so this is built by hand.
    /// </summary>
    private RenderFragment Overskrift => builder =>
    {
        builder.OpenElement(0, $"h{Math.Clamp(OverskriftNivaa, 1, 6)}");
        builder.AddAttribute(1, "class", "variabelutforsker-tittel");
        builder.AddAttribute(2, "id", TittelId);
        builder.AddContent(3, T.Tittel);
        builder.CloseElement();
    };

    /// <summary>
    /// A table cell value, with a spoken stand-in when there is nothing to show. The dash is
    /// decoration: depending on punctuation settings a screen reader reads it as "em dash"
    /// or skips it silently, and neither tells the reader that the value is simply missing.
    /// </summary>
    private RenderFragment Verdi(string? tekst) => builder =>
    {
        if (!string.IsNullOrWhiteSpace(tekst))
        {
            builder.AddContent(0, tekst);
            return;
        }

        builder.OpenElement(1, "span");
        builder.AddAttribute(2, "aria-hidden", "true");
        builder.AddContent(3, "—");
        builder.CloseElement();

        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", VisueltSkjult);
        builder.AddContent(6, T.IkkeOppgitt);
        builder.CloseElement();
    };

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

        _laster = true;
        _feil = null;
        StateHasChanged();

        try
        {
            _resultat = await Client.SokVariablerAsync(_sok, side: 1, sideStorrelse: SideStorrelse);
            _utfortSok = Renset(_sok);

            if (SokChanged.HasDelegate)
            {
                await SokChanged.InvokeAsync(_sok);
            }
        }
        catch (Exception)
        {
            // Say what the reader can do about it; the detail belongs in the host's logs,
            // not on the page.
            _resultat = null;
            _feil = T.Feil;
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
        string Laster,
        string Feil,
        string IkkeOppgitt,
        string KolonneVariabel,
        string KolonneKilde,
        string KolonneDatasamling,
        string KolonnePeriode,
        Func<int, int, string?, string> Treff,
        Func<string?, string> IngenTreff)
    {
        private static readonly Tekster No = new(
            Tittel: "Variabelutforsker",
            SokLedetekst: "Søk i variabler",
            SokPlassholder: "Søk etter variabelnavn eller kode",
            SokKnapp: "Søk",
            Laster: "Henter variabler …",
            Feil: "Kunne ikke hente variabler nå. Prøv igjen om litt.",
            IkkeOppgitt: "Ikke oppgitt",
            KolonneVariabel: "Variabel",
            KolonneKilde: "Datakilde",
            KolonneDatasamling: "Datasamling",
            KolonnePeriode: "Periode",
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
            Laster: "Loading variables …",
            Feil: "Could not load variables right now. Please try again shortly.",
            IkkeOppgitt: "Not specified",
            KolonneVariabel: "Variable",
            KolonneKilde: "Data source",
            KolonneDatasamling: "Data collection",
            KolonnePeriode: "Period",
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
