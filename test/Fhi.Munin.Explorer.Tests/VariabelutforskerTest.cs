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
    public void Render_Alltid_ThenSøkefeltetHarKoblaLedetekst()
    {
        var cut = RenderMed(new FakeClient(EnSide()));

        var input = cut.Find("input[type=search]");
        var label = cut.Find("label");

        Assert.Equal(input.Id, label.GetAttribute("for"));
    }
}
