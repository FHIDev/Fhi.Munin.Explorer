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
/// are <c>form-element__label</c>, <c>searchbox__freetext*</c>,
/// <c>hd-button-square</c>/<c>button-square--primary</c>, <c>headline</c>, <c>caption</c>,
/// <c>infobox</c> and <c>datasourcecard*</c> — the last of these is the same card list
/// helsedata's own datakildeutforsker renders its results with.
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
    /// The heading level for a result card, one step below the component's own title so the
    /// outline stays unbroken however deep the host mounted us.
    /// </summary>
    private int RadNivaa => Math.Clamp(TittelNivaa + 1, 1, 6);

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
        string FeltKode,
        string FeltKilde,
        string FeltDatasamling,
        string FeltPeriode,
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
            FeltKode: "Kode",
            FeltKilde: "Datakilde",
            FeltDatasamling: "Datasamling",
            FeltPeriode: "Periode",
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
            FeltKode: "Code",
            FeltKilde: "Data source",
            FeltDatasamling: "Data collection",
            FeltPeriode: "Period",
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
