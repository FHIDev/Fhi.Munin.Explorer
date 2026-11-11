namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Whether the host offers avansert søk (<see cref="Client.MuninExplorerFeatures.AdvancedSearch"/>); absent means off.</summary>
internal sealed record AdvancedSearchSwitch(bool On);
