using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
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
        public bool TooMany { get; init; }
        public bool RateLimitAdd { get; init; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [.. rows.Select(v => v with { })],
                TotalCount = totalCount,
                PageNumber = 1,
                Size = 25,
                TotalPages = (totalCount + 24) / 25
            });

        public override Task<VariableIdSet> GetVariableIdsAsync(
            string? search, VariableFilter? filter = null, CancellationToken cancellationToken = default)
        {
            IdCalls.Add((search, filter));
            return Task.FromResult(TooMany
                ? new VariableIdSet { TooMany = true, MaxIds = 2000 }
                : new VariableIdSet { Ids = allIds, MaxIds = 2000 });
        }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Variabelliste forsvaret" }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });

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

    private static IReadOnlyList<IElement> RowSaveButtons(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll(".munin-explorer-dataitem-main__save button");

    [Fact]
    public void SignedOut_ThereIsNoButtonAndNoSaveColumn()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]), signedIn: false);

        Assert.Null(SaveAll(cut));
        Assert.Empty(RowSaveButtons(cut));
    }

    [Fact]
    public void Press_SavesEveryIdOfTheSearchIncludingLaterPages_InOneAdd()
    {
        var rows = new[] { Variable("Vekt"), Variable("Høyde") };
        var offPage = Guid.NewGuid();
        var client = new Client(rows, 3, [rows[0].Id, rows[1].Id, offPage]);
        var cut = Render(client);

        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
        SaveAll(cut)!.Click();

        var add = Assert.Single(client.Adds);
        Assert.Equal(new[] { rows[0].Id, rows[1].Id, offPage }.Order(), add.Order());
        Assert.Single(client.IdCalls);
        Assert.Equal("3 variabler lagret i Variabelliste forsvaret.", Status(cut));
        Assert.Equal("✓ Lagret", SaveAll(cut)!.TextContent.Trim());
        Assert.Equal("true", SaveAll(cut)!.GetAttribute("aria-disabled"));

        SaveAll(cut)!.Click();
        Assert.Single(client.Adds);
    }

    [Fact]
    public void Press_CountsOnlyTheVariablesTheListDidNotAlreadyHold()
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

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    public void Press_AResultOf200OrMoreAsksFirst(int total, bool asks)
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, total, [rows[0].Id]);
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal(asks, cut.FindAll($"[id^=munin-explorer-save-all-confirm-]").Count == 1);
        Assert.Equal(asks ? 0 : 1, client.Adds.Count);
    }

    [Fact]
    public void Confirm_NeiSavesNothing_JaSavesAll()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 250, [rows[0].Id]);
        var cut = Render(client);

        SaveAll(cut)!.Click();
        Assert.Equal("Lagre alle 250 variablene i listen?", cut.Find("[id^=munin-explorer-save-all-confirm-]").TextContent);
        Assert.Equal("Nei", SaveAll(cut)!.TextContent.Trim());

        SaveAll(cut)!.Click();
        Assert.Empty(cut.FindAll("[id^=munin-explorer-save-all-confirm-]"));
        Assert.Empty(client.IdCalls);
        Assert.Empty(client.Adds);

        SaveAll(cut)!.Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Ja, lagre dem").Click();
        Assert.Single(client.Adds);
    }

    [Fact]
    public void Press_MoreThanTheApiReturnsIsRefusedWithoutAsking()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 2001, [rows[0].Id]);
        var cut = Render(client);

        SaveAll(cut)!.Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Ja, lagre dem").Click();

        Assert.Equal("Begrens utvalget til høyst 2000 variabler for å lagre dem samlet.", Alert(cut));
        Assert.Empty(client.IdCalls);
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void Press_TheApisTooManyIsSaidAndNothingIsSaved()
    {
        var rows = new[] { Variable("Vekt") };
        var client = new Client(rows, 1, [rows[0].Id]) { TooMany = true };
        var cut = Render(client);

        SaveAll(cut)!.Click();

        Assert.Equal("Begrens utvalget til høyst 2000 variabler for å lagre dem samlet.", Alert(cut));
        Assert.Empty(client.Adds);
    }

    [Fact]
    public void Press_AThrottledAddSaysSoAndOffersTheButtonAgain()
    {
        var rows = new[] { Variable("Vekt") };
        var cut = Render(new Client(rows, 1, [rows[0].Id]) { RateLimitAdd = true });

        SaveAll(cut)!.Click();

        Assert.Contains("for mange forespørsler", Alert(cut));
        Assert.Equal("Lagre disse variablene", SaveAll(cut)!.TextContent.Trim());
    }

    [Fact]
    public void Saved_EachRowSaysFjernAndShowsItsNoticeUntilTheTimePasses()
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
    public void RowButton_IsNoTabStopAndSavesWithoutOpeningTheRow()
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
}
