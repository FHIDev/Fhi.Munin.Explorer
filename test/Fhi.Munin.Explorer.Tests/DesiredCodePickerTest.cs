using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The code picker on «Ønskede data» as a tree (Fhi.Metadata-9huq2.14), against a stubbed API.</summary>
public class DesiredCodePickerTest : ExplorerTestContext
{
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");

    private static readonly KodeverkLink Sivilstand =
        new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", DisplayName = "Sivilstand", HasCodeValues = true };

    private static readonly KodeverkLink Icd10 =
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", DisplayName = "ICD-10", HasCodeValues = true };

    private static KodeverkCodeHit Hit(string code, string name, int? count = null, bool group = false, bool expired = false,
        DateTimeOffset? validTo = null, params string[] above) =>
        new()
        {
            Value = code,
            Name = name,
            HasChildren = count is not null,
            Count = count,
            IsGroup = group,
            Expired = expired,
            ValidTo = validTo,
            Ancestors = above,
        };

    // ICD-10 in small: a chapter, two blocks, codes with subcodes. Sivilstand is a flat list of three.
    private static readonly Dictionary<string, KodeverkCodeHit[]> Icd10Tree = new()
    {
        [""] = [Hit("IV", "Endokrine sykdommer", 15, group: true)],
        ["IV"] = [Hit("E10-E14", "Diabetes mellitus", 10, true, false, null, "IV"), Hit("E15-E16", "Annen glukoseregulering", 5, true, false, null, "IV")],
        ["E10-E14"] =
        [
            Hit("E10", "Diabetes mellitus type 1", 3, false, false, null, "IV", "E10-E14"),
            Hit("E11", "Diabetes mellitus type 2", 3, false, false, null, "IV", "E10-E14"),
            Hit("E14", "Uspesifisert diabetes", null, false, false, null, "IV", "E10-E14"),
        ],
        ["E10"] =
        [
            Hit("E100", "Med koma", null, false, false, null, "IV", "E10-E14", "E10"),
            Hit("E101", "Med ketoacidose", null, false, false, null, "IV", "E10-E14", "E10"),
        ],
        ["E11"] = [Hit("E110", "Med koma", null, false, false, null, "IV", "E10-E14", "E11")],
    };

    private static readonly Dictionary<string, KodeverkCodeHit[]> MaritalStatusList = new()
    {
        [""] =
        [
            Hit("1", "Ugift"),
            Hit("2", "Gift", validTo: new DateTimeOffset(2019, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            Hit("9", "Ukjent", expired: true, validTo: new DateTimeOffset(2019, 12, 31, 0, 0, 0, TimeSpan.Zero)),
        ],
    };

    private sealed class TreeClient : EmptyMuninExplorerClient
    {
        public List<string> Asked { get; } = [];
        public List<int> Takes { get; } = [];
        public int WholeListReads { get; private set; }
        public List<IReadOnlyList<DesiredCodeChoice>> Unions { get; } = [];
        public HashSet<string> Failing { get; } = [];
        public HashSet<string> Missing { get; } = [];
        public Dictionary<string, TaskCompletionSource> Held { get; } = [];
        public Dictionary<string, KodeverkCodeHit[]>? Tree { get; init; }
        public Func<string, int, int, KodeverkCodePage>? Search { get; init; }
        public TaskCompletionSource? SearchHeld { get; set; }
        public bool SearchFails { get; set; }
        public Func<IReadOnlyList<DesiredCodeChoice>, Task<int?>>? Union { get; init; }
        public Func<DesiredCodeChoice, Task<int?>>? Count { get; init; }
        public List<DesiredCodeChoice> Counted { get; } = [];
        public List<string> Searched { get; } = [];

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, CancellationToken cancellationToken = default)
        {
            WholeListReads++;
            return Task.FromResult<KodeverkCodes?>(null);
        }

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            Asked.Add(node ?? "");
            Takes.Add(take);
            if (Failing.Contains(node ?? ""))
            {
                return Task.FromException<KodeverkCodePage?>(new HttpRequestException("down"));
            }

            if (Missing.Contains(node ?? ""))
            {
                return Task.FromResult<KodeverkCodePage?>(null);
            }

            var health = kodeverkType == "HelsefagligKodeverk";
            var codes = (Tree ?? (health ? Icd10Tree : MaritalStatusList)).GetValueOrDefault(node ?? "") ?? [];
            var page = new KodeverkCodePage
            {
                Total = codes.Length,
                CodeCount = health ? 22141 : codes.Length,
                Codes = [.. codes.Skip(skip).Take(take)],
            };
            return Held.TryGetValue(node ?? "", out var gate)
                ? gate.Task.ContinueWith(_ => (KodeverkCodePage?)page, TaskScheduler.Default)
                : Task.FromResult<KodeverkCodePage?>(page);
        }

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            Searched.Add($"{query}:{take}");
            if (SearchFails)
            {
                return Task.FromException<KodeverkCodePage?>(new HttpRequestException("down"));
            }

            var page = Search?.Invoke(query, skip, take) ?? new KodeverkCodePage
            {
                Total = 3,
                Codes = [Icd10Tree["IV"][0], Icd10Tree["E10-E14"][0], Icd10Tree["E10-E14"][1]],
            };
            return SearchHeld is { } held
                ? held.Task.ContinueWith(_ => (KodeverkCodePage?)page, TaskScheduler.Default)
                : Task.FromResult<KodeverkCodePage?>(page);
        }

        // A block counts what the tree says is under it; E10-E14's ten codes in small.
        public override Task<int?> CountKodeverkCodesAsync(
            Guid variableId, DesiredCodeChoice choice, CancellationToken cancellationToken = default)
        {
            Counted.Add(choice);
            return Count?.Invoke(choice)
                ?? Task.FromResult(Icd10Tree.Values.SelectMany(level => level).FirstOrDefault(hit => hit.Value == choice.Code)?.Count);
        }

