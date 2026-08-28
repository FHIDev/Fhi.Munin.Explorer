using System.Net;
using System.Net.Sockets;
using Fhi.Munin.Explorer.Client;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one more attempt a reader would otherwise make by pressing the button again.
/// </summary>
/// <remarks>
/// A pooled connection can die without saying so, and the request written into it fails on the
/// read rather than on a connect — so no connect timeout shortens it. (Fhi.Metadata-phgeg)
/// </remarks>
public class TransientRetryHandlerTest
{
    /// <summary>Fails the first n attempts with <paramref name="failure"/>, then answers.</summary>
    private sealed class Inner(int failures, Exception failure) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;

            return Attempts <= failures
                ? Task.FromException<HttpResponseMessage>(failure)
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static HttpRequestException DeadConnection() =>
        new("An error occurred while sending the request.",
            new IOException("Unable to read data from the transport connection.",
                new SocketException(10054)));

    private static (HttpClient Client, Inner Handler) Chain(int failures, Exception? failure = null)
    {
        var inner = new Inner(failures, failure ?? DeadConnection());
        var retry = new TransientRetryHandler { InnerHandler = inner };

        return (new HttpClient(retry) { BaseAddress = new Uri("https://munin.example/") }, inner);
    }

    [Fact]
    public async Task Send_WhenAReadFailsOnADeadConnection_ThenItIsSentOnceMoreAndSucceeds()
    {
        var (client, inner) = Chain(failures: 1);

        var response = await client.GetAsync("api/explorer/variables");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Attempts);
    }

    [Fact]
    public async Task Send_WhenTheSecondAttemptFailsToo_ThenTheReaderIsToldRatherThanKeptWaiting()
    {
        // Once, not until it works. Two failures is the network being down rather than one stale
        // connection, and a third attempt spends the reader's time to say the same thing.
        var (client, inner) = Chain(failures: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("api/explorer/variables"));

        Assert.Equal(2, inner.Attempts);
    }

    [Fact]
    public async Task Send_WhenTheRequestWritesSomething_ThenItIsNotRepeated()
    {
        // A reset during the response read says nothing about whether the server processed the
        // request, so repeating a save could save twice. Repeating a read cannot.
        var (client, inner) = Chain(failures: 1);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PostAsync("api/explorer/lists", new StringContent("{}")));

        Assert.Equal(1, inner.Attempts);
    }

    [Fact]
    public async Task Send_WhenTheServerAnswered_ThenTheAnswerIsNotAskedForTwice()
    {
        // A 500 is the server's answer, not a dead connection. Retrying asks a working server the
        // same question again.
        var (client, inner) = Chain(failures: 0);

        var response = await client.GetAsync("api/explorer/variables");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Attempts);
    }

    [Fact]
    public async Task Send_WhenTheFailureIsNotTheTransport_ThenItIsNotRepeated()
    {
        // No inner IOException or SocketException: something above the connection went wrong, and
        // sending it again would fail the same way at the same cost.
        var (client, inner) = Chain(failures: 1, failure: new HttpRequestException("nope"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("api/explorer/variables"));

        Assert.Equal(1, inner.Attempts);
    }
}
