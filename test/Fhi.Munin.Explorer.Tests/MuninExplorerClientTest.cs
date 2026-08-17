using System.Net;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The client against a stubbed transport: does a real API response round-trip into the
/// contracts, and does a missing resource come back as "nothing" rather than an exception.
/// </summary>
public class MuninExplorerClientTest
{
    private const string Basisadresse = "https://munin.skytest.fhi.no/";

    private static MuninExplorerClient Klient(HttpMessageHandler handler, string basisadresse = Basisadresse) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(basisadresse) });

    private static MuninExplorerClient MedSvar(string fixtur, out StubbetHttpHandler handler)
    {
        handler = StubbetHttpHandler.Ok(Testdata.Les(fixtur));
        return Klient(handler);
    }

    private static MuninExplorerClient MedStatus(HttpStatusCode status) => Klient(StubbetHttpHandler.Status(status));

    // ---------------------------------------------------------------- round-trip of real payloads

    [Fact]
    public async Task SokVariablerAsync_NårApietSvarerMedEkteRespons_ThenLesesSidenMedRader()
    {
        var side = await MedSvar("variables.json", out _).SokVariablerAsync(null);

        Assert.Equal(2, side.Items.Count);
        Assert.Equal(18289, side.TotalCount);
        Assert.Equal(9145, side.TotalPages);
        Assert.Equal("V_ALS.F1.ALSFRSR1TALE", side.Items[0].Code);
    }

    [Fact]
    public async Task HentFiltreAsync_NårApietSvarerMedEkteRespons_ThenLesesAlleFasettene()
    {
        var filtre = await MedSvar("filters.json", out _).HentFiltreAsync();

        Assert.Equal(3, filtre.KildeTyper.Count);
        Assert.Equal("befolkningsbasertHelseundersokelse", filtre.KildeTyper[0].Value);
        Assert.Equal(41, filtre.Kilder.Count);
        Assert.Equal(20, filtre.Instrumenter.Count);
        Assert.Equal(3876, filtre.KildeKodeverkCount);
        Assert.Equal(18289, filtre.TotalCount);

        // Datatypes arrive as bare codes with no label — the point of the note on DatatypeFasett.
        Assert.Equal(["1", "10", "2", "3", "4", "6", "7"], filtre.Datatyper.Select(d => d.Value));

        // A root-level variabelgruppe has no parent; the delkilde facet carries its kilde.
        Assert.Null(filtre.Variabelgrupper[0].ParentId);
        Assert.NotEqual(Guid.Empty, filtre.Delkilder[0].KildeId);

        // Kodeverk without a resolved name is expected, not a parse failure.
        Assert.Equal("3402", filtre.AdministrativtKodeverk[0].Oid);

        // Only the lower bound is known in the test environment.
        Assert.Equal(1868, filtre.DateRange?.Min?.Year);
        Assert.Null(filtre.DateRange?.Max);
    }

    [Fact]
    public async Task HentKilderAsync_NårApietSvarerMedEkteRespons_ThenLesesListenMedSammendrag()
    {
        var kilder = await MedSvar("kilder.json", out _).HentKilderAsync();

        Assert.Equal(3, kilder.Count);

        var als = kilder[0];
        Assert.Equal("K_ALS", als.Code);
        Assert.Equal("Als registeret", als.Navn);
        Assert.Equal("nasjonaltMedisinskKvalitetsregister", als.Kildetype);
        Assert.True(als.Aktiv);
        Assert.True(als.HarVariabelbeskrivelse);
        Assert.Equal(9, als.DatasamlingCount);
        Assert.Equal(230, als.TotalVariables);
        Assert.Null(als.HealthDcatScore); // never computed yet — see the note on the property
        Assert.Equal("alsregister@stolav.no", als.AdditionalProperties["Epost"]);
    }

    [Fact]
    public async Task HentKildeAsync_NårApietSvarerMedEkteRespons_ThenLesesDetaljenMedDatasamlinger()
    {
        var kilde = await MedSvar("kilde.json", out _).HentKildeAsync(Guid.NewGuid());

        Assert.NotNull(kilde);
        Assert.Equal("K_ALS", kilde.Code);
        Assert.Equal("Als registeret", kilde.PreferredTerm);
        Assert.Equal(230, kilde.TotalVariables);
        Assert.Equal(9, kilde.Datasamlinger.Count);
        Assert.Equal(72, kilde.PropertyMetadata.Count);
        Assert.Null(kilde.DataTo); // ongoing collection

        // Inheritance: no own dataansvarlig, but an effective one resolved from the kilde.
        var inklusjon = kilde.Datasamlinger[0];
        Assert.Equal("Inklusjon", inklusjon.Name);
        Assert.Equal(1, inklusjon.PresentationOrder);
        Assert.Null(inklusjon.Dataansvarlig);
        Assert.Equal("St. Olavs hospital HF", inklusjon.EffectiveDataansvarlig);
        Assert.Equal("nasjonaltMedisinskKvalitetsregister", inklusjon.EffectiveKildetype);

        // Property metadata is what makes the free-form bag renderable.
        var kode = kilde.PropertyMetadata.First(p => p.Key == "Code");
        Assert.Equal("Kode", kode.DisplayNameTranslations["no"]);
    }

    [Fact]
    public async Task HentKildeAsync_NårKildenHarDelkilder_ThenLesesDelkildetreetMedSineDatasamlinger()
    {
        var kilde = await MedSvar("kilde-med-delkilder.json", out _).HentKildeAsync(Guid.NewGuid());

        Assert.NotNull(kilde);
        Assert.Equal("The Tromsø study", kilde.PreferredTerm);
        Assert.Equal(5, kilde.Delkilder.Count);
        Assert.Equal(3, kilde.Datasamlinger.Count); // hanging directly off the kilde

        var tromso4 = kilde.Delkilder.First(d => d.Code == "K_TR.TR4");
        Assert.Equal(2, tromso4.Datasamlinger.Count);
        Assert.Empty(tromso4.Children);

        // A delkilde's datasamling points back at it, and inherits the university as dataansvarlig.
        var forsteBesok = tromso4.Datasamlinger[0];
        Assert.Equal(tromso4.Id, forsteBesok.ParentDelkildeId);
        Assert.Null(tromso4.Dataansvarlig);
        Assert.Equal("UiT The Arctic University of Norway", tromso4.EffectiveDataansvarlig);
    }

    [Fact]
    public async Task HentKildeHierarkiAsync_NårKildenHarDelkilder_ThenLesesHeleTreet()
    {
        var hierarki = await MedSvar("hierarchy.json", out _).HentKildeHierarkiAsync(Guid.NewGuid());

        Assert.NotNull(hierarki);
        Assert.Equal("The Tromsø study", hierarki.KildeName);
        Assert.Equal(5752, hierarki.TotalVariableCount);
        Assert.Equal(5, hierarki.Delkilder.Count);
        Assert.Equal(3, hierarki.DirectDatasamlinger.Count);

        var tromso5 = hierarki.Delkilder.First(d => d.Datasamlinger.Count == 4);
        Assert.Equal(1170, tromso5.VariableCount);

        var forsteBesok = tromso5.Datasamlinger[0];
        Assert.Equal(["ehds-cat:population-health-surveys"], forsteBesok.Categories);
        Assert.NotEmpty(forsteBesok.Variabelgrupper);
        Assert.All(forsteBesok.Variabelgrupper, g => Assert.NotEqual(Guid.Empty, g.Id));
    }

    [Fact]
    public async Task HentDatasamlingAsync_NårApietSvarerMedEkteRespons_ThenLesesDetaljenMedForelder()
    {
        var datasamling = await MedSvar("datasamling.json", out _).HentDatasamlingAsync(Guid.NewGuid());

        Assert.NotNull(datasamling);
        Assert.Equal("K_ALS.INKLUSJON", datasamling.Code);
        Assert.Equal("Inklusjon", datasamling.PreferredTerm);
        Assert.Equal("yearly", datasamling.StatistikkType);
        Assert.Equal(99, datasamling.VariableCount);
        Assert.Equal(18, datasamling.PropertyMetadata.Count);
        Assert.Equal("Als registeret", datasamling.ParentKildeNavn);
        Assert.Null(datasamling.ParentDelkildeId); // hangs directly off the kilde
        Assert.NotNull(datasamling.InklusjonsOgEksklusjonskriterier);

        // Own value absent, effective value inherited from the kilde.
        Assert.Null(datasamling.Lovverk);
        Assert.NotNull(datasamling.EffectiveLovverk);
    }

    [Fact]
    public async Task HentVariabelAsync_NårApietSvarerMedEkteRespons_ThenLesesDetaljenMedVersjonerOgKodeverk()
    {
        var variabel = await MedSvar("variable.json", out _).HentVariabelAsync(Guid.NewGuid());

        Assert.NotNull(variabel);
        Assert.Equal("V_ALS.F1.ALSFRSR1TALE", variabel.Code);
        Assert.Equal("Active", variabel.VersjonStatus);
        Assert.Equal("2", variabel.DataType);
        Assert.Equal("yearly", variabel.DatasamlingStatistikkType);
        Assert.Equal(33, variabel.PropertyMetadata.Count);
        Assert.Equal("ALSFRSR1Tale", variabel.AdditionalProperties["DatabaseReferanse"]);

        var versjon = Assert.Single(variabel.Versjoner);
        Assert.Equal(variabel.VersjonId, versjon.VersjonId);
        Assert.Null(versjon.GyldigTil); // still in force

        var lenke = Assert.Single(variabel.Kodeverklinker);
        Assert.Equal("Kildekodeverk", lenke.KodeverkType);
        Assert.True(lenke.HarKodeverdier);
        Assert.Null(lenke.DisplayName); // unresolved name is normal — fall back to the reference

        Assert.Equal("Funksjonsscore", Assert.Single(variabel.AlleVariabelgrupper).Name);
        Assert.Equal("Inklusjon", Assert.Single(variabel.AlleDatasamlinger).Name);
    }

    [Fact]
    public async Task HentVariabelTidslinjeAsync_NårApietSvarerMedEkteRespons_ThenLesesVersjonene()
    {
        var tidslinje = await MedSvar("timeline.json", out _).HentVariabelTidslinjeAsync(Guid.NewGuid());

        var versjon = Assert.Single(tidslinje);
        Assert.Equal("1. Tale", versjon.PreferredTerm);
        Assert.Equal("Active", versjon.Status);
        Assert.Equal(2010, versjon.GyldigFra?.Year);
        Assert.Null(versjon.PublishedAt); // not tracked for imported versions
        Assert.Equal("2", versjon.AdditionalProperties["DataType"]);
    }

    // ---------------------------------------------------------------------------- 404 and failure

    [Fact]
    public async Task HentKildeAsync_NårKildenIkkeFinnes_ThenNullIStedetForKast()
    {
        Assert.Null(await MedStatus(HttpStatusCode.NotFound).HentKildeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HentKildeHierarkiAsync_NårKildenIkkeFinnes_ThenNullIStedetForKast()
    {
        Assert.Null(await MedStatus(HttpStatusCode.NotFound).HentKildeHierarkiAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HentDatasamlingAsync_NårDatasamlingenIkkeFinnes_ThenNullIStedetForKast()
    {
        Assert.Null(await MedStatus(HttpStatusCode.NotFound).HentDatasamlingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HentVariabelAsync_NårVariabelenIkkeFinnes_ThenNullIStedetForKast()
    {
        Assert.Null(await MedStatus(HttpStatusCode.NotFound).HentVariabelAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HentVariabelTidslinjeAsync_NårVariabelenIkkeFinnes_ThenTomListeIStedetForKast()
    {
        Assert.Empty(await MedStatus(HttpStatusCode.NotFound).HentVariabelTidslinjeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HentKilderAsync_NårIngenKilderFinnes_ThenTomListeIStedetForKast()
    {
        Assert.Empty(await MedStatus(HttpStatusCode.NotFound).HentKilderAsync());
    }

    [Fact]
    public async Task HentFiltreAsync_NårIngenFasetterFinnes_ThenTommeFiltervalgIStedetForKast()
    {
        var filtre = await MedStatus(HttpStatusCode.NotFound).HentFiltreAsync();

        Assert.Empty(filtre.Kilder);
        Assert.Equal(0, filtre.TotalCount);
    }

    [Fact]
    public async Task HentKildeAsync_NårApietFeiler_ThenKastesDetVidere()
    {
        // A 500 is a fault the caller has to be able to tell apart from "not published".
        var klient = MedStatus(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => klient.HentKildeAsync(Guid.NewGuid()));
    }

    // ------------------------------------------------------------------------------ URL-bygging

    [Fact]
    public async Task HentKilderAsync_NårSøkOgKildetypeErSatt_ThenSendesBeggeSomSpørringsparametre()
    {
        var handler = StubbetHttpHandler.Ok("[]");

        await Klient(handler).HentKilderAsync("hjerte og kar", "sentraltHelseregister");

        Assert.Equal("/api/explorer/kilder", handler.SisteUri?.AbsolutePath);
        Assert.Equal("?search=hjerte%20og%20kar&kildeType=sentraltHelseregister", handler.SisteUri?.Query);
    }

    [Fact]
    public async Task HentKilderAsync_NårIngenParametreErSatt_ThenSendesIngenSpørring()
    {
        var handler = StubbetHttpHandler.Ok("[]");

        await Klient(handler).HentKilderAsync();

        Assert.Equal("", handler.SisteUri?.Query);
    }

    [Theory]
    [InlineData(Sorteringsfelt.Navn, Sorteringsretning.Synkende, "name", "desc")]
    [InlineData(Sorteringsfelt.Kilde, Sorteringsretning.Stigende, "kilde", "asc")]
    [InlineData(Sorteringsfelt.Datasamling, Sorteringsretning.Stigende, "datasamling", "asc")]
    [InlineData(Sorteringsfelt.Variabelgruppe, Sorteringsretning.Synkende, "variabelgruppe", "desc")]
    public async Task SokVariablerAsync_NårSorteringErValgt_ThenSendesApietsEgneTokens(
        Sorteringsfelt felt, Sorteringsretning retning, string sort, string sortDir)
    {
        // The API takes these as free text and quietly falls back to the name sort for anything it
        // does not recognise, so a wrong token would not fail — it would return a different order
        // than the UI says it is showing. Hence the tokens are pinned here.
        var handler = StubbetHttpHandler.Ok("{}");

        await Klient(handler).SokVariablerAsync(null, sortering: felt, retning: retning);

        Assert.Equal($"?page=1&size=25&sort={sort}&sortDir={sortDir}", handler.SisteUri?.Query);
    }

    [Fact]
    public async Task SokVariablerAsync_NårSorteringaErStandard_ThenSendesIngenSorteringsparametre()
    {
        // Name ascending is what the API does when neither parameter arrives, so sending them
        // would only make the URL longer — and a shorter URL caches better on a public page.
        var handler = StubbetHttpHandler.Ok("{}");

        await Klient(handler).SokVariablerAsync(null);

        Assert.Equal("?page=1&size=25", handler.SisteUri?.Query);
    }

    [Fact]
    public async Task HentVariabelAsync_NårHistoriskeSkalMed_ThenSendesIncludeHistorical()
    {
        var handler = StubbetHttpHandler.Status(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        await Klient(handler).HentVariabelAsync(id, inkluderHistoriske: true);

        Assert.Equal($"/api/explorer/variables/{id}", handler.SisteUri?.AbsolutePath);
        Assert.Equal("?includeHistorical=true", handler.SisteUri?.Query);
    }

    [Fact]
    public async Task HentVariabelAsync_NårHistoriskeIkkeSkalMed_ThenSendesIngenSpørring()
    {
        var handler = StubbetHttpHandler.Status(HttpStatusCode.NotFound);

        await Klient(handler).HentVariabelAsync(Guid.NewGuid());

        Assert.Equal("", handler.SisteUri?.Query);
    }

    [Fact]
    public async Task HentKildeAsync_NårBasisadressenHarEnSti_ThenBeholdesStien()
    {
        // The relative URLs only stay under a hosted path if the base address keeps its trailing
        // slash — without it, "api/explorer/..." replaces the last segment instead of extending it.
        var handler = StubbetHttpHandler.Status(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        await Klient(handler, "https://helsedata.no/munin/").HentKildeAsync(id);

        Assert.Equal($"https://helsedata.no/munin/api/explorer/kilder/{id}", handler.SisteUri?.ToString());
    }
}
