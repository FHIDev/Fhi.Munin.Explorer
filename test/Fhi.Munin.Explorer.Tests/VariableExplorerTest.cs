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
/// <para>
/// The failure this file exists to prevent is not "the tabs are missing". It is a host that mounts
/// the name it has always mounted and silently gets less than the package can do — which is how
/// helsedata ended up on the bare component with no URL state, and how <c>IsAuthenticated</c> went
/// unpassed until somebody measured it. So <see cref="ShippedDefault"/> is the literal string
/// <c>BlazorComponentPage</c> stores, resolved through reflection: a <c>typeof</c> would follow a
/// rename and go on passing while the CMS field pointed at nothing.
/// </para>
/// <para>
/// The other trap is the one a naive wrapper passes. Two independently mounted components render
/// two tabs and satisfy any check that only counts them; what they do not do is agree about what
/// the reader has saved.
/// </para>
/// <para>
/// One direction of that agreement is missing and is deliberately not asserted here: a removal made
/// in <see cref="VariableListView"/> goes through <c>VariableListState.RemoveVariablesAsync</c>,
/// which does not touch the membership set the save button draws from — so the search row goes on
/// offering to remove a variable that is already out. It predates this composition and is reachable
/// today wherever both components sit on one page. Its own bead: Fhi.Metadata-ehghv.
/// </para>
/// </remarks>
public class VariableExplorerTest : BunitContext
{
    /// <summary>Exactly what helsedata's <c>BlazorComponentPage.TypeName</c> defaults to.</summary>
    private const string ShippedDefault = "Fhi.Munin.Explorer.Blazor.VariableExplorer";

    private static readonly Guid ListId = Guid.NewGuid();

    /// <summary>Answers search and the reader's one list off the same in-memory set.</summary>
    /// <remarks>
    /// One store behind both endpoints on purpose: a fake that kept the search rows and the list
    /// rows apart could not tell a surface that shares state from one that refetched.
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

    /// <summary>
    /// The render mode this component requires and the loose JS runtime its URL mirror needs.
    /// </summary>
    /// <remarks>
    /// Called after the client is registered, never from the constructor: bUnit seals its service
    /// collection the first time anything is resolved, and setting the renderer info resolves the
    /// renderer. Same order as <c>UrlStateComponentTest.Prepare</c>.
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

        // Tabs, and the reader's own lists behind the second one — the heading VariableListView
        // draws only for a signed-in reader, so finding it proves the list surface is really there
        // and really believes it has a reader.
        Assert.Equal(2, cut.FindAll("[role=tab]").Count);
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
            builder.CloseComponent();

            builder.OpenComponent<VariableExplorer>(2);
            builder.AddComponentParameter(3, "Language", "no");
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
        // Asserted before the tab is opened, which is the strongest form of "no reload": the list
        // surface is already right while it is still hidden. A wrapper mounting two independent
        // components renders both tabs and fails exactly here.
        var variable = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var cut = RenderExplorer(new ExplorerClient(variable));

        var listPanel = PanelFor(cut, Tab(cut, "Variabelliste"));
        Assert.DoesNotContain("Alder ved diagnose", listPanel.InnerHtml, StringComparison.Ordinal);

        cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]")[0].Click();

        Assert.Contains(
            "Alder ved diagnose",
            PanelFor(cut, Tab(cut, "Variabelliste")).InnerHtml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ListView_WhenTheReaderSwitchesTabs_ThenItIsNotMountedAgain()
    {
        // The half the assertion above cannot make on its own: a component that were rebuilt on
        // every tab switch would fetch its way back to the same markup and read as correct, while
        // losing the page the reader was on and everything else it holds.
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var before = cut.FindComponent<VariableListView>().Instance;

        Tab(cut, "Variabelliste").Click();
        Tab(cut, "Søkeresultat").Click();
        Tab(cut, "Variabelliste").Click();

        Assert.Same(before, cut.FindComponent<VariableListView>().Instance);
    }

    // -----------------------------------------------------------------------
    // The race a synchronous fake cannot see.

    /// <summary>
    /// Answers <c>GetMyListsAsync</c> only when the test says so, which is what a real HTTP call
    /// does and <see cref="ExplorerClient"/> does not.
    /// </summary>
    private sealed class StallingListsClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MyListsCalls { get; private set; }

        public void AnswerLists() => _gate.TrySetResult();

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [], TotalCount = 0, PageNumber = 1, Size = pageSize, TotalPages = 0,
            });

        public override async Task<IReadOnlyList<VariableList>> GetMyListsAsync(
            CancellationToken cancellationToken = default)
        {
            MyListsCalls++;
            await _gate.Task;

            return [new VariableList { Id = ListId, Name = "Mine hjertevariabler" }];
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [new VariableListItem
                {
                    VariableId = Guid.NewGuid(),
                    VariableCode = "V_BDR.ALDER",
                    VariableName = "Alder ved diagnose",
                }],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
    }

    [Fact]
    public async Task ListTab_WhenTheListsReadIsStillInFlightAsBothTabsMount_ThenItStillShowsTheList()
    {
        // Every other fake here answers synchronously, which closes this window before the second
        // surface reaches it — which is why the suite was green while the page was not. Found by
        // driving the real component in a browser.
        var client = new StallingListsClient();
        Prepare(client);

        var cut = Render<VariableExplorer>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "no"));

        client.AnswerLists();

        await cut.WaitForAssertionAsync(() => Assert.Contains(
            "Alder ved diagnose",
            PanelFor(cut, Tab(cut, "Variabelliste")).InnerHtml,
            StringComparison.Ordinal));

        // And joined rather than sent twice: the second surface waits for the first one's answer.
        Assert.Equal(1, client.MyListsCalls);
    }

    // -----------------------------------------------------------------------
    // Signed out.

    [Fact]
    public void SignedOut_WhenTheListTabIsOpened_ThenItOffersNothingThatNeedsAReader()
    {
        // IsAuthenticated defaults to false and VariableListView already draws nothing at all for
        // a signed-out reader — not an empty frame, not a sign-in prompt this package has no
        // business wording. The composition must not put controls back.
        var cut = RenderExplorer(new ExplorerClient(Variable("Alder ved diagnose", "V_BDR.ALDER")), signedIn: false);

        Tab(cut, "Variabelliste").Click();

        var panel = PanelFor(cut, Tab(cut, "Variabelliste"));

        Assert.Empty(panel.QuerySelectorAll("button"));
        Assert.Empty(panel.QuerySelectorAll("input"));
        Assert.Equal("", panel.TextContent.Trim());

        // And no save button on the search tab either, for the same reader.
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
        Assert.Equal("", PanelFor(cut, Tab(cut, "Variabelliste")).TextContent.Trim());
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

        foreach (var panel in cut.FindAll("[role=tabpanel]"))
        {
            Assert.Equal("munin-explorer-meta__tab-content", panel.ClassName);
        }
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
