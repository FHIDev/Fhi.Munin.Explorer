namespace Fhi.Munin.Explorer.Tests;

/// <summary>Fires each timer only when the test says the time has passed.</summary>
internal sealed class ManualClock : TimeProvider
{
    // Locked: a cancelled wait disposes its timer from the renderer's thread while the test advances.
    private readonly List<(TimeSpan Due, TimerCallback Callback, object? State)> _timers = [];
    private readonly Lock _gate = new();
    private TimeSpan _now;
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return Start + _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var timer = (_now + dueTime, callback, state);
            _timers.Add(timer);
            return new Handle(() => { lock (_gate) { _timers.Remove(timer); } });
        }
    }

    public void Advance(TimeSpan by)
    {
        List<(TimeSpan Due, TimerCallback Callback, object? State)> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.Where(t => t.Due <= _now)];
            _timers.RemoveAll(due.Contains);
        }
        foreach (var timer in due)
        {
            timer.Callback(timer.State);
        }
    }

    private sealed class Handle(Action stop) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() => stop();
        public ValueTask DisposeAsync() { stop(); return ValueTask.CompletedTask; }
    }
}
