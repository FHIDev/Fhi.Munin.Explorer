namespace Fhi.Munin.Explorer.Client;

// HttpClient.Timeout is one number for every call, and the whole-search download can take a minute
// or more (Fhi.Metadata-idusm): so the client holds the ceiling and this the per-call limit.
internal sealed class RequestTimeoutHandler(TimeProvider clock) : DelegatingHandler
{
    /// <summary>How long a call may take in total before the reader is told it failed.</summary>
    internal static readonly TimeSpan Default = TimeSpan.FromSeconds(30);

    /// <summary>Set on a request that is allowed longer than <see cref="Default"/>.</summary>
    internal static readonly HttpRequestOptionsKey<TimeSpan> Limit = new("Fhi.Munin.Explorer.Timeout");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = request.Options.TryGetValue(Limit, out var asked) ? asked : Default;
        using var timeout = new CancellationTokenSource(limit, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        HttpResponseMessage? response = null;
        try
        {
            response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);

            // The body too: HttpClient buffers it after this returns, where this limit no longer reaches.
            await response.Content.LoadIntoBufferAsync(linked.Token).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex)
        {
            response?.Dispose();

            if (ex is OperationCanceledException && timeout.IsCancellationRequested)
            {
                throw new TaskCanceledException(
                    $"The request was canceled after {limit.TotalSeconds} seconds.", new TimeoutException(ex.Message, ex));
            }

            throw;
        }
    }
}
