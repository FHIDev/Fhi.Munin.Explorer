using Fhi.Munin.Explorer.Display;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Writes an item view's page title when it changes, and the host's own back on the list (Fhi.Metadata-mhqx0.14).</summary>
internal sealed class TitleKeeper
{
    private string? _shown;
    private bool _touched;

    /// <summary>Whether this render may need the module: a template is given, or a title was written to undo.</summary>
    internal bool Wanted(string? template) => template is not null || _touched;

    /// <summary>Called from OnAfterRenderAsync with a module already loaded there.</summary>
    internal async Task ApplyAsync(ExplorerInterop interop, string? template, string? name, bool loading = false)
    {
        var wanted = PageTitle.Compose(template, name);

        // A fresh keeper with a template writes once even for the host's title: Back remounts it on the list
        // after an item's title was set. Nothing while the next item loads, so the title does not flicker.
        if (template is null && !_touched || loading || (_touched && wanted == _shown))
        {
            return;
        }

        // Before the await: renders overlap, and each would otherwise write the same title again.
        _shown = wanted;
        _touched = true;
        await interop.SetTitleAsync(wanted);
    }
}