        public override Task<int?> CountKodeverkCodesUnionAsync(
            Guid variableId, IReadOnlyList<DesiredCodeChoice> choices, CancellationToken cancellationToken = default)
        {
            Unions.Add(choices);
            return Union?.Invoke(choices) ?? Task.FromResult<int?>(choices.Count * 7);
        }
    }

    private sealed class Saves
    {
        public List<IReadOnlyList<DesiredCodeView>> Calls { get; } = [];
        public DesiredDataResult Answer { get; set; } = new(DesiredDataOutcome.Saved);

        public Task<DesiredDataResult> SaveAsync(IReadOnlyList<DesiredCodeView> views)
        {
            Calls.Add(views);
            return Task.FromResult(Answer);
        }
    }

    private IRenderedComponent<DesiredCodePicker> Picker(
        Saves saves, IReadOnlyList<KodeverkLink> links, IReadOnlyList<DesiredCodeView>? chosen = null, Action? closed = null,
        IMuninExplorerClient? client = null)
    {
        Services.AddSingleton(client ?? new TreeClient());

        // A clock that stands still unless a test moves it, so a typed query never searches on its own mid-test.
        if (!Services.Any(service => service.ServiceType == typeof(TimeProvider)))
        {
            Services.AddSingleton<TimeProvider>(new ManualClock());
        }

        return Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, links)
            .Add(c => c.Chosen, chosen ?? [])
            .Add(c => c.Save, saves.SaveAsync)
            .Add(c => c.Closed, () => closed?.Invoke()));
    }

    private static DesiredCodeView Saved(string kind, string code, string? to = null, int? count = null, params string[] above) =>
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = kind, Code = code, To = to, Count = count, Ancestors = above };

    private static string Text(IRenderedComponent<DesiredCodePicker> cut) =>
        System.Text.RegularExpressions.Regex.Replace(cut.Find("[role=group]").TextContent, @"\s+", " ");

    private static void Press(IRenderedComponent<DesiredCodePicker> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static IElement Tree(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-labelledby]");

    private static IElement? RowOf(IElement tree, string code) =>
        tree.QuerySelectorAll("[role=treeitem]").FirstOrDefault(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent == code);

    private static IElement Row(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        RowOf(Tree(cut), code) ?? throw new InvalidOperationException($"{code} is not a row in the tree");

    private static void Tick(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    private static void Open(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        Row(cut, code).QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

    private static string? Checked(IRenderedComponent<DesiredCodePicker> cut, string code) => Row(cut, code).GetAttribute("aria-checked");

    private static string Line(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        System.Text.RegularExpressions.Regex.Replace(Row(cut, code).QuerySelector(".munin-explorer-codetree__row")!.TextContent, @"\s+", " ").Trim();

    private static void Key(IRenderedComponent<DesiredCodePicker> cut, string key) =>
        Tree(cut).KeyDown(new KeyboardEventArgs { Key = key });

    private static string? TabStop(IRenderedComponent<DesiredCodePicker> cut) =>
        Tree(cut).QuerySelectorAll("[role=treeitem][tabindex='0']").Select(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent).SingleOrDefault();

    // ------------------------------------------------------------------------------------- small and large lists

    [Fact]
    public void Save_WhenTwoCodesOfASmallListAreTicked_ThenBothAreSentAsCodesAndThePickerCloses()
    {
        var saves = new Saves();
        var closed = false;
        var cut = Picker(saves, [Sivilstand], closed: () => closed = true);

        Tick(cut, "1");
        Tick(cut, "2");
        cut.WaitForAssertion(() => Assert.Contains("2 av 200 valg", cut.Markup));
        Press(cut, "Lagre");

        var sent = Assert.Single(saves.Calls);
        Assert.Equal(["1", "2"], sent.Select(v => v.Code));
        Assert.All(sent, v => Assert.Equal((DesiredCodeKinds.Code, "Kildekodeverk", "2336"), (v.Kind, v.KodeverkType, v.KodeverkReference)));
        Assert.True(closed);
    }

    [Fact]
    public void Open_WhenTheListIsSmall_ThenItIsTheTreeAloneAndTheWholeListIsNeverFetched()
    {
        var client = new TreeClient();
        var cut = Picker(new Saves(), [Sivilstand], client: client);

        Assert.Empty(cut.FindAll("input[type=search]"));
        Assert.Equal(3, Tree(cut).QuerySelectorAll("[role=treeitem]").Length);
        Assert.Equal(0, client.WholeListReads);
    }

    [Fact]
    public void Open_WhenTheListHasMoreThan200Codes_ThenTheSearchComesFirstWithTheFirstLevelBelowAndNothingIsFetchedWhole()
    {
        var client = new TreeClient();
        var cut = Picker(new Saves(), [Icd10], client: client);

        var order = cut.FindAll("input[type=search], ul[role=tree]").Select(e => e.TagName).ToList();
        Assert.Equal(["INPUT", "UL"], order);
        Assert.Equal(["IV"], Tree(cut).QuerySelectorAll(":scope > [role=treeitem]").Select(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent));
        Assert.Equal(0, client.WholeListReads);
        Assert.All(client.Takes, take => Assert.True(take <= 100, "the API refuses a take above 100"));
    }

    [Fact]
    public void Open_WhenALevelIsLongerThanOnePage_ThenItIsReadWholeAndThereIsNoShowMore()
    {
        var client = new LongLevelClient(150);
        var cut = Picker(new Saves(), [Sivilstand], client: client);

        Assert.Equal(150, Tree(cut).QuerySelectorAll("[role=treeitem]").Length);
        Assert.Equal([0, 100], client.Skips);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Vis flere", StringComparison.Ordinal));
    }

    [Fact]
    public void Open_WhenAFlatLevelIsLongerThan200_ThenTheFirst200ShowAndTheReaderIsToldToSearch()
    {
        var client = new LongLevelClient(1830);
        var cut = Picker(new Saves(), [Sivilstand], client: client);

        Assert.Equal(200, Tree(cut).QuerySelectorAll("[role=treeitem]").Length);
        Assert.Contains("Viser 200 av 1830. Søk for å finne resten.", Text(cut));
        Assert.NotNull(cut.Find("input[type=search]"));
    }

    private sealed class LongLevelClient(int total) : EmptyMuninExplorerClient
    {
        public List<int> Skips { get; } = [];

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            Skips.Add(skip);
            return Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage
            {
                Total = total,
                CodeCount = total,
                Codes = [.. Enumerable.Range(skip, Math.Max(0, Math.Min(take, total - skip))).Select(i => Hit($"K{i}", $"Kode {i}"))],
            });
        }
    }

    // ------------------------------------------------------------------------------------------- «utgått»

    [Fact]
    public void Render_WhenTheApiSaysAllUnderItHasExpired_ThenItIsMarkedAndAnEndDateAloneIsNot()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Sivilstand]);

        Assert.Contains("Ukjent (utgått 2019)", Line(cut, "9"));
        Assert.DoesNotContain("utgått", Line(cut, "2"));
        Tick(cut, "9");
        Press(cut, "Lagre");

        Assert.True(Assert.Single(Assert.Single(saves.Calls)).Expired);
    }

    // ------------------------------------------------------------------------------------------ three states

    [Fact]
    public void Tick_WhenACodeUnderOpenedParentsIsTicked_ThenEachParentIsHalfTickedWithHowManyOfItsCodes()
    {
        var cut = Picker(new Saves(), [Icd10]);
        Open(cut, "IV");
        Open(cut, "E10-E14");
        Open(cut, "E10");

        Tick(cut, "E100");

        Assert.Equal("true", Checked(cut, "E100"));
        Assert.Equal(["mixed", "mixed", "mixed"], new[] { "E10", "E10-E14", "IV" }.Select(code => Checked(cut, code)));
        Assert.EndsWith("1 av 3", Line(cut, "E10"));
        Assert.Equal("false", Checked(cut, "E101"));
    }

    [Fact]
    public void Render_WhenASavedCodeNamesItsParents_ThenTheTopLevelIsHalfTickedWithoutItsBranchOpened()
    {
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Code, "E110", null, 1, "IV", "E10-E14", "E11")]);

        Assert.Equal("mixed", Checked(cut, "IV"));
        Assert.Equal("false", Row(cut, "IV").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Render_WhenAParentIsChosenWhole_ThenItAndEverythingOpenedUnderItIsTicked()
    {
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Node, "IV", null, 15)]);
        Open(cut, "IV");
        Open(cut, "E10-E14");

        Assert.Equal(["true", "true", "true"], new[] { "IV", "E10-E14", "E14" }.Select(code => Checked(cut, code)));
        Assert.EndsWith("15 av 15", Line(cut, "IV"));
    }

    [Fact]
    public void Tick_WhenAHalfTickedParentIsTicked_ThenItIsOneChoiceWithEverythingUnderItAndThePicksUnderItGo()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10]);
        Open(cut, "IV");
        Open(cut, "E10-E14");
        Tick(cut, "E14");
        Open(cut, "E10");
        Tick(cut, "E100");
        Assert.Equal("mixed", Checked(cut, "E10-E14"));

        Tick(cut, "E10-E14");
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Node, "E10-E14", 10), (sent.Kind, sent.Code, sent.Count));
        Assert.Equal(["IV"], sent.Ancestors);
    }

    [Fact]
    public async Task Untick_WhenACodeUnderAParentChosenWholeIsUnticked_ThenThatParentBecomesItsOtherChildrenAndTheCodesOnTheWay()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "E10-E14", null, 10, "IV")]);
        Open(cut, "IV");
        Open(cut, "E10-E14");
        Open(cut, "E10");

        Tick(cut, "E100");
        await cut.InvokeAsync(() => { });

        // E10 is a code of its own, which the block covered: it stays chosen, so its box is half-ticked.
        Assert.Equal("false", Checked(cut, "E100"));
        Assert.Equal("mixed", Checked(cut, "E10"));
        Press(cut, "Lagre");
        var sent = Assert.Single(saves.Calls);
        Assert.Equal(
            [(DesiredCodeKinds.Code, "E10"), (DesiredCodeKinds.Code, "E101"), (DesiredCodeKinds.Node, "E11"), (DesiredCodeKinds.Code, "E14")],
            sent.Select(v => (v.Kind, v.Code)).OrderBy(p => p.Code));
    }

    [Fact]
    public void Tick_WhenTheCodeIsAGroupWithNothingListedUnderIt_ThenItIsStillChosenWithEverythingUnderIt()
    {
        var saves = new Saves();
        var client = new TreeClient
        {
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Hit("U00-U49", "Spesielle formål", group: true)] },
        };
        var cut = Picker(saves, [Icd10], client: client);
        cut.Find("input[type=search]").Input("U00");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.Find("ul[role=tree][aria-label] .munin-explorer-codetree__row").Click(new MouseEventArgs { Detail = 1 });
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Node, "U00-U49"), (sent.Kind, sent.Code));
    }

    // ------------------------------------------------------------------------------------------- saved ranges

    [Fact]
    public void Render_WhenASavedRangeHoldsTheSameCodesAsItsBlock_ThenTheBlockIsTickedAndItIsSavedAsThatBlock()
    {
        var saves = new Saves();
        var range = Saved(DesiredCodeKinds.Range, "E10", "E14", 10, "IV", "E10-E14") with { Expired = true };
        var client = new TreeClient();
        var cut = Picker(saves, [Icd10], [range], client: client);
        Open(cut, "IV");

        cut.WaitForAssertion(() => Assert.Equal("true", Checked(cut, "E10-E14")));
        Assert.Equal([new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Node, "E10-E14")], client.Counted);
        Assert.DoesNotContain("eldre valg", Text(cut));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.StartsWith("Fjern ", StringComparison.Ordinal));
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Node, "E10-E14", (string?)null, false, 10), (sent.Kind, sent.Code, sent.To, sent.Expired, sent.Count));
        Assert.Equal(["IV"], sent.Ancestors);
    }

    // E10..E14 stops at E14 and leaves its subcodes out, so where E14 has any the block holds more than the range.
    [Theory]
    [InlineData("unequal")]
    [InlineData("unknown")]
    [InlineData("fails")]
    public void Render_WhenASavedRangeIsNotKnownToHoldTheSameCodesAsItsBlock_ThenItStaysTheRangeItWasSavedAs(string answer)
    {
        var saves = new Saves();
        var client = new TreeClient
        {
            Count = _ => answer switch
            {
                "unequal" => Task.FromResult<int?>(12),
                "unknown" => Task.FromResult<int?>(null),
                _ => Task.FromException<int?>(new HttpRequestException("down")),
            },
        };
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Range, "E10", "E14", 10, "IV", "E10-E14")], client: client);
        Open(cut, "IV");

        cut.WaitForAssertion(() => Assert.Single(client.Counted));
        Assert.Equal("mixed", Checked(cut, "E10-E14"));
        Assert.Contains("E10–E14 (eldre valg)", Text(cut));
        Assert.Single(cut.FindAll("button"), b => b.TextContent.Trim() == "Fjern E10–E14");
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Range, "E10", "E14", 10), (sent.Kind, sent.Code, sent.To, sent.Count));
    }

    [Fact]
    public void Remove_WhenASavedRangeIsNoBlock_ThenItIsAnEarlierChoiceWithTheOnlyRemoveButton()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10],
        [
            Saved(DesiredCodeKinds.Range, "E10", "E12", 6, "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E14", null, 1, "IV", "E10-E14"),
        ]);

        Assert.Contains("E10–E12 (eldre valg)", Text(cut));
        var remove = Assert.Single(cut.FindAll("button"), b => b.TextContent.StartsWith("Fjern ", StringComparison.Ordinal));
        Assert.Equal("Fjern E10–E12", remove.TextContent.Trim());
        remove.Click();
        Press(cut, "Lagre");

        Assert.Equal(["E14"], Assert.Single(saves.Calls).Select(v => v.Code));
    }

    // ------------------------------------------------------------------------------------------- the summary

    [Fact]
    public void Summary_WhenACodeIsChosen_ThenItIsNamedReadOnlyWithTheUnionCountFromTheApi()
    {
        var client = new TreeClient();
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Code, "E14", null, 1, "IV", "E10-E14")], client: client);
        Open(cut, "IV");
        Open(cut, "E10-E14");

        Tick(cut, "E11");

        cut.WaitForAssertion(() => Assert.Contains("Valgt: 14 koder", Text(cut)));
        Assert.Equal(["E14", "E11"], client.Unions[^1].Select(c => c.Code));
        var summary = cut.Find("ul.munin-explorer-data-list");
        Assert.Contains("E11 Diabetes mellitus type 2, med alt under (3)", System.Text.RegularExpressions.Regex.Replace(summary.TextContent, @"\s+", " "));
        Assert.Empty(summary.QuerySelectorAll("input"));
    }

    [Fact]
    public void Summary_WhenItsCodeIsPressed_ThenTheTreeOpensToItAndFocusGoesToItsRow()
    {
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Code, "E110", null, 1, "IV", "E10-E14", "E11")]);
        Assert.Null(RowOf(Tree(cut), "E110"));

        cut.Find("button[aria-label='Vis E110 i treet']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(RowOf(Tree(cut), "E110")));
        Assert.Equal("E110", TabStop(cut));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    // --------------------------------------------------------------------------------------------- keyboard

    [Fact]
    public void Keys_WhenTheTreeIsWalked_ThenTheArrowsMoveOpenAndCloseAndSpaceTicks()
    {
        var cut = Picker(new Saves(), [Icd10]);
        Assert.Equal("IV", TabStop(cut));

        Key(cut, "ArrowRight");
        Assert.Equal("true", Row(cut, "IV").GetAttribute("aria-expanded"));
        Key(cut, "ArrowRight");
        Assert.Equal("E10-E14", TabStop(cut));
        Key(cut, "ArrowDown");
        Assert.Equal("E15-E16", TabStop(cut));
        Key(cut, "Home");
        Assert.Equal("IV", TabStop(cut));
        Key(cut, "End");
        Assert.Equal("E15-E16", TabStop(cut));
        Key(cut, "ArrowLeft");
        Assert.Equal("IV", TabStop(cut));
        Key(cut, " ");
        Assert.Equal("true", Checked(cut, "IV"));
        Key(cut, "ArrowLeft");
        Assert.Equal("false", Row(cut, "IV").GetAttribute("aria-expanded"));

        Assert.Equal("1", Row(cut, "IV").GetAttribute("aria-level"));
        Assert.NotEmpty(JSInterop.Invocations["Blazor._internal.domWrapper.focus"]);
    }

    [Fact]
    public void Press_WhenARowOrItsArrowGetsADoubleClicksSecondClickOrAShiftClick_ThenNothingMoves()
    {
        var cut = Picker(new Saves(), [Icd10]);

        Row(cut, "IV").QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 2 });
        Row(cut, "IV").QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1, ShiftKey = true });

        Assert.Equal(("false", "false"), (Row(cut, "IV").GetAttribute("aria-expanded"), Checked(cut, "IV")));
    }

    // ---------------------------------------------------------------------------------------------- failures

    [Fact]
    public void Open_WhenABranchCannotBeRead_ThenThatBranchAloneOffersARetryWhichReadsIt()
    {
        var client = new TreeClient();
        client.Failing.Add("E15-E16");
        var cut = Picker(new Saves(), [Icd10], client: client);
        Open(cut, "IV");
        Open(cut, "E10-E14");
        Open(cut, "E15-E16");

        var retry = Assert.Single(cut.FindAll("button"), b => b.TextContent.Trim() == "Prøv igjen");
        Assert.True(Row(cut, "E15-E16").Contains(retry));
        Assert.NotNull(RowOf(Tree(cut), "E10"));

        client.Failing.Clear();
        retry.Click();
        cut.WaitForAssertion(() => Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Prøv igjen"));
        Assert.Equal(2, client.Asked.Count(node => node == "E15-E16"));
    }

    [Fact]
    public void Open_WhenTheCodeListTimesOut_ThenTheReaderIsToldAndCanTryAgain()
    {
        var client = new TreeClient();
        client.Failing.Add("");
        var cut = Picker(new Saves(), [Icd10], client: client);

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente kodelisten nå.", Text(cut)));
        client.Failing.Clear();
        Press(cut, "Prøv igjen");
        cut.WaitForAssertion(() => Assert.NotNull(RowOf(Tree(cut), "IV")));
    }

    // ------------------------------------------------------------------------------------------------ search

    [Fact]
    public void Search_WhenTheReaderStopsTyping_ThenItSearchesAfterThePauseAndNotBefore()
    {
        var clock = new ManualClock();
        Services.AddSingleton<TimeProvider>(clock);
        var client = new TreeClient();
        var cut = Picker(new Saves(), [Icd10], client: client);

        cut.Find("input[type=search]").Input("diab");
        clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Empty(client.Searched);
        cut.Find("input[type=search]").Input("diabetes");
        clock.Advance(TimeSpan.FromMilliseconds(300));

        cut.WaitForAssertion(() => Assert.Equal(["diabetes:100"], client.Searched));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("ul[role=tree][aria-label] > [role=treeitem]").Count));
    }

    [Fact]
    public void Search_WhenThereAreMoreHitsThanShown_ThenTheReaderIsToldAndChoosingTheHitsTakesOnlyThose()
    {
        var saves = new Saves();
        var client = new TreeClient
        {
            Search = (query, skip, take) => new KodeverkCodePage
            {
                Total = 250,
                Codes = [.. Enumerable.Range(0, take).Select(i => Hit($"X{i:000}", "Treff"))],
            },
        };
        var cut = Picker(saves, [Icd10], client: client);
        cut.Find("input[type=search]").Input("x");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Contains("Viser 100 av 250. Søk smalere.", Text(cut)));
        Press(cut, "Velg treffene");
        Assert.Contains("100 av 200 valg", Text(cut));
        Press(cut, "Fjern treffene");
        Assert.Contains("0 av 200 valg", Text(cut));
    }

    [Fact]
    public void Search_WhenAHitsParentIsChosen_ThenTheHitShowsTickedAndChoosingTheHitsLeavesIt()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "E10-E14", null, 10, "IV")]);
        cut.Find("input[type=search]").Input("diabetes");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var hits = cut.Find("ul[role=tree][aria-label]");
        Assert.Equal("true", RowOf(hits, "E11")!.GetAttribute("aria-checked"));
        Press(cut, "Velg treffene");
        Press(cut, "Lagre");

        Assert.Equal(["E10-E14"], Assert.Single(saves.Calls).Select(v => v.Code));
    }

    [Fact]
    public void Search_WhenClearedWhileAnAnswerIsOut_ThenTheLateAnswerDoesNotComeBack()
    {
        var client = new PendingSearchClient();
        var cut = Picker(new Saves(), [Icd10], client: client);
        cut.Find("input[type=search]").Input("diabetes");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.Find("input[type=search]").Input("");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        client.Search.SetResult(new KodeverkCodePage { Total = 1, Codes = [Hit("E10", "Diabetes")] });

        cut.WaitForAssertion(() => Assert.DoesNotContain("1 treff", Text(cut)));
        Assert.Empty(cut.FindAll("ul[role=tree][aria-label]"));
    }

    private sealed class PendingSearchClient : EmptyMuninExplorerClient
    {
        public TaskCompletionSource<KodeverkCodePage?> Search { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage { Total = 1, CodeCount = 22141, Codes = [Hit("IV", "Endokrine", 15, true)] });

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) => Search.Task;
    }

    // ------------------------------------------------------------------------------------------------ saving

    [Fact]
    public void Tick_WhenTheTwoHundredthChoiceIsMade_ThenItIsTheLastOneAllowed()
    {
        var chosen = Enumerable.Range(0, 199)
            .Select(i => new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "kode", Code = $"X{i}" })
            .ToList();
        var cut = Picker(new Saves(), [Sivilstand], chosen);

        Tick(cut, "1");
        Assert.Contains("200 av 200 valg", cut.Markup);
        Assert.Contains("Du har valgt 200, som er det meste én variabel kan ha.", cut.Markup);

        Tick(cut, "2");
        Assert.Equal("false", Checked(cut, "2"));
        Assert.Contains("Du har valgt 200", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void Save_WhenAChoiceIsRefused_ThenTheReaderIsToldWhichOneAndThePickerStaysOpen()
    {
        var saves = new Saves { Answer = new DesiredDataResult(DesiredDataOutcome.Refused, InvalidIndex: 1) };
        var closed = false;
        var cut = Picker(saves, [Sivilstand], closed: () => closed = true);

        Tick(cut, "1");
        Tick(cut, "2");
        Press(cut, "Lagre");

        cut.WaitForAssertion(() => Assert.Contains("2 Gift finnes ikke i kodelisten.", cut.Find("[role=alert]").TextContent));
        Assert.False(closed);
    }

    [Fact]
    public void Save_WhenTheListCannotBeRead_ThenTheReaderIsToldNothingWasStored()
    {
        var saves = new Saves { Answer = new DesiredDataResult(DesiredDataOutcome.ListUnavailable) };
        var cut = Picker(saves, [Sivilstand]);

        Tick(cut, "1");
        Press(cut, "Lagre");

        cut.WaitForAssertion(() => Assert.Contains("valgene er ikke lagret", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public void Save_WhenTheSignInIsRefused_ThenTheReaderIsToldToSignInAndThePickerStays()
    {
        var closed = false;
        Services.AddSingleton<IMuninExplorerClient>(new TreeClient());
        var cut = Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Sivilstand])
            .Add(c => c.Save, _ => throw new MuninExplorerUnauthorizedException())
            .Add(c => c.Closed, () => closed = true));

        Press(cut, "Lagre");

        cut.WaitForAssertion(() => Assert.False(string.IsNullOrWhiteSpace(cut.Find("[role=alert]").TextContent)));
        Assert.False(closed);
    }

    [Fact]
    public void Tick_WhileASaveIsOut_ThenTheDraftHoldsAndARefusalNamesTheChoiceThatWasSent()
    {
        var pending = new TaskCompletionSource<DesiredDataResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Services.AddSingleton<IMuninExplorerClient>(new TreeClient());
        var cut = Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Sivilstand])
            .Add(c => c.Save, _ => pending.Task));
        Tick(cut, "1");
        Tick(cut, "2");
        Press(cut, "Lagre");

        Tick(cut, "1");
        Assert.All(Tree(cut).QuerySelectorAll("[role=treeitem]"), row => Assert.Equal("true", row.GetAttribute("aria-disabled")));
        pending.SetResult(new DesiredDataResult(DesiredDataOutcome.Refused, InvalidIndex: 1));

        cut.WaitForAssertion(() => Assert.Contains("2 Gift finnes ikke i kodelisten.", cut.Find("[role=alert]").TextContent));
        Assert.Contains("2 av 200 valg", Text(cut));
    }

    [Fact]
    public void Save_WhenChoicesAreKeptOrAdded_ThenTheirCodeListKeepsItsName()
    {
        var saves = new Saves();
        var stored = new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", KodeverkName = "Sivilstand", Kind = "kode", Code = "1", Name = "Ugift" };
        var cut = Picker(saves, [Sivilstand], [stored]);

        Tick(cut, "2");
        Press(cut, "Lagre");

        Assert.All(Assert.Single(saves.Calls), view => Assert.Equal("Sivilstand", view.KodeverkName));
    }

    // -------------------------------------------------------------------------------------------------- tabs

    [Fact]
    public void Tabs_WhenTwoCodeListsAreShown_ThenEachHasATabAndTheArrowKeySelectsAndFocusesTheNext()
    {
        var cut = Picker(new Saves(), [Sivilstand, Icd10]);

        var tabs = cut.FindAll("[role=tab]");
        Assert.Equal(["Sivilstand", "ICD-10"], tabs.Select(t => t.TextContent.Trim()));
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.WaitForAssertion(() => Assert.Equal("true", cut.FindAll("[role=tab]")[1].GetAttribute("aria-selected")));
        Assert.NotNull(cut.Find("input[type=search]"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void Tabs_WhenOneCodeListIsNamedTwice_ThenItHasOneTabAndNoTablist()
    {
        var cut = Picker(new Saves(), [Sivilstand, Sivilstand]);

        Assert.Empty(cut.FindAll("[role=tablist]"));
        Assert.Contains("Ugift", Text(cut));
    }

    // ------------------------------------------------------------------------------------ paging and failures

    private static IElement HitsTree(IRenderedComponent<DesiredCodePicker> cut) => cut.Find("ul[role=tree][aria-label]");

    private static void SearchFor(IRenderedComponent<DesiredCodePicker> cut, string query)
    {
        cut.Find("input[type=search]").Input(query);
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    private static void TickHit(IRenderedComponent<DesiredCodePicker> cut, string code) =>
        RowOf(HitsTree(cut), code)!.QuerySelector(".munin-explorer-codetree__row")!.Click(new MouseEventArgs { Detail = 1 });

    // An API that pages wrongly: the same hundred codes on every page, or one new code a page, of a claimed thousand.
    private sealed class WrongPagingClient(bool onePerPage) : EmptyMuninExplorerClient
    {
        public int Reads { get; private set; }

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            if (++Reads > 10)
            {
                return Task.FromException<KodeverkCodePage?>(new InvalidOperationException("the level is read without end"));
            }

            KodeverkCodeHit[] codes = onePerPage ? [Hit($"K{skip}", "Kode")] : [.. Enumerable.Range(0, 100).Select(i => Hit($"K{i}", "Kode"))];
            return Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage { Total = 1000, CodeCount = 1000, Codes = codes });
        }
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void Open_WhenTheApiPagesWrongly_ThenTheLevelStopsAtAPageWithNothingNewOrAtThePageCap(bool onePerPage, int reads)
    {
        var client = new WrongPagingClient(onePerPage);
        var cut = Picker(new Saves(), [Icd10], client: client);

        cut.WaitForAssertion(() => Assert.NotNull(RowOf(Tree(cut), "K0")));
        Assert.Equal(reads, client.Reads);
    }

    [Fact]
    public void Open_WhenTheApiDoesNotKnowABranch_ThenThatBranchOffersARetryNamingItsCodeAndIsNotShownEmpty()
    {
        var client = new TreeClient();
        client.Missing.Add("E15-E16");
        var cut = Picker(new Saves(), [Icd10], client: client);
        Open(cut, "IV");
        Open(cut, "E15-E16");

        var retry = cut.Find("button[aria-label='Prøv igjen for E15-E16']");
        Assert.Equal("Prøv igjen", retry.TextContent.Trim());
        Assert.True(Row(cut, "E15-E16").Contains(retry));
    }

    [Fact]
    public void Keys_WhenSpaceOrAnArrowIsPressedOnABranchsRetry_ThenNothingIsTickedAndNothingMoves()
    {
        var client = new TreeClient();
        client.Failing.Add("E15-E16");
        var cut = Picker(new Saves(), [Icd10], client: client);
        Open(cut, "IV");
        Open(cut, "E15-E16");
        Assert.Equal("E15-E16", TabStop(cut));

        // bUnit says so when a key reaches no handler: the tree's is not on the way up from the button.
        foreach (var key in new[] { " ", "ArrowUp", "ArrowDown" })
        {
            Assert.Throws<MissingEventHandlerException>(() =>
                cut.Find("button[aria-label='Prøv igjen for E15-E16']").KeyDown(new KeyboardEventArgs { Key = key }));
        }

        Assert.Equal("E15-E16", TabStop(cut));
        Assert.Equal("false", Checked(cut, "E15-E16"));
        Assert.DoesNotContain("1 av 200 valg", Text(cut));
    }

    // ------------------------------------------------------------------------------------- hits and old ranges

    [Fact]
    public void Search_WhenAnOpenedHitListsCodesThatAreHitsToo_ThenEveryRowIsItsOwnWithOneTabStopAndTheArrowsVisitEachOnce()
    {
        var client = new TreeClient
        {
            Search = (_, _, _) => new KodeverkCodePage { Total = 3, Codes = [Icd10Tree["IV"][0], Icd10Tree["E10-E14"][0], Icd10Tree["E10-E14"][1]] },
        };
        var cut = Picker(new Saves(), [Icd10], client: client);
        SearchFor(cut, "diabetes");
        RowOf(HitsTree(cut), "E10-E14")!.QuerySelector(".munin-explorer-codetree__toggle")!.Click(new MouseEventArgs { Detail = 1 });

        var rows = HitsTree(cut).QuerySelectorAll("[role=treeitem]");
        Assert.Equal(["E10-E14", "E10", "E11", "E14", "E10", "E11"], rows.Select(r => r.QuerySelector(".munin-explorer-codetree__code")!.TextContent));
        Assert.Equal(rows.Length, rows.Select(r => r.Id).Distinct().Count());
        Assert.Single(rows, r => r.GetAttribute("tabindex") == "0");

        var visited = new List<int>();
        for (var i = 0; i < rows.Length; i++)
        {
            visited.Add(HitsTree(cut).QuerySelectorAll("[role=treeitem]").ToList().FindIndex(r => r.GetAttribute("tabindex") == "0"));
            HitsTree(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        }

        Assert.Equal(Enumerable.Range(0, rows.Length), visited);
    }

    [Fact]
    public void Tick_WhenAnOlderRangeStartsUnderAParentThatIsTickedOrClearedAsAHit_ThenTheRangeStaysChosen()
    {
        var saves = new Saves();
        var client = new TreeClient { Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["IV"][0]] } };
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Range, "E10", "E20", 30, "IV", "E10-E14")], client: client);
        Open(cut, "IV");

        Tick(cut, "E10-E14");
        Assert.Contains("E10–E20 (eldre valg)", Text(cut));
        SearchFor(cut, "diabetes");
        Press(cut, "Fjern treffene");
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Range, "E10", "E20"), (sent.Kind, sent.Code, sent.To));
    }

    [Fact]
    public void Search_WhenTheQueryChangesAndTheNewSearchFails_ThenNothingFromTheOldQueryCanBeChosen()
    {
        var client = new TreeClient();
        var cut = Picker(new Saves(), [Icd10], client: client);
        SearchFor(cut, "diabetes");
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Velg treffene");

        client.SearchFails = true;
        cut.Find("input[type=search]").Input("diabetesx");
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() is "Velg treffene" or "Fjern treffene");
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente kodelisten nå.", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll("ul[role=tree][aria-label]"));
        Assert.Contains("0 av 200 valg", Text(cut));
    }

    [Fact]
    public void Search_WhenTheSameQueryFailsOnASecondTry_ThenTheReaderIsToldAndTheEarlierHitsGo()
    {
        var client = new TreeClient();
        var cut = Picker(new Saves(), [Icd10], client: client);
        SearchFor(cut, "diabetes");
        Assert.NotEmpty(cut.FindAll("ul[role=tree][aria-label]"));

        client.SearchFails = true;
        cut.Find("input[type=search]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente kodelisten nå.", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll("ul[role=tree][aria-label]"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() is "Velg treffene" or "Fjern treffene");
    }

    [Fact]
    public async Task Search_WhenTheAnswerComesAfterThePickerIsGone_ThenNothingThrowsAndNothingRenders()
    {
        var client = new PendingSearchClient();
        var cut = Picker(new Saves(), [Icd10], client: client);
        SearchFor(cut, "diabetes");
        var renders = cut.RenderCount;

        await Renderer.DisposeComponents();
        client.Search.SetResult(new KodeverkCodePage { Total = 1, Codes = [Hit("E10", "Diabetes")] });
        await client.Search.Task;
        await cut.InvokeAsync(() => { });

        Assert.Equal(renders, cut.RenderCount);
    }

    // ------------------------------------------------------------------------------------------ unticking

    [Fact]
    public void Untick_WhenALevelUnderTheWholeParentIsCutAt200_ThenTheTextNamesTheLevelAndTheParentToUntick()
    {
        var big = Enumerable.Range(0, 300).Select(i => Hit($"C{i:000}", "Kode", null, false, false, null, "CH", "BIG")).ToArray();
        var client = new TreeClient
        {
            Tree = new() { [""] = [Hit("CH", "Kapittel", 301, true)], ["CH"] = [Hit("BIG", "Stor", 300, false, false, null, "CH")], ["BIG"] = big },
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [big[5]] },
        };
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Node, "CH", null, 301)], client: client);
        SearchFor(cut, "C005");

        TickHit(cut, "C005");

        cut.WaitForAssertion(() => Assert.Contains("BIG har for mange koder til at én kan tas ut her. Fjern haken for CH og velg", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public void Untick_WhenTheTickedParentsLevelIsCutAt200_ThenTheReaderIsToldWhyAndTheParentStays()
    {
        var big = Enumerable.Range(0, 300).Select(i => Hit($"C{i:000}", "Kode", null, false, false, null, "BIG")).ToArray();
        var client = new TreeClient
        {
            Tree = new() { [""] = [Hit("BIG", "Stor", 300)], ["BIG"] = big },
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [big[5]] },
        };
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "BIG", null, 300)], client: client);
        SearchFor(cut, "C005");

        TickHit(cut, "C005");

        cut.WaitForAssertion(() => Assert.Contains("BIG har for mange koder", cut.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("Kunne ikke hente kodelisten", cut.Find("[role=alert]").TextContent);
        Press(cut, "Lagre");
        Assert.Equal(["BIG"], Assert.Single(saves.Calls).Select(v => v.Code));
    }

    [Fact]
    public void Untick_WhenASecondCodeIsUntickedWhileTheFirstReadsItsLevel_ThenBothAreUnticked()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TreeClient { Search = (_, _, _) => new KodeverkCodePage { Total = 2, Codes = [.. Icd10Tree["E10"]] } };
        client.Held["E10"] = gate;
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "E10", null, 3, "IV", "E10-E14")], client: client);
        SearchFor(cut, "koma");

        TickHit(cut, "E100");
        TickHit(cut, "E101");
        gate.SetResult();

        // E10's own code, which the reader did not untick, stays chosen. «1 av 200» also shows before the presses, so the
        // wait is on the rows.
        cut.WaitForAssertion(() => Assert.Equal(
            "false false", $"{RowOf(HitsTree(cut), "E100")!.GetAttribute("aria-checked")} {RowOf(HitsTree(cut), "E101")!.GetAttribute("aria-checked")}"));
        Assert.Contains("1 av 200 valg", Text(cut));
        Press(cut, "Lagre");
        cut.WaitForAssertion(() => Assert.Equal([(DesiredCodeKinds.Code, "E10")], Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code))));
    }

    [Fact]
    public void Untick_WhenASiblingIsAGroupWithNothingListedUnderIt_ThenThatSiblingStaysChosenWholeNeverAsACode()
    {
        var client = new TreeClient
        {
            Tree = new()
            {
                [""] = [Hit("X", "Kapittel", 3, group: true)],
                ["X"] =
                [
                    Hit("G1", "Gruppe", null, true, false, null, "X"),
                    Hit("C1", "Kode 1", null, false, false, null, "X"),
                    Hit("C2", "Kode 2", null, false, false, null, "X"),
                ],
            },
        };
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "X", null, 3)], client: client);
        Open(cut, "X");

        Tick(cut, "C1");
        Press(cut, "Lagre");

        Assert.Equal(
            [(DesiredCodeKinds.Code, "C2"), (DesiredCodeKinds.Node, "G1")],
            Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code)).OrderBy(p => p.Code));
    }

    [Fact]
    public void Tick_WhenEveryChildOfAParentIsTickedOneByOne_ThenEachStaysItsOwnChoiceAndTheParentIsHalfTicked()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10]);
        Open(cut, "IV");
        Open(cut, "E10-E14");

        Tick(cut, "E10");
        Tick(cut, "E11");
        Tick(cut, "E14");

        Assert.Equal("mixed", Checked(cut, "E10-E14"));
        Assert.Contains("7 av 10", Line(cut, "E10-E14"));
        Press(cut, "Lagre");
        Assert.Equal(["E10", "E11", "E14"], Assert.Single(saves.Calls).Select(v => v.Code));
    }

    [Fact]
    public void Untick_WhenTheWholeParentIsOnATopLevelCutAt200_ThenTheTextNamesTheListAsTheLevel()
    {
        var top = Enumerable.Range(0, 300).Select(i => Hit($"T{i:000}", "Kode")).Append(Hit("Z", "Siste", 2)).ToArray();
        var under = Hit("Z1", "Under", null, false, false, null, "Z");
        var client = new TreeClient
        {
            Tree = new() { [""] = top, ["Z"] = [under, Hit("Z2", "Under", null, false, false, null, "Z")] },
            Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [under] },
        };
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "Z", null, 2)], client: client);
        SearchFor(cut, "Z1");

        TickHit(cut, "Z1");

        cut.WaitForAssertion(() => Assert.Contains(
            "ICD-10 har for mange koder til at én kan tas ut her. Fjern haken for Z og velg", cut.Find("[role=alert]").TextContent));
        Press(cut, "Lagre");
        Assert.Equal([(DesiredCodeKinds.Node, "Z")], Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code)));
    }

    [Fact]
    public void ChooseHits_WhenAHitIsChosenAsJustItsOwnCode_ThenItIsChosenWithEverythingUnderItInPlaceOfThatCode()
    {
        var saves = new Saves();
        var client = new TreeClient { Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E10-E14"][1]] } };
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Code, "E11", null, 1, "IV", "E10-E14")], client: client);
        SearchFor(cut, "E11");
        Assert.Equal("mixed", RowOf(HitsTree(cut), "E11")!.GetAttribute("aria-checked"));

        Press(cut, "Velg treffene");
        Press(cut, "Lagre");

        Assert.Equal([(DesiredCodeKinds.Node, "E11")], Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code)));
    }

    // ---------------------------------------------------------------------------------------- the union count

    [Fact]
    public void Summary_WhenTheHostsClientHasNoUnionCount_ThenTheHeadingIsChosenAloneAndNothingThrows()
    {
        // A host client written before the picker: neither the union count nor the tree's levels are implemented.
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Code, "E14", null, 1, "IV", "E10-E14")], client: new UnupgradedHostClient());

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente kodelisten nå.", Text(cut)));
        Assert.Equal("Valgt", cut.Find("p[id$='-chosen']").TextContent.Trim());
        Assert.Contains("E14", cut.Find(".munin-explorer-data-list").TextContent);
        Assert.Empty(cut.FindAll("[role=treeitem]"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Summary_WhenTheUnionFailsOrIsNotFound_ThenTheTotalIsUnknownAndTheHeadingIsChosenAlone(bool fails)
    {
        var client = new TreeClient
        {
            Union = _ => fails ? Task.FromException<int?>(new HttpRequestException("down")) : Task.FromResult<int?>(null),
        };
        var cut = Picker(new Saves(), [Sivilstand], client: client);

        Tick(cut, "1");

        cut.WaitForAssertion(() => Assert.Equal("Valgt", cut.Find("p[id$='-chosen']").TextContent.Trim()));
        Assert.DoesNotContain("Valgt:", Text(cut));
    }

    [Fact]
    public void Summary_WhenAnOlderCountAnswersLast_ThenTheNewestCountStands()
    {
        var first = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var answers = new Queue<TaskCompletionSource<int?>>([first, second]);
        var client = new TreeClient { Union = _ => answers.Dequeue().Task };
        var cut = Picker(new Saves(), [Sivilstand], client: client);
        Tick(cut, "1");
        Tick(cut, "2");

        second.SetResult(2);
        cut.WaitForAssertion(() => Assert.Contains("Valgt: 2 koder", Text(cut)));
        var renders = cut.RenderCount;
        first.SetResult(1);

        cut.WaitForState(() => cut.RenderCount > renders);
        Assert.Contains("Valgt: 2 koder", Text(cut));
    }

    [Fact]
    public void Render_WhenASavedRangeBecomesItsBlock_ThenTheBlockKeepsItsName()
    {
        var saves = new Saves();
        var client = new TreeClient { Search = (query, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["IV"].Single(h => h.Value == query)] } };
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Range, "E10", "E14", 10, "IV", "E10-E14")], client: client);
        Open(cut, "IV");
        cut.WaitForAssertion(() => Assert.Equal("true", Checked(cut, "E10-E14")));

        Assert.Equal(["E10-E14:1"], client.Searched);
        Assert.Contains("Diabetes mellitus", cut.Find(".munin-explorer-data-list").TextContent);
        Press(cut, "Lagre");
        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Node, "E10-E14", "Diabetes mellitus"), (sent.Kind, sent.Code, sent.Name));
    }

    [Fact]
    public void Render_WhenTheBlocksNameCannotBeRead_ThenTheRangeStillBecomesTheBlock()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Range, "E10", "E14", 10, "IV", "E10-E14")], client: new TreeClient { SearchFails = true });
        Open(cut, "IV");
        cut.WaitForAssertion(() => Assert.Equal("true", Checked(cut, "E10-E14")));

        Press(cut, "Lagre");
        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Node, "E10-E14", (string?)null), (sent.Kind, sent.Code, sent.Name));
    }

    [Fact]
    public void Recount_WhenTheNewTotalIsStillAskedFor_ThenTheOldTotalIsNotShown()
    {
        TaskCompletionSource<int?>? held = null;
        var client = new TreeClient { Union = choices => held?.Task ?? Task.FromResult<int?>(choices.Count * 7) };
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Code, "E14", null, 1, "IV", "E10-E14")], client: client);
        cut.WaitForAssertion(() => Assert.Contains("Valgt: 7 koder", Text(cut)));
        Open(cut, "IV");
        Open(cut, "E10-E14");
        held = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Tick(cut, "E11");

        cut.WaitForAssertion(() => Assert.DoesNotContain("Valgt: 7 koder", Text(cut)));
        held.SetResult(14);
        cut.WaitForAssertion(() => Assert.Contains("Valgt: 14 koder", Text(cut)));
    }

    [Fact]
    public void Untick_WhenTheWholeParentHasLeftTheList_ThenItSaysSoAndItCanBeRemoved()
    {
        var saves = new Saves();
        var tree = new Dictionary<string, KodeverkCodeHit[]>(Icd10Tree) { ["E10-E14"] = [Icd10Tree["E10-E14"][1], Icd10Tree["E10-E14"][2]] };
        var client = new TreeClient { Tree = tree, Search = (_, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["E10"][0]] } };
        var cut = Picker(saves, [Icd10], [Saved(DesiredCodeKinds.Node, "E10", null, 3, "IV", "E10-E14")], client: client);
        SearchFor(cut, "E100");

        TickHit(cut, "E100");

        cut.WaitForAssertion(() => Assert.Contains("finnes ikke lenger i kodelisten. Fjern det.", cut.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("Kunne ikke hente kodelisten", cut.Find("[role=alert]").TextContent);
        Assert.Contains(cut.FindAll(".munin-explorer-data-list button"), b => b.TextContent.Trim().StartsWith("Fjern E10", StringComparison.Ordinal));
        Press(cut, "Lagre");
        Assert.Equal([(DesiredCodeKinds.Node, "E10")], Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code)));
    }

    [Fact]
    public void Render_WhenALegacyCodePickOfTheBlockIsSavedBesideAnEqualRange_ThenBothBecomeTheBlock()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10], [
            Saved(DesiredCodeKinds.Range, "E10", "E14", 10, "IV", "E10-E14"),
            Saved(DesiredCodeKinds.Code, "E10-E14", null, null, "IV"),
        ]);
        Open(cut, "IV");
        cut.WaitForAssertion(() => Assert.Single(((TreeClient)Services.GetRequiredService<IMuninExplorerClient>()).Counted));

        Press(cut, "Lagre");
        Assert.Equal([(DesiredCodeKinds.Node, "E10-E14")], Assert.Single(saves.Calls).Select(v => (v.Kind, v.Code)));
        var client = (TreeClient)Services.GetRequiredService<IMuninExplorerClient>();
        Assert.Equal([new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Node, "E10-E14")], client.Unions[^1]);
    }

    [Fact]
    public void Tick_WhenAnotherBlockIsTickedWhileTheBlocksNameIsRead_ThenBothBlocksAreChosen()
    {
        var saves = new Saves();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TreeClient
        {
            SearchHeld = held,
            Search = (query, _, _) => new KodeverkCodePage { Total = 1, Codes = [Icd10Tree["IV"].Single(h => h.Value == query)] },
        };
        var cut = Picker(saves, [Icd10], [
            Saved(DesiredCodeKinds.Code, "E15", null, 1, "IV", "E15-E16"),
            Saved(DesiredCodeKinds.Code, "E16", null, 1, "IV", "E15-E16"),
            Saved(DesiredCodeKinds.Range, "E10", "E14", 10, "IV", "E10-E14"),
        ], client: client);
        Open(cut, "IV");
        cut.WaitForAssertion(() => Assert.Single(client.Searched));

        // Taking in the two codes before the range moves it up a place while its block's name is read.
        Tick(cut, "E15-E16");
        held.SetResult();

        cut.WaitForAssertion(() => Assert.Contains("Diabetes mellitus", cut.Find(".munin-explorer-data-list").TextContent));
        Press(cut, "Lagre");
        cut.WaitForAssertion(() => Assert.Single(saves.Calls));
        Assert.Equal(
            [(DesiredCodeKinds.Node, "E10-E14"), (DesiredCodeKinds.Node, "E15-E16")],
            saves.Calls[0].Select(v => (v.Kind, v.Code)).Order());
    }

    [Fact]
    public void Render_WhenAGroupWithNothingListedUnderItIsSavedAsACode_ThenItIsHalfTicked()
    {
        var client = new TreeClient { Tree = new() { [""] = [Hit("G", "Gruppe", null, group: true)] } };
        var cut = Picker(new Saves(), [Icd10], [Saved(DesiredCodeKinds.Code, "G", null, null)], client: client);

        cut.WaitForAssertion(() => Assert.Equal("mixed", Checked(cut, "G")));
    }
}
