using System.Net;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Redeeming an account-linking code, against a stubbed transport: the right verb at the right
/// route with the host's token on it, and each refusal the API distinguishes mapped to its own
/// <see cref="IdentityLinkOutcome"/>.
/// </summary>
/// <remarks>
/// Built with a <see cref="BearerTokenHandler"/> in front of the stub for the reason
/// <see cref="MyListsClientTest"/> spells out: the endpoint is behind the API's authenticated
/// policy, and a stub answers whatever it is told whether or not a token went with the request —
/// so a route test written without one passes here and 401s in production.
/// </remarks>
public class IdentityLinkClientTest
{
    private const string BaseAddress = "https://runa.munin.skytest.fhi.no/";
    private const string Token = "the-hosts-token";
    private const string Route = "/api/explorer/my/link/redeem";

    private sealed class FixedTokenProvider(string? token) : IMuninExplorerTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(token);
    }

    private static MuninExplorerClient Client(StubHttpHandler handler) =>
        new(new HttpClient(new BearerTokenHandler(new FixedTokenProvider(Token)) { InnerHandler = handler })
        {
            BaseAddress = new Uri(BaseAddress)
        });

    private static void AssertAuthenticated(StubHttpHandler handler)
    {
        Assert.Equal("Bearer", handler.LastAuthorization?.Scheme);
        Assert.Equal(Token, handler.LastAuthorization?.Parameter);
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Redeem_WhenTheApiAccepts_ThenItPostsTheCodeToTheLinkRouteWithTheToken()
    {
        var handler = StubHttpHandler.Ok("""{"linked":true}""");

        var outcome = await Client(handler).RedeemIdentityLinkAsync("ABC123");

        Assert.Equal(IdentityLinkOutcome.Linked, outcome);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(Route, handler.LastUri?.AbsolutePath);
        AssertAuthenticated(handler);
    }

    /// <summary>
    /// The wire name, which the serialiser does not derive: a body whose one property is
    /// unrecognised binds to null, and the API answers that as <c>invalid_code</c> — a refusal
    /// about the code that was really about the envelope it travelled in.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenItSendsTheCode_ThenTheBodySpellsItTheWayTheApiReadsIt()
    {
        var handler = StubHttpHandler.Ok("""{"linked":true}""");

        await Client(handler).RedeemIdentityLinkAsync("ABC123");

        Assert.Equal("""{"code":"ABC123"}""", handler.LastBody);
    }

    /// <summary>
    /// Every refusal the endpoint documents, each to its own outcome. One case per code because
    /// collapsing any two is the defect this mapping exists to prevent: "make a new code" and
    /// "check what you typed" send the reader to different places.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "invalid_code", IdentityLinkOutcome.InvalidCode)]
    [InlineData(HttpStatusCode.BadRequest, "expired_code", IdentityLinkOutcome.ExpiredCode)]
    [InlineData(HttpStatusCode.Conflict, "code_already_used", IdentityLinkOutcome.CodeAlreadyUsed)]
    [InlineData(HttpStatusCode.Conflict, "cannot_link_to_self", IdentityLinkOutcome.CannotLinkToSelf)]
    [InlineData(
        HttpStatusCode.Conflict,
        "both_identities_already_linked",
        IdentityLinkOutcome.BothIdentitiesAlreadyLinked)]
    public async Task Redeem_WhenTheApiRefuses_ThenTheRefusalKeepsItsOwnIdentity(
        HttpStatusCode status,
        string error,
        IdentityLinkOutcome expected)
    {
        var handler = StubHttpHandler.Answering(status, $$"""{"error":"{{error}}"}""");

        var outcome = await Client(handler).RedeemIdentityLinkAsync("ABC123");

        Assert.Equal(expected, outcome);
    }

    /// <summary>
    /// A refusal is an answer, not a failure: the reader can act on all five, so none of them may
    /// arrive as an exception the caller has to unpack.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenTheApiRefuses_ThenItDoesNotThrow()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.Conflict, """{"error":"code_already_used"}""");

        var outcome = await Client(handler).RedeemIdentityLinkAsync("ABC123");

        Assert.Equal(IdentityLinkOutcome.CodeAlreadyUsed, outcome);
    }

    /// <summary>
    /// A refusal string this version has no word for still has to read as something the reader can
    /// act on, and "check what you typed" is the one instruction that is never actively wrong.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenTheApiRefusesWithAnUnknownCode_ThenItReadsAsInvalidRatherThanThrowing()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.Conflict, """{"error":"some_new_refusal"}""");

        Assert.Equal(IdentityLinkOutcome.InvalidCode, await Client(handler).RedeemIdentityLinkAsync("ABC123"));
    }

    [Fact]
    public async Task Redeem_WhenTheRefusalBodyIsNotJson_ThenItStillReadsAsARefusal()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("not json at all", System.Text.Encoding.UTF8, "application/json")
        });

        Assert.Equal(IdentityLinkOutcome.InvalidCode, await Client(handler).RedeemIdentityLinkAsync("ABC123"));
    }

    /// <summary>
    /// The other half of an unreadable refusal, and a different exception: a gateway answering the
    /// 400 as <c>text/html</c> makes <c>ReadFromJsonAsync</c> throw <see cref="NotSupportedException"/>
    /// on the content type before it parses a byte. The status still said this was a refusal.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenTheRefusalArrivesAsHtml_ThenItStillReadsAsARefusal()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                "<html><body>Bad Request</body></html>", System.Text.Encoding.UTF8, "text/html")
        });

        Assert.Equal(IdentityLinkOutcome.InvalidCode, await Client(handler).RedeemIdentityLinkAsync("ABC123"));
    }

    /// <summary>
    /// A 429 is not a refusal about the code. Reading it as one would tell the reader their code
    /// was wrong when it was their request that was declined, and send them to mint another.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenTheApiThrottles_ThenItThrowsRatherThanReadingAsABadCode()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.TooManyRequests, "{}");

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => Client(handler).RedeemIdentityLinkAsync("ABC123"));
    }

    /// <summary>
    /// A 401 or a 500 is not something the reader can act on, so it must not arrive as a sentence
    /// about their code. A host that wired no token provider lands here, and lands loudly.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Redeem_WhenTheCallFailsForAReasonTheReaderCannotActOn_ThenItThrows(
        HttpStatusCode status)
    {
        var handler = StubHttpHandler.Answering(status, "{}");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(handler).RedeemIdentityLinkAsync("ABC123"));
    }

    /// <summary>
    /// Blank goes to the API rather than being refused here. The endpoint answers it with its own
    /// <c>invalid_code</c>, and a second client-side way of saying the same thing would be one the
    /// reader could not tell apart from the server's.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenTheCodeIsBlank_ThenItIsStillSentAndTheApisRefusalIsWhatComesBack()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.BadRequest, """{"error":"invalid_code"}""");

        var outcome = await Client(handler).RedeemIdentityLinkAsync("   ");

        Assert.Equal(IdentityLinkOutcome.InvalidCode, outcome);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// Sent as typed. Case, hyphens and substituted characters are the API's to normalise, and a
    /// second normaliser here would refuse codes the server would have taken.
    /// </summary>
    [Fact]
    public async Task Redeem_WhenTheReaderTypedItLowerCaseAndHyphenated_ThenItGoesOutUnchanged()
    {
        var handler = StubHttpHandler.Ok("""{"linked":true}""");

        await Client(handler).RedeemIdentityLinkAsync("abc1-23de");

        Assert.Equal("""{"code":"abc1-23de"}""", handler.LastBody);
    }
}
