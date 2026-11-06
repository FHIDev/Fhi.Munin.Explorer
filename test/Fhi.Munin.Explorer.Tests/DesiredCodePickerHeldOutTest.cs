using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The code picker's saved ranges, level paging, keys inside the tree, hit rows, stale hits and cut levels (Fhi.Metadata-9huq2.14).</summary>
public class DesiredCodePickerHeldOutTest : ExplorerTestContext
{
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");

    private static readonly KodeverkLink Icd10 =
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", DisplayName = "ICD-10", HasCodeValues = true };

    private static KodeverkCodeHit Hit(string code, string name, int? count = null, bool group = false, params string[] above) =>
        new() { Value = code, Name = name, HasChildren = count is not null, Count = count, IsGroup = group, Ancestors = above };

    private static readonly Dictionary<string, KodeverkCodeHit[]> Icd10Tree = new()
    {
        [""] = [Hit("IV", "Endokrine sykdommer", 15, true)],
        ["IV"] = [Hit("E10-E14", "Diabetes mellitus", 10, true, "IV"), Hit("E15-E16", "Annen glukoseregulering", 5, true, "IV")],
        ["E10-E14"] =
        [
            Hit("E10", "Diabetes mellitus type 1", 3, false, "IV", "E10-E14"),
            Hit("E11", "Diabetes mellitus type 2", 3, false, "IV", "E10-E14"),
            Hit("E14", "Uspesifisert diabetes", null, false, "IV", "E10-E14"),
        ],
        ["E10"] = [Hit("E100", "Med koma", null, false, "IV", "E10-E14", "E10")],
        ["E11"] = [Hit("E110", "Med koma", null, false, "IV", "E10-E14", "E11"), Hit("E119", "Uten komplikasjoner", null, false, "IV", "E10-E14", "E11")],
    };

    private sealed class FakeClient : EmptyMuninExplorerClient
    {
        public Func<string, int, int, KodeverkCodePage?> Children { get; init; } = (node, skip, take) =>
        {
            var codes = Icd10Tree.GetValueOrDefault(node) ?? [];
            return new KodeverkCodePage { Total = codes.Length, CodeCount = 22141, Codes = [.. codes.Skip(skip).Take(take)] };
        };

        public Func<string, int, int, KodeverkCodePage?> Search { get; init; } = (_, _, _) => new KodeverkCodePage();
        public int ChildCalls { get; private set; }
        public HashSet<string> Failing { get; } = [];

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            ChildCalls++;
            if (Failing.Contains(node ?? ""))
            {
                return Task.FromException<KodeverkCodePage?>(new HttpRequestException("down"));
            }

