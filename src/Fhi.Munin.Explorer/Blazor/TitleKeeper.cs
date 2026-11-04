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
    internal async Task ApplyAsync(ExplorerInterop interop, string? template, string? name)
    {
        var wanted = PageTitle.Compose(template, name);

        // Nothing written until a view names itself: a host without a template keeps its title untouched.
        if (wanted == _shown && (_touched || wanted is null))
        {
            return;
        }

        // Before the await: renders overlap, and each would otherwise write the same title again.
        _shown = wanted;
        _touched = true;
        await interop.SetTitleAsync(wanted);
    }
}
