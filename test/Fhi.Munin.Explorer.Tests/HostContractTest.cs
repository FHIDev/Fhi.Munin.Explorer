using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The two host rules that are read off the source rather than off a render, over every component
/// this package ships: no <c>@page</c>, no <c>@rendermode</c>, no router, no <c>HeadOutlet</c>, and
/// no CSS of its own.
/// </summary>
/// <remarks>
/// <para>
/// Both rules bind the package, not one component of it — <c>AGENTS.md</c> and <c>CLAUDE.md</c>
/// state them that way — so they are enumerated over <c>src/</c> rather than pinned to a path. A
/// guard written against one file is a guard the next root component does not get, and the
/// component that has one is never the one that breaks the rule.
/// </para>
/// <para>
/// Neither shows up as a failing render: bUnit supplies a router and a render mode, so a
/// <c>@page</c> or a <c>@rendermode</c> would go on passing here and fail only inside helsedata's
/// Optimizely host, where there is no router at all and the host decides the render mode at the
/// mount site. A scoped <c>.razor.css</c> is the same shape of silence — it is the one way to add
/// CSS to this package without touching the project file, and
/// <c>scripts/assert-package-contents.sh</c> only catches it once the package has been packed.
/// </para>
/// </remarks>
public class HostContractTest
{
    /// <summary>The components this package ships, as paths relative to the RCL's project directory.</summary>
    public static TheoryData<string> Components()
    {
        var data = new TheoryData<string>();

        foreach (var component in ComponentFiles())
        {
            data.Add(Path.GetRelativePath(SourceRoot, component));
        }

        return data;
    }

    [Fact]
    public void Components_WhenTheyAreEnumerated_ThenTheOnesThisPackageShipsAreAmongThem()
    {
        // The theories below say nothing at all if the enumeration comes back empty — a moved
        // project directory or a changed glob would turn both of them green by finding no files.
        // So the roots are named here, and this is the test that fails when one of them moves.
        var found = ComponentFiles().Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("VariableExplorer.razor", found);
        Assert.Contains("KildeExplorer.razor", found);
        Assert.Contains("KildeView.razor", found);
        Assert.Contains("VariableView.razor", found);
    }

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_WhenItsSourceIsRead_ThenItHasNoPageNoRenderModeAndNoRouter(string component)
    {
        // Razor comments are stripped first, because these files explain in prose why they have no
        // @page and no @rendermode — a check that a comment can break is one that gets deleted the
        // first time somebody documents the rule it enforces.
        var markup = Regex.Replace(
            File.ReadAllText(Path.Combine(SourceRoot, component)), @"@\*.*?\*@", " ", RegexOptions.Singleline);

        Assert.DoesNotContain("@page", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@rendermode", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@attribute [Route", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<Router", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("HeadOutlet", markup, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_WhenItsSourceIsRead_ThenItShipsNoStylesheetOfItsOwn(string component) =>
        Assert.False(File.Exists(Path.Combine(SourceRoot, component) + ".css"));

    [Fact]
    public void Package_WhenItsSourceTreeIsRead_ThenThereIsNoStylesheetInItAtAll()
    {
        // The wider half of the same rule: a `.razor.css` is what the theory above catches, and
        // this catches a stylesheet parked anywhere else under src/ — a wwwroot the project file
        // has not been told to exclude yet, a file left beside a helper.
        var stylesheets = Files("*.css").Select(path => Path.GetRelativePath(SourceRoot, path)).Order(StringComparer.Ordinal);

        Assert.Equal([], stylesheets);
    }

    private static string SourceRoot => Repo.In("src", "Fhi.Munin.Explorer");

    private static IEnumerable<string> ComponentFiles() => Files("*.razor");

    /// <summary>
    /// The checked-in files matching <paramref name="pattern"/>, in a stable order.
    /// </summary>
    /// <remarks>
    /// <c>bin</c> and <c>obj</c> are skipped: what is built from the source is not the source, and
    /// a stylesheet the build copied in would report as one this package ships.
    /// </remarks>
    private static IEnumerable<string> Files(string pattern) =>
        Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
                 .Where(path => !Path.GetRelativePath(SourceRoot, path)
                                     .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                     .Any(segment => segment is "bin" or "obj"))
                 .Order(StringComparer.Ordinal);
}
