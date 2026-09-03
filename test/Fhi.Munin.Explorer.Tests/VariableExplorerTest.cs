using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The component a host actually mounts: search and the reader's own lists behind Runa's two tabs,
/// with the view in the address bar.
/// </summary>
/// <remarks>
/// The failure guarded here is not "the tabs are missing" but a host mounting the name it always
/// mounted and silently getting less, so <see cref="ShippedDefault"/> is resolved as a string — a
/// <c>typeof</c> would follow a rename and pass while the CMS field pointed at nothing. One
/// direction of the shared state is knowingly unasserted: Fhi.Metadata-ehghv.
/// </remarks>
public class VariableExplorerTest : BunitContext
{
    /// <summary>Exactly what helsedata's <c>BlazorComponentPage.TypeName</c> defaults to.</summary>
    private const string ShippedDefault = "Fhi.Munin.Explorer.Blazor.VariableExplorer";

    private static readonly Guid ListId = Guid.NewGuid();

    /// <summary>Answers search and the reader's one list off the same in-memory set.</summary>
    /// <remarks>
    /// One store behind both endpoints: a fake that kept them apart could not tell a surface that
    /// shares state from one that refetched.
    /// </remarks>
    private sealed class ExplorerClient : EmptyMuninExplorerClient
    {
        private readonly VariableSummary[] _rows;

        public ExplorerClient(params VariableSummary[] rows) => _rows = rows;

        public readonly HashSet<Guid> Stored = [];

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = _rows,
                TotalCount = _rows.Length,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Mine hjertevariabler" }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            var items = _rows
                .Where(r => Stored.Contains(r.Id))
                .Select(r => new VariableListItem
                {
                    VariableId = r.Id,
                    VariableCode = r.Code,
                    VariableName = r.PreferredTerm,
                })
                .ToArray();

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = items,
                TotalCount = items.Length,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            foreach (var v in variableIds) { Stored.Add(v); }

            return Task.FromResult(true);
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            foreach (var v in variableIds) { Stored.Remove(v); }

