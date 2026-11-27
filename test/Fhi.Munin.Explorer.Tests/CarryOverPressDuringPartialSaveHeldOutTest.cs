using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class CarryOverPressDuringPartialSaveHeldOutTest : ExplorerTestContext
{
    private static readonly Texts No = Texts.For("no");

    private sealed class Client(VariableDatasamlingKey refused) : EmptyMuninExplorerClient
    {
        public List<string> Created { get; } = [];

        public TaskCompletionSource PageHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool PageReached { get; private set; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([]);

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            Created.Add(name);
            return Task.FromResult(new VariableList { Id = Guid.NewGuid(), Name = name });
        }

        public override Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default) =>
            items.Contains(refused)
                ? throw new ListItemsRefusedException([refused.VariableId], [refused])
                : Task.FromResult(true);

        public override async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            if (!PageReached)
            {
                PageReached = true;
                await PageHeld.Task;
            }

            return new Page<VariableListItem> { Items = [], TotalCount = 0, PageNumber = 1, Size = pageSize, TotalPages = 1 };
        }
    }

    private static IElement Offer(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("button").First(b => b.GetAttribute("aria-busy") != null
            || b.TextContent.Trim() == No.CarryBrowserListOver || b.TextContent.Trim() == No.SavingToList);

    [Fact]
    public void VariableListView_WhenTheOfferIsPressedAgainWhileAPartialSaveShowsItsNewList_ThenNoSecondListIsMade()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client(VariableDatasamlingKey.Of(browser.Rows[0]));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped(_ =>
        {
            var state = new VariableListState(client);
            state.OfferBrowserList(browser);
            return state;
        });
        Services.AddSingleton(new CodePickerSwitch(true));
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));

        Offer(cut).Click();
        cut.WaitForAssertion(() => Assert.True(client.PageReached));

        var offer = Offer(cut);
        Assert.True(
            offer.GetAttribute("aria-disabled") == "true",
            "the offer is enabled again while the first press still shows its new list");
        offer.Click();
        client.PageHeld.SetResult();

        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));
        Assert.Single(client.Created);
    }
}
