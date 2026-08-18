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

    public async Task<Page<VariableSummary>> SearchVariablesAsync(
        string? search,
        int page = 1,
        int pageSize = 25,
        SortField sort = SortField.Default,
        SortDirection direction = SortDirection.Ascending,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/explorer/variables?page={page}&size={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        // Left off entirely at the default, the same reasoning as includeHistorical below: the API
        // already uses its default order ascending when neither parameter arrives, and a shorter URL
        // caches better on a public page. Once either differs both are sent, so the URL says which
        // order it asked for rather than leaving half of it implied.
        if ((sort, direction) != (SortField.Default, SortDirection.Ascending))
        {
            url += $"&sort={SortToken(sort)}&sortDir={DirectionToken(direction)}";
        }

        // An empty result is a normal answer to a search, not an error worth throwing over.
        return await GetOrNullAsync<Page<VariableSummary>>(url, cancellationToken) ?? new Page<VariableSummary>();
    }

    public async Task<FilterOptions> GetFiltersAsync(
        string? search = null,
        string? kildeType = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/explorer/filters" + Query(("search", search), ("kildeType", kildeType));

        // No facets is a legitimate answer to a narrow search — same reasoning as an empty page.
        return await GetOrNullAsync<FilterOptions>(url, cancellationToken) ?? new FilterOptions();
    }

    public async Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
        string? search = null,
        string? kildeType = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/explorer/kilder" + Query(("search", search), ("kildeType", kildeType));

        return await GetOrNullAsync<IReadOnlyList<KildeSummary>>(url, cancellationToken) ?? [];
    }

    public Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<KildeDetail>($"api/explorer/kilder/{id}", cancellationToken);

    public Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<KildeHierarchy>($"api/explorer/kilder/{id}/hierarchy", cancellationToken);

    public Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<DatasamlingDetail>($"api/explorer/datasamling/{id}", cancellationToken);

    public Task<VariableDetail?> GetVariableAsync(
        Guid id,
        bool includeHistorical = false,
        CancellationToken cancellationToken = default)
    {
        // Only sent when true: the API defaults to false, and a shorter URL caches better.
        var url = $"api/explorer/variables/{id}" + (includeHistorical ? "?includeHistorical=true" : "");

        return GetOrNullAsync<VariableDetail>(url, cancellationToken);
    }

    public async Task<IReadOnlyList<VariableVersion>> GetVariableTimelineAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await GetOrNullAsync<IReadOnlyList<VariableVersion>>($"api/explorer/variables/{id}/timeline", cancellationToken) ?? [];

    /// <summary>
    /// GET and deserialise, mapping 404 to null.
    /// </summary>
    /// <remarks>
    /// The explorer is a public page reachable by deep link, so a request for something that has
    /// been unpublished — or for an id someone typed — is an ordinary event the caller should be
    /// able to render as "not found". Every other failure still throws.
    /// </remarks>
    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    /// <summary>
    /// The API's own token for a sort order. Spelled out rather than derived from the enum name:
    /// <see cref="SortField.Default"/> goes over the wire as <c>name</c>, and an unrecognised token
    /// is not rejected by the API — it silently falls back to that same default order instead.
    /// </summary>
    /// <remarks>
    /// Every member has its own arm, and an unknown one throws rather than falling through to
    /// <c>name</c>. Falling through is the exact failure <see cref="SortField"/> was made a closed
    /// set to prevent: a member added without a token here would quietly ask for the default order
    /// while the UI claimed another, with nothing to notice it. Throwing makes that a bug report
    /// instead of a wrong list.
    /// </remarks>
    private static string SortToken(SortField sort) => sort switch
    {
        SortField.Default => "name",
        SortField.Kilde => "kilde",
        SortField.Datasamling => "datasamling",
        SortField.Variabelgruppe => "variabelgruppe",
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "No API sort token for this field.")
    };

    /// <summary>The API's own token for a direction — same reasoning as <see cref="SortToken"/>.</summary>
    private static string DirectionToken(SortDirection direction) => direction switch
    {
        SortDirection.Ascending => "asc",
        SortDirection.Descending => "desc",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "No API sort token for this direction.")
    };

    /// <summary>Builds <c>?a=1&amp;b=2</c> from the parameters that actually have a value.</summary>
    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var query = new StringBuilder();

        foreach (var (name, value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            query.Append(query.Length == 0 ? '?' : '&')
                 .Append(name)
                 .Append('=')
                 .Append(Uri.EscapeDataString(value));
        }

        return query.ToString();
    }
}
