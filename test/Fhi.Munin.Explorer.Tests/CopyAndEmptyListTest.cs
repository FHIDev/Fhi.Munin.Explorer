using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Copying the list on screen under a new name, and emptying it (Fhi.Metadata-ntpbd.2). Munin has
/// neither endpoint, so both are the paged read and the batch writes, and a list longer than one
/// page of 1000 is what proves the read walks every page.
/// </summary>
public class CopyAndEmptyListTest : ExplorerTestContext
{
    private static readonly Guid SourceId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = new("22222222-2222-2222-2222-222222222222");

    private const string SourceName = "Mine hjertevariabler";

    private static VariableListItem Item(int index) => new()
    {
        VariableId = Guid.NewGuid(),
        AddedAt = DateTimeOffset.UtcNow,
        VariableName = $"Variabel {index:0000}",
        VariableCode = $"V{index:0000}",
        DesiredDataFreeText = "Ønsker alle år",
    };

    private static VariableListItem[] Items(int count) => [.. Enumerable.Range(0, count).Select(Item)];

    /// <summary>Two lists — the one on screen and another — with every write recorded.</summary>
    private sealed class ListsClient : EmptyMuninExplorerClient
    {
        private readonly List<VariableList> _lists = [];
        private readonly Dictionary<Guid, List<VariableListItem>> _items = [];

        public ListsClient(IReadOnlyList<VariableListItem> source, string otherName = "Hjerte og kar")
        {
            _lists.Add(new VariableList { Id = SourceId, Name = SourceName });
            _lists.Add(new VariableList { Id = OtherId, Name = otherName });
            _items[SourceId] = [.. source];
            _items[OtherId] = [];
        }

        /// <summary>An exception every add throws, standing in for a 500 part-way through a copy.</summary>
        public Exception? AddThrows { get; init; }

        /// <summary>The add call, counted from 1, from which every add throws; the earlier ones land.</summary>
        public int? AddThrowsFrom { get; init; }

        /// <summary>Set part-way through a test, so the source reads as deleted in another tab.</summary>
        public bool SourceGone { get; set; }

        /// <summary>What every add answers when it does not throw: false is a refusal without a fault.</summary>
        public bool AddAccepted { get; init; } = true;

        /// <summary>An exception the create throws, so the copy fails before any list exists.</summary>
        public Exception? CreateThrows { get; init; }

        /// <summary>An exception the holder's whole-list read of the copy throws, failing the switch to it.</summary>
        public Exception? CopyMembershipThrows { get; init; }

        /// <summary>An exception every remove throws.</summary>
        public Exception? RemoveThrows { get; init; }

        /// <summary>The remove call, counted from 1, from which every remove answers false.</summary>
        public int? RemoveRefusedFrom { get; init; }

        /// <summary>Left unfinished until the test completes it, so the reader can act meanwhile.</summary>
        public TaskCompletionSource? HoldCreate { get; init; }

        public TaskCompletionSource? HoldAdd { get; init; }

        /// <summary>How many ids each write carried, so a test can see the batches were split.</summary>
        public List<int> AddBatches { get; } = [];
        public List<int> RemoveBatches { get; } = [];

        /// <summary>A search row, so a save button can be read beside the list view.</summary>
        public VariableSummary? SearchRow { get; init; }

        public List<string> Created { get; } = [];
        public List<Guid> Added { get; } = [];
        public List<Guid> AddedTo { get; } = [];
        public List<Guid> Removed { get; } = [];
        public int DesiredDataCalls { get; private set; }
        public int WriteCalls { get; private set; }

        /// <summary>Set part-way through a test, so the name check's own read fails.</summary>
        public Exception? ListsThrows { get; set; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            ListsThrows is not null
                ? throw ListsThrows
                : Task.FromResult<IReadOnlyList<VariableList>>(
                    [.. _lists.Select(l => l with { VariableCount = _items[l.Id].Count })]);

        public override async Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            WriteCalls++;

            if (HoldCreate is not null)
            {
                await HoldCreate.Task;
            }

            if (CreateThrows is not null)
            {
                throw CreateThrows;
            }

            Created.Add(name);

            var created = new VariableList { Id = Guid.NewGuid(), Name = name };
            _lists.Add(created);
            _items[created.Id] = [];

            return created;
        }

