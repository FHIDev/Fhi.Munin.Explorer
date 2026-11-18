namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Whether the host offers the code picker and the signed-out reader's browser list
/// (<see cref="Client.MuninExplorerFeatures.CodePicker"/>); absent means off.</summary>
internal sealed record CodePickerSwitch(bool On);
