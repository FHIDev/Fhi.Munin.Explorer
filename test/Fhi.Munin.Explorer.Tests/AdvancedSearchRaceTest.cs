using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// Each test holds one await open and presses into it: deleting the guard it names lets the late answer win.
public class AdvancedSearchRaceTest : ExplorerTestContext
{
    private static readonly IReadOnlyList<ConditionField> Fields =
        JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!;

    private sealed class HeldClient : EmptyMuninExplorerClient
    {
        private readonly List<TaskCompletionSource> _fieldCalls = [];
        private readonly TaskCompletionSource _searches = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _searchCalls;
        private bool _firstFieldCallFailed;

        public bool HoldFields { get; init; }

        public bool HoldLaterSearches { get; init; }

        public List<VariableFilter?> Searches { get; } = [];

        public void ReleaseFields(int call) => _fieldCalls[call].TrySetResult();

        public int HeldFieldCalls => _fieldCalls.Count;

        public void ReleaseSearches() => _searches.TrySetResult();

        public override async Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default)
        {
            if (HoldFields && !_firstFieldCallFailed)
            {
                // The link's conditions stay in force unjudged, and every opening asks again.
                _firstFieldCallFailed = true;
                throw new HttpRequestException("503");
            }

            if (HoldFields)
            {
                var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _fieldCalls.Add(held);
                await held.Task;
            }

            return Fields;
        }

        public override async Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(filter);
            if (HoldLaterSearches && Interlocked.Increment(ref _searchCalls) > 1)
            {
                await _searches.Task;
            }

