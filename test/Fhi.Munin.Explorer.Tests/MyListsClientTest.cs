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

    /// <summary>
    /// Fails when the interface has grown a <c>my/lists</c> method the sweep below does not call.
    /// </summary>
    /// <remarks>
    /// The two sweeps spell their calls out rather than deriving them, because a hand-written call
    /// is the only way to prove a token reaches the wire. Spelled out, though, they go stale
    /// silently: an eighth endpoint added to the contract and left out of the arrays leaves exactly
    /// the hole this file exists to close, and the suite stays green while that call 401s in
    /// production. Counting the contract's own methods is what reddens this file instead.
    /// </remarks>
    private static void AssertEveryMyListsMethodIsSwept(int swept)
    {
        var onTheContract = typeof(IMuninExplorerClient)
            .GetMethods()
            .Count(method => method.Name.Contains("MyList", StringComparison.Ordinal));

        Assert.True(
            swept == onTheContract,
            $"{nameof(IMuninExplorerClient)} has {onTheContract} my/lists methods and this sweep "
            + $"calls {swept}. Every one of them is [Authorize], so the missing call is one nothing "
            + "here proves sends the host's token. Add it to the array.");
    }

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
    public async Task GetMyListVariablesAsync_WhenTheApiAnswers_ThenTheDisplayFieldsComeWithIt()
    {
        // The API resolves these as it answers, so a list can be drawn without asking for each
        // variable separately. The wire keeps the Norwegian stem — variabelCode, variabelName — and
        // a contract trusting the default camelCase mapping would deserialise silent nulls that
        // look like "the list is empty" rather than "the names did not arrive".
        var handler = StubHttpHandler.Ok(TestData.Read("my-list-variables.json"));

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        var first = page!.Items[0];
        Assert.Equal("V_BDR.F2.ALDER_VED_DIAGNOSE", first.VariableCode);
        Assert.Equal("Alder ved diagnose", first.VariableName);
        Assert.Equal(new Guid("9f2c7e14-6a8b-4d31-b5e0-2c7a91f4d8e6"), first.KildeId);
        Assert.Equal("BDR", first.KildeShortName);
        Assert.Equal("Førstegangsregistrering", first.DatasamlingName);
        Assert.Equal("Ikke oppgitt", first.VariabelgruppeName);
        // The same code the search results carry — both endpoints read DataType from the same
        // ExplorerVariabelView, so this is not a display value and not a second vocabulary.
        Assert.Equal("2", first.DataType);
        // The whole instant, not just the year: the API writes .fffZ, and a value that lost its
        // zone would be read in the machine's own and still land on 2021 — which is how a
        // timezone regression passes a year assertion.
        Assert.Equal(new DateTimeOffset(2021, 8, 1, 0, 0, 0, TimeSpan.Zero), first.DataFrom);
        Assert.Null(first.DataTo);
        // "Active"/"Historical" are the enum's own names — the API writes it PascalCase with no
        // converter attribute, and the sibling fixtures for VariableSummary use the same.
        Assert.Equal("Active", first.VersionStatus);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenAnEntryHasNoRowInTheReadModel_ThenItIsStillReturned()
    {
        // Retracted, unpublished, or not yet projected: the display fields come back null together
        // and the entry stays in the page, so the paging totals keep meaning what they say. Dropping
        // it would make a list of 247 answer with fewer than it counted.
        var handler = StubHttpHandler.Ok(TestData.Read("my-list-variables.json"));

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        var second = page!.Items[1];
        Assert.Equal(new Guid("3e5a8c11-7b42-49df-a6c8-1d904f2e6b73"), second.VariableId);
        Assert.Null(second.VariableName);
        Assert.Null(second.KildeName);
        Assert.Equal(2, page.Items.Count);
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
        // Nothing to page through, and ceil(0/100) is 0 either way. It is the guard on TotalCount
        // that makes this body safe — the size of zero here is never reached.
        var handler = StubHttpHandler.Ok("""{"items":[],"totalCount":0,"page":1,"size":0}""");

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        Assert.NotNull(page);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task GetMyListVariablesAsync_WhenTheEnvelopeOmitsSize_ThenNoPageCountIsDerivedRatherThanADivideByZero()
    {
        // The body that pins the Size guard, and the only one that does: a positive totalCount with
        // a size of zero — what an envelope that stopped sending size deserialises to, the DTO's
        // own default arriving where data was expected. Without `Size: > 0` in the pattern this
        // throws DivideByZeroException out of a paged read; with it, the count is simply not
        // derived, because there is no size to derive it from.
        var handler = StubHttpHandler.Ok("""{"items":[],"totalCount":247,"page":1,"size":0}""");

        var page = await Client(handler).GetMyListVariablesAsync(ListId);

        Assert.NotNull(page);
        Assert.Equal(0, page.TotalPages);
        Assert.Equal(247, page.TotalCount);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABatch_WhenItIsEmpty_ThenItIsStillSentBecauseTheAnswerSaysWhetherTheListExists(bool adding)
    {
        // The contract says an empty collection is a legitimate call and still goes out. It looks
        // like a free round trip to skip — `if (variableIds.Count == 0) return true;` — but the
        // answer is what tells the caller whether the list is still theirs, so short-circuiting it
        // turns a probe of a list deleted in another tab into a confident true.
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);
        var client = Client(handler);

        Assert.True(adding
            ? await client.AddVariablesToMyListAsync(ListId, [])
            : await client.RemoveVariablesFromMyListAsync(ListId, []));

        Assert.Equal(1, handler.Calls);
        Assert.Equal($"{Collection}/{ListId}/variables", handler.LastUri?.AbsolutePath);
        Assert.Equal("""{"variabelIds":[]}""", handler.LastBody);
        AssertAuthenticated(handler);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnEmptyBatch_WhenTheUserHasNoSuchList_ThenFalseRatherThanAnAssumedTrue(bool adding)
    {
        // The other half of the same point, and the one a short-circuit would get wrong: nothing to
        // add or remove, but the list is gone, and the caller has to be told.
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);
        var client = Client(handler);

        Assert.False(adding
            ? await client.AddVariablesToMyListAsync(ListId, [])
            : await client.RemoveVariablesFromMyListAsync(ListId, []));

        Assert.Equal(1, handler.Calls);
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

    // --------------------------------------------------------------------------------------- 429

    [Theory]
    [InlineData("rename")]
    [InlineData("delete")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task AWrite_WhenTheApiRateLimits_ThenItThrowsRatherThanReadingAsNoSuchList(string call)
    {
        // The trap the reads meet, in the shape a write takes it. These routes sit under
        // api/explorer/ like every other one and are counted against the same per-address limiter,
        // and saving one row after another is exactly the rhythm that meets it — so a throttled
        // write is an ordinary event here. Reporting it as false would tell the reader their list
        // is gone when it is only their request that was refused.
        var handler = StubHttpHandler.RateLimited(TimeSpan.FromSeconds(30));
        var client = Client(handler);

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => call switch
        {
            "rename" => client.RenameMyListAsync(ListId, "Nytt navn"),
            "delete" => client.DeleteMyListAsync(ListId),
            "add" => client.AddVariablesToMyListAsync(ListId, [Guid.NewGuid()]),
            "remove" => client.RemoveVariablesFromMyListAsync(ListId, [Guid.NewGuid()]),
            _ => throw new ArgumentOutOfRangeException(nameof(call), call, "No such call.")
        });

        Assert.Equal(TimeSpan.FromSeconds(30), refused.RetryAfter);

        // The writes get no retry of their own either, for the same reason the reads get none.
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CreateMyListAsync_WhenTheApiRateLimits_ThenItThrowsItsOwnExceptionRatherThanTheGenericOne()
    {
        // The other status-interpreting write. Its EnsureSuccessStatusCode is there for a name the
        // API refused, which is the user's to fix; a 429 is not, and a caller that cannot tell them
        // apart shows "could not save" to a reader whose list was never in doubt.
        var handler = StubHttpHandler.RateLimited();
        var client = Client(handler);

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => client.CreateMyListAsync("Min liste"));

        Assert.Null(refused.RetryAfter);
        Assert.Equal(1, handler.Calls);
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
        // only checked routes would be green while every call 401s against the real API. The list
        // is spelled out rather than derived, because only a real call proves a token reached the
        // wire — and counted against the contract, so adding an endpoint and forgetting it here
        // reddens this test rather than reopening the hole.
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

        AssertEveryMyListsMethodIsSwept(calls.Length);

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

        AssertEveryMyListsMethodIsSwept(calls.Length);

        foreach (var call in calls)
        {
            await Assert.ThrowsAsync<HttpRequestException>(call);
        }

        Assert.Null(handler.LastAuthorization);
    }
}
