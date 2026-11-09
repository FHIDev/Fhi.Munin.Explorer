using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Unticking under overlapping saved picks, half-ticked rows over an older range, and the count beside a saved single code.</summary>
public class DesiredCodePickerSavedOverlapTest : ExplorerTestContext
{
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");

    private static readonly KodeverkLink Icd10 =
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", DisplayName = "ICD-10", HasCodeValues = true };

    private static KodeverkCodeHit Hit(string code, string name, int? count = null, bool group = false, params string[] above) =>
        new() { Value = code, Name = name, HasChildren = count is not null, Count = count, IsGroup = group, Ancestors = above };

    private static readonly Dictionary<string, KodeverkCodeHit[]> Tree = new()
    {
        [""] = [Hit("IV", "Endokrine sykdommer", 8, true)],
        ["IV"] = [Hit("E10-E14", "Diabetes mellitus", 8, true, "IV")],
        ["E10-E14"] =
        [
            Hit("E10", "Diabetes mellitus type 1", 2, false, "IV", "E10-E14"),
            Hit("E11", "Diabetes mellitus type 2", 3, false, "IV", "E10-E14"),
            Hit("E12", "Underernæring", 3, false, "IV", "E10-E14"),
        ],
        ["E10"] = [Hit("E100", "Med koma", null, false, "IV", "E10-E14", "E10"), Hit("E101", "Med ketoacidose", null, false, "IV", "E10-E14", "E10")],
        ["E11"] =
        [
            Hit("E110", "Med koma", null, false, "IV", "E10-E14", "E11"),
            Hit("E111", "Med ketoacidose", null, false, "IV", "E10-E14", "E11"),
            Hit("E119", "Uten komplikasjoner", null, false, "IV", "E10-E14", "E11"),
        ],
    };

    private sealed class FakeClient : EmptyMuninExplorerClient
    {
        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            var codes = Tree.GetValueOrDefault(node ?? "") ?? [];
            return Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage { Total = codes.Length, CodeCount = 22141, Codes = [.. codes.Skip(skip).Take(take)] });
        }
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

    private IRenderedComponent<DesiredCodePicker> Picker(Saves saves, IReadOnlyList<DesiredCodeView> chosen)
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient());
        Services.AddSingleton<TimeProvider>(new ManualClock());
        return Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Icd10])
            .Add(c => c.Chosen, chosen)
            .Add(c => c.Save, saves.SaveAsync));
    }

    private static DesiredCodeView Saved(string kind, string code, string? to, int count, params string[] above) =>
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = kind, Code = code, To = to, Count = count, Ancestors = above };

    private static IElement Row(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        cut.Find("ul[role=tree][aria-labelledby]").QuerySelectorAll("[role=treeitem]")
            .First(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent == code);

    private static void Open(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

    private static void Tick(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    private static string? Checked(IRenderedComponent<DesiredCodePicker> cut, string code) => Row(cut, code).GetAttribute("aria-checked");

    private static void OpenDownTo(IRenderedComponent<DesiredCodePicker> cut, params string[] path)
    {
        foreach (var code in path)
        {
            Open(cut, code);
        }
    }

    private static List<DesiredCodeView> SavedAfter(Saves saves, IRenderedComponent<DesiredCodePicker> cut)
    {
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Lagre").Click();
        return [.. Assert.Single(saves.Calls)];
    }

    private static void AssertNoEqualChoices(IEnumerable<DesiredCodeView> views) =>
        Assert.Equal(views.Count(), views.Select(v => (v.Kind, v.Code, v.To)).Distinct().Count());

    [Fact]
    public void Untick_WhenANodeAndOneOfItsCodesAreSavedAndAnotherCodeIsUnticked_ThenTheSavedCodeIsKeptOnce()
    {
        var saves = new Saves();
        var cut = Picker(saves, [
            Saved(DesiredCodeKinds.Node, "E11", null, 3, "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E119", null, 1, "IV", "E10-E14", "E11")]);
        OpenDownTo(cut, "IV", "E10-E14", "E11");

        Tick(cut, "E110");
        var sent = SavedAfter(saves, cut);

        Assert.Equal(1, sent.Count(v => v.Code == "E119"));
        Assert.DoesNotContain(sent, v => v.Code == "E110");
        AssertNoEqualChoices(sent);
        Assert.Equal("false", Checked(cut, "E110"));
    }

    [Fact]
    public void Untick_WhenAChapterAndABlockCategoryAreSavedAndACodeUnderAnotherCategoryIsUnticked_ThenTheCategoryIsNotChosenTwice()
    {
        var saves = new Saves();
        var cut = Picker(saves, [
            Saved(DesiredCodeKinds.Node, "IV", null, 8, []),
            Saved(DesiredCodeKinds.Node, "E10", null, 2, "IV", "E10-E14")]);
        OpenDownTo(cut, "IV", "E10-E14", "E11");

        Tick(cut, "E110");
        var sent = SavedAfter(saves, cut);

        Assert.Equal(1, sent.Count(v => v.Kind == DesiredCodeKinds.Node && v.Code == "E10"));
        AssertNoEqualChoices(sent);
        Assert.Equal("false", Checked(cut, "E110"));
    }

    [Fact]
    public void Untick_WhenABlockAndOneOfItsCategoriesAreSaved_ThenOnePressUnticksTheCodeAndNothingIsSavedTwice()
    {
        var saves = new Saves();
        var cut = Picker(saves, [
            Saved(DesiredCodeKinds.Node, "E10-E14", null, 8, "IV"),
            Saved(DesiredCodeKinds.Node, "E11", null, 3, "IV", "E10-E14")]);
        OpenDownTo(cut, "IV", "E10-E14", "E11");

        Tick(cut, "E110");

        Assert.Equal("false", Checked(cut, "E110"));
        var sent = SavedAfter(saves, cut);
        AssertNoEqualChoices(sent);
        Assert.DoesNotContain(sent, v => v.Code == "E110");
    }

    [Fact]
    public void Render_WhenAnOlderRangeIsSavedWithTheChapterAndBlockAsParents_ThenBothShowAsHalfTicked()
    {
        var cut = Picker(new Saves(), [Saved(DesiredCodeKinds.Range, "E10", "E12", 8, "IV", "E10-E14")]);
        Open(cut, "IV");

        Assert.Equal("mixed", Checked(cut, "IV"));
        Assert.Equal("mixed", Checked(cut, "E10-E14"));
    }

    [Fact]
    public void Render_WhenASingleCodeThatHasChildrenIsSaved_ThenItsRowCountsOnlyThatCode()
    {
        var cut = Picker(new Saves(), [Saved(DesiredCodeKinds.Code, "E11", null, 1, "IV", "E10-E14")]);
        OpenDownTo(cut, "IV", "E10-E14");

        var text = Row(cut, "E11").QuerySelector(".munin-explorer-codetree__row")!.TextContent;
        Assert.Contains("1 av 3", text);
        Assert.DoesNotContain("3 av 3", text);
    }
}
