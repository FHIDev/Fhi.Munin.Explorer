using System.Reflection;
using Bunit;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Which of Kelda's two roots a host gets when it mounts one by name, and what each one then does
/// with <c>?kilde=</c> (Fhi.Metadata-8uwtd).
/// </summary>
/// <remarks>
/// THE TRAP: helsedata mounts by a type name typed into a CMS field, resolved with
/// <c>Type.GetType</c> and handed a fixed candidate dictionary — <c>Language</c>, <c>SkjemaId</c>,
/// <c>IsAuthenticated</c> — that <c>KomponentParameterVelger</c> filters against the type's own
/// <c>[Parameter]</c> properties. So the mount is a string, not a compile-time reference, and a
/// test that mounts generically would stay green through a rename that silently changed which
/// component that string reaches. Hence: resolved by name, parameters set by name, asserted on the
/// rendered markup and on what reached <c>history.replaceState</c>.
/// </remarks>
public class KildeMountTest : BunitContext
{
    private const string ReplaceState = "history.replaceState";

    private const string Assembly = "Fhi.Munin.Explorer";

    private static readonly Guid KildeId = Guid.NewGuid();

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

    /// <summary>The type a CMS field holding <paramref name="name"/> resolves to, or null.</summary>
    private static Type? Resolve(string name) =>
        Type.GetType($"Fhi.Munin.Explorer.Blazor.{name}, {Assembly}");

    private static Type Mount(string name) =>
        Resolve(name) ?? throw new InvalidOperationException(
            $"'{name}' does not resolve, so a host naming it in a CMS field gets nothing.");

    /// <summary>The last URL the component mirrored, or null when it never wrote one.</summary>
    private string? Mirrored() =>
        JSInterop.Invocations[ReplaceState] is { Count: > 0 } calls
            ? calls[^1].Arguments[2] as string
            : null;

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>
    /// Mount <paramref name="name"/> the way the CMS does — resolved from a string, parameters set
    /// by string — with the browser at <paramref name="url"/>.
    /// </summary>
    /// <remarks>
    /// Registering the client and setting the renderer info come before the navigation: bUnit seals
    /// its service collection the first time anything is resolved from it, and reaching for the
    /// NavigationManager is a resolve.
    /// </remarks>
    private IRenderedComponent<IComponent> MountByName(
        string name, string url, string? variableExplorerPath = null)
    {
        var component = Mount(name);

        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient());
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Navigation.NavigateTo(url);

        return Render(builder =>
        {
            builder.OpenComponent(0, component);
            builder.AddComponentParameter(1, "Language", "no");

            if (variableExplorerPath is { } path)
            {
                builder.AddComponentParameter(2, "VariableExplorerPath", path);
            }

            builder.CloseComponent();
        });
    }

    private static void OpenTheKilde(IRenderedComponent<IComponent> cut) =>
        cut.Find(".munin-explorer-kilder tbody th button").Click();

    // -----------------------------------------------------------------------
    // The name a host reaches for first is the one that owns the address bar.

    [Fact]
    public void KildeExplorer_WhenAHostMountsItByNameAndOpensAKilde_ThenTheKildeIsInTheAddressBar()
    {
        var cut = MountByName("KildeExplorer", "http://localhost/kilder", "/variabler");

        OpenTheKilde(cut);

        Assert.Equal($"/kilder?kilde={KildeId}", Mirrored());
    }

    [Fact]
    public void KildeExplorer_WhenALinkCarriesAKilde_ThenThatKildeIsOpenWithNoQueryParsingByTheHost()
    {
        var cut = MountByName("KildeExplorer", $"http://localhost/kilder?kilde={KildeId}");

        Assert.Contains("Als registeret", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void KildeExplorer_WhenTheHostCanOnlySetLanguage_ThenTheListRendersAndNoHandoverIsOffered()
    {
        // The CMS mount exactly: VariableExplorerPath is not in helsedata's candidate list and
        // cannot be, so null has to be a page rather than a crash. No column is the safe answer —
        // a button leading to a page that host may not have would be worse.
        var cut = MountByName("KildeExplorer", "http://localhost/kilder");

        Assert.Contains("Als registeret", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".munin-explorer-kilder__select"));
    }

    [Fact]
    public void KildeExplorer_WhenTheHostGivesAVariableExplorerPath_ThenTheSelectionColumnIsDrawn()
    {
        var cut = MountByName("KildeExplorer", "http://localhost/kilder", "/variabler");

        Assert.NotEmpty(cut.FindAll(".munin-explorer-kilder__select"));
    }

    // -----------------------------------------------------------------------
    // The bare component keeps the compose-your-own route, and says so by doing nothing.

    [Fact]
    public void KildeSearch_WhenAHostMountsItByNameAndOpensAKilde_ThenNothingIsWrittenToTheAddressBar()
    {
        var cut = MountByName("KildeSearch", "http://localhost/kilder");

        OpenTheKilde(cut);

        Assert.NotEmpty(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Null(Mirrored());
    }

    [Fact]
    public void KildeSearch_WhenALinkCarriesAKilde_ThenItLandsOnTheListRatherThanTheKilde()
    {
        var cut = MountByName("KildeSearch", $"http://localhost/kilder?kilde={KildeId}");

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void KildeSearch_WhenItIsResolvedByName_ThenItIsStillPublicAndMountable() =>
        Assert.NotNull(Resolve("KildeSearch"));

    // -----------------------------------------------------------------------
    // The rename, from the only angle a host feels it.

    [Fact]
    public void KildeExplorerWithUrlState_WhenAHostKeepsTheOldName_ThenItResolvesToNothing()
    {
        // A CMS field is a string: the old name does not fail to compile, it fails to render. The
        // changelog's host note is the only warning there is, so this pins that the name is gone.
        Assert.Null(Resolve("KildeExplorerWithUrlState"));
    }

    [Fact]
    public void KildeExplorer_WhenTheCmsReadsItsParameters_ThenTheOnesAHostCanSetAreDeclaredOnIt()
    {
        // Read the way KomponentParameterVelger reads it, on the type the CMS field names.
        var declared = Mount("KildeExplorer")
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: false))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Language", declared);
        Assert.Contains("ShowAccessAndPrices", declared);
        Assert.Contains("VariableExplorerPath", declared);
    }
}
