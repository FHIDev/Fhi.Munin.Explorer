using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// The signed-in «Fjern» asks for focus only once its row has left the page, with the press watch on (Fhi.Metadata-emsc0.3.24).
public class VariableListViewRemoveFocusHeldOutTest : ExplorerTestContext
{
    private const string FocusIfLost = "focusIfLost";
    private const string WatchPresses = "watchPresses";

    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");

    private sealed class HoldingClient(params string[] names) : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> _items = [.. names.Select((n, i) => Item(n, $"V_{i}"))];

        /// <summary>Armed after the first render; every page read after that waits for it.</summary>
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

            var slice = _items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new Page<VariableListItem>
            {
                Items = slice,
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

    private (IRenderedComponent<VariableListView> Cut, BunitJSModuleInterop Module) RenderSignedIn(HoldingClient client)
    {
        var module = PlanExplorerModule();
        module.Setup<bool>(FocusIfLost, _ => true).SetResult(true);
        module.Setup<bool>(WatchPresses).SetResult(true);
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        return (Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true)), module);
    }

    private static List<AngleSharp.Dom.IElement> RemoveButtons(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("tbody tr td:last-child button")];

    private static int Calls(BunitJSModuleInterop module, string identifier) =>
        module.Invocations.Count(i => i.Identifier == identifier);

    [Fact]
    public void Render_WhenTheSignedInListShowsRows_ThenThePressWatchIsStartedBeforeAnyFjernPress()
    {
        // Without the watch, focusIfLost cannot tell a click on the host page from the button leaving the page.
        var (cut, module) = RenderSignedIn(new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted"));

        Assert.Equal(3, RemoveButtons(cut).Count);
        cut.WaitForAssertion(() => Assert.True(Calls(module, WatchPresses) > 0));
    }

    [Fact]
    public async Task Remove_WhenThePageReReadIsHeld_ThenFocusIsAskedOnlyOnceTheReadLandsAndForTheRowInItsPlace()
    {
        // The removal's own render still draws the pressed, focused «Fjern»; asking then is refused and spends the request.
        var client = new HoldingClient("Alder ved diagnose", "Kjønn", "Bosted");
        var (cut, module) = RenderSignedIn(client);
        var next = RemoveButtons(cut)[2].Id;
        client.HoldRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await cut.InvokeAsync(() => RemoveButtons(cut)[1].Click());
        await client.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        for (var i = 0; i < 3; i++)
        {
            await cut.InvokeAsync(() => { });
        }

        Assert.Equal(3, RemoveButtons(cut).Count);
        Assert.Equal(0, Calls(module, FocusIfLost));

        client.HoldRead.SetResult();

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        cut.WaitForAssertion(() => Assert.Equal(next, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
        Assert.Equal(1, Calls(module, FocusIfLost));
    }
}
