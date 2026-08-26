using System.Net;
using System.Text;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The authenticated half of the client — the signed-in user's own variable lists — against a
/// stubbed transport: the right verb at the right route, the host's token on every one of them,
/// and the batch ceiling refused before it costs a round trip.
/// </summary>
/// <remarks>
/// <para>
/// Its own file rather than more of <see cref="MuninExplorerClientTest"/>, and the reason is the
/// trap this endpoint sets. The whole of <c>api/explorer/my/lists</c> is behind the API's
/// authenticated policy, but a stub handler answers whatever it is told to whether or not the
/// request carried a token — so a route test written the way the anonymous endpoints' tests are
/// written passes here and 401s in production. Every client in this file is therefore built with a
/// <see cref="BearerTokenHandler"/> in front of the stub and a provider that really supplies a
/// token, and <see cref="AssertAuthenticated"/> is asserted alongside every route. There is no
/// helper here that builds an unauthenticated client, so a test added later cannot quietly skip it.
/// </para>
/// <para>
/// <see cref="EveryCall_WhenNoHostRegistersAProvider_ThenTheApisRefusalIsThrownRatherThanReadAsNothing"/>
/// is the other side of the same point: with the anonymous default in place these calls must fail
/// loudly rather than answer "you have no lists".
/// </para>
/// </remarks>
public class MyListsClientTest
{
    private const string BaseAddress = "https://munin.skytest.fhi.no/";
    private const string Token = "the-hosts-token";

    /// <summary>The route every one of these calls hangs off, spelled once.</summary>
    private const string Collection = "/api/explorer/my/lists";

    private static readonly Guid ListId = new("1f9d0b7e-3c14-4a2f-8f0b-2b6a4f1c9d31");

