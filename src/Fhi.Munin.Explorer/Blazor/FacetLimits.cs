namespace Fhi.Munin.Explorer.Blazor;

/// <summary>What makes a facet long, for every filter panel this package draws.</summary>
/// <remarks>
/// One number for three surfaces — Kelda's facets, the variabelutforsker's and the saved list's —
/// so a reader meets one facet behaviour rather than three. It decides both halves at once: past it
/// a facet gets a search box of its own, and it is how many values are drawn before the rest go
/// behind "Vis N til".
/// </remarks>
internal static class FacetLimits
{
    /// <summary>How many values a facet has to have before a panel treats it as long.</summary>
    /// <remarks>
    /// Decided once and applied to every facet, never per facet: kildetype has five values and a
    /// box over five visible choices costs more attention than it saves, while databehandler runs
    /// to 39 and can be neither read nor scrolled past without one.
    /// </remarks>
    internal const int FacetSearchThreshold = 10;
}
