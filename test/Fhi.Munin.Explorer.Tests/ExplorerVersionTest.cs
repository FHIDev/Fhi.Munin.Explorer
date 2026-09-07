using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The version every root element carries in <c>data-munin-explorer-version</c>, and the one thing
/// that has to be true of it: it is the ASSEMBLY's version rather than a string somebody wrote down.
/// </summary>
/// <remarks>
/// <para>
/// Asserting that the attribute is present and non-empty would pass against a hardcoded literal, a
/// stale csproj property or a placeholder — each of which renders a perfectly convincing version
/// while telling the reader something false. That is worse than the silence it replaces, because a
/// reader diagnosing helsedata's deployment will believe it. So the expectation below is read off
/// <see cref="AssemblyInformationalVersionAttribute"/> at runtime, and
/// <see cref="Version_WhenItIsRendered_ThenItIsNotTheAssemblyVersionThatDropsThePrereleaseSuffix"/>
/// rules out the near miss that would survive that: <c>AssemblyVersion</c> is <c>0.1.0.0</c> for
/// every alpha, so a component reading it could not tell alpha.7 from alpha.8.
/// </para>
/// <para>
/// The mount points are enumerated rather than sampled. A version readable on the variable explorer
/// and absent on the kildeutforsker is a trap for whoever checks the second one, and the source
/// guard at the bottom is what covers a root component nobody has written a render test for yet.
/// </para>
/// <para>
/// Anonymous is its own case, twice over. The explorer renders for logged-out visitors on
/// helsedata.no, and that view is the one most often reported as broken — a version that needed a
/// session would be absent from exactly the page somebody is asking about. (Fhi.Metadata-sqbei)
/// </para>
/// </remarks>
public class ExplorerVersionTest : BunitContext
{
    private const string Attribute = "data-munin-explorer-version";

    /// <summary>
    /// What the attribute must equal, read off the assembly rather than off the component.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <c>ExplorerVersion.Current</c>: comparing the component against the code
    /// that feeds it would pass whatever that code says, including a literal.
    /// </remarks>
    private static string Expected =>
        typeof(VariableExplorer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException(
            "The RCL has no AssemblyInformationalVersion, so this test cannot say what the " +
            "attribute should hold. That is a build configuration failure, not a component one.");

    private sealed class OneKildeClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
                [new KildeSummary { Id = Guid.NewGuid(), Code = "K_ALS", Name = "Als registeret" }]);
    }

    private void Prepare(IMuninExplorerClient client)
    {
        Services.AddSingleton(client);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static string VersionOn(IElement element) =>
        element.GetAttribute(Attribute)
        ?? throw new InvalidOperationException($"No {Attribute} on <{element.LocalName}>.");

    // -----------------------------------------------------------------------
    // The trap: the value has to be the assembly's, not a plausible string.

    [Fact]
    public void Version_WhenTheVariableExplorerIsRendered_ThenItIsTheAssemblysOwnInformationalVersion()
    {
        Prepare(new OneKildeClient());

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true));

        Assert.Equal(Expected, VersionOn(cut.Find($"[{Attribute}]")));
    }

    [Fact]
    public void Version_WhenTheKildeExplorerIsRendered_ThenItIsTheAssemblysOwnInformationalVersion()
    {
        Prepare(new OneKildeClient());

        var cut = Render<KildeExplorer>();

        Assert.Equal(Expected, VersionOn(cut.Find($"[{Attribute}]")));
    }

    [Fact]
    public void Version_WhenItIsRendered_ThenItIsNotTheAssemblyVersionThatDropsThePrereleaseSuffix()
    {
        // The near miss the equality above would not explain on its own. AssemblyVersion is
        // major.minor.patch.0 — 0.1.0.0 for every one of the alphas — so a component sourcing it
        // renders something that looks like a version and cannot tell two releases apart.
        Prepare(new OneKildeClient());

        var cut = Render<KildeExplorer>();
        var assemblyVersion = typeof(VariableExplorer).Assembly.GetName().Version?.ToString();

        Assert.NotEqual(assemblyVersion, VersionOn(cut.Find($"[{Attribute}]")));
    }

    // -----------------------------------------------------------------------
    // Anonymous, which is the view helsedata's readers report as broken.

    [Fact]
    public void Version_WhenTheReaderIsSignedOut_ThenTheVariableExplorerStillCarriesIt()
    {
        Prepare(new OneKildeClient());

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, false));

        Assert.Equal(Expected, VersionOn(cut.Find($"[{Attribute}]")));
    }

    [Fact]
    public void Version_WhenTheReaderIsSignedOut_ThenTheKildeExplorerStillCarriesIt()
    {
        // Kelda has no IsAuthenticated at all — it is anonymous by construction — so this asserts
        // that the version rides on the markup rather than on anything a session supplies.
        Prepare(new OneKildeClient());

        var cut = Render<KildeExplorerWithUrlState>();

        Assert.Equal(Expected, VersionOn(cut.Find($"[{Attribute}]")));
    }

    // -----------------------------------------------------------------------
    // Every mount point, not the one that happened to be tested.

    [Fact]
    public void Version_WhenTheSearchSurfaceIsMountedOnItsOwn_ThenItCarriesIt()
    {
        Prepare(new OneKildeClient());

        var cut = Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, false));

        Assert.Equal(Expected, VersionOn(cut.Find($"[{Attribute}]")));
    }

    [Fact]
    public void Version_WhenTheListViewIsMountedOnItsOwn_ThenItCarriesIt()
    {
        Prepare(new OneKildeClient());

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        Assert.Equal(Expected, VersionOn(cut.Find($"[{Attribute}]")));
    }

    [Fact]
    public void Version_WhenAComponentRootIsWritten_ThenItCarriesTheAttribute()
    {
        // The guard the render tests above cannot be: a third root element added later gets no
        // test of its own until somebody writes one, and the failure is invisible — the component
        // renders, and only the page nobody can identify is worse off.
        var roots = Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Any(segment => segment is "bin" or "obj"))
            .Where(path => Roots(File.ReadAllText(path)) > 0)
            .ToList();

        // A floor before the verdict. An extraction that found nothing would report every root as
        // carrying the attribute, having looked at none of them — the silent pass this guard exists
        // to replace. Two is what src/ holds today, and zero is what a stale regex returns.
        Assert.True(roots.Count >= 2, $"Found {roots.Count} root element(s) under src/, so nothing was checked.");

        var missing = roots
            .Where(path => !File.ReadAllText(path).Contains(Attribute, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);

        Assert.Equal([], missing);
    }

    /// <summary>How many <c>class="munin-explorer"</c> root elements the markup opens.</summary>
    /// <remarks>
    /// Razor comments are stripped first, for the reason <c>HostContractTest</c> strips them: these
    /// files explain the root class in prose, and a check a comment can trip is one that gets
    /// deleted the first time somebody documents the rule it enforces.
    /// </remarks>
    private static int Roots(string markup) =>
        Regex.Matches(
            Regex.Replace(markup, @"@\*.*?\*@", " ", RegexOptions.Singleline),
            @"class=""munin-explorer""").Count;
}
