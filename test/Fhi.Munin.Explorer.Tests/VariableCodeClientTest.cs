using System.Net;
using Fhi.Munin.Explorer.Client;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The client half of a variable code link (Fhi.Metadata-l9l2n.182): the route, historical variables included, a fold
/// that names two read as none, and what is never sent.
/// </summary>
public class VariableCodeClientTest
{
    private static readonly Guid Kjonn = new("7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c");

    private static MuninExplorerClient Client(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://munin.example/") });

    [Fact]
    public async Task GetVariableByCode_WhenGivenACode_ThenItAsksTheCodeRouteEscapedWithHistoricalVariables()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.OK, $$"""{"id":"{{Kjonn}}","code":"V_MFR.KJONN"}""");

        var detail = await Client(handler).GetVariableByCodeAsync(" v_mfr.kjønn ");

        Assert.Equal((Kjonn, "V_MFR.KJONN"), (detail?.Id, detail?.Code));
        Assert.Equal("/api/explorer/variables/code/v_mfr.kj%C3%B8nn", handler.LastUri?.AbsolutePath);
        Assert.Equal("?includeHistorical=true", handler.LastUri?.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)] // the code's one-letter fold names two variables
    public async Task GetVariableByCode_WhenTheCodeNamesNoOneVariable_ThenItIsNull(HttpStatusCode status)
    {
        var handler = StubHttpHandler.Answering(status, "");

        Assert.Null(await Client(handler).GetVariableByCodeAsync("V_MFR.UKJENT"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("V_MFR/../x")]
    [InlineData("V MFR")]
    [InlineData("..")]
    public async Task GetVariableByCode_WhenTheValueCannotBeACode_ThenNothingIsSent(string code)
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.OK, "{}");

        Assert.Null(await Client(handler).GetVariableByCodeAsync(code));
        Assert.Equal(0, handler.Calls);
    }
}
