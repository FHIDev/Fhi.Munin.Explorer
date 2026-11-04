namespace Fhi.Munin.Explorer.Tests;

/// <summary>Fires each timer only when the test says the time has passed.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<(TimeSpan Due, TimerCallback Callback, object? State)> _timers = [];
    private TimeSpan _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = (_now + dueTime, callback, state);
        _timers.Add(timer);
        return new Handle(() => _timers.Remove(timer));
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

    private sealed class Handle(Action stop) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() => stop();
        public ValueTask DisposeAsync() { stop(); return ValueTask.CompletedTask; }
    }
}
