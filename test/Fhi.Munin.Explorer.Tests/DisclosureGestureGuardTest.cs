using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The rule every disclosure this package draws follows: a gesture that stands still — the second
/// click of a double-click, or a shift-click extending a selection — never changes what any of them
/// discloses, and a deliberate press still toggles.
/// </summary>
/// <remarks>
/// Every round of this defect was found by hand rather than by the suite (Fhi.Metadata-zel47),
/// because a new <c>&lt;button aria-expanded&gt;</c> passes every other test here. What the
/// enumeration below does and does not establish is on <see cref="Swept"/>. The package's other
/// shape of disclosure is gestured by nothing and pinned instead — see <see cref="NativeDisclosure"/>.
/// </remarks>
public class DisclosureGestureGuardTest : ExplorerTestContext
{
    /// <summary>What a gestured disclosure is, for both halves of this guard: the attribute, not a class.</summary>
    private const string Disclosure = "button[aria-expanded]";

    /// <summary>The package's other disclosure: the element, whose state belongs to the user agent.</summary>
    /// <remarks>
    /// <para>
    /// These are collected and pinned but never gestured, and that is a finding rather than an
    /// omission (Fhi.Metadata-cq29d). Measured in Chromium against a bare <c>&lt;details&gt;</c>: a
    /// double-click toggles twice and ends where it began, from shut and from open alike, so the
    /// invariant above holds — but a shift-click toggles once and the disclosure DOES move. That is
    /// the summary's activation behaviour, identical on every <c>&lt;details&gt;</c> on the web, and
    /// refusing it would take a <c>preventDefault</c> this package ships no script for.
    /// </para>
    /// <para>
    /// Measured in bUnit, gesturing one asserts nothing either way: a handler-free
    /// <c>&lt;summary&gt;</c> raises <c>MissingEventHandlerException</c> rather than toggling, and
    /// AngleSharp's own <c>HtmlDetailsElement</c> has no activation behaviour, so <c>open</c> never
    /// moves however it is clicked. A widened gesture sweep would be green for reasons that have
    /// nothing to do with this package — the shape the bead above exists to prevent.
    /// </para>
    /// <para>
    /// So what is asserted is the one half that IS ours: that these are still the user agent's. A
    /// handler or an <c>aria-expanded</c> on one is the package taking the toggle back, and a
    /// disclosure the package drives has to be a <see cref="Disclosure"/> the sweep gestures.
    /// </para>
    /// </remarks>
    private const string NativeDisclosure = "details > summary";

    // -----------------------------------------------------------------------
    // The sweep

    /// <summary>
    /// What every disclosure on screen says about itself, each named by its id or, where it has
    /// none, by its class.
    /// </summary>
    /// <remarks>
    /// The whole page rather than the one button pressed, because opening one adds disclosures of
    /// its own: a guard reading only the control it pressed would miss a press that moved a sibling.
    /// </remarks>
    private static string Disclosed<T>(IRenderedComponent<T> cut) where T : IComponent =>
        string.Join(
            "\n",
            cut.FindAll(Disclosure).Select(b => $"{Named(b)} = {b.GetAttribute("aria-expanded")}"));

    /// <summary>What a control is called here: its id, a summary's own rule, or its class.</summary>
    /// <remarks>
    /// One rule for both halves: a scene whose disclosures share a class would otherwise be told
    /// by the weaker of two names that the control it pressed is still the one in hand. The summary
    /// arm outranks the class arm so that every native disclosure is named the same way.
    /// </remarks>
    private static string? Named(IElement control) =>
        control.Id is { Length: > 0 } id ? id
        : control.LocalName == "summary" ? NativeNamed(control)
        : control.ClassName is { Length: > 0 } css ? css
        : null;

    /// <summary>The same rule one step out, for a <c>&lt;summary&gt;</c> the rule above cannot name.</summary>
    /// <remarks>
    /// Which is every one of them: none of the twenty-six the scenes collect has an id, and the six
    /// that have a class wear the column picker's borrowed Stiler string, which is no locator at all
    /// and which the fixture below wears too. Its own text last, because that is the label pressed.
    /// </remarks>
    private static string NativeNamed(IElement summary) =>
        summary.ParentElement is { } details && details.Id is { Length: > 0 } id
            ? $"details#{id} > summary"
            : summary.ParentElement is { ClassName: { Length: > 0 } css }
                ? $"details.{css.Replace(' ', '.')} > summary"
                : $"summary \"{summary.TextContent.Trim()}\"";

