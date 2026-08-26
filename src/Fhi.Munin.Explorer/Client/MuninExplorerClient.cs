using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        VariableFilter? filter = null,
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

        url = WithFilter(url, filter);

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
        VariableFilter? filter = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // The same narrowing the variable search was given, so the counts describe the list beside
        // them. The API is what makes a facet not narrow itself — see the remarks on the interface.
        var url = WithFilter("api/explorer/filters" + Query(("search", search)), filter);

        // No facets is a legitimate answer to a narrow search — same reasoning as an empty page.
        return await GetOrNullAsync<FilterOptions>(url, cancellationToken, language)
               ?? new FilterOptions();
    }

    public async Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
        string? search = null,
        string? kildeType = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/explorer/kilder" + Query(("search", search), ("kildeType", kildeType));

        return await GetOrNullAsync<IReadOnlyList<KildeSummary>>(url, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(
        CancellationToken cancellationToken = default) =>
        // The route is the API's own spelling — a sibling of api/explorer/kilder rather than a
        // field on it, because the vocabulary is one row per key and not one per kilde. No
        // Accept-Language: the entries carry optionsJson with every label in it, and a caller that
        // renders one response to readers in two languages is the case that field exists for.
        await GetOrNullAsync<IReadOnlyList<PropertyMetadataEntry>>(
            "api/explorer/kilder/egenskaper", cancellationToken) ?? [];

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

    public Task<KodeverkCodes?> GetKodeverkCodesAsync(
        Guid variableId,
        string kodeverkType,
        string kodeverkReference,
        CancellationToken cancellationToken = default)
    {
        // Both segments are escaped, and both need it for a different reason. The type is one of
        // three enum names today and safe as it stands, but it is the API's vocabulary rather than
        // ours and is passed through verbatim. The reference genuinely varies: V-AK sends dotted
        // OIDs, V-KK sends integers, and helsefaglige references like "NCMP-NCSP-NCRP" are free
        // text — a reference carrying a slash would otherwise be read as two path segments and
        // answer 404 for a link the catalogue does publish. Escaping is not enough on its own for
        // a dot segment, which no escaping survives; see EscapePathSegment.
        var url = $"api/explorer/variables/{variableId}"
                  + $"/kodeverk/{EscapePathSegment(kodeverkType, nameof(kodeverkType))}"
                  + $"/{EscapePathSegment(kodeverkReference, nameof(kodeverkReference))}/codes";

        return GetOrNullAsync<KodeverkCodes>(url, cancellationToken);
    }

    // ------------------------------------------------------------------ the user's own variable lists

    public async Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
        // A user with no lists is answered with an empty array rather than a 404, so the null arm
        // here is only ever the endpoint moving. It reads as "no lists" either way, which is the
        // same bargain GetKilderAsync makes.
        await GetOrNullAsync<IReadOnlyList<VariableList>>(MyLists, cancellationToken) ?? [];

    public async Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        using var response = await SendAsync(HttpMethod.Post, MyLists, new NameBody(name), cancellationToken);

        // 201 with the stored list as its body. A 400 — an empty name, or one over 200 characters —
        // is thrown by EnsureSuccessStatusCode, because a name the user typed and the API refused
        // is something to tell them about rather than an absence to render.
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<VariableList>(Json, cancellationToken)
               ?? throw new InvalidOperationException(
                   $"{MyLists} answered {(int)response.StatusCode} without a list in the body, so there is "
                   + "nothing to return. The endpoint answers 201 with the created list.");
    }

    public Task<bool> RenameMyListAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        return SendForFoundAsync(HttpMethod.Put, MyList(id), new NameBody(name), cancellationToken);
    }

    public Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendForFoundAsync(HttpMethod.Delete, MyList(id), body: null, cancellationToken);

    public async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
        Guid id,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        // Both always sent, unlike the optional parameters on the read endpoints: this one is
        // behind a token and never cached publicly, so there is no shorter URL worth having, and a
        // page that says which page it is beats one that leaves it implied.
        var url = $"{MyListVariables(id)}?page={page}&size={pageSize}";

        var result = await GetOrNullAsync<Page<VariableListItem>>(url, cancellationToken);

        return result is null ? null : WithDerivedTotalPages(result);
    }

    public Task<bool> AddVariablesToMyListAsync(
        Guid id,
        IReadOnlyCollection<Guid> variableIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variableIds);
        RefuseAnOversizedBatch(variableIds.Count, nameof(variableIds));

        return SendForFoundAsync(
            HttpMethod.Post, MyListVariables(id), new VariableIdsBody(variableIds), cancellationToken);
    }

    public Task<bool> RemoveVariablesFromMyListAsync(
        Guid id,
        IReadOnlyCollection<Guid> variableIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variableIds);
        RefuseAnOversizedBatch(variableIds.Count, nameof(variableIds));

        // A DELETE carrying a body, which is unusual enough to be worth saying out loud: the API
        // takes the ids to remove the same way the add takes the ids to add, so the two calls
        // differ only in the verb. Sending them as a query string instead would put up to 2000
        // GUIDs — some 74 000 characters — in a URL, which no server accepts.
        return SendForFoundAsync(
            HttpMethod.Delete, MyListVariables(id), new VariableIdsBody(variableIds), cancellationToken);
    }

    /// <summary>The create and rename body, spelled the way the API spells it.</summary>
    /// <remarks>
    /// A private record rather than a public contract: a caller passes a name, and the envelope it
    /// travels in is this client's business. A host substituting its own
    /// <see cref="IMuninExplorerClient"/> writes its own, and is looking at the API for the shape
    /// either way. The wire name is explicit for the same reason every DTO's is.
    /// </remarks>
    private sealed record NameBody([property: JsonPropertyName("name")] string Name);

    /// <summary>The batch add and remove body, spelled the way the API spells it.</summary>
    /// <remarks>
    /// Private for the same reason <see cref="NameBody"/> is. The wire name matters more here:
    /// <c>variabelIds</c> is not what the web serialiser would derive from <c>VariableIds</c>, and
    /// a body whose one property is unrecognised binds to null — which the API answers as
    /// "request body is required", a message that says nothing about the spelling that caused it.
    /// </remarks>
    private sealed record VariableIdsBody(
        [property: JsonPropertyName("variabelIds")] IReadOnlyCollection<Guid> VariableIds);

    private const string MyLists = "api/explorer/my/lists";

    private static string MyList(Guid id) => $"{MyLists}/{id}";

    private static string MyListVariables(Guid id) => $"{MyList(id)}/variables";

    /// <summary>Refuses a batch the API would refuse, before it costs a round trip.</summary>
    /// <remarks>
    /// The API answers an oversized batch with a 400 whose body names the ceiling — which
    /// <c>EnsureSuccessStatusCode</c> then throws away, leaving the caller an
    /// <see cref="HttpRequestException"/> reading "400 (Bad Request)" and nothing to act on. So the
    /// ceiling is checked here, where the message can say what it is and what to do about it.
    /// <para>
    /// Refused rather than split. See <see cref="IMuninExplorerClient.MaxVariablesPerBatch"/>: a
    /// split turns one call that either happened or did not into several that may have half
    /// happened, and the return value has no way to say which.
    /// </para>
    /// </remarks>
    private static void RefuseAnOversizedBatch(int count, string parameterName)
    {
        if (count <= IMuninExplorerClient.MaxVariablesPerBatch)
        {
            return;
        }

        throw new ArgumentException(
            $"{count} variable ids is more than the API takes in one call: the maximum batch size is "
            + $"{IMuninExplorerClient.MaxVariablesPerBatch}. Split them — "
            + $"ids.Chunk({nameof(IMuninExplorerClient)}.{nameof(IMuninExplorerClient.MaxVariablesPerBatch)}) — "
            + "and call once per batch, so a failure part-way through names the batch it stopped at.",
            parameterName);
    }

    /// <summary>
    /// Fills in the page count the <c>my/lists</c> variables envelope leaves out.
    /// </summary>
    /// <remarks>
    /// That envelope carries items, totalCount, page and size, and — alone among the paged
    /// endpoints — no totalPages. Deserialised into the shared <see cref="Page{T}"/> it would
    /// therefore read as zero pages of a hundred-odd entries, and a pager binding to it would
    /// render nothing at all: the failure this repository keeps meeting, a DTO's own default shown
    /// as though it were data.
    /// <para>
    /// Derived rather than modelled with a second paging type, because the number is not in doubt —
    /// the size in the answer is the one the API actually used, clamps and all. Only filled in when
    /// the API sent none, so the day the envelope grows a totalPages of its own, that is the number
    /// the caller sees rather than ours quietly standing in front of it.
    /// </para>
    /// </remarks>
    private static Page<T> WithDerivedTotalPages<T>(Page<T> page) =>
        page is { TotalPages: 0, TotalCount: > 0, Size: > 0 }
            ? page with { TotalPages = (page.TotalCount + page.Size - 1) / page.Size }
            : page;

    /// <summary>Sends a write and reports whether the list it named was the caller's to write to.</summary>
    /// <remarks>
    /// Every one of these endpoints answers 404 both for a list that does not exist and for one
    /// belonging to somebody else — deliberately, so a caller cannot learn which list ids are real
    /// by watching the difference. It reads as "no such list of yours" here, which is the only
    /// thing a caller can act on and the same not-a-fault the read endpoints map to null.
    /// <para>
    /// A 429 is not one of those, and never reaches this method: <see cref="SendAsync"/> throws it
    /// as <see cref="MuninExplorerRateLimitedException"/> first. Reading it as <c>false</c> would
    /// tell the reader their list is gone when it is only their request that was refused.
    /// </para>
    /// </remarks>
    private async Task<bool> SendForFoundAsync(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, url, body, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        // A 401 lands here, and lands loudly: these endpoints are authenticated, so calling them
        // without a token provider registered is a host wiring mistake and not a user with nothing
        // saved. Returning false would be indistinguishable from the latter.
        response.EnsureSuccessStatusCode();

        return true;
    }

    /// <summary>Sends one request, with <paramref name="body"/> as JSON when there is one.</summary>
    /// <remarks>
    /// A 429 is turned into <see cref="MuninExplorerRateLimitedException"/> here rather than in each
    /// caller, so every write inherits it the way every read inherits the branch in
    /// <see cref="GetOrNullAsync{T}"/>. The reads had one status-interpreting place and the writes
    /// have two — <see cref="CreateMyListAsync"/> and <see cref="SendForFoundAsync"/> — which is
    /// exactly how the gap this repaired came to be missed once already: toggling save down a
    /// result list is the rhythm that meets the limiter, and a throttled save reaching the reader as
    /// "could not save" is the same wrong sentence in a smaller place. A third write added later
    /// gets the branch by going through here at all.
    /// <para>
    /// The response is disposed before the throw, since nothing above can dispose one it never
    /// received.
    /// </para>
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            // The runtime type, not the declared one: body is typed object here, and serialising it
            // as declared would write "{}" for every one of these envelopes.
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = RetryAfter(response);
            response.Dispose();

            throw new MuninExplorerRateLimitedException(retryAfter);
        }

        return response;
    }

    /// <summary>Escape one free-text value for the path, refusing one the path cannot carry.</summary>
    /// <remarks>
    /// <see cref="Uri.EscapeDataString(string)"/> escapes a slash but leaves a dot alone, because a
    /// dot is unreserved — and percent-encoding it does not help: <see cref="Uri"/> unescapes
    /// <c>%2E</c> while canonicalising and removes the dot segments afterwards, so <c>%2E%2E</c>
    /// resolves exactly as <c>..</c> does. A reference of <c>..</c> would then climb out of the
    /// codes endpoint and address a different one on the same host, carrying whatever the message
    /// handlers attach.
    /// <para>
    /// Since it cannot be escaped it is refused, before any request is made. The check runs over
    /// the slash-separated parts rather than the whole value, because a server that percent-decodes
    /// the target before normalising it resolves an encoded separator the same as a plain one — so
    /// <c>a/../b</c> is no safer for having its slashes escaped on the way out. The backslash is in
    /// the split for that same reason: <see cref="Uri.EscapeDataString(string)"/> writes it as
    /// <c>%5C</c>, and a server that decodes before normalising can read it as a separator.
    /// </para>
    /// <para>
    /// The rule is deliberately wider than the two segments that actually normalise. Only <c>.</c>
    /// and <c>..</c> are dot segments — <see cref="Uri"/> leaves a longer run of dots alone — but a
    /// kodeverk reference is never nothing but dots, so refusing every all-dot part costs the
    /// caller nothing and does not depend on which normaliser the target server happens to run.
    /// </para>
    /// </remarks>
    private static string EscapePathSegment(string value, string parameterName)
    {
        foreach (var part in value.Split('/', '\\'))
        {
            if (part.Length > 0 && part.All(character => character == '.'))
            {
                throw new ArgumentException(
                    $"'{value}' cannot be sent as a path segment: '{part}' is nothing but dots.",
                    parameterName);
            }
        }

        return Uri.EscapeDataString(value);
    }

    /// <summary>
    /// GET and deserialise, mapping 404 to null and 429 to
    /// <see cref="MuninExplorerRateLimitedException"/>.
    /// </summary>
    /// <remarks>
    /// The explorer is a public page reachable by deep link, so a request for something that has
    /// been unpublished — or for an id someone typed — is an ordinary event the caller should be
    /// able to render as "not found".
    /// <para>
    /// A 429 is the other status this method reads, and it is read only to throw something the
    /// caller can recognise: the API is answering, and the reader has to be told they asked too
    /// often rather than that the catalogue is down. It is deliberately not given the 404 branch's
    /// treatment — see <see cref="MuninExplorerRateLimitedException"/> for why a throttled call
    /// must not come back as an empty result. Every other failure still throws, as an
    /// <see cref="HttpRequestException"/> from <c>EnsureSuccessStatusCode</c>.
    /// </para>
    /// </remarks>
    private async Task<T?> GetOrNullAsync<T>(
        string url,
        CancellationToken cancellationToken,
        string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Accept-Language, because some names are resolved server side from editable master data
        // and follow the request culture — the datatype facet is the first. Without it a component
        // rendering in English labels its datatype column in Norwegian. The API's output cache is
        // keyed on the resolved culture too, so the header is also what stops the two languages
        // serving each other's cached body.
        if (!string.IsNullOrWhiteSpace(language))
        {
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new MuninExplorerRateLimitedException(RetryAfter(response));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    /// <summary>How long a 429 asked us to wait, or null when it did not say.</summary>
    /// <remarks>
    /// <c>Retry-After</c> comes in two forms and the API is free to send either: delta-seconds,
    /// which lands in <c>Delta</c>, and an HTTP date, which lands in <c>Date</c> and has to be
    /// turned into a wait here. A reader whose clock runs behind the server's would otherwise be
    /// handed a negative wait, so a date already past is floored at zero rather than sent on as a
    /// number that reads like "you should have gone earlier".
    /// <para>
    /// Null when the header is absent, which is not an anomaly worth guessing a default for: the
    /// value is carried for logging and nothing acts on it — see
    /// <see cref="MuninExplorerRateLimitedException.RetryAfter"/>.
    /// </para>
    /// </remarks>
    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;

        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;

            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
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

    /// <summary>Appends a <see cref="VariableFilter"/>'s parameters to a URL that may already have some.</summary>
    /// <remarks>
    /// The filter writes its own query string, using the API's own parameter names, so that the one
    /// place those names are spelled out is the contract every caller already has to hold — a host
    /// putting the same filter in its URL and this client putting it on the wire cannot drift apart.
    /// A filter that narrows nothing adds nothing, which keeps the unfiltered URL as short and as
    /// cacheable as it was before filtering existed.
    /// </remarks>
    private static string WithFilter(string url, VariableFilter? filter)
    {
        var query = filter?.ToQueryString();

        if (string.IsNullOrEmpty(query))
        {
            return url;
        }

        return url + (url.Contains('?', StringComparison.Ordinal) ? '&' : '?') + query;
    }

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
