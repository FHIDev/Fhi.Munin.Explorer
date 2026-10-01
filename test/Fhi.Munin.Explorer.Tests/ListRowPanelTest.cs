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
    private static readonly Guid OtherListId = new("22222222-2222-2222-2222-222222222222");

    private static readonly VariableListItem Databaseversjon = new()
    {
        VariableId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        VariableName = "Databaseversjon",
        VariableCode = "V_HKR.VERSJON_DATABASE",
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

    private sealed class PanelClient(params VariableListItem[] items) : EmptyMuninExplorerClient
    {
        public List<Guid> DetailsAskedFor { get; } = [];

        public bool DetailMissing { get; init; }

        /// <summary>A second list holding the same variables, which makes the list picker appear.</summary>
        public bool TwoLists { get; init; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(TwoLists
                ? [new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = items.Length },
                   new VariableList { Id = OtherListId, Name = "Kopien", VariableCount = items.Length }]
                : [new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = items.Length }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = items,
                TotalCount = items.Length,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailsAskedFor.Add(id);

            if (DetailMissing)
            {
                return Task.FromResult<VariableDetail?>(null);
            }

            var item = items.Single(i => i.VariableId == id);

            return Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                Code = item.VariableCode ?? "",
                PreferredTerm = item.VariableName ?? "",
                Description = $"Om {item.VariableName}.",
                KildeName = "Hjerte- og karregisteret",
                DatasamlingName = "Alle variabler",
                VariabelgruppeName = "Datakilde",
                DataFrom = item.DataFrom,
                DataTo = item.DataTo,
            });
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
            s => s.TextContent.Trim() == "Kunne ikke kopiere lenken. Send den på e-post i stedet."));
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

    [Fact]
    public void Explorer_WhenARowIsShared_ThenTheLinkSearchesItsCodeAndOpensIt()
    {
        Services.AddSingleton<IMuninExplorerClient>(new PanelClient(Databaseversjon));
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/variabler");

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();
        cut.WaitForElement("th[scope=row] button").Click();

        var mail = cut.WaitForElement("a[href^='mailto:']");
        var body = Uri.UnescapeDataString(mail.GetAttribute("href")!).Split("&body=")[1];
        var link = new Uri(body["Databaseversjon: ".Length..]);
        var state = ExplorerUrlState.Parse(link.Query);

        Assert.True(link.IsAbsoluteUri);
        Assert.Equal("V_HKR.VERSJON_DATABASE", state.Search);
        Assert.Equal(Databaseversjon.VariableId, state.SelectedVariableId);
    }
}
