using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// <see cref="IMuninExplorerClient"/> over the public Munin Explorer API.
/// </summary>
internal sealed class MuninExplorerClient(HttpClient httpClient) : IMuninExplorerClient
{
    // Shared by the client and any test host, so a serialisation difference cannot
    // quietly appear between them.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Side<VariabelSammendrag>> SokVariablerAsync(
        string? sok,
        int side = 1,
        int sideStorrelse = 25,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/explorer/variables?page={side}&size={sideStorrelse}";
        if (!string.IsNullOrWhiteSpace(sok))
        {
            url += $"&search={Uri.EscapeDataString(sok)}";
        }

        // An empty result is a normal answer to a search, not an error worth throwing over.
        return await HentAsync<Side<VariabelSammendrag>>(url, cancellationToken) ?? new Side<VariabelSammendrag>();
    }

    public async Task<Filtervalg> HentFiltreAsync(
        string? sok = null,
        string? kildeType = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/explorer/filters" + Sporring(("search", sok), ("kildeType", kildeType));

        // No facets is a legitimate answer to a narrow search — same reasoning as an empty page.
        return await HentAsync<Filtervalg>(url, cancellationToken) ?? new Filtervalg();
    }

    public async Task<IReadOnlyList<KildeSammendrag>> HentKilderAsync(
        string? sok = null,
        string? kildeType = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/explorer/kilder" + Sporring(("search", sok), ("kildeType", kildeType));

        return await HentAsync<IReadOnlyList<KildeSammendrag>>(url, cancellationToken) ?? [];
    }

    public Task<KildeDetalj?> HentKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
        HentAsync<KildeDetalj>($"api/explorer/kilder/{id}", cancellationToken);

    public Task<KildeHierarki?> HentKildeHierarkiAsync(Guid id, CancellationToken cancellationToken = default) =>
        HentAsync<KildeHierarki>($"api/explorer/kilder/{id}/hierarchy", cancellationToken);

    public Task<DatasamlingDetalj?> HentDatasamlingAsync(Guid id, CancellationToken cancellationToken = default) =>
        HentAsync<DatasamlingDetalj>($"api/explorer/datasamling/{id}", cancellationToken);

    public Task<VariabelDetalj?> HentVariabelAsync(
        Guid id,
        bool inkluderHistoriske = false,
        CancellationToken cancellationToken = default)
    {
        // Only sent when true: the API defaults to false, and a shorter URL caches better.
        var url = $"api/explorer/variables/{id}" + (inkluderHistoriske ? "?includeHistorical=true" : "");

        return HentAsync<VariabelDetalj>(url, cancellationToken);
    }

    public async Task<IReadOnlyList<Variabelversjon>> HentVariabelTidslinjeAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await HentAsync<IReadOnlyList<Variabelversjon>>($"api/explorer/variables/{id}/timeline", cancellationToken) ?? [];

    /// <summary>
    /// GET and deserialise, mapping 404 to null.
    /// </summary>
    /// <remarks>
    /// The explorer is a public page reachable by deep link, so a request for something that has
    /// been unpublished — or for an id someone typed — is an ordinary event the caller should be
    /// able to render as "not found". Every other failure still throws.
    /// </remarks>
    private async Task<T?> HentAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    /// <summary>Builds <c>?a=1&amp;b=2</c> from the parameters that actually have a value.</summary>
    private static string Sporring(params (string Navn, string? Verdi)[] parametere)
    {
        var query = new StringBuilder();

        foreach (var (navn, verdi) in parametere)
        {
            if (string.IsNullOrWhiteSpace(verdi))
            {
                continue;
            }

            query.Append(query.Length == 0 ? '?' : '&')
                 .Append(navn)
                 .Append('=')
                 .Append(Uri.EscapeDataString(verdi));
        }

        return query.ToString();
    }
}
