using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// An <see cref="IMuninExplorerClient"/> where every endpoint answers "nothing".
/// </summary>
/// <remarks>
/// Component tests care about one endpoint each. Deriving from this keeps them from having to
/// restate the whole interface — and keeps adding an endpoint from touching every test fake.
/// </remarks>
internal abstract class TomMuninExplorerKlient : IMuninExplorerClient
{
    public virtual Task<Side<VariabelSammendrag>> SokVariablerAsync(
        string? sok, int side = 1, int sideStorrelse = 25,
        Sorteringsfelt sortering = Sorteringsfelt.Navn,
        Sorteringsretning retning = Sorteringsretning.Stigende,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Side<VariabelSammendrag>());

    public virtual Task<Filtervalg> HentFiltreAsync(
        string? sok = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Filtervalg());

    public virtual Task<IReadOnlyList<KildeSammendrag>> HentKilderAsync(
        string? sok = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KildeSammendrag>>([]);

    public virtual Task<KildeDetalj?> HentKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<KildeDetalj?>(null);

    public virtual Task<KildeHierarki?> HentKildeHierarkiAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<KildeHierarki?>(null);

    public virtual Task<DatasamlingDetalj?> HentDatasamlingAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<DatasamlingDetalj?>(null);

    public virtual Task<VariabelDetalj?> HentVariabelAsync(
        Guid id, bool inkluderHistoriske = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<VariabelDetalj?>(null);

    public virtual Task<IReadOnlyList<Variabelversjon>> HentVariabelTidslinjeAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Variabelversjon>>([]);
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a lambda and remembers what it was asked.
/// </summary>
/// <remarks>
/// The request message itself is disposed by <c>HttpClient</c> once the call completes, so what the
/// test needs to assert on is copied out here rather than kept as a reference.
/// </remarks>
internal sealed class StubbetHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> svar) : HttpMessageHandler
{
    public Uri? SisteUri { get; private set; }
    public IReadOnlyList<string> SisteKlientheader { get; private set; } = [];
    public AuthenticationHeaderValue? SisteAutorisasjon { get; private set; }
    public int Kall { get; private set; }

    /// <summary>Answers <c>200 OK</c> with the given JSON body to every request.</summary>
    public static StubbetHttpHandler Ok(string json) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    });

    /// <summary>Answers with the given status and an empty body.</summary>
    public static StubbetHttpHandler Status(HttpStatusCode status) => new(_ => new HttpResponseMessage(status));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Kall++;
        SisteUri = request.RequestUri;
        SisteKlientheader = request.Headers.TryGetValues(Explorer.Client.KlientHeaderHandler.Header, out var verdier)
            ? [.. verdier]
            : [];
        SisteAutorisasjon = request.Headers.Authorization;

        return Task.FromResult(svar(request));
    }
}

/// <summary>Responses captured from the live Munin Explorer API, embedded in the test assembly.</summary>
/// <remarks>
/// Round-tripping a real payload is the test that actually catches contract drift; a fixture
/// written by hand only ever proves the DTO agrees with itself. Re-capture with, for example,
/// <c>curl https://munin.skytest.fhi.no/api/explorer/filters</c> when an endpoint changes.
/// </remarks>
internal static class Testdata
{
    public static string Les(string filnavn)
    {
        var ressurs = $"Fhi.Munin.Explorer.Tests.Testdata.{filnavn}";

        using var strom = typeof(Testdata).Assembly.GetManifestResourceStream(ressurs)
            ?? throw new InvalidOperationException($"Fant ikke innebygde testdata '{ressurs}'.");
        using var leser = new StreamReader(strom, Encoding.UTF8);

        return leser.ReadToEnd();
    }
}
