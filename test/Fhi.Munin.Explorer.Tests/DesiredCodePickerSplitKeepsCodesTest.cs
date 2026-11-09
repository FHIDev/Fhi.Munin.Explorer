using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Unticking a code under a parent chosen whole splits that parent, and every code on the way down (the whole parent's
/// own code included) stays chosen as a code of its own, while a group is never kept as a code (Fhi.Metadata-9huq2.14).
/// </summary>
public class DesiredCodePickerSplitKeepsCodesTest : ExplorerTestContext
{
    private static readonly Guid Variable = new("5f0c2a8e-7d4b-4c1e-9a36-2b8f1d0e6c47");

    private static readonly KodeverkLink Icd10 =
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", DisplayName = "ICD-10", HasCodeValues = true };

    private static KodeverkCodeHit Hit(string code, string name, int? count = null, bool group = false, params string[] above) =>
        new() { Value = code, Name = name, HasChildren = count is not null, Count = count, IsGroup = group, Ancestors = above };

    private static readonly Dictionary<string, KodeverkCodeHit[]> Icd10Tree = new()
    {
        [""] = [Hit("IV", "Endokrine sykdommer", 13, true)],
        ["IV"] = [Hit("E10-E14", "Diabetes mellitus", 8, true, "IV"), Hit("E15-E16", "Annen glukoseregulering", 5, true, "IV")],
        ["E10-E14"] =
        [
            Hit("E10", "Diabetes mellitus type 1", 3, false, "IV", "E10-E14"),
            Hit("E11", "Diabetes mellitus type 2", 3, false, "IV", "E10-E14"),
            Hit("E14", "Uspesifisert diabetes", null, false, "IV", "E10-E14"),
        ],
        ["E10"] = [Hit("E100", "Med koma", null, false, "IV", "E10-E14", "E10"), Hit("E101", "Med ketoacidose", null, false, "IV", "E10-E14", "E10")],
        ["E11"] = [Hit("E110", "Med koma", null, false, "IV", "E10-E14", "E11"), Hit("E119", "Uten komplikasjoner", null, false, "IV", "E10-E14", "E11")],
    };

    private sealed class FakeClient : EmptyMuninExplorerClient
    {
        public Func<string, int, int, KodeverkCodePage?> Search { get; init; } = (_, _, _) => new KodeverkCodePage();

        public HashSet<string> Failing { get; } = [];

        public Dictionary<string, KodeverkCodeHit[]> Levels { get; } = [];

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            if (Failing.Contains(node ?? ""))
            {
                throw new HttpRequestException("Kodeverk is down");
            }

            var codes = Levels.GetValueOrDefault(node ?? "") ?? Icd10Tree.GetValueOrDefault(node ?? "") ?? [];
            return Task.FromResult<KodeverkCodePage?>(
                new KodeverkCodePage { Total = codes.Length, CodeCount = 22141, Codes = [.. codes.Skip(skip).Take(take)] });
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

    private IRenderedComponent<DesiredCodePicker> Picker(Saves saves, IReadOnlyList<DesiredCodeView> chosen, FakeClient? client = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client ?? new FakeClient());
        Services.AddSingleton<TimeProvider>(new ManualClock());
        return Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Icd10])
            .Add(c => c.Chosen, chosen)
            .Add(c => c.Save, saves.SaveAsync));
    }

    private static DesiredCodeView Saved(string kind, string code, int? count, params string[] above) =>
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = kind, Code = code, Count = count, Ancestors = above };

    private static void Press(IRenderedComponent<DesiredCodePicker> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static IElement Row(IElement tree, string code) =>
        tree.QuerySelectorAll("[role=treeitem]").First(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent == code);

    private static IElement Tree(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-labelledby]");

    private static IElement Hits(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-label]");

    private static void Open(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(Tree(cut), code).QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

    private static void Tick(IElement row) =>
        row.QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    private static List<(string Kind, string Code)> SavedAfter(Saves saves, IRenderedComponent<DesiredCodePicker> cut)
    {
        Press(cut, "Lagre");
        return [.. saves.Calls[^1].Select(v => (v.Kind, v.Code)).OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.Kind, StringComparer.Ordinal)];
    }

    private static List<(string Kind, string Code)> Expected(params (string Kind, string Code)[] picks) =>
        [.. picks.OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.Kind, StringComparer.Ordinal)];

    private static async Task OpenDownToE10AndUntickE100(IRenderedComponent<DesiredCodePicker> cut)
    {
        Open(cut, "IV");
        Open(cut, "E10-E14");
        Open(cut, "E10");

        Tick(Row(Tree(cut), "E100"));
        await cut.InvokeAsync(() => { });
    }

    [Fact]
    public async Task Untick_WhenAWholeGroupIsSplitPastACodeWithSubcodes_ThenThatCodeStaysChosenAsACode()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E10-E14", 8, "IV")]);

        await OpenDownToE10AndUntickE100(cut);

        Assert.Equal("mixed", Row(Tree(cut), "E10").GetAttribute("aria-checked"));
        Assert.Equal(
            Expected(
                (DesiredCodeKinds.Code, "E10"),
                (DesiredCodeKinds.Code, "E101"),
                (DesiredCodeKinds.Node, "E11"),
                (DesiredCodeKinds.Code, "E14")),
            SavedAfter(saves, cut));
    }

    [Fact]
    public async Task Untick_WhenTheWholeParentIsItselfACode_ThenItsOwnCodeStaysChosenAsACode()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E10", 3, "IV", "E10-E14")]);

        await OpenDownToE10AndUntickE100(cut);

        Assert.Equal(Expected((DesiredCodeKinds.Code, "E10"), (DesiredCodeKinds.Code, "E101")), SavedAfter(saves, cut));
    }

    [Fact]
    public async Task Untick_WhenAWholeChapterIsSplitThreeLevelsDown_ThenTheGroupsOnTheWayAreNotKeptAsCodes()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "IV", 13)]);

        await OpenDownToE10AndUntickE100(cut);

        var saved = SavedAfter(saves, cut);
        Assert.DoesNotContain((DesiredCodeKinds.Code, "IV"), saved);
        Assert.DoesNotContain((DesiredCodeKinds.Code, "E10-E14"), saved);
        Assert.Equal(
            Expected(
                (DesiredCodeKinds.Code, "E10"),
                (DesiredCodeKinds.Code, "E101"),
                (DesiredCodeKinds.Node, "E11"),
                (DesiredCodeKinds.Code, "E14"),
                (DesiredCodeKinds.Node, "E15-E16")),
            saved);
    }

    [Fact]
    public async Task Untick_WhenTheWholeParentWasOnlyReachedThroughASearchHit_ThenItsOwnCodeStaysChosenAsACode()
    {
        var saves = new Saves();
        var client = new FakeClient
        {
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E10"][0]] },
        };
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E10", 3, "IV", "E10-E14")], client);
        cut.Find("input[type=search]").Input("E100");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("true", Row(Hits(cut), "E100").GetAttribute("aria-checked"));

        Tick(Row(Hits(cut), "E100"));
        await cut.InvokeAsync(() => { });

        Assert.Equal(Expected((DesiredCodeKinds.Code, "E10"), (DesiredCodeKinds.Code, "E101")), SavedAfter(saves, cut));
    }

    private static FakeClient HitsE100() =>
        new() { Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E10"][0]] } };

    private static void SearchAndUntickE100(IRenderedComponent<DesiredCodePicker> cut)
    {
        cut.Find("input[type=search]").Input("E100");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Tick(Row(Hits(cut), "E100"));
    }

    [Fact]
    public void Tick_WhenACodeWithSubcodesIsChosenAsJustItself_ThenOnePressChoosesItWholeAndDropsTheCodePicks()
    {
        var saves = new Saves();
        var cut = Picker(saves, [
            Saved(DesiredCodeKinds.Code, "E10", 1, "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E101", 1, "IV", "E10-E14", "E10"),
        ]);
        Open(cut, "IV");
        Open(cut, "E10-E14");
        Assert.Equal("mixed", Row(Tree(cut), "E10").GetAttribute("aria-checked"));
        Assert.Contains("2 av 3", Row(Tree(cut), "E10").TextContent);

        Tick(Row(Tree(cut), "E10"));

        Assert.Equal("true", Row(Tree(cut), "E10").GetAttribute("aria-checked"));
        Assert.Equal(Expected((DesiredCodeKinds.Node, "E10")), SavedAfter(saves, cut));
    }

    [Fact]
    public void ClearHits_WhenAHitWithSubcodesIsChosenAsJustItself_ThenItsCodeGoesToo()
    {
        var saves = new Saves();
        var client = new FakeClient { Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E10-E14"][0]] } };
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Code, "E10", 1, "IV", "E10-E14")], client);
        cut.Find("input[type=search]").Input("E10");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Press(cut, "Fjern treffene");

        Assert.Empty(SavedAfter(saves, cut));
    }

    [Fact]
    public async Task Untick_WhenACodeOnTheWayIsAlsoSavedOnItsOwn_ThenItIsChosenOnce()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E10-E14", 8, "IV"), Saved(DesiredCodeKinds.Code, "E10", 1, "IV", "E10-E14")]);

        await OpenDownToE10AndUntickE100(cut);

        Assert.Equal(
            Expected((DesiredCodeKinds.Code, "E10"), (DesiredCodeKinds.Code, "E101"), (DesiredCodeKinds.Node, "E11"), (DesiredCodeKinds.Code, "E14")),
            SavedAfter(saves, cut));
    }

    [Fact]
    public async Task Untick_WhenTheWholeParentsLevelCannotBeRead_ThenItSaysSoAndNothingChanges()
    {
        var saves = new Saves();
        var client = HitsE100();
        client.Failing.Add("E10-E14");
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E10", 3, "IV", "E10-E14")], client);

        SearchAndUntickE100(cut);
        await cut.InvokeAsync(() => { });

        Assert.Contains("Kunne ikke hente kodelisten nå", cut.Find("[role=alert]").TextContent);
        Assert.Equal(Expected((DesiredCodeKinds.Node, "E10")), SavedAfter(saves, cut));
    }

    [Fact]
    public async Task Untick_WhenTheWholeParentIsPastTheFirst200OfItsLevel_ThenItSaysTheLevelIsTooLongAndNothingChanges()
    {
        var saves = new Saves();
        var client = HitsE100();
        client.Levels["E10-E14"] =
        [
            .. Enumerable.Range(0, 250).Select(i => Hit($"E09{i:000}", "Annen", null, false, "IV", "E10-E14")),
            Icd10Tree["E10-E14"][0],
        ];
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E10", 3, "IV", "E10-E14")], client);

        SearchAndUntickE100(cut);
        await cut.InvokeAsync(() => { });

        Assert.Contains("E10-E14 har for mange koder til at én kan tas ut her. Fjern E10", cut.Find("[role=alert]").TextContent);
        Assert.Equal(Expected((DesiredCodeKinds.Node, "E10")), SavedAfter(saves, cut));
    }
}
