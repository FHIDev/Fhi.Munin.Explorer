namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Whether the host formats catalogue text (<see cref="Client.MuninExplorerFeatures.CatalogueFormatting"/>); absent means off.</summary>
internal sealed record CatalogueFormattingSwitch(bool On);
