using System.Text.Json;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What crosses between the browser list's state and <c>explorer-interop.js</c> (Fhi.Metadata-emsc0.3.12): the export
/// names, the answer's field names and the key's spelling, which nothing else ties to the script.
/// </summary>
public class BrowserListStoreTest : ExplorerTestContext
{
    private static readonly Guid Variable = new("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90");
    private static readonly Guid Datasamling = new("3e5a8c11-7b42-49df-a6c8-1d904f2e6b73");

    [Fact]
    public void BrowserListStatus_WhenTheModulesAnswerIsRead_ThenEveryFieldLands()
    {
        // The module answers in camelCase; a renamed field reads as false or 0 and the list silently looks empty.
        var status = JsonSerializer.Deserialize<BrowserListStatus>(
            """{"ok":false,"count":3,"kept":true,"full":true,"foreign":true}""");

        Assert.Equal(new BrowserListStatus(false, 3, true, true, true), status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowserListKeys_WhenTheModulesSpellingIsParsed_ThenAnEmptyDatasamlingIsNone(bool withDatasamling)
    {
        var key = new VariableDatasamlingKey(Variable, withDatasamling ? Datasamling : null);

        Assert.Equal(key, BrowserListKeys.Parse(FakeBrowserList.Spelled(key)));
        Assert.Null(BrowserListKeys.Parse("not a key"));
    }

    [Fact]
    public async Task BrowserListStore_WhenItAdds_ThenTheRowCrossesInTheSnapshotsShapeWithNothingPrivate()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<BrowserListStatus>("anonymousListAdd", _ => true).SetResult(new BrowserListStatus(true, 1, true, false, false));
        var store = new BrowserListStore(JSInterop.JSRuntime);

        var status = await store.AddAsync(
            new VariableListItem { VariableId = Variable, DatasamlingId = Datasamling, VariableCode = "V", Notes = "mitt", DesiredDataFreeText = "alt" },
            2000);

        Assert.Equal(1, status!.Count);
        var call = Assert.Single(module.Invocations["anonymousListAdd"]);
        var row = JsonSerializer.SerializeToElement(call.Arguments[0]);
        Assert.Equal(Variable, row.GetProperty("variabelId").GetGuid());
        Assert.Equal(Datasamling, row.GetProperty("datasamlingId").GetGuid());
        Assert.False(row.TryGetProperty("notes", out _));
        Assert.False(row.TryGetProperty("desiredDataFreeText", out _));
        Assert.Equal(2000, call.Arguments[1]);
    }

    [Fact]
    public async Task BrowserListStore_WhenKeysAndAPageArrive_ThenTheyReadAsTheModuleSpellsThem()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<string[]>("anonymousListKeys", _ => true).SetResult([$"{Variable}|{Datasamling}", $"{Variable}|"]);
        module.Setup<JsonDocument>("anonymousListPage", _ => true).SetResult(JsonDocument.Parse(
            $$"""{"items":[{"variabelId":"{{Variable}}","datasamlingId":"{{Datasamling}}","variabelName":"Alder"}]}"""));
        var store = new BrowserListStore(JSInterop.JSRuntime);

        var keys = await store.KeysAsync(0, 100);
        var page = await store.PageAsync(0, 20);

        Assert.Equal([new VariableDatasamlingKey(Variable, Datasamling), new VariableDatasamlingKey(Variable, null)], keys);
        Assert.Equal("Alder", Assert.Single(page!).VariableName);
    }

    [Fact]
    public async Task BrowserListStore_WhenACarryOverRemovesItsRows_ThenTheKeysCrossInTheModulesSpelling()
    {
        // The same two literals test-anonymous-list.mjs hands anonymousListRemoveKeys.
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<BrowserListStatus>("anonymousListRemoveKeys", _ => true).SetResult(new BrowserListStatus(true, 0, true, false, false));
        var store = new BrowserListStore(JSInterop.JSRuntime);

        await store.RemoveKeysAsync([new VariableDatasamlingKey(Variable, Datasamling), new VariableDatasamlingKey(Variable, null)]);

        var sent = Assert.IsAssignableFrom<IEnumerable<string>>(Assert.Single(module.Invocations["anonymousListRemoveKeys"]).Arguments[0]);
        Assert.Equal([$"{Variable}|{Datasamling}", $"{Variable}|"], sent);
    }

    [Fact]
    public async Task BrowserListStore_WhenTheHostServesNoModule_ThenEveryCallAnswersNullRatherThanThrowing()
    {
        // The browser answers the import with a 404, as for a host that does not serve the file.
        var store = new BrowserListStore(new RefusingJsRuntime(new Microsoft.JSInterop.JSException("404")));

        Assert.Null(await store.CountAsync());
        Assert.Null(await store.KeysAsync(0, 100));
        Assert.False(await store.WatchAsync(() => Task.CompletedTask));
    }
}
