namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Whether a link may open one of the reader's own lists (<see cref="Client.MuninExplorerFeatures.ListLink"/>); absent means off.</summary>
internal sealed record ListLinkSwitch(bool On);
