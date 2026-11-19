using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Held-out: the list quota on saving a shared list and on copying a list (Fhi.Metadata-oiips.61).</summary>
public class ListQuotaHeldOutTest : ExplorerTestContext
{
    private static readonly Guid SourceId = new("11111111-1111-1111-1111-111111111111");

    private static VariableListItem Item(int index) => new()
    {
        VariableId = Guid.NewGuid(),
        AddedAt = DateTimeOffset.UtcNow,
        VariableName = $"Variabel {index:0000}",
        VariableCode = $"V{index:0000}",
    };

    private static readonly VariableListItem[] Three = [Item(1), Item(2), Item(3)];

    private sealed class SharedClient(MuninExplorerListQuotaException? createRefusal, MuninExplorerListQuotaException? addRefusal)
        : EmptyMuninExplorerClient
    {
        public Exception? CreateFailure { get; set; } = createRefusal;
        public List<Guid> Deleted { get; } = [];

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Deleted.Add(id);
            return Task.FromResult(true);
        }

        public override Task<SharedList?> GetSharedListAsync(string? code, CancellationToken cancellationToken = default) =>
            Task.FromResult<SharedList?>(new SharedList("Delt liste", Three));

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([]);

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default) =>
            CreateFailure is not null
                ? throw CreateFailure
                : Task.FromResult(new VariableList { Id = Guid.NewGuid(), Name = name });

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
            addRefusal is not null ? throw addRefusal : Task.FromResult(true);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>());
    }

    private sealed class CopyClient : EmptyMuninExplorerClient
    {
        public Exception? CreateFailure { get; set; }
        public Exception AddFailure { get; set; } = new MuninExplorerListQuotaException(ListQuotaKind.Rows, 50, "Maks 50 rader.");

        // So the copy the first press leaves on screen can be copied again.
        public bool EveryListHoldsThree { get; set; }

        private readonly List<VariableList> _lists = [new VariableList { Id = SourceId, Name = "Kilde" }];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [.. _lists.Select(l => l with { VariableCount = l.Id == SourceId || EveryListHoldsThree ? Three.Length : 0 })]);

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            if (CreateFailure is not null)
            {
                throw CreateFailure;
            }

            var created = new VariableList { Id = Guid.NewGuid(), Name = name };
            _lists.Add(created);
            return Task.FromResult(created);
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
            throw AddFailure;

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            var items = id == SourceId || EveryListHoldsThree ? Three : [];

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. items.Skip((page - 1) * pageSize).Take(pageSize)],
                TotalCount = items.Length,
                PageNumber = page,
                Size = pageSize,
                TotalPages = 1,
            });
        }
    }

    private IRenderedComponent<VariableListView> RenderView(IMuninExplorerClient client, string? shareCode = null)
    {
        Services.AddSingleton(client);
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));

        return Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "no")
            .Add(c => c.ShareCode, shareCode));
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<VariableListView> cut, string text) =>
        cut.FindAll("button").Single(b => AccessibleName.Of(b) == text);

    private static string Alert(IRenderedComponent<VariableListView> cut) =>
        cut.Find("div[role=alert][aria-live=assertive]").TextContent.Trim();

    [Fact]
    public void VariableListView_WhenMuninRefusesTheCreateOfASharedListAtTheListQuota_ThenTheAlertSaysTheLimit()
    {
        var texts = Texts.For("no");
        var refusal = new MuninExplorerListQuotaException(ListQuotaKind.Lists, 50, "Maks 50 lister.");
        var cut = RenderView(new SharedClient(refusal, null), shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));

        Button(cut, "Lagre som min liste").Click();
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(texts.ListLimitReached(50), Alert(cut)));
    }

    [Fact]
    public void VariableListView_WhenMuninRefusesTheAddOfASharedListAtTheRowQuota_ThenTheAlertSaysTheLimit()
    {
        var texts = Texts.For("no");
        var refusal = new MuninExplorerListQuotaException(ListQuotaKind.Rows, 50, "Maks 50 rader.");
        var cut = RenderView(new SharedClient(null, refusal), shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));

        Button(cut, "Lagre som min liste").Click();
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() => Assert.Contains(texts.RowLimitReached(50), Alert(cut)));
    }

    [Fact]
    public void VariableListView_WhenACopysRowsAreRefusedAtTheRowQuota_ThenTheAlertSaysTheCopyIsIncompleteAndTheLimit()
    {
        var texts = Texts.For("no");
        var cut = RenderView(new CopyClient());
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Contains(texts.CopyIncomplete, Alert(cut)));
        Assert.Contains(texts.RowLimitReached(50), Alert(cut));
    }

    [Fact]
    public void VariableListView_WhenASharedSavesRowsAreRefusedAtTheRowQuota_ThenTheEmptyListItMadeIsDeleted()
    {
        var client = new SharedClient(null, new MuninExplorerListQuotaException(ListQuotaKind.Rows, 50, apiText: null));
        var cut = RenderView(client, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));

        Button(cut, "Lagre som min liste").Click();
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() => Assert.Single(client.Deleted));
    }

    [Fact]
    public void VariableListView_WhenASharedSaveIsThrottledAfterAQuotaRefusal_ThenItSaysTheThrottleNotTheLimit()
    {
        var texts = Texts.For("no");
        var client = new SharedClient(new MuninExplorerListQuotaException(ListQuotaKind.Lists, 50, apiText: null), null);
        var cut = RenderView(client, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));
        Button(cut, "Lagre som min liste").Click();
        Button(cut, "Lagre listen").Click();
        cut.WaitForAssertion(() => Assert.Equal(texts.ListLimitReached(50), Alert(cut)));
        client.CreateFailure = new MuninExplorerRateLimitedException();

        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(texts.RateLimitError, Alert(cut)));
    }

    [Fact]
    public void VariableListView_WhenACopyIsRefusedAtTheListQuota_ThenItSaysTheLimitAndNotThatACopyIsIncomplete()
    {
        var texts = Texts.For("no");
        var cut = RenderView(new CopyClient { CreateFailure = new MuninExplorerListQuotaException(ListQuotaKind.Lists, 50, apiText: null) });
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(texts.ListLimitReached(50), Alert(cut)));
    }

    [Fact]
    public void VariableListView_WhenTheNextCopyIsCutShortForAnotherReason_ThenTheEarlierLimitIsNotSaid()
    {
        var texts = Texts.For("no");
        var client = new CopyClient { EveryListHoldsThree = true };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table tbody tr").Count));
        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();
        cut.WaitForAssertion(() => Assert.Contains(texts.RowLimitReached(50), Alert(cut)));
        client.AddFailure = new HttpRequestException("nede");

        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(texts.CopyIncomplete, Alert(cut)));
    }
}
