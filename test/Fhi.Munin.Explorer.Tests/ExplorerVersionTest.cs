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
/// The version every root element carries in <c>data-munin-explorer-version</c>, over every mount
/// point a host can use, signed in and signed out.
/// </summary>
/// <remarks>
/// Present-and-non-empty is not the assertion: that passes against a literal, which renders a
/// convincing version while telling the reader something false. The expectation is read off the
/// assembly instead. (Fhi.Metadata-sqbei)
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
        // Off MARKUP, never the raw file: every root explains this attribute in a comment that names
        // it, so a check reading raw text stays green on a root that kept the prose and lost the
        // attribute. Roots are found by class="munin-explorer" alone — VariableListView wears none.
        var roots = Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Any(segment => segment is "bin" or "obj"))
            .Select(path => (Name: Path.GetFileName(path), Markup: RazorSource.WithoutComments(File.ReadAllText(path))))
            .Where(file => RootCount(file.Markup) > 0)
            .ToList();

        // A floor before the verdict. An extraction that found nothing would report every root as
        // carrying the attribute, having looked at none of them — the silent pass this guard exists
        // to replace. Two is what src/ holds today, and zero is what a stale regex returns.
        Assert.True(roots.Count >= 2, $"Found {roots.Count} root element(s) under src/, so nothing was checked.");

        var missing = roots
            .Where(file => !file.Markup.Contains(Attribute, StringComparison.Ordinal))
            .Select(file => file.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal([], missing);
    }

    /// <summary>How many <c>class="munin-explorer"</c> root elements the markup opens.</summary>
    private static int RootCount(string markup) =>
        Regex.Matches(markup, @"class=""munin-explorer""").Count;
}
