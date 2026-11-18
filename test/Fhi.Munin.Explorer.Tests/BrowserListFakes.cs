using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The browser's side of the anonymous list, as <c>explorer-interop.js</c> keeps it: every call reads, changes and
/// writes the one stored list, so two states over the same instance are two tabs over the same storage.
/// </summary>
internal sealed class FakeBrowserList : IBrowserListStore
{
    public List<VariableListItem> Rows { get; } = [];

    /// <summary>Storage refuses: the module keeps the list for the page and says it is not kept.</summary>
    public bool Refuses { get; set; }

    /// <summary>No module: every call answers null.</summary>
    public bool Unreachable { get; set; }

    /// <summary>Thrown from every call, as a fault inside the module crosses the circuit.</summary>
    public Exception? Throws { get; set; }

    /// <summary>The key holds something this version cannot read: answers say foreign and not kept.</summary>
    public bool Foreign { get; set; }

    /// <summary>Calls by these names answer null, as a module that failed only those would.</summary>
    public HashSet<string> Failing { get; } = [];

    /// <summary>
    /// While set, every call works out its answer when it is made, as the browser does, and hands it back only when
    /// a test releases it from <see cref="Pending"/>: the round trip, made as long as a test needs.
    /// </summary>
    public bool Hold { get; set; }

    public List<(string Call, TaskCompletionSource Release)> Pending { get; } = [];

    /// <summary>Releases the newest held call, the order a slow network can deliver answers in.</summary>
    public void ReleaseNewest()
    {
        var (_, release) = Pending[^1];
        Pending.RemoveAt(Pending.Count - 1);
        release.SetResult();
    }

    /// <summary>Every call, with the size it asked for, so a test can see nothing large crossed at once.</summary>
    public List<(string Call, int Offset, int Count)> Calls { get; } = [];

    /// <summary>Every tab's listener; <see cref="ChangedElsewhereAsync"/> is the storage event reaching them.</summary>
    public List<Func<Task>> Watchers { get; } = [];

    public async Task ChangedElsewhereAsync()
    {
        foreach (var watcher in Watchers.ToList())
        {
            await watcher();
        }
    }

    private static string Key(VariableDatasamlingKey key) => $"{key.VariableId}|{key.DatasamlingId}";

    private async Task<T?> Answer<T>(string call, Func<T> answer, int offset = 0, int count = 0) where T : class
    {
        Calls.Add((call, offset, count));

        if (Throws is { } thrown)
        {
            throw thrown;
        }

        var answered = Unreachable || Failing.Contains(call) ? null : answer();

        if (Hold)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending.Add((call, release));
            await release.Task;
        }

        return answered;
    }

    private BrowserListStatus Status(bool ok = true, bool full = false) => new(ok, Rows.Count, !Refuses && !Foreign, full, Foreign);

    public Task<BrowserListStatus?> CountAsync() => Answer("count", () => Status());

    public Task<BrowserListStatus?> AddAsync(VariableListItem row, int max) => Answer("add", () =>
    {
        if (Rows.Any(r => VariableDatasamlingKey.Of(r) == VariableDatasamlingKey.Of(row)))
        {
            return Status();
        }

        if (Rows.Count >= max)
        {
            return Status(ok: false, full: true);
        }

        Rows.Add(row);
        return Status();
    });

    public Task<BrowserListStatus?> RemoveAsync(VariableDatasamlingKey key) => Answer("remove", () =>
    {
        Rows.RemoveAll(r => VariableDatasamlingKey.Of(r) == key);
        return Status();
    });

    /// <summary>A removal storage refused while the rows are in the stored value, where another page still finds them.</summary>
    public bool KeepsStored { get; set; }

    /// <summary>Rows the module hands back as their ids and code alone, too long to send whole.</summary>
    public HashSet<VariableDatasamlingKey> Truncates { get; } = [];

    public Task<BrowserListStatus?> RemoveKeysAsync(IReadOnlyCollection<VariableDatasamlingKey> keys) => Answer("removeKeys", () =>
    {
        Rows.RemoveAll(r => keys.Contains(VariableDatasamlingKey.Of(r)));
        return Status() with { StillStored = KeepsStored };
    });

    public Task<BrowserListStatus?> ClearAsync() => Answer("clear", () =>
    {
        Rows.Clear();
        return Status();
    });

    public Task<IReadOnlyList<VariableDatasamlingKey>?> KeysAsync(int offset, int count) =>
        Answer<IReadOnlyList<VariableDatasamlingKey>>(
            "keys", () => [.. Rows.Skip(offset).Take(count).Select(VariableDatasamlingKey.Of)], offset, count);

    public Task<IReadOnlyList<BrowserRow>?> PageAsync(int offset, int count) =>
        Answer<IReadOnlyList<BrowserRow>>("page", () =>
            [.. Rows.Skip(offset).Take(count).Select(r => new BrowserRow(r, Truncates.Contains(VariableDatasamlingKey.Of(r))))], offset, count);

    public Task<bool> WatchAsync(Func<Task> changedElsewhere)
    {
        Watchers.Add(changedElsewhere);
        return Task.FromResult(!Unreachable);
    }

    public static VariableListItem Row(string name, Guid? datasamling = null) => new()
    {
        VariableId = Guid.NewGuid(),
        VariableCode = name.ToUpperInvariant(),
        VariableName = name,
        DatasamlingId = datasamling,
        KildeName = "Als registeret",
    };

    /// <summary>The module's own spelling of a key, for a test that checks what it parses.</summary>
    public static string Spelled(VariableDatasamlingKey key) => Key(key);
}