            return new Page<VariableSummary>
            {
                Items = [new VariableSummary { Id = Guid.NewGuid(), Code = "V_A", PreferredTerm = "Blodtrykk" }],
                TotalCount = 1, PageNumber = 1, Size = 25, TotalPages = 1
            };
        }
    }

    private IRenderedComponent<VariableSearch> RenderWith(HeldClient client, params string[] conditions)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var filter = new VariableFilter { Conditions = [.. conditions.Select(Condition)] };
        var cut = Render<VariableSearch>(b => b.Add(c => c.Filter, filter));
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        return cut;
    }

    private static SearchCondition Condition(string text) =>
        SearchCondition.TryParse(text, out var condition) ? condition : throw new ArgumentException(text);

    private static IElement? Toggle(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button[aria-expanded]").FirstOrDefault(b => b.TextContent.StartsWith("Avansert søk", StringComparison.Ordinal));

    private static IReadOnlyList<IElement> Rows(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("fieldset fieldset").Where(f => f.QuerySelector("legend")?.TextContent.StartsWith("Betingelse", StringComparison.Ordinal) == true).ToList();

    private static void Press(IRenderedComponent<VariableSearch> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static void PickDate(IRenderedComponent<VariableSearch> cut, string op, int row = 0)
    {
        Rows(cut)[row].QuerySelectorAll("select")[0].Change("gyldigFra");
        Rows(cut)[row].QuerySelectorAll("select")[1].Change(op);
    }

    [Fact]
    public async Task Open_WhenTheFormIsClosedAndReopenedWhileTheFieldsLoad_ThenOneCallServesAndOnlyTheLastOpeningFillsRows()
    {
        var client = new HeldClient { HoldFields = true };
        var cut = RenderWith(client, "navn:inneholder:a");

        Toggle(cut)!.Click();
        Toggle(cut)!.Click();
        Toggle(cut)!.Click();
        Assert.Equal(1, client.HeldFieldCalls);
        await cut.InvokeAsync(() => client.ReleaseFields(0));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".munin-explorer-advanced-search [role=status]")));

        Assert.Single(Rows(cut));
    }

    [Fact]
    public async Task RemoveChip_WhenMoreChipsArePressedWhileTheFirstRemovalSearches_ThenEveryOneStaysRemoved()
    {
        var client = new HeldClient { HoldLaterSearches = true };
        var cut = RenderWith(client, "navn:inneholder:a", "navn:inneholder:b", "navn:inneholder:c");

        RemoveChip(cut, "a");
        RemoveChip(cut, "b");
        Assert.DoesNotContain(cut.FindAll("button"), b => b.GetAttribute("aria-label") == "Fjern filteret Navn inneholder «b»");
        RemoveChip(cut, "c");
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Endre avansert søk");
        await cut.InvokeAsync(client.ReleaseSearches);

        cut.WaitForAssertion(() => Assert.Empty(client.Searches[^1]!.Conditions));
    }

    [Fact]
    public void ShowHits_WhenConditionsAreQueuedBehindASearch_ThenTheirChipsOfferEndre()
    {
        var client = new HeldClient { HoldLaterSearches = true };
        var cut = RenderWith(client, "navn:inneholder:a");
        RemoveChip(cut, "a");
        Toggle(cut)!.Click();
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        Rows(cut)[0].QuerySelector("input")!.Change("b");

        Press(cut, "Vis treff");

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.GetAttribute("aria-label") == "Fjern filteret Navn inneholder «b»"));
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Endre avansert søk");
    }

    [Fact]
    public void Open_WhenAChipRemovalIsQueuedBehindASearch_ThenTheRowsAreTheQueuedConditions()
    {
        var client = new HeldClient { HoldLaterSearches = true };
        var cut = RenderWith(client, "navn:inneholder:a", "navn:inneholder:b", "navn:inneholder:c");

        RemoveChip(cut, "a");
        RemoveChip(cut, "b");
        Toggle(cut)!.Click();

        cut.WaitForAssertion(() => Assert.Equal("c", Assert.Single(Rows(cut)).QuerySelector("input")!.GetAttribute("value")));
    }

    private static void RemoveChip(IRenderedComponent<VariableSearch> cut, string value) =>
        cut.FindAll("button").Single(b => b.GetAttribute("aria-label") == $"Fjern filteret Navn inneholder «{value}»").Click();


    [Fact]
    public async Task ShowHits_WhenNullstillIsPressedWhileTheBrowserIsAskedAboutADate_ThenNothingIsApplied()
    {
        var browser = JSInterop.SetupModule(ExplorerInterop.ModulePath).Setup<DateInputState>("readDateInput", _ => true);
        var client = new HeldClient();
        var cut = RenderWith(client, "navn:inneholder:a");
        Toggle(cut)!.Click();
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        Rows(cut)[0].QuerySelector("input")!.Change("b");
        Press(cut, "Legg til betingelse");
        PickDate(cut, "for", row: 1);
        var before = client.Searches.Count;

        Press(cut, "Vis treff");
        Assert.NotEmpty(browser.Invocations);
        Press(cut, "Nullstill");

        var renders = cut.RenderCount;
        await cut.InvokeAsync(() => browser.SetResult(new DateInputState("", false)));
        cut.WaitForState(() => cut.RenderCount > renders);

        Assert.Equal("true", Toggle(cut)!.GetAttribute("aria-expanded"));
        Assert.Equal(before, client.Searches.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowHits_WhenTheFormIsClosedWhileTheBrowserIsAskedAboutADate_ThenNothingIsApplied(bool reopen)
    {
        var browser = JSInterop.SetupModule(ExplorerInterop.ModulePath).Setup<DateInputState>("readDateInput", _ => true);
        var client = new HeldClient();
        var cut = RenderWith(client, "navn:inneholder:a");
        Toggle(cut)!.Click();
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        Rows(cut)[0].QuerySelector("input")!.Change("b");
        Press(cut, "Legg til betingelse");
        PickDate(cut, "for", row: 1);
        var before = client.Searches.Count;

        Press(cut, "Vis treff");
        Assert.NotEmpty(browser.Invocations);
        Toggle(cut)!.Click();
        if (reopen)
        {
            Toggle(cut)!.Click();
        }

        var renders = cut.RenderCount;
        await cut.InvokeAsync(() => browser.SetResult(new DateInputState("", false)));
        cut.WaitForState(() => cut.RenderCount > renders);

        Assert.Equal(reopen ? "true" : "false", Toggle(cut)!.GetAttribute("aria-expanded"));
        Assert.Equal(before, client.Searches.Count);
    }

    [Fact]
    public void ShowHits_WhenOnlyTheRangesLaterEndHoldsABadNativeEntry_ThenTheRowIsRefusedAsABadDate()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<DateInputState>("readDateInput", call => call.Arguments[0] is string id && id.EndsWith("-from", StringComparison.Ordinal))
            .SetResult(new DateInputState("", false));
        module.Setup<DateInputState>("readDateInput", call => call.Arguments[0] is string id && id.EndsWith("-to", StringComparison.Ordinal))
            .SetResult(new DateInputState("", true));
        var client = new HeldClient();
        var cut = RenderWith(client);
        Toggle(cut)!.Click();
        PickDate(cut, "mellom");
        var before = client.Searches.Count;

        Press(cut, "Vis treff");

        cut.WaitForAssertion(() => Assert.Contains("Skriv en gyldig dato.", Rows(cut)[0].TextContent, StringComparison.Ordinal));
        Assert.Equal(before, client.Searches.Count);
    }
}
