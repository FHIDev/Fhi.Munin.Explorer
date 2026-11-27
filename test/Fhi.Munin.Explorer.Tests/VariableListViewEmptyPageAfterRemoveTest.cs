using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// A page re-read that lands empty past page 1 steps back, wherever the emptying came from (Fhi.Metadata-emsc0.3.26).
public class VariableListViewEmptyPageAfterRemoveTest : ExplorerTestContext
{
    private const string FocusIfLost = "focusIfLost";
    private const string EmptyText = "Denne listen er tom";

    private static readonly Guid ListId = new("22222222-2222-2222-2222-222222222222");

    private sealed class HoldingClient(params string[] names) : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> _items = [.. names.Select((n, i) => Item(n, $"V_{i}"))];

        public IReadOnlyList<VariableListItem> Items => _items;

        /// <summary>Armed by a test; every page read after that waits for it.</summary>
        public TaskCompletionSource? HoldRead { get; set; }

        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = _items.Count }]);

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(i => variableIds.Contains(i.VariableId));
            return Task.FromResult(true);
        }

        public override async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            if (HoldRead is { } hold)
            {
                ReadEntered.TrySetResult();
                await hold.Task;
            }

            return new Page<VariableListItem>
            {
                Items = [.. _items.Skip((page - 1) * pageSize).Take(pageSize)],
                TotalCount = _items.Count,
                PageNumber = page,
                Size = pageSize,
                TotalPages = Math.Max(1, (int)Math.Ceiling(_items.Count / (double)pageSize))
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

    private (IRenderedComponent<VariableListView> Cut, BunitJSModuleInterop Module) RenderOnPageTwo(HoldingClient client)
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<bool>(FocusIfLost, _ => true).SetResult(true);
        module.Setup<bool>("watchPresses").SetResult(true);
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.PageSize, 2));
        cut.FindAll(".munin-explorer-pagination-content button")[^1].Click();
        cut.WaitForAssertion(() => Assert.Contains("Bosted", cut.Markup));

        return (cut, module);
    }

    private static List<AngleSharp.Dom.IElement> RemoveButtons(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("tbody tr td:last-child button")];

    [Fact]
    public async Task Remove_WhenTheOnlyRowOfALaterPageGoesWhileTheReReadIsHeld_ThenThePageBeforeShowsAndItsLastRowTakesFocus()
    {
        // The re-read lands after the press has returned, so a check made at the press saw rows and never stepped back.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted");
        var (cut, module) = RenderOnPageTwo(client);
        client.HoldRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await cut.InvokeAsync(() => Assert.Single(RemoveButtons(cut)).Click());
        await client.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        client.HoldRead.SetResult();

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        Assert.DoesNotContain(EmptyText, cut.Markup);
        Assert.Contains("Kjønn", cut.Markup);
        var last = RemoveButtons(cut)[^1].Id;
        cut.WaitForAssertion(() => Assert.Equal(last, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
    }

    [Fact]
    public async Task Reload_WhenAPageReadPastPageOneLandsEmptyWhileTheListHasRows_ThenItStepsBackAndMovesNoFocus()
    {
        // Emptied from outside this view's «Fjern», as another tab or surface would: no removal here owns focus.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted");
        var (cut, module) = RenderOnPageTwo(client);
        var state = Services.GetRequiredService<VariableListState>();

        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [client.Items[2].VariableId]));

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        Assert.DoesNotContain(EmptyText, cut.Markup);
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == FocusIfLost);
    }
}