    /// <summary>Whether the control at <paramref name="at"/> is still the one that was pressed.</summary>
    private static bool Survives<T>(IRenderedComponent<T> cut, int at, string? named) where T : IComponent
    {
        var still = cut.FindAll(Disclosure);

        return still.Count > at && Named(still[at]) == named;
    }

    /// <summary>What the disclosure at <paramref name="at"/> says about itself.</summary>
    private static string? DisclosedAt<T>(IRenderedComponent<T> cut, int at) where T : IComponent =>
        cut.FindAll(Disclosure)[at].GetAttribute("aria-expanded");

    /// <summary>
    /// A pointer press on the disclosure at <paramref name="at"/>. <paramref name="clicks"/> is the
    /// browser's click count, so 2 is the second click of a double-click, and <paramref name="shift"/>
    /// is the modifier held to extend a selection to where the pointer is.
    /// </summary>
    /// <remarks>Positional, because the ids here carry a per-render instance and no two renders share one.</remarks>
    private static void Press<T>(
        IRenderedComponent<T> cut, int at, long clicks = 1, bool shift = false) where T : IComponent =>
        cut.FindAll(Disclosure)[at].Click(new MouseEventArgs { Detail = clicks, ShiftKey = shift });

    /// <summary>
    /// Whether the package, rather than the browser, is driving <paramref name="element"/>.
    /// </summary>
    /// <remarks>
    /// bUnit writes a handler out as <c>blazor:onclick</c> and the like, which is exact where
    /// clicking is not: a press on a handler-free element is dispatched to whichever ancestor has
    /// one, so "did it throw" answers for the ancestor as readily as for the control.
    /// </remarks>
    private static bool Driven(IElement element) =>
        element.Attributes.Any(a => a.Name.StartsWith("blazor:on", StringComparison.Ordinal))
        || element.HasAttribute("aria-expanded");

    /// <summary>Every native disclosure on screen the package, rather than the browser, drives.</summary>
    private static List<string?> TakenBack<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll(NativeDisclosure)
            .Where(summary => Driven(summary) || Driven(summary.ParentElement!))
            .Select(Named)
            .ToList();

    /// <summary>That every native disclosure on screen is still the user agent's.</summary>
    /// <remarks>Named rather than counted, so a failure says which one the package took back.</remarks>
    private static void AssertNativeDisclosuresAreTheUserAgents<T>(IRenderedComponent<T> cut)
        where T : IComponent =>
        Assert.Equal([], TakenBack(cut));

    /// <summary>
    /// Puts every disclosure <paramref name="scene"/> draws through both standing gestures, from
    /// shut and from open, and through the deliberate presses that must still work between them.
    /// </summary>
    /// <remarks>
    /// A scene per disclosure rather than one for all of them: closing a panel to test the control
    /// that opened it takes its neighbours down with it, and the next control would then be asked of
    /// a page that no longer draws it.
    /// </remarks>
    private static void AssertStandingGesturesAreRefused<T>(
        Func<IRenderedComponent<T>> scene, int expected, int native)
        where T : IComponent
    {
        // Pinned rather than merely non-empty: a component that stopped drawing its disclosures
        // would otherwise pass this by leaving nothing to check.
        var first = scene();
        Assert.Equal(expected, first.FindAll(Disclosure).Count);

        // The same pin for the other shape, and the whole of what this guard can ask of one.
        Assert.Equal(native, first.FindAll(NativeDisclosure).Count);
        AssertNativeDisclosuresAreTheUserAgents(first);

        for (var at = 0; at < expected; at++)
        {
            var cut = scene();

            // Prefixed with the control's own name, so a failure says which one moved rather than
            // only that the page did.
            var named = Named(cut.FindAll(Disclosure)[at]);
            var shut = Disclosed(cut);

            Press(cut, at, clicks: 2);
            Assert.Equal($"{named}\n{shut}", $"{named}\n{Disclosed(cut)}");

            Press(cut, at, shift: true);
            Assert.Equal($"{named}\n{shut}", $"{named}\n{Disclosed(cut)}");

            var closed = DisclosedAt(cut, at);

            Press(cut, at);

            // The control is live, so a guard refusing every press would fail here rather than pass
            // by never toggling at all.
            Assert.NotEqual(shut, Disclosed(cut));

            // Asked again with something open, which is the only state several of them are drawn in.
            AssertNativeDisclosuresAreTheUserAgents(cut);

            // "Vis datakilde" and "Vis datasamling" put the owner's view where the list was, taking
            // themselves with it, so there is no from-open half to ask of those two.
            if (!Survives(cut, at, named))
            {
                continue;
            }

            var open = Disclosed(cut);

            Press(cut, at, clicks: 2);
            Assert.Equal($"{named}\n{open}", $"{named}\n{Disclosed(cut)}");

            Press(cut, at, shift: true);
            Assert.Equal($"{named}\n{open}", $"{named}\n{Disclosed(cut)}");

            // And back, which is what says the guard refuses gestures rather than presses. Asked of
            // this control alone: what the page around it draws is its own scene's business.
            Press(cut, at);
            Assert.Equal(closed, DisclosedAt(cut, at));
        }
    }