    private sealed class FixedTokenProvider(string? token) : IMuninExplorerTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(token);
    }

    /// <summary>
    /// The client wired the way <c>AddMuninExplorer</c> wires it for a host that supplies tokens:
    /// the bearer handler in front of the stub, so what the stub records is what the API would see.
    /// </summary>
    private static MuninExplorerClient Client(StubHttpHandler handler, string? token = Token) =>
        new(new HttpClient(new BearerTokenHandler(new FixedTokenProvider(token)) { InnerHandler = handler })
        {
            BaseAddress = new Uri(BaseAddress)
        });

    /// <summary>The assertion that makes a route test evidence about an authenticated endpoint.</summary>
    private static void AssertAuthenticated(StubHttpHandler handler)
    {
        Assert.Equal("Bearer", handler.LastAuthorization?.Scheme);
        Assert.Equal(Token, handler.LastAuthorization?.Parameter);
    }

    private static Guid[] Ids(int count) => [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];

    /// <summary>What <c>POST /api/explorer/my/lists</c> answers with, under <c>201</c>.</summary>
    private const string CreatedList =
        """{"id":"1f9d0b7e-3c14-4a2f-8f0b-2b6a4f1c9d31","name":"Ny liste","createdAt":"2026-06-02T08:14:37.412+00:00","updatedAt":"2026-06-02T08:14:37.412+00:00"}""";

    /// <summary>An empty page of a list's variables, with the envelope's own field set.</summary>
    private const string EmptyVariablesPage = """{"items":[],"totalCount":0,"page":1,"size":100}""";

    private static HttpResponseMessage Answer(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    // --------------------------------------------------------------------------------- the lists

    [Fact]
    public async Task GetMyListsAsync_WhenTheApiAnswers_ThenTheListsAreReadFromTheCollectionRoute()
    {
        var handler = StubHttpHandler.Ok(TestData.Read("my-lists.json"));

        var lists = await Client(handler).GetMyListsAsync();

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal(Collection, handler.LastUri?.AbsolutePath);
        Assert.Equal("", handler.LastUri?.Query);
        AssertAuthenticated(handler);

        Assert.Equal(2, lists.Count);
        Assert.Equal(ListId, lists[0].Id);
        Assert.Equal("Mine hjertevariabler", lists[0].Name);
        Assert.Equal(2026, lists[0].CreatedAt.Year);

        // Changed since it was made, where the second list has never been touched again. The two
        // timestamps are separate fields on purpose; reading one into both would hide that.
        Assert.NotEqual(lists[0].CreatedAt, lists[0].UpdatedAt);
        Assert.Equal(lists[1].CreatedAt, lists[1].UpdatedAt);
    }

    [Fact]
    public async Task GetMyListsAsync_WhenTheUserHasNoLists_ThenAnEmptyListRatherThanAThrow()
    {
        var handler = StubHttpHandler.Ok("[]");

        Assert.Empty(await Client(handler).GetMyListsAsync());
        AssertAuthenticated(handler);
    }

    [Fact]
    public async Task CreateMyListAsync_WhenAListIsCreated_ThenItIsPostedToTheCollectionAndComesBackStored()
    {
        // 201 rather than 200, which is what the endpoint actually answers — and the id and the
        // timestamps come from the server, so the returned record is the only place a caller can
        // learn the id it has to use next.
        var handler = StubHttpHandler.Answering(HttpStatusCode.Created, """
            {"id":"1f9d0b7e-3c14-4a2f-8f0b-2b6a4f1c9d31","name":"Mine hjertevariabler",
             "createdAt":"2026-06-02T08:14:37.412+00:00","updatedAt":"2026-06-02T08:14:37.412+00:00"}
            """);

        var created = await Client(handler).CreateMyListAsync("Mine hjertevariabler");

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(Collection, handler.LastUri?.AbsolutePath);
        Assert.Equal("""{"name":"Mine hjertevariabler"}""", handler.LastBody);
        AssertAuthenticated(handler);

        Assert.Equal(ListId, created.Id);
        Assert.Equal("Mine hjertevariabler", created.Name);
    }

    [Fact]
    public async Task CreateMyListAsync_WhenTheApiRefusesTheName_ThenItIsThrownRatherThanReadAsNothing()
    {
        // An empty name, or one over 200 characters, comes back as a 400 the user has to be told
        // about. Answering it with a default record would put a nameless list on their screen.
        var handler = StubHttpHandler.Answering(
            HttpStatusCode.BadRequest, """{"error":"Name must not be empty."}""");

        await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).CreateMyListAsync(" "));
    }

    [Fact]
    public async Task RenameMyListAsync_WhenTheListIsTheUsersOwn_ThenTheNameIsPutToTheListsOwnRoute()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);

        Assert.True(await Client(handler).RenameMyListAsync(ListId, "Hjerte og kar"));

        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"{Collection}/{ListId}", handler.LastUri?.AbsolutePath);
        Assert.Equal("""{"name":"Hjerte og kar"}""", handler.LastBody);
        AssertAuthenticated(handler);
    }

    [Fact]
    public async Task DeleteMyListAsync_WhenTheListIsTheUsersOwn_ThenItIsDeletedFromTheListsOwnRoute()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);

        Assert.True(await Client(handler).DeleteMyListAsync(ListId));

        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal($"{Collection}/{ListId}", handler.LastUri?.AbsolutePath);
        Assert.Null(handler.LastBody);
        AssertAuthenticated(handler);
    }

    // ------------------------------------------------------------------- what is inside one list

    [Fact]
    public async Task GetMyListVariablesAsync_WhenTheApiAnswers_ThenThePageAndItsEntriesAreRead()
    {
        var handler = StubHttpHandler.Ok(TestData.Read("my-list-variables.json"));

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"{Collection}/{ListId}/variables", handler.LastUri?.AbsolutePath);
        Assert.Equal("?page=1&size=100", handler.LastUri?.Query);
        AssertAuthenticated(handler);

        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(247, page.TotalCount);

        // The wire spells this one variabelId while the property is VariableId — the rename that a
        // missing [JsonPropertyName] would turn into an all-zero Guid nobody can look up.
        Assert.Equal(new Guid("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90"), page.Items[0].VariableId);
        Assert.Equal(2026, page.Items[0].AddedAt.Year);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenAPageIsAskedFor_ThenBothNumbersAreSent()
    {
        // Always both, unlike the public read endpoints where a default is left off to keep the
        // URL cacheable: nothing behind a token is cached publicly, and a request that says which
        // page it wants is easier to read in a log than one leaving half of it implied.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).GetMyListVariablesAsync(ListId, page: 3, pageSize: 500);

        Assert.Equal("?page=3&size=500", handler.LastUri?.Query);
        AssertAuthenticated(handler);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenTheEnvelopeOmitsTotalPages_ThenItIsDerivedFromTheCountAndSize()
    {
        // This envelope is the one paged response in the API that carries no totalPages. Read into
        // the shared Page<T> as it stands it says nought pages of 247 entries, and a pager binding
        // to that renders nothing at all — the DTO's own default shown as though it were data.
        var handler = StubHttpHandler.Ok("""{"items":[],"totalCount":247,"page":1,"size":100}""");

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        Assert.NotNull(page);
        Assert.Equal(3, page.TotalPages); // 247 over 100, rounded up — not 2
        Assert.Equal(247, page.TotalCount);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenTheApiSendsATotalPagesOfItsOwn_ThenItIsLeftAlone()
    {
        // The derivation only fills a gap. The day the envelope grows a totalPages, that number is
        // the API's answer and ours must not stand in front of it — including when the two would
        // disagree, which is exactly when a caller needs to see the API's.
        var handler = StubHttpHandler.Ok("""{"items":[],"totalCount":247,"page":1,"size":100,"totalPages":9}""");

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        Assert.NotNull(page);
        Assert.Equal(9, page.TotalPages);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenTheListIsEmpty_ThenNoPageCountIsInvented()
    {
        // Nothing to page through, and ceil(0/100) is 0 either way — but the guard on TotalCount is
        // what keeps a size of zero, which an empty or absent body deserialises to, out of a divide.
        var handler = StubHttpHandler.Ok("""{"items":[],"totalCount":0,"page":1,"size":0}""");

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        Assert.NotNull(page);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task AddVariablesToMyListAsync_WhenVariablesAreAdded_ThenTheyArePostedAsVariabelIds()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);
        var first = new Guid("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90");
        var second = new Guid("3e5a8c11-7b42-49df-a6c8-1d904f2e6b73");

        Assert.True(await Client(handler).AddVariablesToMyListAsync(ListId, [first, second]));

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"{Collection}/{ListId}/variables", handler.LastUri?.AbsolutePath);

        // The property is VariableIds and the wire name is not. A body whose one property the API
        // does not recognise binds to null, and the API answers that as "request body is required"
        // — a 400 that says nothing about the spelling that caused it.
        Assert.Equal($$"""{"variabelIds":["{{first}}","{{second}}"]}""", handler.LastBody);
        AssertAuthenticated(handler);
    }

    [Fact]
    public async Task RemoveVariablesFromMyListAsync_WhenVariablesAreRemoved_ThenTheyAreSentAsABodyOnTheDelete()
    {
        // A DELETE carrying a body, which is unusual enough to pin: the same route removes on
        // DELETE what it adds on POST, so a test asserting only on the path would pass with the
        // two swapped — and up to 2000 GUIDs in a query string is a URL no server accepts.
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);
        var id = new Guid("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90");

        Assert.True(await Client(handler).RemoveVariablesFromMyListAsync(ListId, [id]));

        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal($"{Collection}/{ListId}/variables", handler.LastUri?.AbsolutePath);
        Assert.Equal($$"""{"variabelIds":["{{id}}"]}""", handler.LastBody);
        AssertAuthenticated(handler);
    }

    // ------------------------------------------------------------------------- "no such list of yours"

    [Theory]
    [InlineData("rename")]
    [InlineData("delete")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task AWrite_WhenTheUserHasNoSuchList_ThenFalseRatherThanAThrow(string call)
    {
        // The API answers 404 both for a list that does not exist and for one belonging to someone
        // else, deliberately, so a caller cannot learn which ids are real by watching the
        // difference. Either way it is a list deleted in another tab, which a caller renders — not
        // a fault to take a page down over.
        var client = Client(StubHttpHandler.Status(HttpStatusCode.NotFound));

        var found = call switch
        {
            "rename" => await client.RenameMyListAsync(ListId, "Nytt navn"),
            "delete" => await client.DeleteMyListAsync(ListId),
            "add" => await client.AddVariablesToMyListAsync(ListId, [Guid.NewGuid()]),
            "remove" => await client.RemoveVariablesFromMyListAsync(ListId, [Guid.NewGuid()]),
            _ => throw new ArgumentOutOfRangeException(nameof(call), call, "No such call.")
        };

        Assert.False(found);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenTheUserHasNoSuchList_ThenNullRatherThanAThrow()
    {
        Assert.Null(await Client(StubHttpHandler.Status(HttpStatusCode.NotFound))
            .GetMyListVariablesAsync(ListId));
    }

    [Fact]
    public async Task DeleteMyListAsync_WhenTheApiFails_ThenItIsRethrown()
    {
        // A 500 is a fault, and the caller has to be able to tell it from "you had no such list" —
        // the same rule the anonymous endpoints follow.
        var client = Client(StubHttpHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.DeleteMyListAsync(ListId));
    }

    // ------------------------------------------------------------------------------ the batch ceiling

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABatch_WhenItIsOverTheApisCeiling_ThenNothingIsSentAtAll(bool adding)
    {
        // The API caps a batch at 2000 and answers more with a 400 whose body names the ceiling —
        // which EnsureSuccessStatusCode throws away, leaving a caller "400 (Bad Request)" and
        // nothing to act on. So it is refused here, where the message can say what the limit is and
        // what to do instead, and refused before the request rather than after it.
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);
        var client = Client(handler);
        var tooMany = Ids(IMuninExplorerClient.MaxVariablesPerBatch + 1);

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => adding
            ? client.AddVariablesToMyListAsync(ListId, tooMany)
            : client.RemoveVariablesFromMyListAsync(ListId, tooMany));

        Assert.Equal("variableIds", refused.ParamName);
        Assert.Contains("2000", refused.Message, StringComparison.Ordinal);
        Assert.Contains("2001", refused.Message, StringComparison.Ordinal);

        // The half that makes this worth having: no request went out, so nothing was half applied
        // and there is nothing to undo.
        Assert.Equal(0, handler.Calls);
        Assert.Null(handler.LastUri);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABatch_WhenItIsExactlyTheApisCeiling_ThenItIsSentAsOneRequest(bool adding)
    {
        // The boundary, in the direction an off-by-one would break: 2000 is accepted, and accepted
        // whole. A client that split at the ceiling rather than above it would turn every full
        // batch into two requests, the second of them empty.
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);
        var client = Client(handler);
        var exactly = Ids(IMuninExplorerClient.MaxVariablesPerBatch);

        Assert.True(adding
            ? await client.AddVariablesToMyListAsync(ListId, exactly)
            : await client.RemoveVariablesFromMyListAsync(ListId, exactly));

        Assert.Equal(1, handler.Calls);
        AssertAuthenticated(handler);
    }

    [Fact]
    public void MaxVariablesPerBatch_WhenReadFromTheContract_ThenItIsTheCeilingTheApiEnforces()
    {
        // MyListsController.MaxBatchSize. The two live in different repositories and are checked
        // against each other by nobody, so the number is pinned here: if the API raises its
        // ceiling, this is the test that says the constant has to move with it.
        Assert.Equal(2000, IMuninExplorerClient.MaxVariablesPerBatch);
    }

    // ------------------------------------------------------------------------------- the trap itself

    [Fact]
    public async Task EveryCall_WhenAHostSuppliesAToken_ThenItIsSentAsBearerOnAllSeven()
    {
        // The point of this file in one test. Every one of these endpoints is [Authorize], and a
        // stub handler answers whatever it is told whether or not a token arrived — so a suite that
        // only checked routes would be green while every call 401s against the real API. Adding an
        // endpoint to the interface without adding it here leaves that hole open again, which is
        // why the list is spelled out rather than derived.
        var handler = new StubHttpHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post && path.EndsWith("/lists", StringComparison.Ordinal))
            {
                return Answer(HttpStatusCode.Created, CreatedList);
            }

            if (request.Method == HttpMethod.Get)
            {
                return Answer(
                    HttpStatusCode.OK,
                    path.EndsWith("/variables", StringComparison.Ordinal) ? EmptyVariablesPage : "[]");
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var client = Client(handler);
        var ids = new[] { Guid.NewGuid() };

        var calls = new (string Name, Func<Task> Call)[]
        {
            ("GetMyListsAsync", () => client.GetMyListsAsync()),
            ("CreateMyListAsync", () => client.CreateMyListAsync("Ny liste")),
            ("RenameMyListAsync", () => client.RenameMyListAsync(ListId, "Nytt navn")),
            ("DeleteMyListAsync", () => client.DeleteMyListAsync(ListId)),
            ("GetMyListVariablesAsync", () => client.GetMyListVariablesAsync(ListId)),
            ("AddVariablesToMyListAsync", () => client.AddVariablesToMyListAsync(ListId, ids)),
            ("RemoveVariablesFromMyListAsync", () => client.RemoveVariablesFromMyListAsync(ListId, ids))
        };

        foreach (var (name, call) in calls)
        {
            await call();

            Assert.True(
                handler.LastAuthorization is { Scheme: "Bearer", Parameter: Token },
                $"{name} reached {handler.LastUri} without the host's token. The endpoint is "
                + "[Authorize], so this call answers 401 against the real API however green the "
                + "route assertions are.");
        }

        Assert.Equal(calls.Length, handler.Calls);
    }

    [Fact]
    public async Task EveryCall_WhenNoHostRegistersAProvider_ThenTheApisRefusalIsThrownRatherThanReadAsNothing()
    {
        // The other half. With the anonymous default in place — a host that never registered a
        // provider, or registered one after AddMuninExplorer and lost to TryAdd — every one of
        // these answers 401. That must arrive as a fault, because the alternative reads as "you
        // have no saved lists" and sends the user looking for the lists they saved yesterday.
        var handler = StubHttpHandler.Status(HttpStatusCode.Unauthorized);
        var client = Client(handler, token: null);
        var ids = new[] { Guid.NewGuid() };

        var calls = new Func<Task>[]
        {
            () => client.GetMyListsAsync(),
            () => client.CreateMyListAsync("Ny liste"),
            () => client.RenameMyListAsync(ListId, "Nytt navn"),
            () => client.DeleteMyListAsync(ListId),
            () => client.GetMyListVariablesAsync(ListId),
            () => client.AddVariablesToMyListAsync(ListId, ids),
            () => client.RemoveVariablesFromMyListAsync(ListId, ids)
        };

        foreach (var call in calls)
        {
            await Assert.ThrowsAsync<HttpRequestException>(call);
        }

        Assert.Null(handler.LastAuthorization);
    }
}
