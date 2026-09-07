using System.Reflection;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Whether a CMS host can actually reach <see cref="KildeSearch.ShowAccessAndPrices"/>, on both
/// of Kelda's mounts, and whether the page follows it either way (Fhi.Metadata-ay3zz).
/// </summary>
/// <remarks>
/// THE TRAP: helsedata's <c>KomponentParameterVelger</c> drops every candidate the mounted type
/// does not declare as a public <c>[Parameter]</c>, silently, so a parameter declared on the wrong
/// type compiles and the blocks appear anyway. Hence: both roots, set by string name, asserted on
/// the markup — and both values, since off is also what a parameter nobody reads produces.
/// </remarks>
public class KildeHostParameterTest : BunitContext
{
    private const string Parameter = "ShowAccessAndPrices";

    private const string AccessCriteria = "Kriterier for tilgang til data";

    private const string Prices = "Priser";

    private static readonly Guid KildeId = Guid.NewGuid();

    /// <summary>One kilde, so there is a row to open and a detail to draw the sections over.</summary>
    private sealed class OneKildeClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
                [new KildeSummary { Id = KildeId, Name = "Als registeret", Code = "K_ALS" }]);

        public override Task<KildeDetail?> GetKildeAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(
                id == KildeId ? new KildeDetail { Id = id, PreferredTerm = "Als registeret" } : null);
    }

    /// <summary>
    /// Mount <paramref name="component"/> the way the CMS does: by string name, with only the
    /// candidates <c>BlazorComponentPage</c> offers plus the one under test, then open the kilde.
    /// </summary>
    private IRenderedComponent<IComponent> OpenKilde(Type component, bool? show)
    {
        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient());
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render(builder =>
        {
            builder.OpenComponent(0, component);
            builder.AddComponentParameter(1, "Language", "no");

            if (show is { } value)
            {
                builder.AddComponentParameter(2, Parameter, value);
            }

            builder.CloseComponent();
        });

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        return cut;
    }

    public static TheoryData<Type> Mounts() => [typeof(KildeSearch), typeof(KildeExplorer)];

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Mount_WhenItIsRead_ThenItDeclaresTheParameterTheCmsWouldSet(Type mount)
    {
        // Read the way KomponentParameterVelger reads it — a public instance property carrying
        // [Parameter], matched on the name Ordinal — rather than through a compile-time reference,
        // which is what a rename would keep green while the CMS silently stopped setting anything.
        var declared = mount
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: false))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(Parameter, declared);
    }

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Mount_WhenTheHostAsksForTheBlocks_ThenTheyAreOnThePage(Type mount)
    {
        var cut = OpenKilde(mount, show: true);

        Assert.Contains(AccessCriteria, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(Prices, cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Mount_WhenTheHostAsksForNothing_ThenTheBlocksAreNotOnThePage(Type mount)
    {
        // The embedded host: helsedata's candidate list carries Language, SkjemaId and
        // IsAuthenticated and nothing else, so this is what its page renders today without a line
        // changing on their side. The default has to be off for that to be the safe answer.
        var cut = OpenKilde(mount, show: null);

        Assert.DoesNotContain(AccessCriteria, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(Prices, cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Mount_WhenTheHostAsksForThemOff_ThenTheBlocksAreNotOnThePage(Type mount)
    {
        var cut = OpenKilde(mount, show: false);

        Assert.DoesNotContain(AccessCriteria, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(Prices, cut.Markup, StringComparison.Ordinal);
    }
}
