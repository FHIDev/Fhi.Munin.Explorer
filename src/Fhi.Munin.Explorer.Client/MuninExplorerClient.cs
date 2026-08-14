using System.Net;
using System.Net.Http.Json;
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

        using var response = await httpClient.GetAsync(url, cancellationToken);

        // An empty result is a normal answer to a search, not an error worth throwing over.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new Side<VariabelSammendrag>();
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Side<VariabelSammendrag>>(Json, cancellationToken)
               ?? new Side<VariabelSammendrag>();
    }
}
