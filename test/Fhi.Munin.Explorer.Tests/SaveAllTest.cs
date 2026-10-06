using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>«Lagre disse variablene» and the collapsed row's pointer save. (ADO 118713, Fhi.Metadata-dfy9u.8)</summary>
public class SaveAllTest : ExplorerTestContext
{
    private static readonly Guid ListId = Guid.NewGuid();

    private static VariableSummary Variable(string name) =>
        new() { Id = Guid.NewGuid(), Code = name.ToUpperInvariant(), PreferredTerm = name, KildeName = "Forsvarets helseregister" };

    private sealed class Client(IReadOnlyList<VariableSummary> rows, int totalCount, IReadOnlyList<Guid> allIds)
        : EmptyMuninExplorerClient
    {
        public readonly HashSet<Guid> Stored = [];
        public readonly List<IReadOnlyCollection<Guid>> Adds = [];
        public readonly List<(string? Search, VariableFilter? Filter)> IdCalls = [];
        public int CreateCalls { get; private set; }
        public bool TooMany { get; init; }
        public bool RateLimitAdd { get; init; }
        public bool HasList { get; init; } = true;
        public bool RateLimitMembership { get; set; }

        /// <summary>Time out every membership read once an add has gone through, as HttpClient's timeout does.</summary>
        public bool TimeOutMembershipAfterAdd { get; init; }

        /// <summary>Searches after the first wait on this while it is set, so a test can press mid-fetch.</summary>
        public TaskCompletionSource? HoldSearches { get; set; }

        private int _searches;

        public override async Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            if (_searches++ > 0 && HoldSearches is { } hold)
            {
                await hold.Task;
            }

            return new Page<VariableSummary>
            {
                Items = [.. rows.Select(v => v with { })],
                TotalCount = totalCount,
                PageNumber = 1,
                Size = 25,
                TotalPages = (totalCount + 24) / 25
            };
        }

        /// <summary>The ids answer waits on this while it is set, so a test can act mid-fetch.</summary>
        public TaskCompletionSource? HoldIds { get; set; }

        public int ListsCalls { get; private set; }

