namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Whether the filter panel is folded away on a wide screen, kept in this browser for both explorers
/// (Fhi.Metadata-amp8c). The phone fold is a separate state: there the panel starts folded.
/// </summary>
internal sealed class FilterFoldPreference
{
    internal const string Key = "munin-explorer.filters-collapsed";

    private bool _read;
    private int _presses;

    internal bool Collapsed { get; private set; }

    /// <summary>Reads the kept choice once the module is up; true when it changed what is drawn.</summary>
    internal async Task<bool> ReadOnceAsync(ExplorerInterop interop)
    {
        if (_read || !interop.IsLoaded)
        {
            return false;
        }

        _read = true;
        var presses = _presses;
        var collapsed = await interop.ReadPreferenceAsync(Key) == "1";

        // A press while the read was out is the newer choice, and the stored one must not undo it.
        if (presses != _presses || collapsed == Collapsed)
        {
            return false;
        }

        Collapsed = collapsed;

        return true;
    }

    /// <summary>A press: the choice flips at once, and is kept where the browser lets it be.</summary>
    internal Task ToggleAsync(ExplorerInterop? interop)
    {
        Collapsed = !Collapsed;
        _read = true;
        _presses++;

        return interop?.WritePreferenceAsync(Key, Collapsed ? "1" : null) ?? Task.CompletedTask;
    }
}
