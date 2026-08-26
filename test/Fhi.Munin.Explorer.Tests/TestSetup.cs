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
internal abstract class EmptyMuninExplorerClient : IMuninExplorerClient
{
    public virtual Task<Page<VariableSummary>> SearchVariablesAsync(
        string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
        SortField sort = SortField.Default,
        SortDirection direction = SortDirection.Ascending,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Page<VariableSummary>());

    public virtual Task<FilterOptions> GetFiltersAsync(
        string? search = null,
        VariableFilter? filter = null,
        string? language = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FilterOptions());

    public virtual Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
        string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KildeSummary>>([]);

    public virtual Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<KildeDetail?>(null);

    public virtual Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<KildeHierarchy?>(null);

    public virtual Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<DatasamlingDetail?>(null);

    public virtual Task<VariableDetail?> GetVariableAsync(
        Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<VariableDetail?>(null);

    public virtual Task<IReadOnlyList<VariableVersion>> GetVariableTimelineAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VariableVersion>>([]);

    public virtual Task<KodeverkCodes?> GetKodeverkCodesAsync(
        Guid variableId, string kodeverkType, string kodeverkReference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<KodeverkCodes?>(null);

    public virtual Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VariableList>>([]);

    // "Nothing" has no honest shape for a create, which either produces a list or fails. A fake
    // that is asked to create one and has not been told what to answer is a test setup mistake,
    // so it says so rather than handing back an empty record that reads as a real list.
    public virtual Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{nameof(EmptyMuninExplorerClient)} has no list to create. Override "
            + $"{nameof(CreateMyListAsync)} in the fake that needs it.");

    public virtual Task<bool> RenameMyListAsync(Guid id, string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public virtual Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public virtual Task<Page<VariableListItem>?> GetMyListVariablesAsync(
        Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<Page<VariableListItem>?>(null);

    public virtual Task<bool> AddVariablesToMyListAsync(
        Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public virtual Task<bool> RemoveVariablesFromMyListAsync(
        Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a lambda and remembers what it was asked.
/// </summary>
/// <remarks>
/// The request message itself is disposed by <c>HttpClient</c> once the call completes, so what the
/// test needs to assert on is copied out here rather than kept as a reference.
/// </remarks>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public Uri? LastUri { get; private set; }

    /// <summary>
    /// The verb of the last request, which is half of what a route assertion is about.
    /// </summary>
    /// <remarks>
    /// The variable-list endpoints are the reason this is recorded: <c>my/lists/{id}/variables</c>
    /// is one URL that adds on POST and removes on DELETE, so a test asserting only on the path
    /// would pass with the two swapped.
    /// </remarks>
    public HttpMethod? LastMethod { get; private set; }

    /// <summary>The last request body, verbatim, or null where there was none.</summary>
    /// <remarks>
    /// Kept as the JSON that actually went out rather than as a deserialised object: what these
    /// tests are checking is the spelling of the wire names, and reading it back through the same
    /// contract that wrote it would agree with itself whatever that spelling turned out to be.
    /// </remarks>
    public string? LastBody { get; private set; }

    public IReadOnlyList<string> LastClientHeader { get; private set; } = [];
    public AuthenticationHeaderValue? LastAuthorization { get; private set; }

    /// <summary>The languages asked for, which decide what the API names its facets in.</summary>
    public IReadOnlyList<string> LastAcceptLanguage { get; private set; } = [];
    public int Calls { get; private set; }

    /// <summary>Answers <c>200 OK</c> with the given JSON body to every request.</summary>
    public static StubHttpHandler Ok(string json) => Answering(HttpStatusCode.OK, json);

    /// <summary>Answers with the given status and JSON body.</summary>
    /// <remarks>
    /// <c>201 Created</c> is what this exists for: <c>POST /api/explorer/my/lists</c> answers with
    /// the stored list under that status rather than under 200, so a stub that only knows how to
    /// say 200 would test a response the API never sends.
    /// </remarks>
    public static StubHttpHandler Answering(HttpStatusCode status, string json) => new(_ => new HttpResponseMessage(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    });

    /// <summary>Answers with the given status and an empty body.</summary>
    public static StubHttpHandler Status(HttpStatusCode status) => new(_ => new HttpResponseMessage(status));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastUri = request.RequestUri;
        LastMethod = request.Method;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        LastClientHeader = request.Headers.TryGetValues(Explorer.Client.ClientHeaderHandler.Header, out var values)
            ? [.. values]
            : [];
        LastAuthorization = request.Headers.Authorization;
        LastAcceptLanguage = [.. request.Headers.AcceptLanguage.Select(v => v.Value)];

        return respond(request);
    }
}

/// <summary>Responses captured from the live Munin Explorer API, embedded in the test assembly.</summary>
/// <remarks>
/// Round-tripping a real payload is the test that actually catches contract drift; a fixture
/// written by hand only ever proves the DTO agrees with itself. Re-capture with, for example,
/// <c>curl https://munin.skytest.fhi.no/api/explorer/filters</c> when an endpoint changes.
/// </remarks>
internal static class TestData
{
    public static string Read(string fileName)
    {
        // The prefix is the assembly name plus the folder the fixtures live in — `Testdata/`,
        // which is a directory name and therefore not renamed with the code.
        var resource = $"Fhi.Munin.Explorer.Tests.Testdata.{fileName}";

        using var stream = typeof(TestData).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded test data '{resource}' not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
