using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>A row of an own list opens into the variable's panel, with a way to share it (ADO 121586).</summary>
public class ListRowPanelTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HjerteKilde = new("cccccccc-0000-0000-0000-000000000001");

    private static readonly Guid OtherListId = new("22222222-2222-2222-2222-222222222222");

    private static readonly VariableListItem Databaseversjon = new()
    {
        VariableId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        VariableName = "Databaseversjon",
        VariableCode = "V_HKR.VERSJON_DATABASE",
        KildeId = HjerteKilde,
        KildeName = "Hjerte- og karregisteret",
        DataFrom = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2022, 12, 31, 0, 0, 0, TimeSpan.Zero),
    };

    private static readonly VariableListItem Alder = new()
    {
        VariableId = new Guid("aaaaaaaa-0000-0000-0000-000000000002"),
        VariableName = "Alder",
        VariableCode = "V_HKR.ALDER",
    };

    private sealed class PanelClient(params VariableListItem[] all) : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> items = [.. all];

        public List<Guid> DetailsAskedFor { get; } = [];

        public bool DetailMissing { get; init; }

        /// <summary>Held until the test releases it, as a browser's fetch is never instant.</summary>
        public TaskCompletionSource? DetailGate { get; init; }

        /// <summary>One nameless kildekodeverk, whose codes are fetched with the variable and never answer.</summary>
        public bool NamelessStalled { get; init; }

        public int CodeRequests { get; private set; }

        public List<string> CodeReferences { get; } = [];

        private readonly List<TaskCompletionSource<KodeverkCodes?>> _codeStalls = [];

        /// <summary>A second nameless kildekodeverk, fetched only after the first answers.</summary>
        public bool TwoNameless { get; init; }

        public void AnswerOldestCodes() =>
            _codeStalls.First(stall => !stall.Task.IsCompleted).TrySetResult(null);

        /// <summary>The detail's own period, where a test needs it to differ from the list's.</summary>
        public (DateTimeOffset? From, DateTimeOffset? To)? DetailPeriod { get; init; }

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, CancellationToken cancellationToken = default)
        {
            CodeRequests++;
            CodeReferences.Add(kodeverkReference);
            var stall = new TaskCompletionSource<KodeverkCodes?>();
            _codeStalls.Add(stall);
            return stall.Task;
        }

        /// <summary>A second list holding the same variables, which makes the list picker appear.</summary>
        public bool TwoLists { get; init; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(TwoLists
                ? [new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = items.Count },
                   new VariableList { Id = OtherListId, Name = "Kopien", VariableCount = items.Count }]
                : [new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = items.Count }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. items.Where(item => !_removed.Contains((id, item.VariableId)))],
                TotalCount = items.Count(item => !_removed.Contains((id, item.VariableId))),
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            _removed.RemoveWhere(entry => entry.List == id && variableIds.Contains(entry.Variable));
            items.AddRange(all.Where(item => variableIds.Contains(item.VariableId) && !items.Contains(item)));
            return Task.FromResult(true);
        }

        public bool RefuseRemoval { get; init; }

        /// <summary>Removals that have reached the fake, so a test can wait for one to land.</summary>
        public int Removals { get; private set; }

        private readonly HashSet<(Guid List, Guid Variable)> _removed = [];

        /// <summary>Held until the test releases it, so the reader can move on meanwhile.</summary>
        public TaskCompletionSource? RemovalGate { get; init; }

        public override async Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            if (RemovalGate is { } gate)
            {
                await gate.Task;
            }

            Removals++;

            if (RefuseRemoval)
            {
                return false;
            }

            _removed.UnionWith(variableIds.Select(variable => (id, variable)));
            return true;
        }

        public override async Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailsAskedFor.Add(id);

            if (DetailGate is { } gate)
            {
                await gate.Task;
            }

            if (DetailMissing)
            {
                return null;
            }

            var item = all.Single(i => i.VariableId == id);

            return new VariableDetail
            {
                Id = id,
                Code = item.VariableCode ?? "",
                PreferredTerm = item.VariableName ?? "",
                Description = $"Om {item.VariableName}.",
                KildeName = "Hjerte- og karregisteret",
                DatasamlingName = "Alle variabler",
                VariabelgruppeName = "Datakilde",
                DataFrom = DetailPeriod is { } period ? period.From : item.DataFrom,
                DataTo = DetailPeriod is { } periodTo ? periodTo.To : item.DataTo,
                KodeverkLinks = (NamelessStalled, TwoNameless) switch
                {
                    (_, true) =>
                    [
                        new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true },
                        new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2338", HasCodeValues = true },
                    ],
                    (true, _) => [new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true }],
                    _ => [],
                },
            };
        }
    }

    private IRenderedComponent<VariableListView> RenderView(
        PanelClient client, Func<VariableListItem, string>? variableHref = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        return Render<VariableListView>(p =>
        {
            p.Add(c => c.IsAuthenticated, true);

            if (variableHref is not null)
            {
                p.Add(c => c.VariableHref, variableHref);
            }
        });
    }

    private static IElement NameButton(IRenderedComponent<VariableListView> cut, string name) =>
        cut.FindAll("th[scope=row] button").Single(b => b.TextContent.Trim() == name);

    private static IElement? Panel(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("[id^='munin-explorer-list-panel-']").SingleOrDefault(e => e.GetAttribute("role") == "region");

    private static string Fact(IElement panel, string label) =>
        panel.QuerySelectorAll("dt").Single(dt => dt.TextContent.Trim() == label).NextElementSibling!.TextContent.Trim();

    [Fact]
    public void Row_WhenTheListOpens_ThenItIsAClosedDisclosure()
    {
        var cut = RenderView(new PanelClient(Databaseversjon));

        var name = NameButton(cut, "Databaseversjon");

        Assert.Equal("false", name.GetAttribute("aria-expanded"));
        Assert.Null(name.GetAttribute("aria-controls"));
        Assert.Null(Panel(cut));
    }

    [Fact]
    public void Row_WhenItsNameIsPressed_ThenItsPanelOpensNamedAfterTheVariable()
    {
        var cut = RenderView(new PanelClient(Databaseversjon));

        NameButton(cut, "Databaseversjon").Click();

        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        var name = NameButton(cut, "Databaseversjon");

        Assert.Equal("true", name.GetAttribute("aria-expanded"));
        Assert.Equal(panel.Id, name.GetAttribute("aria-controls"));
        Assert.Equal("Databaseversjon", cut.Find($"#{panel.GetAttribute("aria-labelledby")}").TextContent.Trim());

        // The panel spans the whole row, so it is not squeezed into the name column.
        Assert.Equal(
            cut.FindAll("thead th").Count.ToString(),
            panel.ParentElement!.GetAttribute("colspan"));
    }

    [Fact]
    public void Panel_WhenTheDetailArrives_ThenItShowsDescriptionPeriodKildeAndVariabelgruppe()
    {
        var cut = RenderView(new PanelClient(Databaseversjon));

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Equal("Om Databaseversjon.", Fact(panel, "Beskrivelse"));
        Assert.Contains("2012", Fact(panel, "Dataperiode"));
        Assert.Contains("2022", Fact(panel, "Dataperiode"));
        Assert.Contains("Hjerte- og karregisteret", Fact(panel, "Kilde"));
        Assert.Contains("Alle variabler", Fact(panel, "Kilde"));
        Assert.Equal("Datakilde", Fact(panel, "Variabelgruppe"));
    }

    [Fact]
    public void Panel_WhenOpened_ThenItHasTheExplorersTwoTabsAndTheSecondDoesNotRepeatTheDescription()
    {
        var cut = RenderView(new PanelClient(Databaseversjon));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=tablist]");

        Assert.Equal(["Data", "Om variabelen"], cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()));
        Assert.Equal("true", cut.FindAll("[role=tab]")[0].GetAttribute("aria-selected"));

        cut.FindAll("[role=tab]")[1].Click();

        var about = cut.Find("[role=tabpanel]");
        Assert.Contains("V_HKR.VERSJON_DATABASE", about.TextContent);
        Assert.DoesNotContain("Om Databaseversjon.", about.TextContent);
    }

    [Fact]
    public void Row_WhenItsNameIsPressedAgain_ThenThePanelClosesAndNamesNothing()
    {
        var cut = RenderView(new PanelClient(Databaseversjon));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        NameButton(cut, "Databaseversjon").Click();

        Assert.Null(Panel(cut));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Null(NameButton(cut, "Databaseversjon").GetAttribute("aria-controls"));
    }

    [Fact]
    public void Row_WhenAnotherRowIsOpened_ThenOnlyThatOneIsOpen()
    {
        var client = new PanelClient(Databaseversjon, Alder);
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        NameButton(cut, "Alder").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("Alder", cut.Find($"#{Panel(cut)!.GetAttribute("aria-labelledby")}").TextContent.Trim()));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Equal([Databaseversjon.VariableId, Alder.VariableId], client.DetailsAskedFor);
    }

    [Fact]
    public async Task Panel_WhenTheReaderSwitchesList_ThenTheSameVariableThereIsClosed()
    {
        var cut = RenderView(new PanelClient(Databaseversjon) { TwoLists = true });

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));

        cut.WaitForAssertion(() => Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded")));
        Assert.Null(Panel(cut));

        await cut.InvokeAsync(() => cut.Find("select").Change(ListId.ToString()));

        cut.WaitForAssertion(() => Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded")));
        Assert.Null(Panel(cut));
    }

    private static IElement RemoveButton(IRenderedComponent<VariableListView> cut, string name)
    {
        var nameId = cut.FindAll("th[scope=row] span[id]").Single(span => span.TextContent.Trim() == name).Id;
        return cut.FindAll("button").Single(b => b.GetAttribute("aria-labelledby")?.Contains(nameId!) == true);
    }

    [Fact]
    public async Task Row_WhenTheOpenVariableIsRemovedAndAddedBack_ThenItComesBackClosed()
    {
        var cut = RenderView(new PanelClient(Databaseversjon, Alder));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        RemoveButton(cut, "Databaseversjon").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));

        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [Databaseversjon.VariableId]));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("th[scope=row] button").Count));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Null(Panel(cut));
    }

    [Fact]
    public void Row_WhenRemovingTheOpenVariableIsRefused_ThenItsPanelStaysOpen()
    {
        var cut = RenderView(new PanelClient(Databaseversjon, Alder) { RefuseRemoval = true });

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        RemoveButton(cut, "Databaseversjon").Click();

        Assert.Equal("true", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.NotNull(Panel(cut));
    }

    [Fact]
    public async Task Row_WhenARemovalLandsAfterTheSameVariableWasOpenedInAnotherList_ThenThatPanelStaysOpen()
    {
        var gate = new TaskCompletionSource();
        var cut = RenderView(new PanelClient(Databaseversjon) { TwoLists = true, RemovalGate = gate });

        RemoveButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));
        cut.WaitForAssertion(() => NameButton(cut, "Databaseversjon").Click());
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        await cut.InvokeAsync(gate.SetResult);
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal("true", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.NotNull(Panel(cut));
    }

    [Fact]
    public void Row_WhenAnotherVariableIsRemoved_ThenTheOpenOneStaysOpen()
    {
        var cut = RenderView(new PanelClient(Databaseversjon, Alder));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        RemoveButton(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));
        Assert.Equal("true", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.NotNull(Panel(cut));
    }

    [Fact]
    public void Row_WhenItsVariableHasLeftTheCatalogue_ThenItsNameIsTextAndOpensNothing()
    {
        var orphan = new VariableListItem { VariableId = Guid.NewGuid() };
        var cut = RenderView(new PanelClient(Databaseversjon, orphan));

        Assert.Single(cut.FindAll("th[scope=row] button"));
        Assert.Contains(cut.FindAll("th[scope=row] span"), s => s.TextContent.Trim() == "Variabelen er ikke tilgjengelig lenger");
    }

    [Fact]
    public void Panel_WhenTheVariableIsNotFound_ThenItSaysSoAndKeepsItsHeading()
    {
        var cut = RenderView(new PanelClient(Databaseversjon) { DetailMissing = true });

        NameButton(cut, "Databaseversjon").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("Fant ingen detaljer for denne variabelen.", Panel(cut)!.QuerySelector("[role=status]")!.TextContent.Trim()));
        Assert.Empty(cut.FindAll("[role=tablist]"));
    }

    [Fact]
    public async Task Panel_WhileANamelessKodeverksCodesLoad_ThenItIsAlreadyDrawn()
    {
        var gate = new TaskCompletionSource();
        var client = new PanelClient(Databaseversjon) { DetailGate = gate, NamelessStalled = true };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(gate.SetResult);

        cut.WaitForAssertion(() => Assert.Equal(1, client.CodeRequests));
        cut.WaitForAssertion(() => Assert.NotNull(Panel(cut)!.QuerySelector("[role=tablist]")));
    }

    [Fact]
    public async Task NamelessCodes_WhenThePanelClosesWhileTheyLoad_ThenTheRestAreNotAskedFor()
    {
        var client = new PanelClient(Databaseversjon) { TwoNameless = true };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForAssertion(() => Assert.Equal(["2336"], client.CodeReferences));
        NameButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(client.AnswerOldestCodes);

        Assert.Equal(["2336"], client.CodeReferences);
    }

    [Fact]
    public void Period_WhenTheDetailKnowsNoPeriod_ThenTheListsIsShown()
    {
        var cut = RenderView(new PanelClient(Databaseversjon) { DetailPeriod = (null, null) });

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Contains("2012", Fact(panel, "Dataperiode"));
        Assert.Contains("2022", Fact(panel, "Dataperiode"));
    }

    [Fact]
    public void Period_WhenTheDetailKnowsOnlyItsEnd_ThenTheListsStartIsNotBorrowed()
    {
        var client = new PanelClient(Databaseversjon)
        {
            DetailPeriod = (null, new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero)),
        };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Contains("2024", Fact(panel, "Dataperiode"));
        Assert.DoesNotContain("2012", Fact(panel, "Dataperiode"));
    }

    [Fact]
    public void Period_WhenTheDetailKnowsOnlyItsStart_ThenTheListsEndIsNotBorrowed()
    {
        var client = new PanelClient(Databaseversjon)
        {
            DetailPeriod = (new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero), null),
        };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Contains("2015", Fact(panel, "Dataperiode"));
        Assert.DoesNotContain("2022", Fact(panel, "Dataperiode"));
    }

    [Fact]
    public void Panel_WhenTheHostGivesNoVariableAddress_ThenItOffersNoSharing()
    {
        var cut = RenderView(new PanelClient(Databaseversjon));

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.DoesNotContain("Del variabel", panel.TextContent);
    }

    [Fact]
    public void CopyLink_WhenPressed_ThenTheVariablesAddressGoesToTheClipboardAndThatIsSaid()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        copied.SetVoidResult();
        var cut = RenderView(new PanelClient(Databaseversjon), item => $"https://helsedata.example/variabler?variabelId={item.VariableId}");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

        Assert.Equal(
            $"https://helsedata.example/variabler?variabelId={Databaseversjon.VariableId}",
            copied.Invocations.Single().Arguments.Single());
        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("[role=status]"), s => s.TextContent.Trim() == "Lenken er kopiert."));
    }

    [Fact]
    public void CopyLink_WhenTheBrowserRefuses_ThenItPointsToTheEmailInstead()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("NotAllowedError"));
        var cut = RenderView(new PanelClient(Databaseversjon), _ => "https://helsedata.example/variabler");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("[role=status]"),
            s => s.TextContent.Trim() == "Nettleseren tillot ikke kopiering. Kopier lenken under, eller send den på e-post."));

        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == "Lenke til variabelen");
        var field = cut.Find($"#{label.GetAttribute("for")}");
        Assert.Equal("https://helsedata.example/variabler", field.GetAttribute("value"));
        Assert.True(field.HasAttribute("readonly"));
    }

    [Fact]
    public void CopyLink_WhenPressedAgain_ThenTheStatusEmptiesFirstSoItIsAnnouncedAgain()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        var cut = RenderView(new PanelClient(Databaseversjon), _ => "https://helsedata.example/variabler");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Copy(cut);
        Assert.Equal("", LinkStatus(cut));
        cut.InvokeAsync(() => copied.SetVoidResult());
        cut.WaitForAssertion(() => Assert.Equal("Lenken er kopiert.", LinkStatus(cut)));

        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        Copy(cut);
        Assert.Equal("", LinkStatus(cut));
    }

    private static void Copy(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

    private static string LinkStatus(IRenderedComponent<VariableListView> cut) =>
        cut.Find("[role=group] [role=status]").TextContent.Trim();

    [Fact]
    public void CopyLink_WhenItFailedOnOneRow_ThenAnotherRowShowsNoField()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("NotAllowedError"));
        var cut = RenderView(new PanelClient(Databaseversjon, Alder), item => $"https://helsedata.example/{item.VariableCode}");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        Copy(cut);
        cut.WaitForElement("input[readonly]");

        NameButton(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal("Alder", cut.Find($"#{Panel(cut)!.GetAttribute("aria-labelledby")}").TextContent.Trim()));

        Assert.Empty(cut.FindAll("[role=region] input[readonly]"));
        Assert.Equal("", LinkStatus(cut));
    }

    [Fact]
    public async Task CopyLink_WhenItAnswersAfterAnotherRowOpened_ThenThatRowSaysNothing()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        var cut = RenderView(new PanelClient(Databaseversjon, Alder), item => $"https://helsedata.example/{item.VariableCode}");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        Copy(cut);
        NameButton(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal("Alder", cut.Find($"#{Panel(cut)!.GetAttribute("aria-labelledby")}").TextContent.Trim()));

        await cut.InvokeAsync(() => copied.SetVoidResult());
        await cut.InvokeAsync(() => Task.Delay(50));

        Assert.Equal("", LinkStatus(cut));
    }

    [Fact]
    public void CopyLink_WhenTheCircuitIsGone_ThenThePressDoesNotThrow()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSDisconnectedException("gone"));
        var cut = RenderView(new PanelClient(Databaseversjon), _ => "https://helsedata.example/variabler");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        Copy(cut);

        cut.WaitForAssertion(() => Assert.StartsWith("Nettleseren tillot ikke kopiering", LinkStatus(cut)));
    }

    [Fact]
    public void SendByEmail_Always_ThenTheMessageCarriesTheNameAndTheAddress()
    {
        var cut = RenderView(new PanelClient(Databaseversjon), _ => "https://helsedata.example/variabler?search=V_HKR&variabelId=1");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        var href = Uri.UnescapeDataString(cut.FindAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href")!);

        Assert.StartsWith("mailto:?subject=Variabel: Databaseversjon&body=", href);
        Assert.EndsWith("Databaseversjon: https://helsedata.example/variabler?search=V_HKR&variabelId=1", href);
    }

    private IRenderedComponent<VariableExplorer> OpenInExplorer(
        PanelClient client, IReadOnlyCollection<string>? declined = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/variabler");

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.DeclinedKeys, declined));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();
        cut.WaitForElement("th[scope=row] button").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        return cut;
    }

    [Fact]
    public void Explorer_WhenARowIsShared_ThenTheLinkSearchesItsCodeWithinItsKilde()
    {
        var cut = OpenInExplorer(new PanelClient(Databaseversjon));

        var mail = cut.WaitForElement("a[href^='mailto:']");
        var body = Uri.UnescapeDataString(mail.GetAttribute("href")!).Split("&body=")[1];
        var link = new Uri(body["Databaseversjon: ".Length..]);
        var state = ExplorerUrlState.Parse(link.Query);

        Assert.True(link.IsAbsoluteUri);
        Assert.Equal("V_HKR.VERSJON_DATABASE", state.Search);
        Assert.Equal([HjerteKilde], state.Filter.KildeIds);
        Assert.Equal(Databaseversjon.VariableId, state.SelectedVariableId);
    }

    [Fact]
    public void Explorer_WhenTheHostDeclinesSearch_ThenThePanelOffersNoLinkThatCouldNotOpenIt()
    {
        var cut = OpenInExplorer(new PanelClient(Databaseversjon), declined: ["search"]);

        Assert.DoesNotContain("Del variabel", cut.Find("[role=region][id^='munin-explorer-list-panel-']").TextContent);
    }

    [Theory]
    [InlineData("Historical")]
    [InlineData("historisk")]
    public void Explorer_WhenTheVariableIsHistorical_ThenTheLinkShowsHistoricalVariables(string status)
    {
        var cut = OpenInExplorer(new PanelClient(Databaseversjon with { VersionStatus = status }));

        Assert.True(SharedLinkState(cut).Filter.IncludeHistorical);
    }

    [Fact]
    public void Explorer_WhenTheVariableIsActive_ThenTheLinkLeavesHistoricalVariablesOut()
    {
        var cut = OpenInExplorer(new PanelClient(Databaseversjon with { VersionStatus = "Active" }));

        Assert.False(SharedLinkState(cut).Filter.IncludeHistorical);
    }

    private static ExplorerUrlState SharedLinkState(IRenderedComponent<VariableExplorer> cut)
    {
        var mail = cut.Find("a[href^='mailto:']");
        var body = Uri.UnescapeDataString(mail.GetAttribute("href")!).Split("&body=")[1];
        return ExplorerUrlState.Parse(new Uri(body[(body.IndexOf("http", StringComparison.Ordinal))..]).Query);
    }

    [Fact]
    public void Explorer_WhenTheVariableHasNoCode_ThenThePanelOffersNoLink()
    {
        var cut = OpenInExplorer(new PanelClient(Databaseversjon with { VariableCode = null }));

        Assert.DoesNotContain("Del variabel", cut.Find("[role=region][id^='munin-explorer-list-panel-']").TextContent);
    }
}
