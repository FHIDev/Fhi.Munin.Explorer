using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

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
    [Parameter] public string Sprak { get; set; } = "no";

    [Inject] private IMuninExplorerClient Client { get; set; } = null!;

    private string? _sok;
    private bool _laster;
    private string? _feil;
    private Side<VariabelSammendrag>? _resultat;

    // Unique per instance so two explorers on one page cannot collide on DOM ids,
    // which would be a WCAG 4.1.1 failure as well as breaking label association.
    private readonly string _instans = Guid.NewGuid().ToString("N")[..8];
    private string SokId => $"variabelutforsker-sok-{_instans}";
    private string TittelId => $"variabelutforsker-tittel-{_instans}";

    private Tekster T => Tekster.For(Sprak);

    protected override async Task OnInitializedAsync()
    {
        _sok = Sok;
        await SokAsync();
    }

    private async Task SokAsync()
    {
        _laster = true;
        _feil = null;
        StateHasChanged();

        try
        {
            _resultat = await Client.SokVariablerAsync(_sok, side: 1, sideStorrelse: SideStorrelse);

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

    private static string Perioden(VariabelSammendrag v)
    {
        var fra = v.DataFrom?.Year.ToString();
        var til = v.DataTo?.Year.ToString();
        return (fra, til) switch
        {
            (null, null) => "—",
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
        string IngenTreff,
        string Feil,
        string KolonneVariabel,
        string KolonneKilde,
        string KolonneDatasamling,
        string KolonnePeriode,
        Func<int, string> Treff)
    {
        private static readonly Tekster No = new(
            "Variabelutforsker", "Søk i variabler", "Søk etter variabelnavn eller kode", "Søk",
            "Henter variabler …", "Ingen variabler passet søket.",
            "Kunne ikke hente variabler nå. Prøv igjen om litt.",
            "Variabel", "Datakilde", "Datasamling", "Periode",
            n => n == 1 ? "1 variabel" : $"{n} variabler");

        private static readonly Tekster En = new(
            "Variable explorer", "Search variables", "Search by variable name or code", "Search",
            "Loading variables …", "No variables matched your search.",
            "Could not load variables right now. Please try again shortly.",
            "Variable", "Data source", "Data collection", "Period",
            n => n == 1 ? "1 variable" : $"{n} variables");

        public static Tekster For(string? sprak) =>
            string.Equals(sprak, "en", StringComparison.OrdinalIgnoreCase) ? En : No;
    }
}
