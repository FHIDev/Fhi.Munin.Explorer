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

    private sealed class FakeClient(Side<VariabelSammendrag> svar) : TomMuninExplorerKlient
    {
        public string? SisteSok { get; private set; }
        public int Kall { get; private set; }

        public override Task<Side<VariabelSammendrag>> SokVariablerAsync(
            string? sok, int side = 1, int sideStorrelse = 25, CancellationToken cancellationToken = default)
        {
            SisteSok = sok;
            Kall++;
            return Task.FromResult(svar);
        }
    }

    private sealed class FeilendeClient : TomMuninExplorerKlient
    {
        public override Task<Side<VariabelSammendrag>> SokVariablerAsync(
            string? sok, int side = 1, int sideStorrelse = 25, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("nede");
    }

    /// <summary>
    /// A client that never answers until the test lets it, so the loading state can be inspected.
    /// Given a <paramref name="forsteSvar"/> it answers the first call at once and stalls only on
    /// the next one — the case where a second search is in flight over rows already on screen.
    /// </summary>
    private sealed class TregClient(Side<VariabelSammendrag>? forsteSvar = null) : TomMuninExplorerKlient
    {
        private readonly TaskCompletionSource<Side<VariabelSammendrag>> _svar =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Kall { get; private set; }

        public override Task<Side<VariabelSammendrag>> SokVariablerAsync(
            string? sok, int side = 1, int sideStorrelse = 25, CancellationToken cancellationToken = default)
        {
            Kall++;
            return Kall == 1 && forsteSvar is not null ? Task.FromResult(forsteSvar) : _svar.Task;
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
    public void Render_NårSøketGirTreff_ThenViserKortPerVariabel()
    {
        var client = new FakeClient(EnSide(Variabel("1. Tale", "V_ALS.F1.ALSFRSR1TALE"),
                                           Variabel("2. Spyttsekresjon", "V_ALS.F1.ALSFRSR2SPYTT")));

        var cut = RenderMed(client);

        Assert.Equal(2, cut.FindAll("ul.datasourcecard-list > li").Count);
        Assert.Contains("1. Tale", cut.Markup);
        Assert.Contains("V_ALS.F1.ALSFRSR1TALE", cut.Markup);
        Assert.Contains("2 variabler", cut.Markup);
    }

    [Fact]
    public void Render_NårIngenTreff_ThenViserTomMelding()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        Assert.Empty(cut.FindAll("ul.datasourcecard-list > li"));
        Assert.Contains("Ingen variabler passet søket", cut.Markup);
    }

    [Fact]
    public void Render_NårApietFeiler_ThenViserFeilmeldingIStedetForÅKaste()
    {
        var cut = RenderMed(new FeilendeClient());

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("ul.datasourcecard-list > li"));
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

        var status = cut.Find("p[role='status']");

        // role + aria-live together, because older screen readers honour one or the other.
        // aria-atomic so the whole sentence is read: hearing "12" on its own is not news.
        Assert.Equal("status", status.GetAttribute("role"));
        Assert.Equal("polite", status.GetAttribute("aria-live"));
        Assert.Equal("true", status.GetAttribute("aria-atomic"));
    }

    // ---------------------------------------------------------------------------------
    // Styling contract. The package ships no CSS, so every class name it emits has to be
    // one Fhi.Helsedata.Stiler already defines — otherwise the host stylesheet has never
    // heard of it and the element renders as a raw browser default inside a styled page.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Alltid_ThenBrukesStilerSineKlassenavnPåSøkefeltet()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        Assert.Equal("form-element__label", cut.Find("label").ClassName);
        Assert.Equal("searchbox__freetext", cut.Find("input[type=search]").ClassName);
        Assert.NotNull(cut.Find("div.searchbox__freetext-container"));

        // hd-button-square carries the shape, button-square--primary the colour, and
        // searchbox__freetext-submit-button places it inside the field's reserved padding.
        var knapp = cut.Find("button[type=submit]").ClassName!;
        Assert.Contains("hd-button-square", knapp);
        Assert.Contains("button-square--primary", knapp);
        Assert.Contains("searchbox__freetext-submit-button", knapp);
    }

    [Fact]
    public void Render_Alltid_ThenFinnesIngenOppfunneKlassenavnUtenomRotkroken()
    {
        // The root class is a DOM handle, not a style hook — nothing styles it. Everything
        // else has to come from Stiler, and this is the guard that says so out loud.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        var oppfunne = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => k.StartsWith("variabelutforsker", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.Equal(["variabelutforsker"], oppfunne);
        Assert.Equal("variabelutforsker", cut.Find("section").ClassName);
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenBrukesStilerSittKortoppsettTilResultatene()
    {
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))));

        Assert.NotNull(cut.Find("ul.datasourcecard-list > li.datasourcecard-list__item > div.datasourcecard"));
        Assert.NotNull(cut.Find(".datasourcecard__heading"));
        Assert.NotNull(cut.Find(".datasourcecard__info > .datasourcecard__info--text"));
    }

    [Fact]
    public void Render_NårApietFeiler_ThenFårFeilmeldingaStilerSinInfoboks()
    {
        var cut = RenderMed(new FeilendeClient());

        Assert.Contains("infobox", cut.Find("[role='alert'] p").ClassName!);
    }

    [Fact]
    public void Render_NårIngentingErGalt_ThenTegnesIngenTomInfoboks()
    {
        // The alert container is always in the document (see below), so it must carry no
        // class of its own — an `infobox` there would paint an empty coloured box on every
        // page that has nothing to report.
        var cut = RenderMed(new FakeClient(EnSide()));

        Assert.False(cut.Find("[role='alert']").HasAttribute("class"));
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenNevnerStatuslinjaBådeAntalletOgSøkeordet()
    {
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        var status = cut.Find("p[role='status']").TextContent;

        Assert.Contains("1 variabel funnet", status);
        Assert.Contains("«tale»", status);
    }

    [Fact]
    public void Render_NårIngenTreff_ThenSierMeldingaHvilketSøkSomIkkeGaTreff()
    {
        var cut = RenderMed(new FakeClient(EnSide()), b => b.Add(c => c.Sok, "svelging"));

        Assert.Contains("Ingen variabler passet søket «svelging»",
                        cut.Find("p[role='status']").TextContent);
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
                        cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Sok_NårFeltetEndresUtenÅSendes_ThenBeskriverStatusFortsattSøketSomGaRadene()
    {
        // @bind writes the field on blur, so the box can hold an unsubmitted query while the
        // table still shows the previous result. The announcement follows the table.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        cut.Find("input[type=search]").Change("noe helt annet");

        Assert.Contains("«tale»", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Render_NårApietFeiler_ThenMeldesFeilenAssertivtOgSierHvaBrukerenKanGjøre()
    {
        var cut = RenderMed(new FeilendeClient());

        var varsel = cut.Find("[role='alert']");

        Assert.Equal("assertive", varsel.GetAttribute("aria-live"));
        Assert.Equal("true", varsel.GetAttribute("aria-atomic"));
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
    public void Render_NårSøketGirTreff_ThenHarResultatlistaEtTilgjengeligNavn()
    {
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sok, "tale"));

        // aria-label rather than a clipped <caption>: Stiler has no visually-hidden rule, so
        // markup that needs one is markup that shows its scaffolding on helsedata's page.
        var navn = cut.Find("ul.datasourcecard-list").GetAttribute("aria-label")!;

        Assert.Contains("1 variabel funnet", navn);
        Assert.Contains("«tale»", navn);
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenErHvertResultatEnOverskriftEttNivåUnderTittelen()
    {
        // Real headings per result are what let a screen-reader user move between them with
        // the heading rotor. One level below the component's own title keeps the outline whole.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.OverskriftNivaa, 3));

        var kortoverskrift = cut.Find("li h4");

        Assert.Equal("1. Tale", kortoverskrift.TextContent);
        Assert.Equal("datasourcecard__heading", kortoverskrift.ClassName);
    }

    [Fact]
    public void Render_NårSøketGirTreff_ThenErHvertFeltMerketMedHvaDetEr()
    {
        // A table had column headers doing this job. A card has nothing, and "Inklusjon" on
        // its own does not say which field it is.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "V_ALS.F1.TALE"))));

        var info = cut.Find(".datasourcecard__info").TextContent;

        Assert.Contains("Kode: V_ALS.F1.TALE", info);
        Assert.Contains("Datakilde: Als registeret", info);
        Assert.Contains("Datasamling: Inklusjon", info);
        Assert.Contains("Periode: 2010–2025", info);
    }

    [Fact]
    public void Render_NårResultateneVises_ThenErListaMerketSomOpptattUtenEkstraTabbstopp()
    {
        // The table version wrapped itself in a focusable scroll box, because a box that
        // scrolls sideways and cannot be focused cannot be scrolled from the keyboard. Cards
        // wrap instead of scrolling, so that tab stop is gone rather than merely moved.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))));

        var liste = cut.Find("ul.datasourcecard-list");

        Assert.Equal("false", liste.GetAttribute("aria-busy"));
        Assert.False(liste.HasAttribute("tabindex"));
        Assert.Empty(cut.FindAll("[tabindex]"));
    }

    [Fact]
    public void Render_NårEnVerdiMangler_ThenSkrivesIkkeOppgittSynligForAlle()
    {
        // "—" is either read as "em dash" or skipped in silence, depending on the reader's
        // punctuation setting. Neither says "we do not know". The words used to be there but
        // clipped out of sight for everyone except a screen reader; now they are simply there,
        // which needs no visually-hidden rule from the host — and Stiler has none to give.
        var utenKilde = new VariabelSammendrag { Id = Guid.NewGuid(), Code = "K", PreferredTerm = "Uten kilde" };

        var cut = RenderMed(new FakeClient(EnSide(utenKilde)));

        var info = cut.Find(".datasourcecard__info");

        Assert.Contains("Datakilde: Ikke oppgitt", info.TextContent);
        Assert.Contains("Periode: Ikke oppgitt", info.TextContent);
        Assert.DoesNotContain("—", info.TextContent);
    }

    [Fact]
    public void Render_NårVariabelenHarBeskrivelse_ThenStårDenForSegSelvUnderNøkkelopplysningene()
    {
        // The code and the description used to be two adjacent spans in one table cell, and
        // Razor eats the whitespace between them: "…ALSFRSR1TALEHvordan er talen?". They are
        // now different parts of the card, so nothing can run them together.
        var medBeskrivelse = new VariabelSammendrag
        {
            Id = Guid.NewGuid(),
            Code = "V_ALS.F1.TALE",
            PreferredTerm = "1. Tale",
            Beskrivelse = "Hvordan er talen?"
        };

        var cut = RenderMed(new FakeClient(EnSide(medBeskrivelse)));

        Assert.Equal("Hvordan er talen?", cut.Find(".datasourcecard__intro p").TextContent);
        Assert.DoesNotContain("Hvordan er talen?", cut.Find(".datasourcecard__info").TextContent);
    }

    [Fact]
    public void Render_NårSpråkErEn_ThenErSjølveMetadataenFortsattMerketSomNorsk()
    {
        // The UI turns English; Munin's variable names do not. An English synthesiser
        // reading Norwegian terms is unintelligible (WCAG 3.1.2). The mark sits on the data
        // rather than on the whole list, so the English field labels around it are not
        // announced as Norwegian too.
        var cut = RenderMed(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))),
                            b => b.Add(c => c.Sprak, "en"));

        Assert.Equal("no", cut.Find(".datasourcecard__heading").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".datasourcecard__info--text span[lang]").GetAttribute("lang"));
        Assert.False(cut.Find("ul.datasourcecard-list").HasAttribute("lang"));
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
    public void Render_ToInstanserPåSammeSide_ThenErOgsåOverskrifteneUnike()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(EnSide(Variabel("1. Tale", "KODE"))));

        var a = Render<Variabelutforsker>();
        var b = Render<Variabelutforsker>();

        // The title id is what both the section and the search landmark are named by, so a
        // collision would leave a screen reader with two identically named landmarks.
        Assert.NotEqual(a.Find("h2").Id, b.Find("h2").Id);
    }

    [Fact]
    public void Sok_NårEtSøkPågår_ThenDeaktiveresIkkeSøkeknappen()
    {
        // Disabling the element that has focus drops focus to <body>: press Enter on Søk
        // and a keyboard user starts tabbing from the top of the page again.
        var cut = RenderMed(new TregClient());

        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Contains("Henter variabler", cut.Find("p[role='status']").TextContent);
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
    public async Task Sok_NårResultateneErForeldetAvEtNyttSøk_ThenMerkesListaSomOpptatt()
    {
        // The previous cards stay on screen while the next search runs, so they are stale
        // rather than current — aria-busy is what says so to a screen reader.
        var treff = EnSide(Variabel("1. Tale", "KODE"));
        var client = new TregClient(treff);
        var cut = RenderMed(client);

        Assert.Equal("false", cut.Find("ul.datasourcecard-list").GetAttribute("aria-busy"));

        cut.Find("form").Submit(); // second search, still in flight

        Assert.Equal("true", cut.Find("ul.datasourcecard-list").GetAttribute("aria-busy"));

        await cut.InvokeAsync(() => client.Svar(treff));

        cut.WaitForAssertion(() =>
            Assert.Equal("false", cut.Find("ul.datasourcecard-list").GetAttribute("aria-busy")));
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
            var status = cut.Find("p[role='status']").TextContent;
            Assert.Contains("1 variabel funnet", status);
            Assert.DoesNotContain("Henter variabler", status);
        });
    }
}