    // -----------------------------------------------------------------------
    // The components that draw them

    private static readonly Guid KildeId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid DatasamlingId = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid VariableId = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid ListId = new("aaaaaaaa-0000-0000-0000-000000000004");
    private static readonly Guid OtherKildeId = new("aaaaaaaa-0000-0000-0000-000000000005");

    private class DisclosureClient : EmptyMuninExplorerClient
    {
        private static readonly KildeSummary Kilde = new()
        {
            Id = KildeId,
            Code = "K_ALS",
            Name = "Als registeret",
            ShortName = "ALS",
            Kildetype = "sentraltHelseregister",
            IsActive = true,
            DataProcessor = "Folkehelseinstituttet",
            DatasamlingCount = 1,
            TotalVariables = 42,
        };

        private static readonly VariableSummary Row = new()
        {
            Id = VariableId,
            Code = "V_BDR.ALDER",
            PreferredTerm = "Alder ved diagnose",
            KildeId = KildeId,
            KildeName = "Als registeret",
            DatasamlingId = DatasamlingId,
            DatasamlingName = "Inklusjon",
            DataType = "2",
        };

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>([Kilde]);

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new KildeHierarchy
            {
                KildeId = KildeId,
                KildeName = "Als registeret",
                TotalVariableCount = 42,
                DirectDatasamlinger =
                [
                    new HierarchyDatasamling { Id = DatasamlingId, Name = "Inklusjon", VariableCount = 42 }
                ],
            });

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(new KildeDetail { Id = KildeId, Code = "K_ALS", PreferredTerm = "Als registeret" });

