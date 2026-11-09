using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The code picker's way out for a saved choice the tree cannot show (no tab, no count, not found on the way into the tree,
/// refused on save), unticking a parent saved with a pick under it, «Fjern treffene» over a hit inside a parent chosen
/// whole, and a save pressed while an untick still reads (Fhi.Metadata-9huq2.14).
/// </summary>
public class DesiredCodePickerRemoveAndUntickTest : ExplorerTestContext
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
        public Func<string, int, int, KodeverkCodePage?> Search { get; init; } = (_, _, _) => new KodeverkCodePage();

        public bool TopFails { get; init; }

        public Dictionary<string, TaskCompletionSource> Gates { get; } = [];

        public override async Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            if (node is null && TopFails)
            {
                throw new HttpRequestException("Kodeverk is down");
            }

            if (node is not null && Gates.TryGetValue(node, out var gate))
            {
                await gate.Task;
            }

            var codes = Icd10Tree.GetValueOrDefault(node ?? "") ?? [];
            return new KodeverkCodePage { Total = codes.Length, CodeCount = 22141, Codes = [.. codes.Skip(skip).Take(take)] };
        }

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) => Task.FromResult(Search(query, skip, take));
    }

    private sealed class Saves(params DesiredDataResult[] answers)
    {
        public List<IReadOnlyList<DesiredCodeView>> Calls { get; } = [];

        public Task<DesiredDataResult> SaveAsync(IReadOnlyList<DesiredCodeView> views)
        {
            Calls.Add(views);
            return Task.FromResult(Calls.Count <= answers.Length ? answers[Calls.Count - 1] : new DesiredDataResult(DesiredDataOutcome.Saved));
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

    private static DesiredCodeView Saved(string kind, string code, int? count, string reference = "ICD-10", params string[] above) =>
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = reference, Kind = kind, Code = code, Count = count, Ancestors = above };

    private static void Press(IRenderedComponent<DesiredCodePicker> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static List<IElement> RemoveButtons(IRenderedComponent<DesiredCodePicker> cut) =>
        [.. cut.FindAll(".munin-explorer-data-list button").Where(b => b.TextContent.Trim().StartsWith("Fjern ", StringComparison.Ordinal))];

    private static IElement Row(IElement tree, string code) =>
        tree.QuerySelectorAll("[role=treeitem]").First(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent == code);

    private static IElement Tree(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-labelledby]");

    private static IElement Hits(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-label]");

    private static void Open(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(Tree(cut), code).QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

    private static void Tick(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(Tree(cut), code).QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    private static List<(string Kind, string Code)> SavedAfter(Saves saves, IRenderedComponent<DesiredCodePicker> cut)
    {
        Press(cut, "Lagre");
        return [.. saves.Calls[^1].Select(v => (v.Kind, v.Code))];
    }

    private static void RemoveThenSaveSendsNothing(Saves saves, IRenderedComponent<DesiredCodePicker> cut)
    {
        Assert.Single(RemoveButtons(cut)).Click();

        Assert.Empty(SavedAfter(saves, cut));
    }

    [Fact]
    public void Remove_WhenASavedCodesListHasNoTab_ThenItsLineHasARemoveButtonAndSavingSendsNothing()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Code, "1A00", 1, "ICD-11")]);

        RemoveThenSaveSendsNothing(saves, cut);
    }

    [Fact]
    public void Remove_WhenASavedCodeHasNoCount_ThenItsLineHasARemoveButtonAndSavingSendsNothing()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Code, "E139", null, "ICD-10", "IV", "E10-E14")]);

        RemoveThenSaveSendsNothing(saves, cut);
    }

    [Fact]
    public void Remove_WhenTheSaveRefusesASavedCode_ThenItsLineHasARemoveButtonAndTheNextSaveLeavesItOut()
    {
        var saves = new Saves(new DesiredDataResult(DesiredDataOutcome.Refused, InvalidIndex: 0));
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Code, "E14", 1, "ICD-10", "IV", "E10-E14")]);
        Press(cut, "Lagre");
        Assert.Contains("finnes ikke i kodelisten", cut.Find("[role=alert]").TextContent);

        RemoveThenSaveSendsNothing(saves, cut);
        Assert.Equal(2, saves.Calls.Count);
    }

    [Fact]
    public void Render_WhenASavedCodeWithACountIsOnAListedList_ThenItsLineHasNoRemoveButton()
    {
        var cut = Picker(new Saves(), [Saved(DesiredCodeKinds.Code, "E14", 1, "ICD-10", "IV", "E10-E14")]);

        Assert.Single(cut.FindAll(".munin-explorer-data-list li"));
        Assert.Empty(RemoveButtons(cut));
    }

    [Fact]
    public async Task Untick_WhenAParentIsSavedWithAPickUnderIt_ThenOnePressUnticksItAndNeitherIsSaved()
    {
        var saves = new Saves();
        var cut = Picker(saves, [
            Saved(DesiredCodeKinds.Node, "E11", 3, "ICD-10", "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E119", 1, "ICD-10", "IV", "E10-E14", "E11"),
        ]);
        Open(cut, "IV");
        Open(cut, "E10-E14");

        Tick(cut, "E11");
        await cut.InvokeAsync(() => { });

        Assert.Equal("false", Row(Tree(cut), "E11").GetAttribute("aria-checked"));
        Assert.Empty(SavedAfter(saves, cut));
    }

    [Fact]
    public async Task ClearHits_WhenAHitIsUnderASavedWholeParent_ThenTheHitIsUntickedAndTheParentsOtherChildrenStayChosen()
    {
        var saves = new Saves();
        var client = new FakeClient
        {
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E11"][1]] },
        };
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E11", 3, "ICD-10", "IV", "E10-E14")], client);
        cut.Find("input[type=search]").Input("E119");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("true", Row(Hits(cut), "E119").GetAttribute("aria-checked"));

        Press(cut, "Fjern treffene");
        await cut.InvokeAsync(() => { });

        Assert.Equal("false", Row(Hits(cut), "E119").GetAttribute("aria-checked"));
        Assert.Equal([(DesiredCodeKinds.Code, "E110")], SavedAfter(saves, cut));
    }

    [Fact]
    public void Render_WhenACodeWithNoCountIsShownByTheTree_ThenItsLineHasNoRemoveButton()
    {
        var cut = Picker(new Saves(), [Saved(DesiredCodeKinds.Code, "E14", null, "ICD-10", "IV", "E10-E14")]);
        Assert.Single(RemoveButtons(cut));

        Open(cut, "IV");
        Open(cut, "E10-E14");

        Assert.Empty(RemoveButtons(cut));
    }

    [Fact]
    public async Task Reveal_WhenTheCodeIsNotInTheTree_ThenItsLineHasARemoveButton()
    {
        var cut = Picker(new Saves(), [Saved(DesiredCodeKinds.Code, "E139", 1, "ICD-10", "IV", "E10-E14")]);
        Assert.Empty(RemoveButtons(cut));

        cut.Find("button[aria-label='Vis E139 i treet']").Click();
        await cut.InvokeAsync(() => { });

        Assert.Contains("Fant ikke E139 i treet", cut.Find("[role=alert]").TextContent);
        Assert.Single(RemoveButtons(cut));
    }

    [Fact]
    public async Task Reveal_WhenTheTopLevelCannotBeRead_ThenItSaysSoAndOffersNoRemoveButton()
    {
        var cut = Picker(new Saves(), [Saved(DesiredCodeKinds.Code, "E14", 1, "ICD-10")], new FakeClient { TopFails = true });

        cut.Find("button[aria-label='Vis E14 i treet']").Click();
        await cut.InvokeAsync(() => { });

        Assert.Contains("Kunne ikke hente kodelisten nå", cut.Find("[role=alert]").TextContent);
        Assert.Empty(RemoveButtons(cut));
    }

    [Fact]
    public async Task Save_WhenPressedWhileAnUntickStillReads_ThenItSendsWhatTheUntickLeaves()
    {
        var saves = new Saves();
        var client = new FakeClient
        {
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E11"][1]] },
        };
        var gate = client.Gates["E11"] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Picker(saves, [Saved(DesiredCodeKinds.Node, "E11", 3, "ICD-10", "IV", "E10-E14")], client);
        cut.Find("input[type=search]").Input("E119");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Row(Hits(cut), "E119").QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });
        Press(cut, "Lagre");
        Assert.Empty(saves.Calls);
        gate.SetResult();
        await cut.InvokeAsync(() => { });

        cut.WaitForAssertion(() => Assert.Single(saves.Calls));
        Assert.Equal([(DesiredCodeKinds.Code, "E110")], saves.Calls[0].Select(v => (v.Kind, v.Code)));
    }
}
