using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class FilterFoldPreferenceTest : ExplorerTestContext
{
    [Fact]
    public async Task ToggleAsync_WhenPressedBeforeTheModuleIsUp_ThenTheChoiceIsKeptOnceItIsAndNotReadOver()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<string?>("readPreference", FilterFoldPreference.Key).SetResult(null);
        module.SetupVoid("writePreference", _ => true).SetVoidResult();
        var interop = new ExplorerInterop(JSInterop.JSRuntime);
        var fold = new FilterFoldPreference();

        await fold.ToggleAsync(interop);
        Assert.True(await interop.TryLoadAsync());
        var changed = await fold.ReadOnceAsync(interop);

        Assert.False(changed);
        Assert.True(fold.Collapsed);
        Assert.Contains(module.Invocations["writePreference"],
            call => Equals(call.Arguments[0], FilterFoldPreference.Key) && Equals(call.Arguments[1], "1"));
        Assert.Empty(module.Invocations["readPreference"]);
    }
}