        public override Task<DatasamlingDetail?> GetDatasamlingAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<DatasamlingDetail?>(new DatasamlingDetail { Id = DatasamlingId, Code = "D_ALS", PreferredTerm = "Inklusjon" });

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary> { Items = [Row], TotalCount = 1, PageNumber = 1, Size = pageSize, TotalPages = 1 });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Variable());

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodes?>(new KodeverkCodes
            {
                Codes = [new KodeverkCode { Value = "1", Name = "Ja" }],
            });

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = 1 }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items =
                [
                    new VariableListItem
                    {
                        VariableId = VariableId,
                        AddedAt = DateTimeOffset.UtcNow,
                        VariableName = "Alder ved diagnose",
                        VariableCode = "V_BDR.ALDER",
                        KildeName = "Als registeret",
                        DatasamlingName = "Inklusjon",
                    }
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
    }

    /// <summary>The variable behind the row, with both owners and a kodeverk the panel can open.</summary>
    private static VariableDetail Variable() => new()
    {
        Id = VariableId,
        Code = "V_BDR.ALDER",
        PreferredTerm = "Alder ved diagnose",
        KildeId = KildeId,
        KildeName = "Als registeret",
        DatasamlingId = DatasamlingId,
        DatasamlingName = "Inklusjon",
        DataType = "2",
        KodeverkLinks =
        [
            new KodeverkLink
            {
                KodeverkType = "Kildekodeverk",
                KodeverkReference = "2336",
                DisplayName = "Ja/Nei",
                HasCodeValues = true,
            }
        ],
    };

    [Fact]
    public void KildeSearch_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Two: the facet panel's fold control and the row's chevron.
        // Three native: the Kildetype and Databehandler facet folds, and the column picker.
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());

        AssertStandingGesturesAreRefused(() => Render<KildeSearch>(), expected: 2, native: 3);
    }

    [Fact]
    public void KildeSearchSelectable_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // The same two, and the same three native: the mode a host wiring ExploreVariablesRequested
        // gets adds a column of tick boxes and no disclosure of either shape, and the scene above
        // never enters those branches to say so.
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());

        AssertStandingGesturesAreRefused(
            () => Render<KildeSearch>(b => b.Add(
                c => c.ExploreVariablesRequested, (IReadOnlyList<Guid> _) => { })),
            expected: 2,
            native: 3);
    }

    [Fact]
    public void VariableSearch_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Five: Vis filtre, the row's name button, and three that exist only once a row is open —
        // "Vis datakilde", "Vis datasamling" and, on the Data tab, "Vis koder".
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());
        Services.AddScoped<VariableListState>();

        // Four native: the Variabelgruppe and Andre filtre folds, the legend, and the column picker.
        AssertStandingGesturesAreRefused(OpenPanelOnData, expected: 5, native: 4);
    }

    [Fact]
    public void VariableSearchFilterTree_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Four, and two of them are the facet tree's own: the scene above answers the filters
        // endpoint with nothing, so its panel draws no tree at all and the branch disclosures added
        // by Fhi.Metadata-adog5 were swept by neither half of this guard.
        Services.AddSingleton<IMuninExplorerClient>(new FacetTreeClient());
        Services.AddScoped<VariableListState>();

        // Six native: the four of the scene above, plus the tree's own Type datakilde and Kilde.
        AssertStandingGesturesAreRefused(() => Render<VariableSearch>(), expected: 4, native: 6);
    }

    [Fact]
    public void KildeSearchLongFacet_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Two: the facet panel's fold control and the cap's own "Vis N til", which is drawn only
        // past the threshold and so is in neither scene above. No row chevron in this one — these
        // kilder have no datasamlinger, which is what keeps the count exact.
        Services.AddSingleton<IMuninExplorerClient>(new LongFacetClient());

        // Three native, the same three as the scenes above: the cap is drawn inside the Databehandler
        // fold rather than beside it, so a facet past the threshold adds no <details> of its own.
        AssertStandingGesturesAreRefused(() => Render<KildeSearch>(), expected: 2, native: 3);
    }

    [Fact]
    public void VariableSearchLongFacet_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Three: Vis filtre, the row's chevron, and the cap's "Vis N til" on the kilde facet. The
        // kilder have nothing under them, so no branch disclosure joins the count.
        Services.AddSingleton<IMuninExplorerClient>(new LongKildeFacetClient());
        Services.AddScoped<VariableListState>();

        // Six native, the same six as the filter-tree scene: the kilde facet draws its Type datakilde
        // and Kilde folds whether or not the cap is past the threshold.
        AssertStandingGesturesAreRefused(() => Render<VariableSearch>(), expected: 3, native: 6);
    }

    [Fact]
    public void VariableListFilters_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // One: the cap's "Vis N til". It is the only disclosure this panel has ever drawn, which is
        // why the panel joins the sweep with the cap rather than before it.
        Services.AddSingleton<IMuninExplorerClient>(new LongListClient());
        Services.AddScoped<VariableListState>();

        // No native: this panel draws its one facet as a labelled group rather than a <details>, and
        // the column picker belongs to the table it sits beside rather than to the panel.
        AssertStandingGesturesAreRefused(
            () => Render<VariableListFilters>(b => b.Add(c => c.IsAuthenticated, true)),
            expected: 1,
            native: 0);
    }

    /// <summary>Kelda's list, with a databehandler facet longer than the panel draws.</summary>
    /// <remarks>
    /// No datasamlinger, so no row draws a chevron: the scene above is about the panel, and a
    /// dozen row disclosures would make its count a statement about the table instead.
    /// </remarks>
    private sealed class LongFacetClient : DisclosureClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
            [
                .. Enumerable.Range(0, 12).Select(index => new KildeSummary
                {
                    Id = new Guid($"dddddddd-0000-0000-0000-{index:000000000000}"),
                    Code = $"K_{index:00}",
                    Name = $"Kilde {index:00}",
                    Kildetype = "sentraltHelseregister",
                    IsActive = true,
                    DataProcessor = $"Databehandler {index:00}",
                    DatasamlingCount = 0,
                    TotalVariables = 1,
                })
            ]);
    }

    /// <summary>The variabelutforsker's filters, with a kilde facet longer than the panel draws.</summary>
    /// <remarks>
    /// One kildetype, so the kilder are lifted out of a group heading that would be the only one
    /// and stand at the facet's top level, which is where the cap applies.
    /// </remarks>
    private sealed class LongKildeFacetClient : DisclosureClient
    {
        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FilterOptions
            {
                KildeTyper =
                [
                    new() { Value = "sentraltHelseregister", DisplayName = "Sentralt helseregister", Count = 12 }
                ],
                Kilder =
                [
                    .. Enumerable.Range(0, 12).Select(index => new Contracts.KildeFacet
                    {
                        Id = new Guid($"dddddddd-0000-0000-0000-{index:000000000000}"),
                        Name = $"Kilde {index:00}",
                        KildeType = "sentraltHelseregister",
                        Count = 1,
                    })
                ],
                TotalCount = 12,
            });
    }

    /// <summary>A saved list drawing on more kilder than its own panel draws.</summary>
    private sealed class LongListClient : DisclosureClient
    {
        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            List<VariableListItem> items =
            [
                .. Enumerable.Range(0, 12).Select(index => new VariableListItem
                {
                    VariableId = Guid.NewGuid(),
                    AddedAt = DateTimeOffset.UtcNow,
                    VariableName = $"Variabel {index:00}",
                    VariableCode = $"V{index:00}",
                    KildeId = new Guid($"dddddddd-0000-0000-0000-{index:000000000000}"),
                    KildeName = $"Kilde {index:00}",
                })
            ];

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = items,
                TotalCount = items.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
        }
    }

    /// <summary>The same client, answering the filters endpoint with a tree two levels deep.</summary>
    /// <remarks>
    /// Two kildetyper, because one is lifted away: the panel drops a group heading that would be
    /// the only one, so a single-kildetype payload draws no group row to gesture.
    /// </remarks>
    private sealed class FacetTreeClient : DisclosureClient
    {
        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FilterOptions
            {
                KildeTyper =
                [
                    new() { Value = "sentraltHelseregister", DisplayName = "Sentralt helseregister", Count = 1 },
                    new() { Value = "biobank", DisplayName = "Biobank", Count = 1 }
                ],
                Kilder =
                [
                    new() { Id = KildeId, Name = "Als registeret", KildeType = "sentraltHelseregister", Count = 1 },
                    new() { Id = OtherKildeId, Name = "Tromsøundersøkelsen", KildeType = "biobank", Count = 1 }
                ],
                Datasamlinger =
                [
                    new() { Id = DatasamlingId, Name = "Inklusjon", KildeId = KildeId, Count = 1 }
                ],
                TotalCount = 2,
            });
    }

    /// <summary>The explorer with its first row open and the Data tab chosen, where kodeverk live.</summary>
    private IRenderedComponent<VariableSearch> OpenPanelOnData()
    {
        var cut = Render<VariableSearch>();

        // Data is the tab a panel opens on (Fhi.Metadata-l9l2n.101), so no tab press is needed.
        cut.Find("button.munin-explorer-dataitem-main__name").Click(new MouseEventArgs { Detail = 1 });

        return cut;
    }

    [Fact]
    public void VariableView_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // One: a version row in the history. None native — a version row is a button.
        var detail = Variable() with
        {
            VersionId = Guid.NewGuid(),
            Versions = [new VariableVersion { VersionId = Guid.NewGuid(), PreferredTerm = "Alder ved diagnose" }],
        };

        AssertStandingGesturesAreRefused(
            () => Render<VariableView>(b => b.Add(c => c.Variable, detail)), expected: 1, native: 0);
    }

    [Fact]
    public void VariableListView_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Seven: create, open a shared list, rename, copy, the delete confirmation, the empty
        // confirmation and share. One native: the download fold.
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());
        Services.AddScoped<VariableListState>();

        AssertStandingGesturesAreRefused(
            () => Render<VariableListView>(b => b.Add(c => c.IsAuthenticated, true)),
            expected: 7,
            native: 1);
    }

    // -----------------------------------------------------------------------
    // Completeness

    /// <summary>The components the scenes above render, and whose files may therefore draw one.</summary>
    /// <remarks>
    /// Types rather than file names, so the check below cannot be satisfied by appending a string to
    /// it. What it establishes is only where a disclosure lives: it is the scenes that gesture one,
    /// and each renders the states it renders, so a branch none of them enters is in neither half.
    /// </remarks>
    private static readonly Type[] Swept =
    [
        typeof(KildeSearch),
        typeof(VariableListFilters),
        typeof(VariableListView),
        typeof(VariableSearch),
        typeof(VariableView),
    ];

    /// <summary>The component sources, which are the only place a disclosure can be drawn.</summary>
    /// <remarks>
    /// Blazor/ rather than the project, because obj/ holds generated sources that would answer for
    /// the files they were generated from.
    /// </remarks>
    private static IEnumerable<string> Sources() =>
        Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer", "Blazor"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                           || path.EndsWith(".cs", StringComparison.Ordinal));

    [Fact]
    public void Disclosures_Always_ThenEveryOneIsInAComponentTheSweepRenders()
    {
        var elsewhere = Sources()
            .Where(path => Renders(File.ReadAllText(path)))
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !Swept.Any(c => name.StartsWith($"{c.Name}.", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Named rather than counted, so a failure says which file drew a disclosure the sweep has
        // no scene for.
        Assert.Equal([], elsewhere);
    }

    /// <summary>Whether <paramref name="source"/> renders the attribute, rather than mentioning it.</summary>
    /// <remarks>
    /// Comments are stripped first: these files explain this very rule in prose, so a check a
    /// paragraph can satisfy is a check prose can switch off.
    /// </remarks>
    private static bool Renders(string source)
    {
        var code = WithoutProse(source);

        return code.Contains("aria-expanded=", StringComparison.Ordinal)
               || code.Contains("\"aria-expanded\"", StringComparison.Ordinal);
    }

    /// <summary><paramref name="source"/> with the comments of both kinds blanked out.</summary>
    /// <remarks>
    /// The doc comments matter to the native half rather than to <see cref="Renders"/>: every file
    /// here spells <c>&lt;summary&gt;</c> dozens of times in its XML docs, and every one of those
    /// would read as a disclosure.
    /// </remarks>
    private static string WithoutProse(string source) =>
        Regex.Replace(
            RazorSource.WithoutComments(source), @"^\s*///?.*$", " ", RegexOptions.Multiline);

    [Fact]
    public void NativeDisclosures_Always_ThenTheUserAgentStillOwnsEveryOne()
    {
        // Every file rather than the swept ones, because this half needs no scene, and three of the
        // seven that draw a native disclosure are in none — DetailBlocks, KildeHierarchyView and
        // KildeView, whose disclosures the sweep above therefore never collects.
        var taken = Sources()
            .SelectMany(path => TakenIn(File.ReadAllText(path))
                .Select(one => $"{Path.GetFileName(path)}: {one}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Named rather than counted, so a failure says which element in which file, with the
        // attribute that took it back.
        Assert.Equal([], taken);
    }

    /// <summary>A native disclosure as this package draws one, read no further than the element.</summary>
    /// <remarks>
    /// Deliberately wider than what <see cref="NativeDisclosuresIn"/> can read, so that the two
    /// disagreeing says the scan walked past one — which is the only way a source check of this
    /// shape fails, since a pattern that stops matching reports nothing rather than a hole.
    /// </remarks>
    private const string DrawnPattern =
        """<(?:details|summary)\b|OpenElement\([^;]*?,\s*"(?:details|summary)"\s*\)""";

    /// <summary>Each <c>&lt;details&gt;</c> and <c>&lt;summary&gt;</c> in <paramref name="source"/>, with what it is opened with.</summary>
    /// <remarks>
    /// Both spellings, because this package writes markup two ways: a Razor tag, and a
    /// <c>RenderTreeBuilder</c> whose <c>AddAttribute</c> calls up to the next element or content
    /// are the ones belonging to it. The sequence number is an expression rather than a literal
    /// because <c>seq</c>, <c>seq + 1</c> and <c>seq++</c> are all live in these files.
    /// </remarks>
    private static IEnumerable<(string Element, string Attributes)> NativeDisclosuresIn(string source)
    {
        var code = WithoutProse(source);

        foreach (Match tag in Regex.Matches(code, @"<(details|summary)\b([^>]*)>"))
        {
            yield return (tag.Groups[1].Value, tag.Groups[2].Value);
        }

        foreach (Match opened in Regex.Matches(
                     code,
                     """OpenElement\([^;"]*,\s*"(details|summary)"\s*\)\s*;(.*?)(?=\s*\w+\.(?:OpenElement|OpenComponent|AddContent|AddMarkupContent|CloseElement)\()""",
                     RegexOptions.Singleline))
        {
            yield return (opened.Groups[1].Value, opened.Groups[2].Value);
        }
    }

    [Fact]
    public void NativeDisclosureScan_Always_ThenItReadsEveryOneTheSourceDraws()
    {
        var unread = Sources()
            .Select(path => (Name: Path.GetFileName(path)!, Source: File.ReadAllText(path)))
            .Select(file => (
                file.Name,
                Drawn: Regex.Matches(WithoutProse(file.Source), DrawnPattern).Count,
                Read: NativeDisclosuresIn(file.Source).Count()))
            .Where(file => file.Read != file.Drawn)
            .Select(file => $"{file.Name}: {file.Drawn} drawn, {file.Read} read")
            .Order(StringComparer.Ordinal)
            .ToList();

        // Named rather than counted, so a failure says which file holds the shape the scan cannot
        // read, and which way round the two disagree.
        Assert.Equal([], unread);

        // And pinned, because a scan reading nothing agrees with a source drawing nothing — which
        // is what a wrong path, or a rewrite past both patterns at once, would leave behind.
        Assert.Equal(
            NativeDisclosuresDrawn,
            Sources().Sum(path => NativeDisclosuresIn(File.ReadAllText(path)).Count()));
    }

    /// <summary>How many <c>&lt;details&gt;</c> and <c>&lt;summary&gt;</c> elements <c>src/</c> draws.</summary>
    private const int NativeDisclosuresDrawn = 16;

    /// <summary>Whether what a native disclosure was opened with means the package drives it.</summary>
    /// <remarks>
    /// A handler in any spelling this package writes one in, or an <c>aria-expanded</c> — on a
    /// <c>&lt;details&gt;</c> a second opinion about <c>open</c> that drifts from it. A splat and a
    /// computed name read as driven: unreadable is loud here, where a missed spelling is silent.
    /// </remarks>
    private static bool TakenFromTheUserAgent(string attributes) =>
        Regex.IsMatch(
            attributes,
            """@on[a-z]+|\bon[a-z]+\s*=|@attributes\b|aria-expanded"""
            + """|Add\w*Attributes?\([^,]+,\s*(?:"on[a-z]+"|[^\s"])""");

    /// <summary>The source half over one source: each native disclosure it reads as driven.</summary>
    private static IEnumerable<string> TakenIn(string source) =>
        NativeDisclosuresIn(source)
            .Where(opened => TakenFromTheUserAgent(opened.Attributes))
            .Select(opened => $"<{opened.Element} {Regex.Replace(opened.Attributes, @"\s+", " ").Trim()}>");

    [Theory]
    // A scan only ever run over clean sources asserts nothing: both detectors were replaceable by
    // `false` with every other test in this class green (Fhi.Metadata-cq29d). So the taken shapes
    // are fed in directly, in both the spellings this package writes markup in.
    [InlineData("""<details><summary>Kolonner</summary></details>""", 2, 0)]
    [InlineData("""<details @key="K" open="@Open"><summary>Kolonner</summary></details>""", 2, 0)]
    [InlineData("""<details><summary @onclick="Toggle">Kolonner</summary></details>""", 2, 1)]
    [InlineData("""<details @ontoggle="Toggle"><summary>Kolonner</summary></details>""", 2, 1)]
    [InlineData("""<details onclick="toggle()"><summary>Kolonner</summary></details>""", 2, 1)]
    [InlineData("""<details aria-expanded="true"><summary>Kolonner</summary></details>""", 2, 1)]
    // And the two a name alone gets wrong in opposite directions: a splat, whose attributes are
    // somewhere else entirely, and a class value that merely begins the way an event name does.
    [InlineData("""<details @attributes="Extra"><summary>Kolonner</summary></details>""", 2, 1)]
    [InlineData("""<details class="once"><summary>Kolonner</summary></details>""", 2, 0)]
    // And the builder, where the attributes are statements rather than an attribute list — so a
    // lookahead that stopped one call early would read the element and score it clean.
    [InlineData(
        """b.OpenElement(4, "details"); b.AddAttribute(5, "class", "dropdown"); b.OpenElement(6, "summary"); b.AddContent(7, label);""",
        2, 0)]
    [InlineData(
        """b.OpenElement(seq + 1, "details"); b.AddAttribute(seq + 2, "class", Fold); b.CloseElement();""",
        1, 0)]
    [InlineData("""b.OpenElement(4, "details"); b.AddAttribute(5, "ontoggle", cb); b.CloseElement();""", 1, 1)]
    [InlineData("""b.OpenElement(4, "summary"); b.AddAttribute(5, name, value); b.CloseElement();""", 1, 1)]
    [InlineData("""b.OpenElement(4, "details"); b.AddMultipleAttributes(5, extra); b.CloseElement();""", 1, 1)]
    [InlineData("""b.OpenElement(4, "details"); b.AddAttribute(5, "aria-expanded", "true"); b.CloseElement();""", 1, 1)]
    [InlineData("nothing here draws a disclosure", 0, 0)]
    public void Fixture_WhenTheSourceHalfReadsIt_ThenOnlyADrivenDisclosureIsTaken(
        string source, int drawn, int taken)
    {
        // Both, because a scan reading nothing scores nothing taken — which is the clean fixtures
        // passing for the reason the sources they stand in for must not.
        Assert.Equal(drawn, NativeDisclosuresIn(source).Count());
        Assert.Equal(taken, TakenIn(source).Count());
    }

    /// <summary>A native disclosure the package drives, in each of the two places it can be driven.</summary>
    /// <remarks>
    /// A fixture rather than a scene: every page this suite renders is clean, so nothing else here
    /// runs <see cref="Driven"/> down a matching path and it was <c>false</c>-replaceable too.
    /// </remarks>
    private sealed class TakenBackDisclosure : ComponentBase
    {
        [Parameter] public bool OnTheSummary { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "details");

            if (!OnTheSummary)
            {
                builder.AddAttribute(1, "aria-expanded", "true");
            }

            builder.OpenElement(2, "summary");

            // The column picker's own borrowed string, which is what all four real ones wear: the
            // weaker of the two names here, and the one Named must not answer with.
            builder.AddAttribute(3, "class",
                "hd-button-square button-square--ghost munin-explorer-header__actions-button");

            if (OnTheSummary)
            {
                builder.AddAttribute(
                    4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => { }));
            }

            builder.AddContent(5, "Kolonner");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeDisclosure_WhenThePackageDrivesIt_ThenTheRenderedHalfNamesIt(bool onTheSummary)
    {
        var cut = Render<TakenBackDisclosure>(b => b.Add(c => c.OnTheSummary, onTheSummary));

        // The name rather than the count, because naming it is the whole of what this half offers
        // a reader — and the label it reports is the one on screen rather than the borrowed class
        // the summary also carries, which four picker folds would otherwise share.
        Assert.Equal(["summary \"Kolonner\""], TakenBack(cut));
    }
}
