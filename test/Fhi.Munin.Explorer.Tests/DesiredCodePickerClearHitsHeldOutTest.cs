// Held out: written from the behaviour, not the code (Fhi.Metadata-9huq2.43).
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class DesiredCodePickerClearHitsHeldOutTest : ExplorerTestContext
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
        ["E10"] = [Hit("E100", "Med koma", null, false, "IV", "E10-E14", "E10"), Hit("E101", "Med ketoacidose", null, false, "IV", "E10-E14", "E10")],
        ["E11"] = [Hit("E110", "Med koma", null, false, "IV", "E10-E14", "E11")],
        ["E15-E16"] = [Hit("E150", "Annen", null, false, "IV", "E15-E16")],
    };

    private sealed class TreeClient : EmptyMuninExplorerClient
    {
        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodes?>(null);

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            var codes = Icd10Tree.GetValueOrDefault(node ?? "") ?? [];
            return Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage
            {
                Total = codes.Length,
                CodeCount = 22141,
                Codes = [.. codes.Skip(skip).Take(take)],
            });
        }

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage
            {
                Total = 3,
                Codes = [Icd10Tree["E10-E14"][0], Icd10Tree["E10-E14"][1], Icd10Tree["E10-E14"][2]],
            });

        public override Task<int?> CountKodeverkCodesAsync(
            Guid variableId, DesiredCodeChoice choice, CancellationToken cancellationToken = default) =>
            Task.FromResult(Icd10Tree.Values.SelectMany(level => level).FirstOrDefault(hit => hit.Value == choice.Code)?.Count);

        public override Task<int?> CountKodeverkCodesUnionAsync(
            Guid variableId, IReadOnlyList<DesiredCodeChoice> choices, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(choices.Count * 7);
    }

    private sealed class Saves
    {
        public DesiredDataResult Answer { get; set; } = new(DesiredDataOutcome.Saved);

        public List<IReadOnlyList<DesiredCodeView>> Calls { get; } = [];

        public Task<DesiredDataResult> SaveAsync(IReadOnlyList<DesiredCodeView> views)
        {
            Calls.Add(views);
            return Task.FromResult(Answer);
        }
    }

    private IRenderedComponent<DesiredCodePicker> Picker(Saves saves, IReadOnlyList<DesiredCodeView> chosen)
    {
        Services.AddSingleton<IMuninExplorerClient>(new TreeClient());
        Services.AddSingleton<TimeProvider>(new ManualClock());
        return Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Icd10])
            .Add(c => c.Chosen, chosen)
            .Add(c => c.Save, saves.SaveAsync)
            .Add(c => c.Closed, () => { }));
    }

    private static DesiredCodeView Saved(string code, params string[] above) =>
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = DesiredCodeKinds.Code, Code = code, Count = null, Ancestors = above };

    private static IReadOnlyList<DesiredCodeView> Fillers(int count) =>
        [.. Enumerable.Range(1, count).Select(i => Saved($"C{i:000}"))];

    private static string Text(IRenderedComponent<DesiredCodePicker> cut) =>
        System.Text.RegularExpressions.Regex.Replace(cut.Find("[role=group]").TextContent, @"\s+", " ");

    private static void Press(IRenderedComponent<DesiredCodePicker> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static IElement Row(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        cut.Find("ul[role=tree][aria-labelledby]").QuerySelectorAll("[role=treeitem]")
            .First(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent == code);

    private static void Tick(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    private static void Open(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

    private static string Live(IRenderedComponent<DesiredCodePicker> cut) =>
        string.Join(" ", cut.FindAll("[role=status], [role=alert]").Select(e => e.TextContent));

    private static void SearchAndRefuse(IRenderedComponent<DesiredCodePicker> cut)
    {
        cut.Find("input[type=search]").Input("e1");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() => Assert.Contains("Fjern treffene", cut.Markup));
        Press(cut, "Lagre");
    }

    private static IReadOnlyList<DesiredCodeView> OverCap(int total) =>
        [Saved("E10", "IV", "E10-E14"), Saved("E11", "IV", "E10-E14"), .. Fillers(total - 2)];

    private static string ImportSentence(int count) =>
        $"Velg høyst 200 kodevalg; dette elementet har {count} fra importen. Fjern noen før du lagrer.";

    [Fact]
    public void ClearHits_WhenAnOverCapItemIsBroughtUnderTheCap_ThenNothingMoreIsSaidOfTheImport()
    {
        var saves = new Saves();
        var cut = Picker(saves, OverCap(202));
        SearchAndRefuse(cut);
        Assert.Contains(ImportSentence(202), Live(cut));

        Press(cut, "Fjern treffene");

        Assert.Contains("200 av 200 valg", Text(cut));
        Assert.DoesNotContain("fra importen", Live(cut));

        Press(cut, "Lagre");

        Assert.Equal(200, Assert.Single(saves.Calls).Count);
    }

    [Fact]
    public void ClearHits_WhenTheItemIsStillOverAfterward_ThenNoLiveRegionStatesTheOldCount()
    {
        var saves = new Saves();
        var cut = Picker(saves, OverCap(210));
        SearchAndRefuse(cut);
        Assert.Contains(ImportSentence(210), Live(cut));

        Press(cut, "Fjern treffene");

        Assert.Contains("208 av 200 valg", Text(cut));
        var live = Live(cut);
        Assert.DoesNotContain("har 210 fra importen", live);
        if (live.Contains("fra importen"))
        {
            Assert.Contains(ImportSentence(208), live);
        }
    }

    [Fact]
    public void Remove_WhenSavingWasRefusedForAnotherChoice_ThenThatRefusalStays()
    {
        var saves = new Saves { Answer = new DesiredDataResult(DesiredDataOutcome.Refused, InvalidIndex: 1) };
        var cut = Picker(saves, [Saved("E10", "IV", "E10-E14"), Saved("E11", "IV", "E10-E14"), Saved("E14", "IV", "E10-E14")]);

        Press(cut, "Lagre");
        cut.WaitForAssertion(() => Assert.Contains("finnes ikke i kodelisten", Live(cut)));

        Press(cut, "Fjern E10");

        Assert.Contains("finnes ikke i kodelisten", Live(cut));
    }
}
