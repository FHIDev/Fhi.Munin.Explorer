using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class VariabelutforskerTest : BunitContext
{
    private static Side<VariabelSammendrag> EnSide(params VariabelSammendrag[] rader) =>
        new() { Items = rader, TotalCount = rader.Length, Page = 1, Size = 25, TotalPages = 1 };

    private static VariabelSammendrag Variabel(string navn, string kode, string? kilde = "Als registeret") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = kode,
            PreferredTerm = navn,
            KildeName = kilde,
            DatasamlingName = "Inklusjon",
            DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DataTo = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };

    private sealed class FakeClient(Side<VariabelSammendrag> svar) : IMuninExplorerClient
    {
        public string? SisteSok { get; private set; }
        public int Kall { get; private set; }

        public Task<Side<VariabelSammendrag>> SokVariablerAsync(
            string? sok, int side = 1, int sideStorrelse = 25, CancellationToken cancellationToken = default)
        {
            SisteSok = sok;
            Kall++;
            return Task.FromResult(svar);
        }
    }

    private sealed class FeilendeClient : IMuninExplorerClient
    {
        public Task<Side<VariabelSammendrag>> SokVariablerAsync(
            string? sok, int side = 1, int sideStorrelse = 25, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("nede");
    }

    /// <summary>A client that never answers until the test lets it, so the loading state can be inspected.</summary>
    private sealed class TregClient : IMuninExplorerClient
    {
        private readonly TaskCompletionSource<Side<VariabelSammendrag>> _svar =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Kall { get; private set; }

        public Task<Side<VariabelSammendrag>> SokVariablerAsync(
            string? sok, int side = 1, int sideStorrelse = 25, CancellationToken cancellationToken = default)
        {
            Kall++;
            return _svar.Task;
        }

        public void Svar(Side<VariabelSammendrag> side) => _svar.TrySetResult(side);
    }

    private IRenderedComponent<Variabelutforsker> RenderMed(
        IMuninExplorerClient client, Action<ComponentParameterCollectionBuilder<Variabelutforsker>>? p = null)
    {
        Services.AddSingleton(client);
        return Render<Variabelutforsker>(b => p?.Invoke(b));
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenViserRadPerVariabel()
    {
        var client = new FakeClient(EnSide(Variabel("1. Tale", "V_ALS.F1.ALSFRSR1TALE"),
                                           Variabel("2. Spyttsekresjon", "V_ALS.F1.ALSFRSR2SPYTT")));

        var cut = RenderMed(client);

        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Contains("1. Tale", cut.Markup);
        Assert.Contains("V_ALS.F1.ALSFRSR1TALE", cut.Markup);
        Assert.Contains("2 variabler", cut.Markup);
    }

    [Fact]
    public void Render_NårIngenTreff_ThenViserTomMelding()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        Assert.Empty(cut.FindAll("tbody tr"));
        Assert.Contains("Ingen variabler passet søket", cut.Markup);
    }

    [Fact]
    public void Render_NårApietFeiler_ThenViserFeilmeldingIStedetForÅKaste()
    {
        var cut = RenderMed(new FeilendeClient());

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("tbody tr"));
    }

    [Fact]
    public void Render_NårSpråkErEn_ThenBrukerEngelskeTekster()
    {
        // helsedata's culture token is "en"/"no", not "nb" — worth pinning.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                         b => b.Add(c => c.Sprak, "en"));

        Assert.Contains("Variable explorer", cut.Markup);
        Assert.Contains("1 variable", cut.Markup);
        Assert.DoesNotContain("Variabelutforsker", cut.Markup);
    }

    [Fact]
    public void Render_NårSokErSattAvHosten_ThenSendesDenTilApiet()
    {
        var client = new FakeClient(EnSide());

        RenderMed(client, b => b.Add(c => c.Sok, "tale"));

        Assert.Equal("tale", client.SisteSok);
        Assert.Equal(1, client.Kall);
    }

    [Fact]
    public void Render_ToInstanserPåSammeSide_ThenKolliderIkkePåDomId()
    {
        // Duplicate ids break label association and fail WCAG 4.1.1. helsedata can
        // legitimately put more than one explorer on a page.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(EnSide()));

        var a = Render<Variabelutforsker>();
        var b = Render<Variabelutforsker>();

        var idA = a.Find("input[type=search]").Id;
        var idB = b.Find("input[type=search]").Id;

        Assert.False(string.IsNullOrWhiteSpace(idA));
        Assert.NotEqual(idA, idB);
    }

    [Fact]
    public void Sok_NårBrukerenTasterITekstfeltet_ThenGjøresIngenTjenerrundtur()
    {
        // Regression guard. The field used to be value="@_sok" + @oninput, which on
        // helsedata's Blazor Server circuit is one round-trip per keystroke — and the
        // re-render each round-trip triggers rewrote the element while more input was
        // still arriving, so a fast fill lost characters ("svelging" arrived as "sng").
        // No registered oninput handler means the browser event never reaches the circuit,
        // and bUnit says so by refusing to dispatch it.
        var client = new FakeClient(EnSide());
        var cut = RenderMed(client);

        var input = cut.Find("input[type=search]");

        Assert.Throws<MissingEventHandlerException>(() => input.Input("svelging"));
        Assert.Equal(1, client.Kall); // only the initial load
    }

    [Fact]
    public void Sok_NårHeleTekstenErSkrevetFørInnsending_ThenSøkesDetÉnGangMedHeleTeksten()
    {
        var client = new FakeClient(EnSide());
        var cut = RenderMed(client);

        // onchange carries the finished value, however fast it was typed or pasted.
        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal("svelging", client.SisteSok);
        Assert.Equal(2, client.Kall); // initial load + this one search
    }

    [Fact]
    public void Sok_NårBrukerenKlikkerSøkUtenÅForlateFeltetFørst_ThenSøkesDetMedHeleTeksten()
    {
        // The case onchange has to survive: type, then go straight for the Søk button
        // without tabbing away. The browser blurs the field as the button takes focus, so
        // change reaches the circuit before the click turns into a submit — this test
        // pins that order, and would fail if the value only ever arrived on blur-by-tab.
        var client = new FakeClient(EnSide());
        var cut = RenderMed(client);

        cut.Find("input[type=search]").Change("svelging"); // blur caused by the click
        cut.Find("button[type=submit]").Click();

        Assert.Equal("svelging", client.SisteSok);
        Assert.Equal(2, client.Kall);
    }

    [Fact]
    public void Render_Alltid_ThenSøkefeltetHarKoblaLedetekst()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        var input = cut.Find("input[type=search]");
        var label = cut.Find("label");

        Assert.Equal(input.Id, label.GetAttribute("for"));
    }

    // ---------------------------------------------------------------------------------
    // Accessibility. helsedata.no is a public-sector site, so WCAG 2.1 AA is a legal
    // requirement there — and this is our markup on their page. Each test below pins one
    // property a screen-reader or keyboard user depends on, so it cannot quietly go away.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Alltid_ThenErStatuslinjaEtHøfligOgAtomiskStatusområde()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        var status = cut.Find("p.variabelutforsker-status");

        // role + aria-live together, because older screen readers honour one or the other.
        // aria-atomic so the whole sentence is read: hearing "12" on its own is not news.
        Assert.Equal("status", status.GetAttribute("role"));
        Assert.Equal("polite", status.GetAttribute("aria-live"));
        Assert.Equal("true", status.GetAttribute("aria-atomic"));
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenNevnerStatuslinjaBådeAntalletOgSøkeordet()
    {
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        var status = cut.Find("p.variabelutforsker-status").TextContent;

        Assert.Contains("1 variabel funnet", status);
        Assert.Contains("«tale»", status);
    }

    [Fact]
    public void Render_NårIngenTreff_ThenSierMeldingaHvilketSøkSomIkkeGaTreff()
    {
        var cut = RenderMed(new FakeClient(EnSide()), b => b.Add(c => c.Sok, "svelging"));

        Assert.Contains("Ingen variabler passet søket «svelging»",
                        cut.Find("p.variabelutforsker-status").TextContent);
    }

    [Fact]
    public void Render_NårBareFørsteSideVises_ThenSierSammendragetHvorMangeAvTotalen()
    {
        // 25 rows captioned "312 variabler" would be a lie to whoever cannot see the table.
        var side = new Side<VariabelSammendrag>
        {
            Items = [Variabel("1. Tale", "K1"), Variabel("2. Spytt", "K2")],
            TotalCount = 312,
            Page = 1,
            Size = 25,
            TotalPages = 156
        };

        var cut = RenderMed(new FakeClient(side));

        Assert.Contains("Viser 2 av 312 variabler funnet",
                        cut.Find("p.variabelutforsker-status").TextContent);
    }

    [Fact]
    public void Sok_NårFeltetEndresUtenÅSendes_ThenBeskriverStatusFortsattSøketSomGaRadene()
    {
        // @bind writes the field on blur, so the box can hold an unsubmitted query while the
        // table still shows the previous result. The announcement follows the table.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        cut.Find("input[type=search]").Change("noe helt annet");

        Assert.Contains("«tale»", cut.Find("p.variabelutforsker-status").TextContent);
    }

    [Fact]
    public void Render_NårApietFeiler_ThenMeldesFeilenAssertivtOgSierHvaBrukerenKanGjøre()
    {
        var cut = RenderMed(new FeilendeClient());

        var varsel = cut.Find("[role='alert']");

        Assert.Equal("assertive", varsel.GetAttribute("aria-live"));
        Assert.Contains("Kunne ikke hente variabler", varsel.TextContent);
        Assert.Contains("Prøv igjen", varsel.TextContent); // a way out, not just bad news
    }

    [Fact]
    public void Render_NårIngentingErGalt_ThenFinnesVarselområdetLikevelOgErTomt()
    {
        // A role="alert" element inserted and filled in the same DOM update is announced
        // unreliably; one already sitting in the document is not. So it is always rendered.
        var cut = RenderMed(new FakeClient(EnSide()));

        var varsel = cut.Find("[role='alert']");

        Assert.Equal(string.Empty, varsel.TextContent.Trim());
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenHarTabellenEtTilgjengeligNavn()
    {
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        var caption = cut.Find("table caption");

        Assert.Contains("1 variabel funnet", caption.TextContent);
        Assert.Contains("«tale»", caption.TextContent);
        // Hidden from the eye only — the same sentence is already visible in the status line.
        Assert.Contains("variabelutforsker-visuelt-skjult", caption.ClassName);
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenHarAlleKolonneoverskrifterScopeCol()
    {
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))));

        var overskrifter = cut.FindAll("thead th");

        Assert.Equal(4, overskrifter.Count);
        Assert.All(overskrifter, th => Assert.Equal("col", th.GetAttribute("scope")));
    }

    [Fact]
    public void Render_NårTabellenVises_ThenErRulleområdetTastaturnåbartOgNavngitt()
    {
        // The wrapper scrolls sideways on narrow screens; a scroll box nothing can focus
        // cannot be scrolled from the keyboard at all.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))));

        var omslag = cut.Find("div.variabelutforsker-tabell-omslag");

        Assert.Equal("0", omslag.GetAttribute("tabindex"));
        Assert.Equal("region", omslag.GetAttribute("role"));
        Assert.Equal(cut.Find("caption").Id, omslag.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Render_NårEnRadManglerVerdier_ThenLesesCellaSomIkkeOppgittIStedetForEnStrek()
    {
        // "—" is either read as "em dash" or skipped in silence, depending on the reader's
        // punctuation setting. Neither says "we do not know".
        var utenKilde = new VariabelSammendrag { Id = Guid.NewGuid(), Code = "K", PreferredTerm = "Uten kilde" };

        var cut = RenderMed(new FakeClient(EnSide(utenKilde)));

        var kildeCelle = cut.FindAll("tbody td")[1];

        Assert.Equal("true", kildeCelle.QuerySelector("span[aria-hidden]")!.GetAttribute("aria-hidden"));
        Assert.Contains("Ikke oppgitt", kildeCelle.TextContent);
        Assert.Contains("variabelutforsker-visuelt-skjult", kildeCelle.InnerHtml);
    }

    [Fact]
    public void Render_NårVariabelenHarBeskrivelse_ThenSkillesKodeOgBeskrivelseAvEtMellomrom()
    {
        // Razor eats the whitespace around a code block, and the two spans are then read as
        // one word: "…ALSFRSR1TALEHvordan er talen?".
        var medBeskrivelse = new VariabelSammendrag
        {
            Id = Guid.NewGuid(),
            Code = "V_ALS.F1.TALE",
            PreferredTerm = "1. Tale",
            Beskrivelse = "Hvordan er talen?"
        };

        var cut = RenderMed(new FakeClient(EnSide(medBeskrivelse)));

        Assert.Contains("V_ALS.F1.TALE Hvordan er talen?", cut.Find("tbody td").TextContent);
    }

    [Fact]
    public void Render_NårSpråkErEn_ThenErDataradeneFortsattMerketSomNorske()
    {
        // The UI turns English; Munin's variable names do not. An English synthesiser
        // reading Norwegian terms is unintelligible (WCAG 3.1.2).
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sprak, "en"));

        Assert.Equal("no", cut.Find("tbody").GetAttribute("lang"));
    }

    [Fact]
    public void Render_NårHostenIkkeSierNoe_ThenErTittelenH2()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        Assert.Equal("Variabelutforsker", cut.Find("h2").TextContent);
    }

    [Fact]
    public void Render_NårHostenSetterOverskriftsnivå_ThenBrukesDetNivåetOgSeksjonenPekerPåDet()
    {
        // The level that keeps a page outline unbroken is only knowable at the mount site.
        var cut = RenderMed(new FakeClient(EnSide()), b => b.Add(c => c.OverskriftNivaa, 3));

        var overskrift = cut.Find("h3");

        Assert.Equal("Variabelutforsker", overskrift.TextContent);
        Assert.Empty(cut.FindAll("h2"));
        Assert.Equal(overskrift.Id, cut.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Render_NårOverskriftsnivåetErUtenforOmrådet_ThenKlemmesDetInnI1Til6()
    {
        // An <h9> is not a heading at all, which would be a worse failure than an
        // approximately-right level.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(EnSide()));

        var lavt = Render<Variabelutforsker>(b => b.Add(c => c.OverskriftNivaa, 0));
        var hoyt = Render<Variabelutforsker>(b => b.Add(c => c.OverskriftNivaa, 9));

        Assert.NotEmpty(lavt.FindAll("h1"));
        Assert.NotEmpty(hoyt.FindAll("h6"));
    }

    [Fact]
    public void Render_Alltid_ThenErSøkelandemerketNavngittEtterInstansen()
    {
        // Two explorers on one page otherwise leave two identical, unnamed "search"
        // entries in a screen reader's landmark list.
        var cut = RenderMed(new FakeClient(EnSide()));

        var form = cut.Find("form[role='search']");

        Assert.Equal(cut.Find("h2").Id, form.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Render_ToInstanserPåSammeSide_ThenErOgsåOverskriftOgSammendragUnike()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))));

        var a = Render<Variabelutforsker>();
        var b = Render<Variabelutforsker>();

        Assert.NotEqual(a.Find("h2").Id, b.Find("h2").Id);
        Assert.NotEqual(a.Find("caption").Id, b.Find("caption").Id);
    }

    [Fact]
    public void Sok_NårEtSøkPågår_ThenDeaktiveresIkkeSøkeknappen()
    {
        // Disabling the element that has focus drops focus to <body>: press Enter on Søk
        // and a keyboard user starts tabbing from the top of the page again.
        var cut = RenderMed(new TregClient());

        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Contains("Henter variabler", cut.Find("p.variabelutforsker-status").TextContent);
    }

    [Fact]
    public void Sok_NårEtSøkAlleredePågår_ThenIgnoreresNyInnsending()
    {
        // What the disabled attribute used to do, without taking focus away to do it.
        var client = new TregClient();
        var cut = RenderMed(client);

        cut.Find("form").Submit();
        cut.Find("form").Submit();

        Assert.Equal(1, client.Kall);
    }

    [Fact]
    public async Task Sok_NårSvaretKommer_ThenErstatterResultatetLastemeldinga()
    {
        // One shared status region, so the messages replace each other instead of stacking.
        var client = new TregClient();
        var cut = RenderMed(client);

        await cut.InvokeAsync(() => client.Svar(EnSide(Variabel("1. Tale", "KODE"))));

        cut.WaitForAssertion(() =>
        {
            var status = cut.Find("p.variabelutforsker-status").TextContent;
            Assert.Contains("1 variabel funnet", status);
            Assert.DoesNotContain("Henter variabler", status);
        });
    }
}
