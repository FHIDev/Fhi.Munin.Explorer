using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// A page re-read that lands empty past page 1 steps back, wherever the emptying came from (Fhi.Metadata-emsc0.3.26).
public class VariableListViewEmptyPageAfterRemoveTest : ExplorerTestContext
{
    private const string FocusIfLost = "focusIfLost";
    private const string EmptyText = "Denne listen er tom";

    private static readonly Guid ListId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherListId = new("33333333-3333-3333-3333-333333333333");

    private sealed class HoldingClient(params string[] names) : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> _items = [.. names.Select((n, i) => Item(n, $"V_{i}"))];
        private readonly List<VariableListItem> _other = [Item("Høyde", "V_OTHER")];
        private readonly Dictionary<Guid, VariableListItem> _removed = [];

        public IReadOnlyList<VariableListItem> Items => _items;

        public List<int> PagesAsked { get; } = [];

        /// <summary>Armed by a test; every page read after that waits for it.</summary>
        public TaskCompletionSource? HoldRead { get; set; }

        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Armed by a test; the next removal is written at once and answers when it is set.</summary>
        public TaskCompletionSource? HoldRemove { get; set; }

        public TaskCompletionSource RemoveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private List<VariableListItem> Rows(Guid id) => id == OtherListId ? _other : _items;

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = _items.Count },
                 new VariableList { Id = OtherListId, Name = "Mine høydevariabler", VariableCount = _other.Count }]);

        public override async Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            foreach (var gone in Rows(id).Where(i => variableIds.Contains(i.VariableId)))
            {
                _removed[gone.VariableId] = gone;
            }

            Rows(id).RemoveAll(i => variableIds.Contains(i.VariableId));

            if (HoldRemove is { } hold)
            {
                HoldRemove = null;
                RemoveEntered.TrySetResult();
                await hold.Task;
            }

            return true;
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            Rows(id).AddRange(variableIds.Select(v => _removed[v]));
            return Task.FromResult(true);
        }

        public override async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            PagesAsked.Add(page);

            if (HoldRead is { } hold)
            {
                ReadEntered.TrySetResult();
                await hold.Task;
            }

            var rows = Rows(id);

            return new Page<VariableListItem>
            {
                Items = [.. rows.Skip((page - 1) * pageSize).Take(pageSize)],
                TotalCount = rows.Count,
                PageNumber = page,
                Size = pageSize,
                TotalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)pageSize))
            };
        }
    }

    private static VariableListItem Item(string name, string code) =>
        new()
        {
            VariableId = Guid.NewGuid(),
            AddedAt = DateTimeOffset.UtcNow,
            VariableName = name,
            VariableCode = code,
            KildeName = "Als registeret",
            KildeShortName = "ALS",
            DatasamlingName = "Inklusjon",
            VariabelgruppeName = "Ikke oppgitt",
            DataType = "2"
        };

    private (IRenderedComponent<VariableListView> Cut, BunitJSModuleInterop Module) RenderOnLastPage(HoldingClient client)
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<bool>(FocusIfLost, _ => true).SetResult(true);
        module.Setup<bool>("watchPresses").SetResult(true);
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.PageSize, 2));
        cut.FindAll(".munin-explorer-pagination-pages > button")[^1].Click();
        cut.WaitForAssertion(() => Assert.Contains(client.Items[^1].VariableName!, cut.Markup));

        return (cut, module);
    }

    private static List<AngleSharp.Dom.IElement> RemoveButtons(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("tbody tr td:last-child button")];

    private static Task PressPage(IRenderedComponent<VariableListView> cut, string number) =>
        cut.InvokeAsync(() => cut.FindAll(".munin-explorer-pagination-pages > button").Single(b => b.TextContent.Trim() == number).Click());

    private static string Said(IRenderedComponent<VariableListView> cut) =>
        cut.Find("[role=status].screenreader-only").TextContent;

    // The press is held, and the page it was made on is emptied and stepped back from elsewhere meanwhile.
    private async Task<(IRenderedComponent<VariableListView> Cut, BunitJSModuleInterop Module, Task Press, TaskCompletionSource Release)>
        StepBackUnderAHeldRemovalAsync(params string[] names)
    {
        var client = new HoldingClient(names);
        var (cut, module) = RenderOnLastPage(client);
        var state = Services.GetRequiredService<VariableListState>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.HoldRemove = release;
        var first = client.Items[0].VariableId;

        var press = cut.InvokeAsync(() => Assert.Single(RemoveButtons(cut)).ClickAsync(new MouseEventArgs()));
        await client.RemoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [first]));

        return (cut, module, press, release);
    }

    [Fact]
    public async Task View_WhenTheOnlyRowOfALaterPageGoesWhileTheReReadIsHeld_ThenThePageBeforeShowsAndItsLastRowTakesFocus()
    {
        // The re-read lands after the press has returned, so a check made at the press saw rows and never stepped back.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted");
        var (cut, module) = RenderOnLastPage(client);
        client.HoldRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await cut.InvokeAsync(() => Assert.Single(RemoveButtons(cut)).Click());
        await client.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        client.HoldRead.SetResult();

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        Assert.DoesNotContain(EmptyText, cut.Markup);
        Assert.Contains("Kjønn", cut.Markup);
        Assert.Contains("2 variabler", cut.Markup);
        var last = RemoveButtons(cut)[^1].Id;
        cut.WaitForAssertion(() => Assert.Equal(last, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
    }

    [Fact]
    public async Task View_WhenAPageReadPastPageOneLandsEmptyWhileTheListHasRows_ThenItStepsBackAndMovesNoFocus()
    {
        // Emptied through the list state, as another surface on the same page would: no removal here owns focus.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted");
        var (cut, module) = RenderOnLastPage(client);
        var state = Services.GetRequiredService<VariableListState>();

        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [client.Items[2].VariableId]));

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        Assert.DoesNotContain(EmptyText, cut.Markup);
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == FocusIfLost);
    }

    [Fact]
    public async Task View_WhenTheReReadLandsBeforeTheRemovalReturns_ThenFocusStillGoesToTheLastRowAndTheRemovalIsSaid()
    {
        // A read that answers at once steps back before the press's own code resumes, on a page the press was not made on.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted");
        var (cut, module) = RenderOnLastPage(client);

        await cut.InvokeAsync(() => Assert.Single(RemoveButtons(cut)).Click());

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        var last = RemoveButtons(cut)[^1].Id;
        cut.WaitForAssertion(() => Assert.Equal(last, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
        Assert.Contains("fjernet fra listen", cut.Find("[role=status].screenreader-only").TextContent);
    }

    [Fact]
    public async Task View_WhenAPageReadLandsEmptyPagesPastTheLastOne_ThenTheNextReadIsTheLastPageWithRows()
    {
        // Stepping one page at a time would read every emptied page between; the answer's own total names the last one.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted", "Diagnose", "Vekt");
        var (cut, _) = RenderOnLastPage(client);
        var state = Services.GetRequiredService<VariableListState>();
        client.PagesAsked.Clear();

        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [.. client.Items.Skip(2).Select(i => i.VariableId)]));

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        Assert.Equal([3, 1], client.PagesAsked);
    }

    [Fact]
    public async Task View_WhenTheReaderPagesAfterAStepBackDuringTheirRemoval_ThenTheRemovalIsNotSaidOnThePageTheyChose()
    {
        // The step back moved this press's page, and the reader then chose another one: the press is stale there.
        var (cut, module, press, release) = await StepBackUnderAHeldRemovalAsync("Alder ved diagnose", "Kjønn", "Bosted", "Diagnose", "Vekt");
        cut.WaitForAssertion(() => Assert.Single(RemoveButtons(cut)));

        await PressPage(cut, "1");
        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        release.SetResult();
        await press;

        cut.WaitForAssertion(() => Assert.Contains("Kjønn", cut.Markup));
        Assert.DoesNotContain("fjernet fra listen", Said(cut));
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == FocusIfLost);
    }

    [Fact]
    public async Task View_WhenTheReaderSwitchesListAfterAStepBackDuringTheirRemoval_ThenTheRemovalIsNotSaidInTheOtherList()
    {
        // Page 1 of the other list is the page the step back went to, so only the list tells the two apart.
        var (cut, module, press, release) = await StepBackUnderAHeldRemovalAsync("Alder ved diagnose", "Kjønn", "Bosted");
        cut.WaitForAssertion(() => Assert.Single(RemoveButtons(cut)));

        await cut.InvokeAsync(() => cut.Find("label select").Change(OtherListId.ToString()));
        cut.WaitForAssertion(() => Assert.Contains("Høyde", cut.Markup));
        release.SetResult();
        await press;

        Assert.DoesNotContain("fjernet fra listen", Said(cut));
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == FocusIfLost);
    }

    [Fact]
    public async Task View_WhenAnEarlierStepBackMatchesALaterRemovalAndPageTurn_ThenTheLaterRemovalIsNotSaid()
    {
        // A step back recorded before this press is not this press's: left standing, it would pass for one.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted", "Diagnose", "Vekt");
        var (cut, module) = RenderOnLastPage(client);
        var state = Services.GetRequiredService<VariableListState>();
        var last = client.Items[^1].VariableId;

        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [last]));
        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [last]));
        await PressPage(cut, "3");
        cut.WaitForAssertion(() => Assert.Single(RemoveButtons(cut)));

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.HoldRemove = release;
        var press = cut.InvokeAsync(() => Assert.Single(RemoveButtons(cut)).ClickAsync(new MouseEventArgs()));
        await client.RemoveEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await PressPage(cut, "2");
        release.SetResult();
        await press;

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        Assert.DoesNotContain("fjernet fra listen", Said(cut));
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == FocusIfLost);
    }

    [Fact]
    public async Task View_WhenAPageIsSteppedBackFromAfterARemovalOnItHasTakenItsFocus_ThenNothingMovesFocusAndTheNextRemovalFocusesItsOwnNeighbour()
    {
        // No removal is waiting, yet the finished one left its page behind: the step back must not revive it or its last-row pick.
        var client = new HoldingClient("A", "B", "C", "D", "Enhet", "Fødeland", "Gruppe", "Høyde");
        var (cut, module) = RenderOnLastPage(client);
        var state = Services.GetRequiredService<VariableListState>();
        var stayed = RemoveButtons(cut)[1].Id;

        await cut.InvokeAsync(() => RemoveButtons(cut)[0].ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.Equal(stayed, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));

        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [client.Items[^1].VariableId]));
        cut.WaitForAssertion(() => Assert.Contains("Fødeland", cut.Markup));
        Assert.Single(module.Invocations[FocusIfLost]);

        var neighbour = RemoveButtons(cut)[1].Id;
        await cut.InvokeAsync(() => RemoveButtons(cut)[0].ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal(2, module.Invocations[FocusIfLost].Count));
        Assert.Equal(neighbour, module.Invocations[FocusIfLost][^1].Arguments[0]);
    }
}