        public override async Task<VariableIdSet> GetVariableIdsAsync(
            string? search, VariableFilter? filter = null, CancellationToken cancellationToken = default)
        {
            IdCalls.Add((search, filter));

            if (HoldIds is { } hold)
            {
                await hold.Task;
            }

            return TooMany
                ? new VariableIdSet { TooMany = true, MaxIds = 2000 }
                : new VariableIdSet { Ids = allIds, MaxIds = 2000 };
        }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            ListsCalls++;
            return Task.FromResult<IReadOnlyList<VariableList>>(
                HasList || CreateCalls > 0 ? [new VariableList { Id = ListId, Name = "Variabelliste forsvaret" }] : []);
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new VariableList { Id = ListId, Name = name });
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            if (RateLimitMembership)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (TimeOutMembershipAfterAdd && Adds.Count > 0)
            {
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
            }

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            if (RateLimitAdd)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            Adds.Add(variableIds);
            Stored.UnionWith(variableIds);
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            Stored.ExceptWith(variableIds);
            return Task.FromResult(true);
        }
    }

    /// <summary>Fires each timer only when the test says the time has passed.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly List<(TimeSpan Due, TimerCallback Callback, object? State)> _timers = [];
        private TimeSpan _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timers.Add((_now + dueTime, callback, state));
            return new NoTimer();
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var timer in _timers.Where(t => t.Due <= _now).ToList())
            {
                _timers.Remove(timer);
                timer.Callback(timer.State);
            }
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private readonly ManualClock _clock = new();

    private IRenderedComponent<VariableSearch> Render(Client client, bool signedIn = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        Services.AddSingleton<TimeProvider>(_clock);
        return Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IElement? SaveAll(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button[id^=munin-explorer-save-all-]").SingleOrDefault();

    private static string Status(IRenderedComponent<VariableSearch> cut) =>
        SaveAll(cut)!.ParentElement!.QuerySelector("[role=status]")!.TextContent;

    private static string Alert(IRenderedComponent<VariableSearch> cut) =>
        SaveAll(cut)!.ParentElement!.QuerySelector("[role=alert]")!.TextContent;

    private static bool Asking(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("[id^=munin-explorer-save-all-confirm-]").Count == 1;

    private static void PressYes(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Ja, lagre dem").Click();

    private static void Search(IRenderedComponent<VariableSearch> cut, string term)
    {
        cut.Find(".searchbox__freetext").Change(term);
        cut.Find("form").Submit();
    }

    private static IReadOnlyList<IElement> RowSaveButtons(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll(".munin-explorer-dataitem-main__save button");

    [Fact]
    public void SaveAll_WhenSignedOut_ThenThereIsNoButtonAndNoSaveColumn()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]), signedIn: false);

        Assert.Null(SaveAll(cut));
        Assert.Empty(RowSaveButtons(cut));
    }

    [Fact]
    public void SaveAll_WhenPressed_ThenEveryIdOfTheSearchIsAddedInOneCall()
    {
        var rows = new[] { Variable("Vekt"), Variable("Høyde") };
        var offPage = Guid.NewGuid();
        var client = new Client(rows, 3, [rows[0].Id, rows[1].Id, offPage]);
        var cut = Render(client);

        Search(cut, "sesjon");
        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
        var listsBefore = client.ListsCalls;
        SaveAll(cut)!.Click();

        var add = Assert.Single(client.Adds);
        Assert.Equal(new[] { rows[0].Id, rows[1].Id, offPage }.Order(), add.Order());
        Assert.Equal("sesjon", Assert.Single(client.IdCalls).Search);
        Assert.Equal(listsBefore, client.ListsCalls);
        Assert.Equal("3 variabler lagret i Variabelliste forsvaret.", Status(cut));
        Assert.Equal("✓ Lagret", SaveAll(cut)!.TextContent.Trim());
        Assert.Equal("true", SaveAll(cut)!.GetAttribute("aria-disabled"));

        SaveAll(cut)!.Click();
        Assert.Single(client.Adds);
    }

    [Fact]
    public void SaveAll_WhenAnotherSearchRuns_ThenTheButtonIsOfferedAgainAndTheSavedSearchStaysDone()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]));

        SaveAll(cut)!.Click();
        Search(cut, "høyde");

        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
        Assert.Equal("", Status(cut));

        Search(cut, "");
        Assert.Equal("✓ Lagret", SaveAll(cut)!.TextContent.Trim());
    }

    [Fact]
    public void SaveAll_WhenAnotherReaderSignsIn_ThenTheLastReadersOutcomeIsNotShown()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]));

        SaveAll(cut)!.Click();
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
        Assert.Equal("", Status(cut));
        Assert.Equal("", Alert(cut));
    }

    [Fact]
    public void SaveAll_WhenSomeAreAlreadyInTheList_ThenOnlyTheNewOnesAreCounted()
    {
        var rows = new[] { Variable("Vekt"), Variable("Høyde") };
        var client = new Client(rows, 2, [rows[0].Id, rows[1].Id]);
        client.Stored.Add(rows[0].Id);
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal("1 variabel lagret i Variabelliste forsvaret.", Status(cut));
        Assert.Equal(["Lagret i liste Variabelliste forsvaret"], cut.FindAll(".munin-explorer-data-list__saved-notice").Select(n => n.TextContent));
        Assert.Contains("munin-explorer-data-list__item--saved", cut.FindAll("ul.munin-explorer-data-list > li")[1].ClassName);
    }

    [Fact]
    public void SaveAll_WhenTheListHoldsThemAll_ThenTheStatusSaysSo()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]);
        client.Stored.Add(rows[0].Id);
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal("Alle variablene var allerede i Variabelliste forsvaret.", Status(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-data-list__saved-notice"));
    }

    [Fact]
    public void SaveAll_WhenTheMembershipCannotBeRead_ThenNoCountIsClaimed()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { RateLimitMembership = true };
        var cut = Render(client);
        var listsBefore = client.ListsCalls;

        SaveAll(cut)!.Click();

        Assert.Single(client.Adds);
        Assert.Equal("Variablene er lagret i Variabelliste forsvaret.", Status(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-data-list__saved-notice"));
        Assert.Equal(listsBefore + 1, client.ListsCalls);
    }

    [Fact]
    public void SaveAll_WhenTheReadBackTimesOut_ThenTheSaveIsStillReportedSaved()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { TimeOutMembershipAfterAdd = true };
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Single(client.Adds);
        Assert.Equal("1 variabel lagret i Variabelliste forsvaret.", Status(cut));
        Assert.Equal("", Alert(cut));
    }

    [Fact]
    public void SaveAll_WhenAnotherReaderSignsInDuringTheFetch_ThenTheirListIsNotWritten()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { HoldIds = new TaskCompletionSource() };
        var cut = Render(client);

        SaveAll(cut)!.Click();
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        client.HoldIds.SetResult();

        cut.WaitForAssertion(() => Assert.Null(SaveAll(cut)!.GetAttribute("aria-disabled")));
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void SaveAll_WhenTheNewFirstListCannotBeRead_ThenTheSaveStillGoesThrough()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { HasList = false, RateLimitMembership = true };
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal(1, client.CreateCalls);
        Assert.Single(client.Adds);
        Assert.Equal("", Alert(cut));
    }

    [Fact]
    public void Notices_WhenTheReaderSignsOut_ThenTheyAreNotShownToTheNext()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]));

        SaveAll(cut)!.Click();
        Assert.Single(cut.FindAll(".munin-explorer-data-list__saved-notice"));
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.Empty(cut.FindAll(".munin-explorer-data-list__saved-notice"));
    }

    [Fact]
    public void SaveAll_WhenTheReaderHasNoList_ThenAFirstListIsMade()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { HasList = false };
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal(1, client.CreateCalls);
        Assert.Single(client.Adds);
        Assert.Equal("1 variabel lagret i Min variabelliste.", Status(cut));
    }

    [Fact]
    public void SaveAll_WhenTheResultHasEmptied_ThenNothingIsSavedAndNoListIsMade()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, []) { HasList = false };
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal(0, client.CreateCalls);
        Assert.Empty(client.Adds);
        Assert.Equal("Søket gir ingen variabler å lagre lenger.", Status(cut));
        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
    }

    [Fact]
    public async Task SaveAllAsync_WhenTheSetIsEmpty_ThenNothingIsSavedAndNoListIsMade()
    {
        var client = new Client([Variable("Vekt")], 1, []) { HasList = false };
        Render(client);

        var result = await Services.GetRequiredService<VariableListState>().SaveAllAsync([], "Min variabelliste");

        Assert.Null(result);
        Assert.Equal(0, client.CreateCalls);
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void Confirm_WhenAskedAgain_ThenTheLastFailureForThisSearchIsCleared()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 250, [rows[0].Id]) { RateLimitAdd = true });

        SaveAll(cut)!.Click();
        PressYes(cut);
        Assert.Contains("for mange forespørsler", Alert(cut));

        SaveAll(cut)!.Click();

        Assert.True(Asking(cut));
        Assert.Equal("", Alert(cut));
    }

    [Fact]
    public void Confirm_WhenTheReaderSignsOut_ThenTheQuestionIsGone()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 250, [rows[0].Id]);
        var cut = Render(client);

        SaveAll(cut)!.Click();
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.False(Asking(cut));
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void Confirm_WhenTheSavedSearchComesBack_ThenNoQuestionIsShown()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 250, [rows[0].Id]);
        var cut = Render(client);

        Search(cut, "vekt");
        SaveAll(cut)!.Click();
        PressYes(cut);
        Assert.Equal("\u2713 Lagret", SaveAll(cut)!.TextContent.Trim());
        Search(cut, "høyde");
        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
        SaveAll(cut)!.Click();
        Search(cut, "vekt");

        Assert.Equal("\u2713 Lagret", SaveAll(cut)!.TextContent.Trim());
        Assert.False(Asking(cut));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Ja, lagre dem");
        Assert.Single(client.Adds);
    }

    [Fact]
    public void SaveAll_WhenASearchIsBeingFetched_ThenThePressDoesNothing()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { HoldSearches = new TaskCompletionSource() };
        var cut = Render(client);

        Search(cut, "høyde");
        Assert.Equal("true", SaveAll(cut)!.GetAttribute("aria-disabled"));
        SaveAll(cut)!.Click();

        Assert.Empty(client.IdCalls);
        client.HoldSearches.SetResult();
        cut.WaitForAssertion(() => Assert.Null(SaveAll(cut)!.GetAttribute("aria-disabled")));
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(2000, true)]
    public void SaveAll_WhenTheResultIs200OrMore_ThenItAsksFirst(int total, bool asks)
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, total, [rows[0].Id]);
        var cut = Render(client);

        Assert.Equal(asks ? "false" : null, SaveAll(cut)!.GetAttribute("aria-expanded"));
        SaveAll(cut)!.Click();

        Assert.Equal(asks, Asking(cut));
        Assert.Equal(asks ? "true" : null, SaveAll(cut)!.GetAttribute("aria-expanded"));
        Assert.Equal(asks ? 0 : 1, client.Adds.Count);
    }

    [Fact]
    public void Confirm_WhenDeclined_ThenNothingIsSavedAndWhenAcceptedAllIs()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 250, [rows[0].Id]);
        var cut = Render(client);

        SaveAll(cut)!.Click();
        Assert.Equal("Lagre alle 250 variablene i listen?", cut.Find("[id^=munin-explorer-save-all-confirm-]").TextContent);
        Assert.Equal("Nei", SaveAll(cut)!.TextContent.Trim());
        Assert.Equal(cut.Find("[id^=munin-explorer-save-all-confirm-]").Id, SaveAll(cut)!.GetAttribute("aria-describedby"));

        SaveAll(cut)!.Click();
        Assert.False(Asking(cut));
        Assert.Empty(client.IdCalls);
        Assert.Empty(client.Adds);

        SaveAll(cut)!.Click();
        PressYes(cut);
        Assert.Single(client.Adds);
    }

    [Fact]
    public void Confirm_WhenAnotherSearchRuns_ThenTheQuestionIsNotShown()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 250, [rows[0].Id]);
        var cut = Render(client);

        SaveAll(cut)!.Click();
        Search(cut, "høyde");

        Assert.False(Asking(cut));
        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
    }

    [Fact]
    public void SaveAll_WhenTheResultExceedsTheCap_ThenItIsRefusedWithoutAsking()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 2001, [rows[0].Id]);
        var cut = Render(client);

        Assert.Null(SaveAll(cut)!.GetAttribute("aria-expanded"));
        SaveAll(cut)!.Click();

        Assert.False(Asking(cut));
        Assert.Equal("Begrens utvalget til høyst 2000 variabler for å lagre dem samlet.", Alert(cut));
        Assert.Empty(client.IdCalls);
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void SaveAll_WhenTheApiAnswersTooMany_ThenNothingIsSaved()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { TooMany = true };
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal("Begrens utvalget til høyst 2000 variabler for å lagre dem samlet.", Alert(cut));
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void SaveAll_WhenTheAddIsThrottled_ThenItSaysSoAndOffersTheButtonAgain()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]) { RateLimitAdd = true });

        SaveAll(cut)!.Click();

        Assert.Contains("for mange forespørsler", Alert(cut));
        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
    }

    [Fact]
    public void Notices_WhenSaved_ThenEachRowOffersRemovalAndShowsANoticeForThreeSeconds()
    {
        var rows = new[] { Variable("Vekt"), Variable("Høyde") };
        var cut = Render(new Client(rows, 2, [rows[0].Id, rows[1].Id]));

        SaveAll(cut)!.Click();

        Assert.All(RowSaveButtons(cut), b => Assert.Equal("Fjern fra liste", b.TextContent.Trim()));
        Assert.Equal(2, cut.FindAll(".munin-explorer-data-list__saved-notice").Count);
        Assert.All(cut.FindAll(".munin-explorer-data-list__saved-notice"), n => Assert.Equal("true", n.GetAttribute("aria-hidden")));

        _clock.Advance(VariableSearch.SavedNoticeDuration - TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, cut.FindAll(".munin-explorer-data-list__saved-notice").Count);

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".munin-explorer-data-list__saved-notice")));
        Assert.DoesNotContain("--saved", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RowButton_WhenPressed_ThenItSavesWithoutOpeningTheRowAndIsNoTabStop()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]);
        var cut = Render(client);

        var button = Assert.Single(RowSaveButtons(cut));
        Assert.Equal("-1", button.GetAttribute("tabindex"));
        Assert.Equal("Lagre i liste Vekt", AccessibleName.Of(button));

        button.Click();

        Assert.Contains(rows[0].Id, client.Stored);
        Assert.Equal("false", cut.Find("button.munin-explorer-dataitem-main__name").GetAttribute("aria-expanded"));
        Assert.Equal("Fjern fra liste", Assert.Single(RowSaveButtons(cut)).TextContent.Trim());
    }

    [Fact]
    public void RowCells_WhenSignedIn_ThenTheyMatchTheHeaderColumns()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]));

        var headers = cut.FindAll("[role=columnheader]").Count;
        var cells = cut.Find("ul.munin-explorer-data-list > li").QuerySelectorAll("[role=cell], [role=rowheader]").Length;

        Assert.Equal(headers, cells);
    }
}
