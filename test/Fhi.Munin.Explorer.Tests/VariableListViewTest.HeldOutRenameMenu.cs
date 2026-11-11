using Bunit;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    // The menu's rename item must not fold the form away under a rename still out, as the «Ny liste» toggle never does.
    [Fact]
    public async Task Menu_WhenGiNyttNavnIsChosenWhileARenameIsOut_ThenTheFormStaysAndTheRenameLands()
    {
        var renameHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { DuringRename = () => renameHeld.Task };
        var cut = RenderView(client);
        RenameField(cut).Change("Hjertet mitt");
        var rename = PressAsync(cut, "Lagre navnet");
        OpenMenu(cut);

        await ChooseAsync(cut, "Gi nytt navn");

        Assert.Single(cut.FindAll("input[id^='munin-explorer-rename-list-']"));

        await cut.InvokeAsync(renameHeld.SetResult);
        await rename;

        cut.WaitForAssertion(() => Assert.Equal("Hjertet mitt", ListHeading(cut).TextContent));
        Assert.Equal(1, client.RenameCalls);
        Assert.Equal("Hjertet mitt", client.LastRenamedTo);
    }
}
