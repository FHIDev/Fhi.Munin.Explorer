namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Whether the host offers a signed-out reader a list in the browser (<see cref="Client.MuninExplorerFeatures.AnonymousLists"/>); absent means off.</summary>
internal sealed record AnonymousListsSwitch(bool On);