        public override async Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            AddBatches.Add(variableIds.Count);

            if (HoldAdd is not null)
            {
                await HoldAdd.Task;
            }

            if (AddThrows is not null)
            {
                throw AddThrows;
            }

            if (AddBatches.Count >= AddThrowsFrom)
            {
                throw new HttpRequestException("500");
            }

            if (!AddAccepted)
            {
                return false;
            }

            Added.AddRange(variableIds);
            AddedTo.Add(id);
            _items[id].AddRange(variableIds.Select(v => new VariableListItem
            {
                VariableId = v,
                AddedAt = DateTimeOffset.UtcNow,
                VariableName = _items[SourceId].FirstOrDefault(i => i.VariableId == v)?.VariableName,
            }));

            return true;
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            RemoveBatches.Add(variableIds.Count);

            if (RemoveThrows is not null)
            {
                throw RemoveThrows;
            }

            if (RemoveBatches.Count >= RemoveRefusedFrom)
            {
                return Task.FromResult(false);
            }

            Removed.AddRange(variableIds);
            _items[id].RemoveAll(i => variableIds.Contains(i.VariableId));

            return Task.FromResult(true);
        }

        public override Task<DesiredDataResult> SetMyListDesiredDataAsync(
            Guid id, Guid variableId, string? freeText, CancellationToken cancellationToken = default)
        {
            DesiredDataCalls++;
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            if (CopyMembershipThrows is not null && id != SourceId && id != OtherId && pageSize == 1000)
            {
                throw CopyMembershipThrows;
            }

            if ((SourceGone && id == SourceId) || !_items.TryGetValue(id, out var items))
            {
                return Task.FromResult<Page<VariableListItem>?>(null);
            }

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. items.Skip((page - 1) * pageSize).Take(pageSize)],
                TotalCount = items.Count,
                PageNumber = page,
                Size = pageSize,
                TotalPages = Math.Max(1, (items.Count + pageSize - 1) / pageSize),
            });
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(SearchRow is { } row && row.Id == id
                ? new VariableDetail { Id = id, Code = row.Code, PreferredTerm = row.PreferredTerm }
                : null);

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SearchRow is null
                ? new Page<VariableSummary>()
                : new Page<VariableSummary>
                {
                    Items = [SearchRow],
                    TotalCount = 1,
                    PageNumber = 1,
                    Size = pageSize,
                    TotalPages = 1,
                });
    }

    private void Register(ListsClient client)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedComponent<VariableListView> RenderView(ListsClient client, string language = "no")
    {
        Register(client);

        return Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, language));
    }

    private static IElement Button<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static IElement Labelled<T>(IRenderedComponent<T> cut, string label) where T : IComponent
    {
        var target = cut.FindAll("label").Single(l => l.TextContent.Trim() == label).GetAttribute("for");
        return cut.Find($"#{target}");
    }

    private static string Alert<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.Find("div[role=alert][aria-live=assertive]").TextContent.Trim();

    private const string RateLimited = "Du har gjort for mange forespørsler. Vent litt før du prøver igjen.";

    private VariableListState State => Services.GetRequiredService<VariableListState>();

    private static string Heading(IRenderedComponent<VariableListView> cut) =>
        cut.Find("[id^=munin-explorer-list-heading-]").TextContent.Trim();

    private static int RowCount(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("table.munin-explorer-data-list tbody tr").Count;

    /// <summary>The text of every element one control's aria-describedby names, joined.</summary>
    private static string Described<T>(IRenderedComponent<T> cut, IElement control) where T : IComponent =>
        string.Join(
            " ",
            (control.GetAttribute("aria-describedby") ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => cut.Find($"#{id}").TextContent.Trim()));

    // -----------------------------------------------------------------------
    // AC1: a name already in use, in another case and padded, is refused before any write

    [Fact]
    public void Copy_WhenTheNameMatchesAnotherListIgnoringCaseAndSpaces_ThenItIsRefusedBeforeAnyWrite()
    {
        var client = new ListsClient(Items(3));
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Assert.Equal($"{SourceName} - kopi", Labelled(cut, "Navn på kopien").GetAttribute("value"));

        Labelled(cut, "Navn på kopien").Change("  HJERTE OG KAR ");
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() =>
        {
            var field = Labelled(cut, "Navn på kopien");
            Assert.Equal("true", field.GetAttribute("aria-invalid"));
            Assert.Contains(
                "Du har allerede en liste med dette navnet. Velg et annet navn.",
                Described(cut, field));
        });
        Assert.Empty(client.Created);
        Assert.Equal(0, client.WriteCalls);
        Assert.Equal(SourceName, Heading(cut));
    }

    // -----------------------------------------------------------------------
    // AC2: a copy of a list three pages and two batches long carries every id, no annotation, and is shown

    [Fact]
    public void Copy_WhenTheNameIsUniqueAndTheListIsLongerThanAPage_ThenEveryIdIsCopiedAndTheCopyIsShown()
    {
        var source = Items(2500);
        var client = new ListsClient(source);
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(25, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Labelled(cut, "Navn på kopien").Change("  Kopi til prosjektet  ");
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kopi til prosjektet", Heading(cut)));
        Assert.Equal(["Kopi til prosjektet"], client.Created);
        Assert.Equal(source.Select(i => i.VariableId).Order(), client.Added.Order());
        Assert.Equal([IMuninExplorerClient.MaxVariablesPerBatch, 500], client.AddBatches);
        Assert.DoesNotContain(SourceId, client.AddedTo);
        Assert.Equal(0, client.DesiredDataCalls);
        Assert.Equal("", Alert(cut));
        // Left open, offering to copy the copy: the reader's focus is on its submit button.
        Assert.Equal("Kopi til prosjektet - kopi", Labelled(cut, "Navn på kopien").GetAttribute("value"));
        // Counted against the copy, not left at the zero it was made with: the picker says so.
        cut.WaitForAssertion(() => Assert.Contains("Kopi til prosjektet (2500 variabler)", cut.Markup));
    }

    // -----------------------------------------------------------------------
    // AC3: a later batch that fails leaves the copy shown with the earlier batches and says it is incomplete

    [Fact]
    public void Copy_WhenTheSecondBatchFails_ThenTheCopyIsShownWithTheFirstAndTheAlertSaysItIsIncomplete()
    {
        var source = Items(2500);
        var client = new ListsClient(source) { AddThrowsFrom = 2 };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(25, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal($"{SourceName} - kopi", Heading(cut)));
        Assert.Equal(
            "Kopien er ufullstendig: ikke alle variablene ble kopiert. Listen under viser det som kom med.",
            Alert(cut));
        Assert.Equal([IMuninExplorerClient.MaxVariablesPerBatch, 500], client.AddBatches);
        Assert.Equal(IMuninExplorerClient.MaxVariablesPerBatch, client.Added.Count);
        cut.WaitForAssertion(() => Assert.Equal(25, RowCount(cut)));
        Assert.Contains("Variabel 0000", cut.Markup);
        Assert.Equal([$"{SourceName} - kopi"], client.Created);
        Assert.NotEqual(SourceId, State.ActiveListId);
        cut.WaitForAssertion(() => Assert.Contains($"{SourceName} - kopi (2000 variabler)", cut.Markup));
    }

    [Fact]
    public void Copy_WhenTheSourceIsDeletedElsewhereBeforeItIsRead_ThenNoCopyIsMade()
    {
        var client = new ListsClient(Items(3));
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        client.SourceGone = true;
        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke lagre nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Empty(client.Created);
        Assert.Equal(0, client.WriteCalls);
        Assert.Equal(SourceName, Heading(cut));
    }

    [Fact]
    public void Copy_WhenAnAddIsRefusedWithoutAFault_ThenTheCopyIsShownAndTheAlertSaysItIsIncomplete()
    {
        var client = new ListsClient(Items(3)) { AddAccepted = false };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal($"{SourceName} - kopi", Heading(cut)));
        Assert.Equal(
            "Kopien er ufullstendig: ikke alle variablene ble kopiert. Listen under viser det som kom med.",
            Alert(cut));
    }

    [Fact]
    public void Copy_WhenTheCreateIsThrottled_ThenTheSourceStaysShownAndActiveWithTheFormOpen()
    {
        var client = new ListsClient(Items(3)) { CreateThrows = new MuninExplorerRateLimitedException() };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));
        var active = State.ActiveListId;

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(RateLimited, Alert(cut)));
        Assert.Equal(SourceName, Heading(cut));
        Assert.Equal(3, RowCount(cut));
        Assert.Equal($"{SourceName} - kopi", Labelled(cut, "Navn på kopien").GetAttribute("value"));
        Assert.Equal(active, State.ActiveListId);
        Assert.Empty(client.AddBatches);
    }

    // -----------------------------------------------------------------------
    // A switch to the copy that fails still leaves view and holder on the same list, and says so:
    // the holder names the copy active before the read that throws

    [Fact]
    public void Copy_WhenTheSwitchToTheCopyIsThrottled_ThenTheCopyIsShownAndActiveAndTheAlertSaysSo()
    {
        var client = new ListsClient(Items(3)) { CopyMembershipThrows = new MuninExplorerRateLimitedException() };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(RateLimited, Alert(cut)));
        Assert.Equal($"{SourceName} - kopi", Heading(cut));
        Assert.Equal(client.AddedTo.Single(), State.ActiveListId);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));
    }

    [Fact]
    public void Copy_WhenTheSwitchToTheCopyFails_ThenTheCopyIsShownAndActiveAndTheAlertSaysSo()
    {
        var client = new ListsClient(Items(3)) { CopyMembershipThrows = new HttpRequestException("500") };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke hente listen nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Equal($"{SourceName} - kopi", Heading(cut));
        Assert.Equal(client.AddedTo.Single(), State.ActiveListId);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));
        // Still open, so the focus on its submit button is not dropped to the document body.
        Assert.Equal($"{SourceName} - kopi - kopi", Labelled(cut, "Navn på kopien").GetAttribute("value"));
    }

    [Fact]
    public void Copy_WhenTheSwitchToTheCopyIsRefusedAsUnauthorised_ThenTheCopyIsShownAndActiveAndTheAlertSaysSo()
    {
        var client = new ListsClient(Items(3)) { CopyMembershipThrows = new MuninExplorerUnauthorizedException() };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke hente listen nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Equal($"{SourceName} - kopi", Heading(cut));
        Assert.Equal(client.AddedTo.Single(), State.ActiveListId);
        Assert.Equal($"{SourceName} - kopi - kopi", Labelled(cut, "Navn på kopien").GetAttribute("value"));
    }

    [Fact]
    public void Copy_WhenAnAddAndThenTheSwitchFail_ThenTheAlertSaysTheCopyIsIncomplete()
    {
        var client = new ListsClient(Items(3))
        {
            AddAccepted = false,
            CopyMembershipThrows = new HttpRequestException("500"),
        };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "Kopien er ufullstendig: ikke alle variablene ble kopiert. Listen under viser det som kom med.",
            Alert(cut)));
        Assert.Equal($"{SourceName} - kopi", Heading(cut));
    }

    [Fact]
    public void Copy_WhenTheNameCheckFailsAfterARefusal_ThenTheEarlierRefusalIsGone()
    {
        var client = new ListsClient(Items(3));
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Labelled(cut, "Navn på kopien").Change("hjerte og kar");
        Button(cut, "Kopier listen").Click();
        cut.WaitForAssertion(() => Assert.Equal("true", Labelled(cut, "Navn på kopien").GetAttribute("aria-invalid")));

        client.ListsThrows = new MuninExplorerRateLimitedException();
        Labelled(cut, "Navn på kopien").Change("Et nytt navn");
        Button(cut, "Kopier listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(RateLimited, Alert(cut)));
        var field = Labelled(cut, "Navn på kopien");
        Assert.NotEqual("true", field.GetAttribute("aria-invalid"));
        Assert.DoesNotContain("Du har allerede en liste", Described(cut, field));
    }

    // -----------------------------------------------------------------------
    // The source stays the active list until the copy's writes are done, and a reader who chooses
    // another list meanwhile stays on it — even one who came back to the source

    [Fact]
    public async Task Copy_WhileTheVariablesAreAdded_ThenTheSourceStaysTheActiveList()
    {
        var hold = new TaskCompletionSource();
        var client = new ListsClient(Items(3)) { HoldAdd = hold };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();
        cut.WaitForAssertion(() => Assert.Single(client.AddBatches));

        Assert.Equal(SourceId, State.ActiveListId);
        Assert.Equal(SourceName, Heading(cut));

        await cut.InvokeAsync(hold.SetResult);

        cut.WaitForAssertion(() => Assert.Equal($"{SourceName} - kopi", Heading(cut)));
        Assert.NotEqual(SourceId, State.ActiveListId);
        Assert.NotEqual(OtherId, State.ActiveListId);
    }

    [Fact]
    public async Task Copy_WhenTheReaderLeavesAndComesBackToTheSourceDuringTheAdds_ThenTheSourceStaysShownAndActive()
    {
        var hold = new TaskCompletionSource();
        var client = new ListsClient(Items(3)) { HoldAdd = hold };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();
        cut.WaitForAssertion(() => Assert.Single(client.AddBatches));

        cut.Find("select").Change(OtherId.ToString());
        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", Heading(cut)));
        cut.Find("select").Change(SourceId.ToString());
        cut.WaitForAssertion(() => Assert.Equal(SourceName, Heading(cut)));

        await cut.InvokeAsync(hold.SetResult);

        cut.WaitForAssertion(() => Assert.Contains($"{SourceName} - kopi (3 variabler)", cut.Markup));
        Assert.Equal(SourceName, Heading(cut));
        Assert.Equal(SourceId, State.ActiveListId);
        Assert.Equal("", Alert(cut));
    }

    [Fact]
    public async Task Copy_WhenTheReaderChoosesAnotherListWhileTheVariablesAreAdded_ThenTheyStayOnIt()
    {
        var hold = new TaskCompletionSource();
        // The add fails once released, so the alert marks the moment the copy is over.
        var client = new ListsClient(Items(3)) { HoldAdd = hold, AddThrows = new HttpRequestException("500") };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();
        cut.WaitForAssertion(() => Assert.Single(client.AddBatches));

        cut.Find("select").Change(OtherId.ToString());
        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", Heading(cut)));

        await cut.InvokeAsync(hold.SetResult);

        // Names the copy rather than pointing at the list below, which is not it.
        cut.WaitForAssertion(() => Assert.Equal(
            $"Kopien «{SourceName} - kopi» er ufullstendig: ikke alle variablene ble kopiert.",
            Alert(cut)));
        Assert.Equal("Hjerte og kar", Heading(cut));
        Assert.Equal(OtherId, State.ActiveListId);
        Assert.Equal("false", Button(cut, "Kopier liste").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Copy_WhenTheReaderChoosesAnotherListBeforeTheCopyExists_ThenTheCopyIsNotMadeActive()
    {
        var hold = new TaskCompletionSource();
        var client = new ListsClient(Items(3)) { HoldCreate = hold };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Kopier liste").Click();
        Button(cut, "Kopier listen").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, client.WriteCalls));

        cut.Find("select").Change(OtherId.ToString());
        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", Heading(cut)));

        await cut.InvokeAsync(hold.SetResult);

        cut.WaitForAssertion(() => Assert.Equal(3, client.Added.Count));
        Assert.Equal([$"{SourceName} - kopi"], client.Created);
        Assert.Equal("Hjerte og kar", Heading(cut));
        Assert.Equal(OtherId, State.ActiveListId);
    }

    // -----------------------------------------------------------------------
    // AC4: emptying asks first, "Nei" stands it down, and confirming takes every id out

    [Fact]
    public void Empty_WhenConfirmed_ThenEveryIdIsRemovedTheNameStaysAndTheSearchRowOffersSave()
    {
        var source = Items(2500);
        var client = new ListsClient(source)
        {
            SearchRow = new VariableSummary { Id = source[0].VariableId, Code = "V0000", PreferredTerm = "Variabel 0000" },
        };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(25, RowCount(cut)));

        var search = Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, true));
        // In the open panel since Fhi.Metadata-35w0p.78, so the row is opened first when shut.
        IElement SaveButton()
        {
            var name = search.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name");

            if (name.GetAttribute("aria-expanded") != "true")
            {
                name.Click();
            }

            return search.Find(".munin-explorer-detail button[aria-pressed]");
        }
        search.WaitForAssertion(() => Assert.Equal("true", SaveButton().GetAttribute("aria-pressed")));

        Button(cut, "Tøm liste").Click();

        var toggle = Button(cut, "Nei");
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.Equal(
            "Fjerne alle variabler fra denne listen? Det kan ikke angres.",
            Described(cut, Button(cut, "Ja, tøm listen")));
        Assert.Empty(client.Removed);

        toggle.Click();

        Assert.Equal("false", Button(cut, "Tøm liste").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Ja, tøm listen");
        Assert.Empty(client.Removed);

        Button(cut, "Tøm liste").Click();
        Button(cut, "Ja, tøm listen").Click();

        cut.WaitForAssertion(() => Assert.Contains("Denne listen er tom.", cut.Markup));
        Assert.Equal(source.Select(i => i.VariableId).Order(), client.Removed.Order());
        Assert.Equal([IMuninExplorerClient.MaxVariablesPerBatch, 500], client.RemoveBatches);
        Assert.Equal(SourceName, Heading(cut));
        Assert.Equal(0, RowCount(cut));
        search.WaitForAssertion(() => Assert.Equal("false", SaveButton().GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Empty_WhenTheSecondBatchIsRefused_ThenTheAlertSaysSoAndWhatSurvivedIsShown()
    {
        var client = new ListsClient(Items(2500)) { RemoveRefusedFrom = 2 };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(25, RowCount(cut)));

        Button(cut, "Tøm liste").Click();
        Button(cut, "Ja, tøm listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke endre listen nå. Prøv igjen om litt.", Alert(cut)));
        cut.WaitForAssertion(() => Assert.Contains("Variabel 2000", cut.Markup));
        Assert.DoesNotContain("Variabel 0000", cut.Markup);
        Assert.Equal(25, RowCount(cut));
        Assert.Equal(SourceName, Heading(cut));
        Assert.Equal([IMuninExplorerClient.MaxVariablesPerBatch, 500], client.RemoveBatches);
    }

    [Fact]
    public void Empty_WhenTheListIsDeletedElsewhere_ThenTheAlertSaysItFailedAndNothingIsRemoved()
    {
        var client = new ListsClient(Items(3));
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        client.SourceGone = true;
        Button(cut, "Tøm liste").Click();
        Button(cut, "Ja, tøm listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke endre listen nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Empty(client.RemoveBatches);
    }

    [Fact]
    public void Empty_WhenTheRemoveIsThrottled_ThenTheAlertSaysSoAndTheRowsStay()
    {
        var client = new ListsClient(Items(3)) { RemoveThrows = new MuninExplorerRateLimitedException() };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, "Tøm liste").Click();
        Button(cut, "Ja, tøm listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(RateLimited, Alert(cut)));
        Assert.Equal(3, RowCount(cut));
        Assert.Equal(SourceName, Heading(cut));
    }

    // -----------------------------------------------------------------------
    // AC5: an empty list refuses both, with a visible reason rather than a tooltip

    [Theory]
    [InlineData("no", "Kopier liste", "Tøm liste", "Listen er tom")]
    [InlineData("en", "Copy list", "Empty list", "The list is empty")]
    public void CopyAndEmpty_WhenTheListIsEmpty_ThenBothAreAriaDisabledWithAVisibleReasonAndSendNothing(
        string language, string copy, string empty, string reason)
    {
        var client = new ListsClient([]);
        var cut = RenderView(client, language);
        cut.WaitForAssertion(() => Assert.Equal(SourceName, Heading(cut)));

        foreach (var text in new[] { copy, empty })
        {
            var button = Button(cut, text);

            Assert.Equal("true", button.GetAttribute("aria-disabled"));
            Assert.Equal(reason, Described(cut, button));
            Assert.Null(button.GetAttribute("title"));
            Assert.False(button.HasAttribute("disabled"));

            button.Click();

            Assert.Equal("false", Button(cut, text).GetAttribute("aria-expanded"));
        }

        Assert.Equal(0, client.WriteCalls);
    }

    // -----------------------------------------------------------------------
    // AC6: every new string, in both languages, on the rendered page

    public sealed record Words(
        string Copy, string CopyName, string Default, string Submit, string Note, string Incomplete,
        string Empty, string Question, string Yes, string No, string Taken);

    public static TheoryData<string, Words> Languages => new()
    {
        {
            "no", new Words(
                "Kopier liste", "Navn på kopien", $"{SourceName} - kopi", "Kopier listen",
                "Kopien får variablene, men ikke det som står under Ønskede data.",
                "Kopien er ufullstendig: ikke alle variablene ble kopiert. Listen under viser det som kom med.",
                "Tøm liste", "Fjerne alle variabler fra denne listen? Det kan ikke angres.", "Ja, tøm listen", "Nei",
                "Du har allerede en liste med dette navnet. Velg et annet navn.")
        },
        {
            "en", new Words(
                "Copy list", "Name of the copy", $"{SourceName} - copy", "Copy the list",
                "The copy gets the variables, but not what is written under Desired data.",
                "The copy is incomplete: not every variable was copied. The list below shows what was.",
                "Empty list", "Remove every variable from this list? It cannot be undone.", "Yes, empty the list", "No",
                "You already have a list with this name. Choose another name.")
        },
    };

    [Theory]
    [MemberData(nameof(Languages))]
    public void Words_WhenCopyingAndEmptying_ThenEveryStringRendersInTheReadersLanguage(string language, Words w)
    {
        var client = new ListsClient(Items(3)) { AddThrows = new HttpRequestException("500") };
        var cut = RenderView(client, language);
        cut.WaitForAssertion(() => Assert.Equal(3, RowCount(cut)));

        Button(cut, w.Empty).Click();
        Assert.Equal(w.Question, Described(cut, Button(cut, w.Yes)));
        Button(cut, w.No).Click();

        Button(cut, w.Copy).Click();
        var field = Labelled(cut, w.CopyName);
        Assert.Equal(w.Default, field.GetAttribute("value"));
        Assert.Equal(w.Note, Described(cut, field));

        Labelled(cut, w.CopyName).Change(SourceName.ToUpperInvariant());
        Button(cut, w.Submit).Click();
        cut.WaitForAssertion(() => Assert.Contains(w.Taken, Described(cut, Labelled(cut, w.CopyName))));

        Labelled(cut, w.CopyName).Change(w.Default);
        Button(cut, w.Submit).Click();
        cut.WaitForAssertion(() => Assert.Equal(w.Incomplete, Alert(cut)));
    }
}
