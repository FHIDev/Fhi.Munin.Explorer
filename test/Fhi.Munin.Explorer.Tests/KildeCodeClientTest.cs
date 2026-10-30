using System.Net;
using Fhi.Munin.Explorer.Client;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The client half of a code link (Fhi.Metadata-9huq2.12): the route, the escaping, and what is never sent.</summary>
public class KildeCodeClientTest
{
    private static readonly Guid Dar = new("8ec4c2c4-662d-47a5-a946-f1086a014070");

    private static MuninExplorerClient Client(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://munin.example/") });

    [Fact]
    public async Task FindKildeIdByCode_AsksTheCodeRoute_WithTheCodeEscaped()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.OK, $$"""{"id":"{{Dar}}","code":"K_DAR"}""");

        var id = await Client(handler).FindKildeIdByCodeAsync(" dår ");

        Assert.Equal(Dar, id);
        Assert.Equal("/api/explorer/kilder/code/d%C3%A5r", handler.LastUri?.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    [Fact]
    public async Task FindKildeIdByCode_WhenNoKildeHasIt_IsNull()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.NotFound, "");

        Assert.Null(await Client(handler).FindKildeIdByCodeAsync("ukjent"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("dar/../x")]
    [InlineData("d ar")]
    public async Task FindKildeIdByCode_ForAValueNoCodeCanBe_SendsNothing(string code)
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.OK, "{}");

        Assert.Null(await Client(handler).FindKildeIdByCodeAsync(code));
        Assert.Equal(0, handler.Calls);
    }
}