            return Task.FromResult(Children(node ?? "", skip, take));
        }

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) => Task.FromResult(Search(query, skip, take));
    }

    private sealed class Saves
    {
        public List<IReadOnlyList<DesiredCodeView>> Calls { get; } = [];

        public Task<DesiredDataResult> SaveAsync(IReadOnlyList<DesiredCodeView> views)
        {
            Calls.Add(views);
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }
    }

    private IRenderedComponent<DesiredCodePicker> Picker(Saves saves, IMuninExplorerClient client, IReadOnlyList<DesiredCodeView>? chosen = null)
    {
        Services.AddSingleton(client);
        Services.AddSingleton<TimeProvider>(new ManualClock());
        return Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Icd10])
            .Add(c => c.Chosen, chosen ?? [])
            .Add(c => c.Save, saves.SaveAsync));
    }

    private static DesiredCodeView Saved(string kind, string code, string? to, int count, params string[] above) =>
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = kind, Code = code, To = to, Count = count, Ancestors = above };

    private static void Press(IRenderedComponent<DesiredCodePicker> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static IElement Tree(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-labelledby]");

    private static IElement Row(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Tree(cut).QuerySelectorAll("[role=treeitem]").First(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent == code);

    private static void Open(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

    private static void Tick(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    private static string? Checked(IRenderedComponent<DesiredCodePicker> cut, string code) => Row(cut, code).GetAttribute("aria-checked");

    private static string? TabStop(IRenderedComponent<DesiredCodePicker> cut) =>
        Tree(cut).QuerySelectorAll("[role=treeitem][tabindex='0']").Select(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent).SingleOrDefault();

    private static void SearchFor(IRenderedComponent<DesiredCodePicker> cut, string query)
    {
        cut.Find("input[type=search]").Input(query);
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    [Fact]
    public void Save_WhenASavedRangeIsInsideABlockWithMoreCodes_ThenItIsStoredUnchangedAsARange()
    {
        var saves = new Saves();
        var cut = Picker(saves, new FakeClient(), [Saved(DesiredCodeKinds.Range, "E10", "E14", 6, "IV", "E10-E14")]);
        Open(cut, "IV");

        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Range, "E10", "E14"), (sent.Kind, sent.Code, sent.To));
    }

    [Fact]
    public void Open_WhenALevelIgnoresSkipAndRepeatsItsPage_ThenTheReadStopsAfterAPageThatAddsNothing()
    {
        var asked = 0;
        var client = new FakeClient
        {
            // Returns nothing after a run of calls, so a loop that never ends is a failed assertion, not a hang.
            Children = (_, _, _) => asked++ > 25
                ? new KodeverkCodePage { Total = 500, Codes = [] }
                : new KodeverkCodePage { Total = 500, Codes = [Hit("A", "En"), Hit("B", "To")] },
        };

        Picker(new Saves(), client);

        Assert.True(client.ChildCalls <= 3, $"the level was asked {client.ChildCalls} times");
    }

    private IRenderedComponent<DesiredCodePicker> WithFailedBranch(out FakeClient client)
    {
        client = new FakeClient();
        client.Failing.Add("E15-E16");
        var cut = Picker(new Saves(), client);
        Open(cut, "IV");
        Open(cut, "E15-E16");
        Tree(cut).KeyDown(new KeyboardEventArgs { Key = "Home" });
        return cut;
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("ArrowDown")]
    public void Keys_WhenPressedOnABranchsRetryButton_ThenNoRowIsTickedAndTheTabStopStays(string key)
    {
        var cut = WithFailedBranch(out _);
        var retry = Assert.Single(cut.FindAll("button"), b => b.TextContent.Trim() == "Prøv igjen");
        var before = TabStop(cut);

        try
        {
            retry.KeyDown(new KeyboardEventArgs { Key = key });
        }
        catch (MissingEventHandlerException)
        {
            // A branch that stops the key before the tree has no handler to reach: the browser does nothing either.
        }

        Assert.All(Tree(cut).QuerySelectorAll("[role=treeitem]"), row => Assert.Equal("false", row.GetAttribute("aria-checked")));
        Assert.Equal(before, TabStop(cut));
    }

    [Fact]
    public void Open_WhenSearchHitsListABlockAndItsCategoriesAndTheBlockIsOpened_ThenIdsAreUniqueAndTheArrowKeyVisitsEachRowOnce()
    {
        var client = new FakeClient
        {
            Search = (_, _, _) => new KodeverkCodePage
            {
                Total = 3,
                Codes = [Icd10Tree["IV"][0], Icd10Tree["E10-E14"][0], Icd10Tree["E10-E14"][1]],
            },
        };
        var cut = Picker(new Saves(), client);
        SearchFor(cut, "diabetes");
        var hits = () => cut.Find("ul[role=tree][aria-label]");
        hits().QuerySelector("[role=treeitem] .munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

        var ids = cut.FindAll("[id]").Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var rows = hits().QuerySelectorAll("[role=treeitem]").ToList();
        Assert.True(rows.Count >= 5);
        for (var step = 0; step < rows.Count; step++)
        {
            var stops = hits().QuerySelectorAll("[role=treeitem][tabindex='0']").ToList();
            var only = Assert.Single(stops);
            Assert.Equal(step, hits().QuerySelectorAll("[role=treeitem]").ToList().IndexOf(only));
            hits().KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        }
    }

    [Fact]
    public void Tick_WhenABlockInsideASavedRangeIsTicked_ThenTheRangeStaysChosen()
    {
        var saves = new Saves();
        var cut = Picker(saves, new FakeClient(), [Saved(DesiredCodeKinds.Range, "E10", "E20", 12, "IV", "E10-E14")]);
        Open(cut, "IV");

        Tick(cut, "E10-E14");
        Press(cut, "Lagre");

        var sent = Assert.Single(saves.Calls);
        Assert.Contains(sent, v => (v.Kind, v.Code, v.To) == (DesiredCodeKinds.Range, "E10", "E20"));
    }

    [Fact]
    public void ChooseHits_WhenTheNewSearchFailed_ThenTheOldHitsCannotBeChosen()
    {
        var saves = new Saves();
        var client = new FakeClient
        {
            Search = (query, _, _) => query == "E11"
                ? new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E10-E14"][1]] }
                : throw new HttpRequestException("down"),
        };
        var cut = Picker(saves, client);
        SearchFor(cut, "E11");
        Assert.Single(cut.FindAll("ul[role=tree][aria-label] [role=treeitem]"));

        SearchFor(cut, "E12");
        var choose = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Velg treffene");
        choose?.Click();
        Press(cut, "Lagre");

        Assert.Empty(Assert.Single(saves.Calls));
    }

    [Fact]
    public async Task Untick_WhenTheTickedParentsLevelHasMoreThan200Codes_ThenNoLoadErrorShowsAndTheParentStaysChosen()
    {
        var client = new FakeClient
        {
            Children = (node, skip, take) => node == ""
                ? new KodeverkCodePage { Total = 1, CodeCount = 22141, Codes = [Hit("BIG", "Stor", 250)] }
                : new KodeverkCodePage
                {
                    Total = 250,
                    Codes = [.. Enumerable.Range(skip, Math.Max(0, Math.Min(take, 250 - skip))).Select(i => Hit($"C{i}", $"Kode {i}", null, false, "BIG"))],
                },
        };
        var cut = Picker(new Saves(), client, [Saved(DesiredCodeKinds.Node, "BIG", null, 250)]);
        Open(cut, "BIG");

        Tick(cut, "C5");
        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain("Kunne ikke hente kodelisten", cut.Find("[role=alert]").TextContent);
        Assert.Equal("true", Checked(cut, "BIG"));
    }

    private static List<(string Kind, string Code)> SavedAfter(Saves saves, IRenderedComponent<DesiredCodePicker> cut)
    {
        Press(cut, "Lagre");
        return [.. Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code))];
    }

    private static IRenderedComponent<DesiredCodePicker> OpenedTo(IRenderedComponent<DesiredCodePicker> cut, params string[] codes)
    {
        foreach (var code in codes)
        {
            Open(cut, code);
        }

        return cut;
    }

    [Fact]
    public async Task Untick_WhenAParentChosenWholeAlsoHasAPickUnderIt_ThenThatPickIsChosenOnceAndTheCodeIsUnticked()
    {
        var saves = new Saves();
        var cut = OpenedTo(Picker(saves, new FakeClient(), [
            Saved(DesiredCodeKinds.Node, "E11", null, 3, "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E119", null, 1, "IV", "E10-E14", "E11"),
        ]), "IV", "E10-E14", "E11");

        Tick(cut, "E110");
        await cut.InvokeAsync(() => { });

        Assert.Equal("false", Checked(cut, "E110"));
        Assert.Equal([(DesiredCodeKinds.Code, "E119")], SavedAfter(saves, cut));
    }

    [Fact]
    public async Task Untick_WhenAWholeParentIsSavedInsideAnotherWholeParent_ThenTheInnerOneIsNotAddedTwice()
    {
        var saves = new Saves();
        var cut = OpenedTo(Picker(saves, new FakeClient(), [
            Saved(DesiredCodeKinds.Node, "IV", null, 15),
            Saved(DesiredCodeKinds.Node, "E10", null, 3, "IV", "E10-E14"),
        ]), "IV", "E10-E14", "E11");

        Tick(cut, "E110");
        await cut.InvokeAsync(() => { });

        Assert.Equal("false", Checked(cut, "E110"));
        var sent = SavedAfter(saves, cut);
        Assert.Equal(sent.Count, sent.Distinct().Count());
        Assert.Equal(
            [(DesiredCodeKinds.Code, "E119"), (DesiredCodeKinds.Code, "E14"), (DesiredCodeKinds.Node, "E10"), (DesiredCodeKinds.Node, "E15-E16")],
            sent.Order());
    }

    [Fact]
    public async Task Untick_WhenTheCodesParentIsChosenWholeInsideAWholeBlock_ThenOnePressUnticksItAndAddsItsSiblingsOnce()
    {
        var saves = new Saves();
        var cut = OpenedTo(Picker(saves, new FakeClient(), [
            Saved(DesiredCodeKinds.Node, "E10-E14", null, 10, "IV"),
            Saved(DesiredCodeKinds.Node, "E11", null, 3, "IV", "E10-E14"),
        ]), "IV", "E10-E14", "E11");

        Tick(cut, "E110");
        await cut.InvokeAsync(() => { });

        Assert.Equal("false", Checked(cut, "E110"));
        Assert.Equal("mixed", Checked(cut, "E11"));
        Assert.Equal([(DesiredCodeKinds.Code, "E119"), (DesiredCodeKinds.Code, "E14"), (DesiredCodeKinds.Node, "E10")], SavedAfter(saves, cut).Order());
    }

    [Fact]
    public void Render_WhenTheSavedChoicesRepeatOne_ThenItIsShownAndSavedOnce()
    {
        var saves = new Saves();
        var cut = Picker(saves, new FakeClient(), [
            Saved(DesiredCodeKinds.Code, "E14", null, 1, "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E14", null, 1, "IV", "E10-E14"),
        ]);

        Assert.Single(cut.FindAll(".munin-explorer-data-list li"));
        Assert.Equal([(DesiredCodeKinds.Code, "E14")], SavedAfter(saves, cut));
    }

    [Fact]
    public void Render_WhenAnOlderRangeStartsUnderParents_ThenTheyAreHalfTickedWithNoCountAndTickingOneKeepsTheRange()
    {
        var saves = new Saves();
        var cut = OpenedTo(Picker(saves, new FakeClient(), [Saved(DesiredCodeKinds.Range, "E10", "E12", 7, "IV", "E10-E14")]), "IV");

        Assert.Equal("mixed", Checked(cut, "IV"));
        Assert.Equal("mixed", Checked(cut, "E10-E14"));
        Assert.Null(Row(cut, "E10-E14").Children[0].QuerySelector(".munin-explorer-codetree__count"));

        Tick(cut, "E10-E14");

        Assert.Contains((DesiredCodeKinds.Range, "E10"), SavedAfter(saves, cut));
    }

    [Fact]
    public void Render_WhenACodeWithChildrenIsSavedAsACode_ThenItIsTickedWithNoCountAndItsChildrenAreNot()
    {
        var cut = OpenedTo(Picker(new Saves(), new FakeClient(), [Saved(DesiredCodeKinds.Code, "E11", null, 1, "IV", "E10-E14")]), "IV", "E10-E14", "E11");

        Assert.Equal("true", Checked(cut, "E11"));
        Assert.Null(Row(cut, "E11").Children[0].QuerySelector(".munin-explorer-codetree__count"));
        Assert.Equal("false", Checked(cut, "E110"));
    }
}
