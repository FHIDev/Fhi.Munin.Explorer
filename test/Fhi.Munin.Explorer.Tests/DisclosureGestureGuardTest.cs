using System.Text.RegularExpressions;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// One rule over every disclosure this package draws: a gesture that stands still — the second
/// click of a double-click, or a shift-click extending a selection — never changes what any of them
/// discloses, and a deliberate press still toggles.
/// </summary>
/// <remarks>
/// Written because the same defect was found by hand three times — the kilde chevron
/// (Fhi.Metadata-l9l2n.72), five more disclosures (Fhi.Metadata-j1j3i), then four more
/// (Fhi.Metadata-zel47) — and nothing in the suite enumerated them, so each round only ever fixed
/// the instances someone had thought to look at. A new
/// <c>&lt;button aria-expanded @onclick="() =&gt; _flag = !_flag"&gt;</c> compiles, renders and
/// passes every other test here.
/// </remarks>
public class DisclosureGestureGuardTest : BunitContext
{
    /// <summary>What a disclosure is, for both halves of this guard: the attribute, not a class.</summary>
    private const string Disclosure = "button[aria-expanded]";

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
            cut.FindAll(Disclosure).Select(b =>
                $"{(b.Id is { Length: > 0 } id ? id : b.ClassName)} = {b.GetAttribute("aria-expanded")}"));

    /// <summary>Whether the control at <paramref name="at"/> is still the one that was pressed.</summary>
    private static bool Survives<T>(IRenderedComponent<T> cut, int at, string? named) where T : IComponent
    {
        var still = cut.FindAll(Disclosure);

        return still.Count > at && still[at].ClassName == named;
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
    /// Puts every disclosure <paramref name="scene"/> draws through both standing gestures, from
    /// shut and from open, and through the deliberate presses that must still work between them.
    /// </summary>
    /// <remarks>
    /// A scene per disclosure rather than one for all of them: closing a panel to test the control
    /// that opened it takes its neighbours down with it, and the next control would then be asked of
    /// a page that no longer draws it.
    /// </remarks>
    private static void AssertStandingGesturesAreRefused<T>(Func<IRenderedComponent<T>> scene, int expected)
        where T : IComponent
    {
        // Pinned rather than merely non-empty: a component that stopped drawing its disclosures
        // would otherwise pass this by leaving nothing to check.
        Assert.Equal(expected, scene().FindAll(Disclosure).Count);

        for (var at = 0; at < expected; at++)
        {
            var cut = scene();

            // Prefixed with the control's own name, so a failure says which one moved rather than
            // only that the page did.
            var named = cut.FindAll(Disclosure)[at].ClassName;
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

    private sealed class DisclosureClient : EmptyMuninExplorerClient
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
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());

        AssertStandingGesturesAreRefused(() => Render<KildeSearch>(), expected: 2);
    }

    [Fact]
    public void VariableSearch_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Four, and three of them only exist once a row is open: the row's own name, then
        // "Vis datakilde", "Vis datasamling" and — on the Data tab — "Vis koder".
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());
        Services.AddScoped<VariableListState>();

        AssertStandingGesturesAreRefused(OpenPanelOnData, expected: 4);
    }

    /// <summary>The explorer with its first row open and the Data tab chosen, where kodeverk live.</summary>
    private IRenderedComponent<VariableSearch> OpenPanelOnData()
    {
        var cut = Render<VariableSearch>();

        cut.FindAll(Disclosure)[0].Click(new MouseEventArgs { Detail = 1 });
        cut.FindAll(".munin-explorer-meta__tabs [role=tab]")[1].Click();

        return cut;
    }

    [Fact]
    public void VariableView_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // One: a version row in the history.
        var detail = Variable() with
        {
            VersionId = Guid.NewGuid(),
            Versions = [new VariableVersion { VersionId = Guid.NewGuid(), PreferredTerm = "Alder ved diagnose" }],
        };

        AssertStandingGesturesAreRefused(
            () => Render<VariableView>(b => b.Add(c => c.Variable, detail)), expected: 1);
    }

    [Fact]
    public void VariableListView_WhenEveryDisclosureIsGestured_ThenNoneOfThemMoves()
    {
        // Three: create, rename and the delete confirmation.
        Services.AddSingleton<IMuninExplorerClient>(new DisclosureClient());
        Services.AddScoped<VariableListState>();

        AssertStandingGesturesAreRefused(
            () => Render<VariableListView>(b => b.Add(c => c.IsAuthenticated, true)), expected: 3);
    }

    // -----------------------------------------------------------------------
    // Completeness

    /// <summary>Every source file above that renders an <c>aria-expanded</c> control.</summary>
    /// <remarks>
    /// The sweep only sees the components it renders, so a disclosure added to a fifth component
    /// would sail past it. This is what fails instead — loudly, and at the point the disclosure is
    /// written rather than the next time someone double-clicks it.
    /// </remarks>
    private static readonly string[] Swept =
    [
        "KildeSearch.razor",
        "VariableListView.razor",
        "VariableSearch.DetailPanel.cs",
        "VariableSearch.razor",
        "VariableSearch.razor.cs",
        "VariableView.Versions.cs",
    ];

    [Fact]
    public void Disclosures_Always_ThenEveryOneIsInAComponentTheSweepRenders()
    {
        // Blazor/ rather than the project, because obj/ holds generated sources that would answer
        // for the files they were generated from.
        var drawing = Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer", "Blazor"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                           || path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => Renders(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Compared against the list rather than asserted a subset, so a component that stops drawing
        // one leaves a scene above checking nothing and says so.
        Assert.Equal(Swept, drawing);
    }

    /// <summary>Whether <paramref name="source"/> renders the attribute, rather than mentioning it.</summary>
    /// <remarks>
    /// Comments are stripped first: these files explain this very rule in prose, so a check a
    /// paragraph can satisfy is a check prose can switch off.
    /// </remarks>
    private static bool Renders(string source)
    {
        var code = Regex.Replace(
            RazorSource.WithoutComments(source), @"^\s*///?.*$", " ", RegexOptions.Multiline);

        return code.Contains("aria-expanded=", StringComparison.Ordinal)
               || code.Contains("\"aria-expanded\"", StringComparison.Ordinal);
    }
}
