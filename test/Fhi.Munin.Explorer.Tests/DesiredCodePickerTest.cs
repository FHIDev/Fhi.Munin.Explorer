using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The code picker on «Ønskede data» (Fhi.Metadata-emsc0.3.7), against a stubbed API.</summary>
public class DesiredCodePickerTest : ExplorerTestContext
{
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");

    private static readonly KodeverkLink Sivilstand =
        new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", DisplayName = "Sivilstand", HasCodeValues = true };

    private static readonly KodeverkLink Icd10 =
        new() { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", DisplayName = "ICD-10", HasCodeValues = true };

    private sealed class CodesClient : EmptyMuninExplorerClient
    {
        public int Searches { get; private set; }

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodes?>(new KodeverkCodes
            {
                KodeverkType = kodeverkType,
                KodeverkReference = kodeverkReference,
                Codes =
                [
                    new KodeverkCode { Value = "1", Name = "Ugift" },
                    new KodeverkCode { Value = "2", Name = "Gift" },
                    new KodeverkCode { Value = "9", Name = "Ukjent", ValidTo = new DateTimeOffset(2019, 12, 31, 0, 0, 0, TimeSpan.Zero) },
                ],
            });

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodePage?>(node switch
            {
                null => Page(new KodeverkCodeHit { Value = "E00-E90", Name = "Endokrine sykdommer", HasChildren = true }),
                "E00-E90" => Page(new KodeverkCodeHit { Value = "E10", Name = "Diabetes mellitus type 1", HasChildren = true }),
                _ => Page(),
            });

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            Searches++;
            return Task.FromResult<KodeverkCodePage?>(Page(
                new KodeverkCodeHit { Value = "E10", Name = "Diabetes mellitus type 1", HasChildren = true },
                new KodeverkCodeHit { Value = "E11", Name = "Diabetes mellitus type 2", HasChildren = true }));
        }

        public override Task<int?> CountKodeverkCodesAsync(
            Guid variableId, DesiredCodeChoice choice, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(choice.Kind switch
            {
                DesiredCodeKinds.Node => 10,
                DesiredCodeKinds.Range when choice.Code == "E10" && choice.To == "E14" => 50,
                _ => null,
            });

        private static KodeverkCodePage Page(params KodeverkCodeHit[] hits) => new() { Total = hits.Length, Codes = hits };
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
        Saves saves, IReadOnlyList<KodeverkLink> links, IReadOnlyList<DesiredCodeView>? chosen = null, Action? closed = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new CodesClient());
        return Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, links)
            .Add(c => c.Chosen, chosen ?? [])
            .Add(c => c.Save, saves.SaveAsync)
            .Add(c => c.Closed, () => closed?.Invoke()));
    }

    // What a reader sees, with the markup's own whitespace folded: the terms sit in their own lang spans.
    private static string Text(IRenderedComponent<DesiredCodePicker> cut) =>
        System.Text.RegularExpressions.Regex.Replace(cut.Find("[role=group]").TextContent, @"\s+", " ");

    private static void Press(IRenderedComponent<DesiredCodePicker> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static void Tick(IRenderedComponent<DesiredCodePicker> cut, string label) =>
        cut.FindAll("label").First(l => l.TextContent.Contains(label, StringComparison.Ordinal))
            .QuerySelector("input[type=checkbox]")!.Change(true);

    [Fact]
    public void Save_WhenTwoCodesOfASmallListAreTicked_ThenBothAreSentAsCodesAndThePickerCloses()
    {
        var saves = new Saves();
        var closed = false;
        var cut = Picker(saves, [Sivilstand], closed: () => closed = true);

        Tick(cut, "Ugift");
        Tick(cut, "Gift");
        cut.WaitForAssertion(() => Assert.Contains("2 av 200 valg", cut.Markup));
        Press(cut, "Lagre");

        var sent = Assert.Single(saves.Calls);
        Assert.Equal(["1", "2"], sent.Select(v => v.Code));
        Assert.All(sent, v => Assert.Equal((DesiredCodeKinds.Code, "Kildekodeverk", "2336"), (v.Kind, v.KodeverkType, v.KodeverkReference)));
        Assert.True(closed);
    }

    [Fact]
    public void Render_WhenACodeHasExpired_ThenItIsMarkedWithItsYearAndCanStillBeChosen()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Sivilstand]);

        Assert.Contains("Ukjent (utgått 2019)", Text(cut));
        Tick(cut, "Ukjent");
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.True(sent.Expired);
    }

    [Fact]
    public void Search_WhenANodeIsChosenWithEverythingUnderIt_ThenItsCountIsShownAndItIsSentAsANode()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10]);

        cut.Find("input[type=search]").Input("diabetes");
        Press(cut, "Søk");
        cut.WaitForAssertion(() => Assert.Contains("2 treff", cut.Markup));

        Tick(cut, "E11 med alt under");
        cut.WaitForAssertion(() => Assert.Contains("E11 med alt under (10 koder)", Text(cut)));
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Node, "E11", 10), (sent.Kind, sent.Code, sent.Count));
    }

    [Fact]
    public void Tree_WhenANodeIsOpened_ThenItsChildrenShowAndOneCanBeChosen()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10]);

        var open = cut.Find("button[aria-expanded]");
        Assert.Equal("false", open.GetAttribute("aria-expanded"));
        open.Click(new MouseEventArgs { Detail = 1 });

        cut.WaitForAssertion(() => Assert.Contains("Diabetes mellitus type 1", cut.Markup));
        Assert.Equal("true", cut.Find("button[aria-expanded]").GetAttribute("aria-expanded"));
        Tick(cut, "E10 Diabetes");
        Press(cut, "Lagre");

        Assert.Equal((DesiredCodeKinds.Code, "E10"), (Assert.Single(Assert.Single(saves.Calls)).Kind, saves.Calls[0][0].Code));
    }

    [Fact]
    public void Toggle_WhenACodeAndTheSameCodeWithEverythingUnderItAreTicked_ThenOnlyTheLastStands()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10]);
        cut.Find("input[type=search]").Input("E1");
        Press(cut, "Søk");
        cut.WaitForAssertion(() => Assert.Contains("2 treff", cut.Markup));

        Tick(cut, "E10 Diabetes");
        Tick(cut, "E10 med alt under");
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal(DesiredCodeKinds.Node, sent.Kind);
    }

    [Fact]
    public void AddRange_WhenTheListHasIt_ThenItIsAddedWithItsCountAndOneItLacksIsRefusedOnTheSpot()
    {
        var saves = new Saves();
        var cut = Picker(saves, [Icd10]);

        cut.Find($"input[id$='-from']").Input("E10");
        cut.Find($"input[id$='-to']").Input("E99");
        Press(cut, "Legg til intervall");
        cut.WaitForAssertion(() => Assert.Contains("Fant ikke det intervallet i kodelisten.", cut.Markup));

        cut.Find($"input[id$='-to']").Input("E14");
        Press(cut, "Legg til intervall");
        cut.WaitForAssertion(() => Assert.Contains("E10–E14 (50 koder)", Text(cut)));
        Press(cut, "Lagre");

        var sent = Assert.Single(Assert.Single(saves.Calls));
        Assert.Equal((DesiredCodeKinds.Range, "E10", "E14"), (sent.Kind, sent.Code, sent.To));
    }

    [Fact]
    public void Toggle_WhenTheTwoHundredthChoiceIsMade_ThenItIsTheLastOneAllowed()
    {
        var saves = new Saves();
        var chosen = Enumerable.Range(0, 199)
            .Select(i => new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "kode", Code = $"X{i}" })
            .ToList();
        var cut = Picker(saves, [Sivilstand], chosen);

        Tick(cut, "Ugift");

        cut.WaitForAssertion(() => Assert.Contains("200 av 200 valg", cut.Markup));
        Assert.Contains("Du har valgt 200, som er det meste én variabel kan ha.", cut.Markup);
        Assert.True(cut.FindAll("label").First(l => l.TextContent.Contains("Gift ", StringComparison.Ordinal) || l.TextContent.TrimEnd().EndsWith("Gift", StringComparison.Ordinal))
            .QuerySelector("input")!.HasAttribute("disabled"));
    }

    [Fact]
    public void Save_WhenAChoiceIsRefused_ThenTheReaderIsToldWhichOneAndThePickerStaysOpen()
    {
        var saves = new Saves { Answer = new DesiredDataResult(DesiredDataOutcome.Refused, InvalidIndex: 1) };
        var closed = false;
        var cut = Picker(saves, [Sivilstand], closed: () => closed = true);

        Tick(cut, "Ugift");
        Tick(cut, "Gift");
        Press(cut, "Lagre");

        cut.WaitForAssertion(() => Assert.Contains("2 Gift finnes ikke i kodelisten.", cut.Find("[role=alert]").TextContent));
        Assert.False(closed);
    }

    [Fact]
    public void Save_WhenTheListCannotBeRead_ThenTheReaderIsToldNothingWasStored()
    {
        var saves = new Saves { Answer = new DesiredDataResult(DesiredDataOutcome.ListUnavailable) };
        var cut = Picker(saves, [Sivilstand]);

        Tick(cut, "Ugift");
        Press(cut, "Lagre");

        cut.WaitForAssertion(() => Assert.Contains("valgene er ikke lagret", cut.Find("[role=alert]").TextContent));
    }

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
    public void Render_WhenChoicesAreStored_ThenTheyAreTheDraftAndCanBeRemoved()
    {
        var saves = new Saves();
        var chosen = new[] { new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "node", Code = "E10", Count = 10 } };
        var cut = Picker(saves, [Icd10], chosen);

        Assert.Contains("E10 med alt under (10 koder)", Text(cut));
        Press(cut, "Fjern E10 med alt under");
        Press(cut, "Lagre");

        Assert.Empty(Assert.Single(saves.Calls));
    }

    [Fact]
    public void Tabs_WhenOneCodeListIsNamedTwice_ThenItHasOneTabAndNoTablist()
    {
        var cut = Picker(new Saves(), [Sivilstand, Sivilstand]);

        Assert.Empty(cut.FindAll("[role=tablist]"));
        Assert.Contains("Ugift", Text(cut));
    }

    [Fact]
    public void Save_WhenTheSignInIsRefused_ThenTheReaderIsToldToSignInAndThePickerStays()
    {
        var saves = new SignInRefused();
        var closed = false;
        Services.AddSingleton<IMuninExplorerClient>(new CodesClient());
        var cut = Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Sivilstand])
            .Add(c => c.Save, saves.SaveAsync)
            .Add(c => c.Closed, () => closed = true));

        Press(cut, "Lagre");

        cut.WaitForAssertion(() => Assert.False(string.IsNullOrWhiteSpace(cut.Find("[role=alert]").TextContent)));
        Assert.False(closed);
    }

    private sealed class SignInRefused
    {
        public Task<DesiredDataResult> SaveAsync(IReadOnlyList<DesiredCodeView> views) =>
            throw new MuninExplorerUnauthorizedException();
    }

    [Fact]
    public void MoreHits_WhenTheFieldWasChangedSinceTheSearch_ThenThePageIsOfTheSearchShown()
    {
        var client = new PagedClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        var cut = Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Icd10])
            .Add(c => c.Save, new Saves().SaveAsync));

        cut.Find("input[type=search]").Input("diabetes");
        Press(cut, "Søk");
        cut.WaitForAssertion(() => Assert.Contains("120 treff", Text(cut)));
        cut.Find("input[type=search]").Input("astma");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Vis flere").Click(new MouseEventArgs { Detail = 1 });

        cut.WaitForAssertion(() => Assert.Equal(["diabetes:0", "diabetes:50"], client.Asked));
    }

    private sealed class PagedClient : EmptyMuninExplorerClient
    {
        public List<string> Asked { get; } = [];

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default)
        {
            Asked.Add($"{query}:{skip}");
            return Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage
            {
                Total = 120,
                Codes = [.. Enumerable.Range(skip, 50).Select(i => new KodeverkCodeHit { Value = $"{query}{i}", Name = query })],
            });
        }
    }

    private sealed class PendingClient : EmptyMuninExplorerClient
    {
        public TaskCompletionSource<int?> Count { get; } = new();
        public TaskCompletionSource<KodeverkCodePage?> Search { get; } = new();
        public Exception? LoadFailure { get; init; }

        public override Task<KodeverkCodePage?> GetKodeverkCodeChildrenAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string? node, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) =>
            LoadFailure is { } failure ? Task.FromException<KodeverkCodePage?>(failure) : Task.FromResult<KodeverkCodePage?>(new KodeverkCodePage());

        public override Task<int?> CountKodeverkCodesAsync(Guid variableId, DesiredCodeChoice choice, CancellationToken cancellationToken = default) =>
            Count.Task;

        public override Task<KodeverkCodePage?> SearchKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, string query, int skip = 0, int take = 50,
            CancellationToken cancellationToken = default) => Search.Task;
    }

    private IRenderedComponent<DesiredCodePicker> PickerWith(EmptyMuninExplorerClient client, Saves saves, IReadOnlyList<KodeverkLink> links)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        return Render<DesiredCodePicker>(b => b.Add(c => c.VariableId, Variable).Add(c => c.Links, links).Add(c => c.Save, saves.SaveAsync));
    }

    [Fact]
    public void AddRange_WhenPressedTwiceWhileTheCountIsOut_ThenTheRangeIsAddedOnce()
    {
        var client = new PendingClient();
        var saves = new Saves();
        var cut = PickerWith(client, saves, [Icd10]);
        cut.Find("input[id$='-from']").Input("E10");
        cut.Find("input[id$='-to']").Input("E14");

        Press(cut, "Legg til intervall");
        Press(cut, "Legg til intervall");
        client.Count.SetResult(50);

        cut.WaitForAssertion(() => Assert.Contains("E10–E14 (50 koder)", Text(cut)));
        Press(cut, "Lagre");
        Assert.Single(Assert.Single(saves.Calls));
    }

    [Fact]
    public void Remove_WhileASaveIsOut_ThenTheDraftHoldsAndARefusalNamesTheChoiceThatWasSent()
    {
        var pending = new TaskCompletionSource<DesiredDataResult>();
        Services.AddSingleton<IMuninExplorerClient>(new CodesClient());
        var cut = Render<DesiredCodePicker>(b => b
            .Add(c => c.VariableId, Variable)
            .Add(c => c.Links, [Sivilstand])
            .Add(c => c.Save, _ => pending.Task));
        Tick(cut, "Ugift");
        Tick(cut, "Gift");
        Press(cut, "Lagre");

        cut.FindAll("button").First(b => b.TextContent.Trim().StartsWith("Fjern 1", StringComparison.Ordinal)).Click();
        Assert.All(cut.FindAll("input[type=checkbox]"), box => Assert.True(box.HasAttribute("disabled"), "no box can be ticked while saving"));
        pending.SetResult(new DesiredDataResult(DesiredDataOutcome.Refused, InvalidIndex: 1));

        cut.WaitForAssertion(() => Assert.Contains("2 Gift finnes ikke i kodelisten.", cut.Find("[role=alert]").TextContent));
        Assert.Contains("2 av 200 valg", Text(cut));
    }

    [Fact]
    public void Open_WhenTheCodeListTimesOut_ThenTheReaderIsToldItCouldNotBeFetched()
    {
        var cut = PickerWith(new PendingClient { LoadFailure = new TaskCanceledException("timeout") }, new Saves(), [Icd10]);

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente kodelisten nå.", Text(cut)));
    }

    [Fact]
    public void Search_WhenClearedWhileAnAnswerIsOut_ThenTheLateAnswerDoesNotComeBack()
    {
        var client = new PendingClient();
        var cut = PickerWith(client, new Saves(), [Icd10]);
        cut.Find("input[type=search]").Input("diabetes");
        Press(cut, "Søk");
        cut.Find("input[type=search]").Input("");
        Press(cut, "Søk");

        client.Search.SetResult(new KodeverkCodePage { Total = 1, Codes = [new KodeverkCodeHit { Value = "E10", Name = "Diabetes" }] });

        cut.WaitForAssertion(() => Assert.DoesNotContain("1 treff", Text(cut)));
        Assert.DoesNotContain("E10 Diabetes", Text(cut));
    }

    [Fact]
    public void Save_WhenChoicesAreKeptOrAdded_ThenTheirCodeListKeepsItsName()
    {
        var saves = new Saves();
        var stored = new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", KodeverkName = "Sivilstand", Kind = "kode", Code = "1", Name = "Ugift" };
        var cut = Picker(saves, [Sivilstand], [stored]);

        Tick(cut, "Gift");
        Press(cut, "Lagre");

        Assert.All(Assert.Single(saves.Calls), view => Assert.Equal("Sivilstand", view.KodeverkName));
    }
}
