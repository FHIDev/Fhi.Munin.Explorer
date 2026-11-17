// Held out: written from the behaviour, not the code (Fhi.Metadata-9huq2.43).
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class DesiredCodePickerOverCapHeldOutTest : ExplorerTestContext
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

        public override Task<int?> CountKodeverkCodesAsync(
            Guid variableId, DesiredCodeChoice choice, CancellationToken cancellationToken = default) =>
            Task.FromResult(Icd10Tree.Values.SelectMany(level => level).FirstOrDefault(hit => hit.Value == choice.Code)?.Count);

        public override Task<int?> CountKodeverkCodesUnionAsync(
            Guid variableId, IReadOnlyList<DesiredCodeChoice> choices, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(choices.Count * 7);
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

    private static string ImportSentence(int count) =>
        $"Velg høyst 200 kodevalg; dette elementet har {count} fra importen. Fjern noen før du lagrer.";

    [Fact]
    public void Remove_WhenAnOverCapItemIsBroughtTo200_ThenNothingMoreIsSaidOfTheImport()
    {
        var saves = new Saves();
        var cut = Picker(saves, Fillers(203));

        for (var i = 1; i <= 3; i++)
        {
            Press(cut, $"Fjern C{i:000}");
        }

        Assert.Contains("200 av 200 valg", Text(cut));
        Assert.DoesNotContain("fra importen", Live(cut));
        Assert.DoesNotContain("fra importen", Text(cut));

        Press(cut, "Lagre");

        var sent = Assert.Single(saves.Calls);
        Assert.Equal(200, sent.Count);
        Assert.Equal("C004", sent[0].Code);
        Assert.Equal("C203", sent[^1].Code);
    }

    [Fact]
    public void Remove_WhenSavingWasRefusedWhileOverAndTheItemIsThenBroughtTo200_ThenNothingMoreIsSaidOfTheImport()
    {
        var saves = new Saves();
        var cut = Picker(saves, Fillers(202));

        Press(cut, "Lagre");
        Assert.Empty(saves.Calls);
        Assert.Contains(ImportSentence(202), cut.Find("[role=alert]").TextContent);

        Press(cut, "Fjern C001");
        Press(cut, "Fjern C002");

        Assert.DoesNotContain("fra importen", Live(cut));

        Press(cut, "Lagre");

        Assert.Equal(200, Assert.Single(saves.Calls).Count);
    }

    [Fact]
    public void Tick_WhenAnItemIsOverTheCap_ThenNothingChanges()
    {
        var saves = new Saves();
        var cut = Picker(saves, Fillers(203));
        Open(cut, "IV");

        Tick(cut, "E15-E16");

        Assert.Contains("203 av 200 valg", Text(cut));
        Assert.Contains(ImportSentence(203), cut.Find("[role=alert]").TextContent);
        Assert.Equal("false", Row(cut, "E15-E16").GetAttribute("aria-checked"));
        Assert.Empty(saves.Calls);
    }

    [Fact]
    public void Tick_WhenAParentWouldFoldEnoughChosenChildren_ThenItIsStillRefused()
    {
        var saves = new Saves();
        // Six of these sit under E10-E14; ticking it would fold them into one choice and take 203 down to 198.
        IReadOnlyList<DesiredCodeView> chosen =
        [
            Saved("E10", "IV", "E10-E14"), Saved("E11", "IV", "E10-E14"), Saved("E14", "IV", "E10-E14"),
            Saved("E100", "IV", "E10-E14", "E10"), Saved("E101", "IV", "E10-E14", "E10"), Saved("E110", "IV", "E10-E14", "E11"),
            .. Fillers(197),
        ];
        var cut = Picker(saves, chosen);
        Assert.Contains("203 av 200 valg", Text(cut));
        Open(cut, "IV");

        Tick(cut, "E10-E14");

        Assert.Contains("203 av 200 valg", Text(cut));
        Assert.Contains(ImportSentence(203), cut.Find("[role=alert]").TextContent);

        Press(cut, "Lagre");

        Assert.Empty(saves.Calls);
    }
}
