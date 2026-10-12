using System.Net;
using Fhi.Munin.Explorer.Client;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Thirty seconds for a call, longer only for one that asks. (Fhi.Metadata-idusm)</summary>
public class RequestTimeoutHandlerTest
{
    /// <summary>Fires each timer only when the test says the time has passed.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly List<(TimeSpan Due, TimerCallback Callback, object? State)> _timers = [];
        private TimeSpan _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timers.Add((_now + dueTime, callback, state));
            return new NoTimer();
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var timer in _timers.Where(t => t.Due <= _now).ToList())
            {
                _timers.Remove(timer);
                timer.Callback(timer.State);
            }
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>Answers only once cancelled; with <c>StallBody</c>, answers at once with a body that never ends.</summary>
    private sealed class Hanging : HttpMessageHandler
    {
        public bool StallBody { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (StallBody)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) };
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private static (HttpMessageInvoker Invoker, ManualClock Clock) Chain(Hanging? inner = null)
    {
        var clock = new ManualClock();
        var handler = new RequestTimeoutHandler(clock) { InnerHandler = inner ?? new Hanging() };

        return (new HttpMessageInvoker(handler), clock);
    }

    private static HttpRequestMessage Request(TimeSpan? limit = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://munin.example/api/explorer/variables");
        if (limit is { } asked)
        {
            request.Options.Set(RequestTimeoutHandler.Limit, asked);
        }

        return request;
    }

    /// <summary>Whether the call has finished, given a moment for continuations the clock released.</summary>
    private static async Task<bool> Settled(Task call)
    {
        await Task.WhenAny(call, Task.Delay(200));
        return call.IsCompleted;
    }

    /// <summary>The call's failure, or a TimeoutException of the test's own if it never ends.</summary>
    private static Task Outcome(Task call) => call.WaitAsync(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task Send_WhenACallRunsPastThirtySeconds_ThenItFailsAsATimeout()
    {
        var (invoker, clock) = Chain();
        var call = invoker.SendAsync(Request(), CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(await Settled(call));
        clock.Advance(TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<TaskCanceledException>(() => Outcome(call));
        Assert.IsType<TimeoutException>(thrown.InnerException);
    }

    [Fact]
    public async Task Send_WhenTheRequestAsksForLonger_ThenItIsGivenThatAndNoMore()
    {
        var (invoker, clock) = Chain();
        var call = invoker.SendAsync(Request(TimeSpan.FromSeconds(150)), CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(149));
        Assert.False(await Settled(call));
        clock.Advance(TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<TaskCanceledException>(() => Outcome(call));
        Assert.IsType<TimeoutException>(thrown.InnerException);
    }

    [Fact]
    public async Task Send_WhenTheBodyStalls_ThenTheLimitCoversItToo()
    {
        var (invoker, clock) = Chain(new Hanging { StallBody = true });
        var call = invoker.SendAsync(Request(), CancellationToken.None);

        Assert.False(await Settled(call));
        clock.Advance(TimeSpan.FromSeconds(30));

        var thrown = await Assert.ThrowsAsync<TaskCanceledException>(() => Outcome(call));
        Assert.IsType<TimeoutException>(thrown.InnerException);
    }

    [Fact]
    public async Task Send_WhenTheCallerCancels_ThenItIsNotReportedAsATimeout()
    {
        var (invoker, _) = Chain();
        using var cancel = new CancellationTokenSource();
        var call = invoker.SendAsync(Request(), cancel.Token);

        await cancel.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Outcome(call));
        Assert.IsNotType<TimeoutException>(thrown.InnerException);
    }
}
