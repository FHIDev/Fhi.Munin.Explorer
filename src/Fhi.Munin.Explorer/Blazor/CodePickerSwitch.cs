namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Whether the host offers the code picker (<see cref="Client.MuninExplorerFeatures.CodePicker"/>); absent means off.</summary>
internal sealed record CodePickerSwitch(bool On);
