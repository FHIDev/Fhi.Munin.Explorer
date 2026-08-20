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
    public IReadOnlyList<string> LastClientHeader { get; private set; } = [];
    public AuthenticationHeaderValue? LastAuthorization { get; private set; }

    /// <summary>The languages asked for, which decide what the API names its facets in.</summary>
    public IReadOnlyList<string> LastAcceptLanguage { get; private set; } = [];
    public int Calls { get; private set; }

    /// <summary>Answers <c>200 OK</c> with the given JSON body to every request.</summary>
    public static StubHttpHandler Ok(string json) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    });

    /// <summary>Answers with the given status and an empty body.</summary>
    public static StubHttpHandler Status(HttpStatusCode status) => new(_ => new HttpResponseMessage(status));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastUri = request.RequestUri;
        LastClientHeader = request.Headers.TryGetValues(Explorer.Client.ClientHeaderHandler.Header, out var values)
            ? [.. values]
            : [];
        LastAuthorization = request.Headers.Authorization;
        LastAcceptLanguage = [.. request.Headers.AcceptLanguage.Select(v => v.Value)];

        return Task.FromResult(respond(request));
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
