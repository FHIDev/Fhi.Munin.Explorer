using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Display;

namespace Fhi.Munin.Explorer.Tests;

public class PageTitleTest : ExplorerTestContext
{
    [Theory]
    [InlineData(null, "NPR", null)]
    [InlineData("  ", "NPR", null)]
    [InlineData("Kildeutforsker", "NPR", null)]
    [InlineData("{0} – Kildeutforsker", null, null)]
    [InlineData("{0} – Kildeutforsker", "  ", null)]
    [InlineData("{0} – Kildeutforsker", " NPR ", "NPR – Kildeutforsker")]
    public void Compose_GivesTheNamedTitleOrNullForTheHostsOwn(string? template, string? name, string? expected)
    {
        // Null is the host's own title: a missing {0} or name never yields «– Kildeutforsker» (Fhi.Metadata-mhqx0.14).
        Assert.Equal(expected, PageTitle.Compose(template, name));
    }

    [Fact]
    public async Task ApplyAsync_WritesATitleOnceAndGivesTheHostItsOwnBackOnTheList()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.SetupVoid("setTitle", _ => true).SetVoidResult();
        var interop = new ExplorerInterop(JSInterop.JSRuntime);
        Assert.True(await interop.TryLoadAsync());
        var title = new TitleKeeper();

        await title.ApplyAsync(interop, "{0} – Kildeutforsker", "NPR");
        await title.ApplyAsync(interop, "{0} – Kildeutforsker", "NPR");
        await title.ApplyAsync(interop, "{0} – Kildeutforsker", null);

        Assert.Equal(["NPR – Kildeutforsker", null], module.Invocations["setTitle"].Select(call => call.Arguments[0] as string));
    }

    [Fact]
    public async Task ApplyAsync_InAFreshKeeperOnTheList_GivesTheHostItsTitleBackOnce()
    {
        // Back remounts the search on the list with a fresh keeper, after the old one set an item's title.
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.SetupVoid("setTitle", _ => true).SetVoidResult();
        var interop = new ExplorerInterop(JSInterop.JSRuntime);
        Assert.True(await interop.TryLoadAsync());
        var remounted = new TitleKeeper();

        Assert.True(remounted.Wanted("{0} – Kildeutforsker"));
        await remounted.ApplyAsync(interop, "{0} – Kildeutforsker", null);
        await remounted.ApplyAsync(interop, "{0} – Kildeutforsker", null);

        Assert.Equal([null], module.Invocations["setTitle"].Select(call => call.Arguments[0] as string));
    }

    [Fact]
    public async Task ApplyAsync_WhileTheNextItemLoads_LeavesTheTitleAsItIs()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.SetupVoid("setTitle", _ => true).SetVoidResult();
        var interop = new ExplorerInterop(JSInterop.JSRuntime);
        Assert.True(await interop.TryLoadAsync());
        var title = new TitleKeeper();

        await title.ApplyAsync(interop, "{0} – Kildeutforsker", "NPR");
        await title.ApplyAsync(interop, "{0} – Kildeutforsker", null, loading: true);
        await title.ApplyAsync(interop, "{0} – Kildeutforsker", "ALS");

        Assert.Equal(["NPR – Kildeutforsker", "ALS – Kildeutforsker"], module.Invocations["setTitle"].Select(call => call.Arguments[0] as string));
    }

    [Fact]
    public async Task ApplyAsync_WithoutATemplate_NeverTouchesTheTitle()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.SetupVoid("setTitle", _ => true).SetVoidResult();
        var interop = new ExplorerInterop(JSInterop.JSRuntime);
        Assert.True(await interop.TryLoadAsync());
        var title = new TitleKeeper();

        Assert.False(title.Wanted(null));
        await title.ApplyAsync(interop, null, "NPR");
        await title.ApplyAsync(interop, null, null);

        Assert.Empty(module.Invocations["setTitle"]);
    }
}