            return Task.FromResult(true);
        }
    }

    private static VariableSummary Variable(string name, string code) =>
        new() { Id = Guid.NewGuid(), Code = code, PreferredTerm = name, KildeName = "Als registeret" };

    /// <summary>The render mode this component requires, and the JS runtime its URL mirror needs.</summary>
    /// <remarks>
    /// Called after the client is registered, never from the constructor: bUnit seals its service
    /// collection the first time anything is resolved, and the renderer info resolves the renderer.
    /// </remarks>
    private void Prepare(IMuninExplorerClient client)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<VariableExplorer> RenderExplorer(
        ExplorerClient client, bool signedIn = true, string language = "no")
    {
        Prepare(client);

        return Render<VariableExplorer>(p => p
            .Add(c => c.IsAuthenticated, signedIn)
            .Add(c => c.Language, language));
    }

    private static IElement Tab<T>(IRenderedComponent<T> cut, string label) where T : IComponent =>
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == label);

    private static IElement PanelFor<T>(IRenderedComponent<T> cut, IElement tab) where T : IComponent =>
        cut.Find($"#{tab.GetAttribute("aria-controls")}");

    private static bool Hidden(IElement panel) => panel.HasAttribute("hidden");

    // -----------------------------------------------------------------------
    // What the shipped default name gets a host.

    [Fact]
    public void ShippedDefaultName_WhenAHostMountsIt_ThenItIsTheWholeExplorer()
    {
        // Resolved from the string the CMS stores, not from a type reference: the whole point is
        // that a rename must not be able to leave that field pointing at nothing while this passes.
        var component = typeof(IMuninExplorerClient).Assembly.GetType(ShippedDefault, throwOnError: false);

        Assert.NotNull(component);

        var client = new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER"));
        Prepare(client);

        // Only the two parameters BlazorComponentPage offers, in the way it offers them.
        var cut = Render(builder =>
        {
            builder.OpenComponent(0, component!);
            builder.AddComponentParameter(1, "Language", "no");
            builder.AddComponentParameter(2, "IsAuthenticated", true);
            builder.CloseComponent();
        });

        // Search.
        Assert.Contains("Alder ved diagnose", cut.Markup, StringComparison.Ordinal);

        // Tabs, and the reader's own lists behind the second one. Opened first: the panel holds
        // nothing until its tab is, which is what keeps it off screen in a host whose stylesheet
        // defeats `hidden`. The heading is drawn only for a signed-in reader, so finding it proves
        // the list surface is really there and really believes it has a reader.
        Assert.Equal(2, cut.FindAll("[role=tab]").Count);

        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();

        Assert.Contains("Mine variabellister", cut.Markup, StringComparison.Ordinal);

        // URL state: the component wrote the view into the address bar without a host asking.
        Assert.NotEmpty(JSInterop.Invocations["history.replaceState"]);
    }

    [Fact]
    public void ShippedDefaultName_WhenItIsResolved_ThenItTakesOnlyWhatTheCmsCanOffer()
    {
        // BlazorComponentPage offers Language, SkjemaId and IsAuthenticated and nothing else, and
        // it drops any candidate the component does not declare. A required parameter outside that
        // set would leave the CMS mounting an explorer that cannot work, with no error anywhere.
        var component = typeof(IMuninExplorerClient).Assembly.GetType(ShippedDefault, throwOnError: true)!;

        var declared = component
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: false))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Language", declared);
        Assert.Contains("IsAuthenticated", declared);
    }

    // -----------------------------------------------------------------------
    // The tabs themselves.

    [Fact]
    public void Tabs_WhenTheReaderIsNorwegian_ThenTheyAreWordedAsRunaWordsThem()
    {
        var cut = RenderExplorer(new ExplorerClient());

        Assert.Equal(
            ["Søkeresultat", "Variabelliste"],
            cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()));
    }

    [Fact]
    public void Tabs_WhenTheReaderIsEnglish_ThenTheyAreWordedAsRunaWordsThem()
    {
        var cut = RenderExplorer(new ExplorerClient(), language: "en");

        Assert.Equal(
            ["Search results", "Variable list"],
            cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()));
    }

    [Fact]
    public void Tabs_WhenThePageOpens_ThenSearchIsSelectedAndTheListPanelIsHidden()
    {
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var search = Tab(cut, "Søkeresultat");
        var list = Tab(cut, "Variabelliste");

        Assert.Equal("true", search.GetAttribute("aria-selected"));
        Assert.Equal("false", list.GetAttribute("aria-selected"));
        Assert.False(Hidden(PanelFor(cut, search)));
        Assert.True(Hidden(PanelFor(cut, list)));
    }

    [Fact]
    public void Tabs_WhenTheOtherOneIsPressed_ThenItIsSelectedAndItsPanelIsShown()
    {
        var cut = RenderExplorer(new ExplorerClient());

        Tab(cut, "Variabelliste").Click();

        Assert.Equal("true", Tab(cut, "Variabelliste").GetAttribute("aria-selected"));
        Assert.True(Hidden(PanelFor(cut, Tab(cut, "Søkeresultat"))));
        Assert.False(Hidden(PanelFor(cut, Tab(cut, "Variabelliste"))));
    }

    [Fact]
    public void Tabs_WhenTheKeyboardMovesAlongThem_ThenTheSelectionFollows()
    {
        // The unselected tab carries tabindex="-1" so the tablist costs one tab stop rather than
        // one per tab; arrow keys are what replaces those stops (APG). Without them a keyboard
        // reader can reach the tablist and never reach the second tab.
        var cut = RenderExplorer(new ExplorerClient());

        cut.Find("[role=tablist]").KeyDown("ArrowRight");

        Assert.Equal("true", Tab(cut, "Variabelliste").GetAttribute("aria-selected"));
        Assert.Equal("0", Tab(cut, "Variabelliste").GetAttribute("tabindex"));
        Assert.Equal("-1", Tab(cut, "Søkeresultat").GetAttribute("tabindex"));
    }

    [Fact]
    public void Tabs_WhenTwoExplorersAreOnOnePage_ThenNeitherPanelIsLabelledByTheOthersTab()
    {
        // A host may mount this twice. Ids shared between the two mounts would leave both panels
        // pointing at the first tablist, which is the defect the discriminator exists to prevent.
        var client = new ExplorerClient();
        Prepare(client);

        var cut = Render(builder =>
        {
            builder.OpenComponent<VariableExplorer>(0);
            builder.AddComponentParameter(1, "Language", "no");
            builder.AddComponentParameter(2, "IsAuthenticated", true);
            builder.CloseComponent();

            builder.OpenComponent<VariableExplorer>(3);
            builder.AddComponentParameter(4, "Language", "no");
            builder.AddComponentParameter(5, "IsAuthenticated", true);
            builder.CloseComponent();
        });

        var ids = cut.FindAll("[role=tab]").Select(t => t.Id).ToList();
        var panels = cut.FindAll("[role=tabpanel]").Select(p => p.Id).ToList();

        Assert.Equal(4, ids.Count);
        Assert.Equal(4, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, panels.Distinct(StringComparer.Ordinal).Count());
    }

    // -----------------------------------------------------------------------
    // THE TRAP: one state behind both tabs.

    [Fact]
    public void SavedOnTheSearchTab_WhenTheListTabIsOpened_ThenTheVariableIsAlreadyThere()
    {
        // The trap: a wrapper that mounts two components which do not agree about what the reader
        // has saved renders both tabs and fails here. No page reload and no second sign-in — the
        // save goes through the circuit's VariableListState, and the list reads the same store.
        var variable = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var cut = RenderExplorer(new ExplorerClient(variable));

        Tab(cut, "Variabelliste").Click();
        Assert.DoesNotContain(
            "Alder ved diagnose",
            PanelFor(cut, Tab(cut, "Variabelliste")).InnerHtml,
            StringComparison.Ordinal);

        Tab(cut, "Søkeresultat").Click();
        cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]")[0].Click();

        Tab(cut, "Variabelliste").Click();

        Assert.Contains(
            "Alder ved diagnose",
            PanelFor(cut, Tab(cut, "Variabelliste")).InnerHtml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ListView_WhenItsTabIsNotOpen_ThenItIsNotInTheDocumentAtAll()
    {
        // Not merely `hidden`. helsedata's stylesheet carries a bare `div { display: block }`,
        // which beats the browser's `[hidden] { display: none }` — so a hidden panel is a visible
        // panel there. The panel element stays, because a tab's aria-controls has to resolve.
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var panel = PanelFor(cut, Tab(cut, "Variabelliste"));

        Assert.Equal("", panel.TextContent.Trim());
        Assert.Empty(panel.QuerySelectorAll("button"));
        Assert.Empty(panel.QuerySelectorAll("input"));
        Assert.Empty(cut.FindComponents<VariableListView>());

        // And the results are the other way round once the list tab is the open one.
        Tab(cut, "Variabelliste").Click();

        Assert.Equal("", PanelFor(cut, Tab(cut, "Søkeresultat")).TextContent.Trim());
        Assert.Single(cut.FindComponents<VariableListView>());
    }

    // -----------------------------------------------------------------------
    // Signed out.

    [Fact]
    public void SignedOut_WhenTheExplorerIsDrawn_ThenThereIsNoTablistAtAll()
    {
        // A signed-out reader has no lists, so a second tab would name an empty panel. No tablist,
        // and the results stand where they always did — the shape that host had before the tabs.
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")), signedIn: false);

        Assert.Empty(cut.FindAll("[role=tablist]"));
        Assert.Empty(cut.FindAll("[role=tab]"));
        Assert.Empty(cut.FindAll("[role=tabpanel]"));
        Assert.DoesNotContain("Variabelliste", cut.Markup, StringComparison.Ordinal);

        // The results are still there, and still offer nothing that needs a reader.
        Assert.Contains("Alder ved diagnose", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]"));
    }

    [Fact]
    public void SignedOut_WhenNoIsAuthenticatedIsPassedAtAll_ThenTheDefaultIsStillSignedOut()
    {
        // The parameter a host forgets. It has to fail closed: the alternative is an explorer that
        // offers to save into a list it cannot reach.
        var client = new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER"));
        Prepare(client);

        var cut = Render<VariableExplorer>(p => p.Add(c => c.Language, "no"));

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]"));
        Assert.Empty(cut.FindAll("[role=tablist]"));
    }

    // -----------------------------------------------------------------------
    // The tablist's own shape.

    [Fact]
    public void Tablist_WhenItIsRendered_ThenEveryTabNamesAPanelThatIsInThePage()
    {
        // Both panels are in the DOM at once here, unlike the detail panel's single tabpanel, so
        // an aria-controls that named one shared id would have two elements answering to it.
        var cut = RenderExplorer(new ExplorerClient());

        var tabs = cut.FindAll("[role=tab]");

        Assert.Equal(2, tabs.Count);

        foreach (var tab in tabs)
        {
            var panel = PanelFor(cut, tab);

            Assert.Equal("tabpanel", panel.GetAttribute("role"));
            Assert.Equal(tab.Id, panel.GetAttribute("aria-labelledby"));
        }
    }

    [Fact]
    public void Tablist_WhenItIsRendered_ThenItInventsNoClassNameOfItsOwn()
    {
        // The package ships no CSS, so a new munin-explorer* name here would render at raw browser
        // defaults in every host until somebody wrote a rule for it in Fhi.Helsedata.Stiler. These
        // four already have rules; the detail panel's tabs wear them.
        var cut = RenderExplorer(new ExplorerClient());

        Assert.Equal("munin-explorer-meta__tabs", cut.Find("[role=tablist]").ClassName);

        // The results panel is deliberately unclassed: it wraps markup that already carries its
        // own names, and a wrapper with a name would be one more rule for a host to write.
        var list = PanelFor(cut, Tab(cut, "Variabelliste"));
        Assert.Equal("munin-explorer-meta__tab-content", list.ClassName);
        Assert.True(string.IsNullOrEmpty(PanelFor(cut, Tab(cut, "Søkeresultat")).ClassName));
    }

    [Fact]
    public void Tabs_WhenTheyAreDrawn_ThenTheSearchBoxAndFiltersAreAboveThemAndOnBothTabs()
    {
        // Runa's placement, and the defect this replaces: the tablist used to wrap the whole
        // component, so it sat at the very top where helsedata's own header covers it — and
        // switching to Variabelliste took the search box away with it.
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        // Document order, read off a flat walk of the rendered tree rather than off the markup
        // string: what matters is where the tablist lands relative to the search box and the
        // facets, and only their order in the document decides that.
        var order = cut.Find("section.munin-explorer").QuerySelectorAll("*").ToList();

        var search = order.FindIndex(e => e.GetAttribute("role") == "search");
        var filters = order.FindIndex(e => e.ClassList.Contains("munin-explorer-filters"));
        var tablist = order.FindIndex(e => e.GetAttribute("role") == "tablist");

        Assert.True(search >= 0 && filters >= 0 && tablist >= 0);
        Assert.True(search < tablist, "the search box must come before the tablist");
        Assert.True(filters < tablist, "the filters must come before the tablist");

        // And on the other tab the search box is still in the page rather than hidden with it.
        Tab(cut, "Variabelliste").Click();

        Assert.NotNull(cut.Find("form[role=search]"));
        Assert.Null(cut.Find("form[role=search]").GetAttribute("hidden"));
        Assert.Equal("true", Tab(cut, "Variabelliste").GetAttribute("aria-selected"));
    }

    [Fact]
    public void Explorer_WhenItIsDrawn_ThenNothingOffersToLinkAnAccount()
    {
        // The account link was Munin's own and helsedata does not want it, so it is gone from the
        // package rather than switched off. Asserted on the rendered words a reader would see.
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        Assert.DoesNotContain("Koble konto", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("munin-explorer-account-link", cut.Markup, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Mounting it the way a host must not.

    [Fact]
    public void Mount_WhenItIsPrerendered_ThenItRefusesRatherThanDrawingAStaleUrl()
    {
        var client = new ExplorerClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Static", false));
        JSInterop.Mode = JSRuntimeMode.Loose;

        Assert.Throws<InvalidOperationException>(() => Render<VariableExplorer>());
    }
}
