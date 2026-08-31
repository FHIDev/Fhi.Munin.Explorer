namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The render-mode requirement the URL-state components cannot work without.</summary>
internal static class InteractiveMount
{
    /// <summary>
    /// Refuses a static or prerendered mount, loudly.
    /// </summary>
    /// <remarks>
    /// The failure it replaces is invisible: statically rendered, nothing calls into the browser and
    /// no callback fires, so the page draws and the address bar simply never follows the view. The
    /// host sees a working explorer that will not produce a shareable link, with nothing to search
    /// for. (Fhi.Metadata-zrcf4)
    /// </remarks>
    /// <exception cref="InvalidOperationException">The mount point is not interactive.</exception>
    internal static void Require(bool isInteractive, string component)
    {
        if (isInteractive)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{component} has to be mounted at an interactive render mode: render-mode=\"Server\" " +
            "in a legacy Blazor Server host, or @rendermode=\"new InteractiveServerRenderMode(prerender: false)\" " +
            "in a modern one. ServerPrerendered and static SSR both render it without a browser to " +
            "talk to, so it would never write the address bar and would fetch everything twice. " +
            "A host that does not want the URL touched should mount the explorer component itself.");
    }
}
