using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>«Kolonner» as one of the list menus: whether it is open, its ids, and its keys.</summary>
/// <remarks>
/// Held by the component that draws the picker: <paramref name="opened"/> closes its other menus, and
/// <paramref name="moved"/>, when given, hears every press and Escape that moves this one.
/// </remarks>
internal sealed class ColumnsFold(string instance, Action opened, Action? moved = null)
{
    internal bool Open { get; private set; }

    internal string ToggleId => $"munin-explorer-columns-toggle-{instance}";

    internal string PanelId => $"munin-explorer-columns-{instance}";

    internal ElementReference Toggle { get; set; }

    internal void Press(MouseEventArgs released)
    {
        if (RowPress.WasSelectionStandingStill(released))
        {
            return;
        }

        Open = !Open;
        moved?.Invoke();
        if (Open)
        {
            opened();
        }
    }

    internal void Close() => Open = false;

    internal async Task CloseOnEscapeAsync(KeyboardEventArgs pressed)
    {
        if (pressed.Key != "Escape" || !Open)
        {
            return;
        }

        Open = false;
        moved?.Invoke();
        await Toggle.FocusAsync();
    }
}
