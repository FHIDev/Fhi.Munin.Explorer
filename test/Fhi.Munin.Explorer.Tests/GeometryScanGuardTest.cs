using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Holds the <c>GEOMETRY_ASSERTIONS</c> subset in <c>scripts/geometry-scan.mjs</c> to the claim it
/// is written for: that a subset run which measures nothing says so instead of reporting success.
/// </summary>
/// <remarks>
/// <para>
/// The subset makes two false greens reachable that the full suite could not have. A name nothing
/// defines — a rename, or a typo in <c>check-accessibility.sh</c>, which passes its three as
/// literals — would filter the suite down to nothing; and a name that IS defined but is scoped to
/// states the targets are not in goes <c>n/a</c> everywhere, leaving <c>failures</c> at 0. Both
/// exit 2 as tooling failures, and both are checked here against the real script.
/// </para>
/// <para>
/// No browser is started by any of this: every check the script makes about names, states and
/// targets happens before it imports playwright, which is the property that lets these run on a
/// machine that has none — the <c>build + test</c> job, which installs no scanner.
/// </para>
/// </remarks>
[Collection(GuardScripts.Name)]
public class GeometryScanGuardTest
{
    /// <summary>A target the script parses and never loads, since every case exits before that.</summary>
    private const string Target = "http://127.0.0.1:1/kilder";

    [Fact]
    public void TheCaller_WhenItNamesAssertions_ThenGeometryAssertionsDefinesEveryOne()
    {
        var known = Assertions.Keys.ToList();

        Assert.NotEmpty(Chosen);

        foreach (var name in Chosen)
        {
            Assert.True(
                known.Contains(name),
                $"check-accessibility.sh asks for the assertion '{name}', which "
                + $"geometry-assertions.mjs does not define. It defines:{Environment.NewLine}  "
                + string.Join(Environment.NewLine + "  ", known));
        }
    }

    [Fact]
    public void TheCaller_WhenItNamesAssertions_ThenEachOneAppliesToTheStateItMeasures()
    {
        // The second false green, in the one place it can actually happen: the reflow run drives a
        // single target, so a chosen assertion scoped elsewhere is an assertion that never runs.
        var state = ReflowState;

        foreach (var name in Chosen)
        {
            var scopes = Assertions[name];

            Assert.True(
                scopes is null || scopes.Contains(state),
                $"check-accessibility.sh measures {ReflowTarget} at 320px and asks for '{name}', "
                + $"which is only measured in {string.Join(", ", scopes ?? [])}. It would print "
                + "n/a there and measure nothing.");
        }
    }

    [NodeFact]
    public void Scan_WhenAskedForANameNothingDefines_ThenItExitsTwoAndSaysWhichNamesExist()
    {
        var run = Scan("no such assertion", $"{Target}::kilder-list");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("unknown assertion \"no such assertion\"", run.Output, StringComparison.Ordinal);
        Assert.Contains("known assertions:", run.Output, StringComparison.Ordinal);
    }

    [NodeFact]
    public void Scan_WhenEveryNameAskedForIsScopedToAnotherState_ThenItExitsTwo()
    {
        // Every pin in geometry-assertions.mjs is state-scoped, so this is a caller's ordinary
        // mistake rather than an exotic one: ask for one against a target it is not written for.
        var run = Scan("the tablist clears the header", $"{Target}::kilder-list");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("no assertion asked for applies", run.Output, StringComparison.Ordinal);
    }

    [NodeFact]
    public void Scan_WhenANameIsGivenTwice_ThenTheBannerCountsItOnce()
    {
        // An unreachable state, because it is the last thing checked before a browser starts: the
        // banner is printed by then, and nothing here has to install playwright to read it.
        var run = Scan("hidden means hidden,hidden means hidden", $"{Target}::no-such-state");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains(
            $"==> ASSERTIONS: 1 of {Assertions.Count}: hidden means hidden{Environment.NewLine}",
            run.Output.ReplaceLineEndings(),
            StringComparison.Ordinal);
    }

    [NodeFact]
    public void Scan_WhenGivenTheNamesTheCallerPasses_ThenItGetsPastEveryNameCheck()
    {
        var run = Scan(string.Join(',', Chosen), $"{Target}::no-such-state");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains($"==> ASSERTIONS: {Chosen.Count} of {Assertions.Count}:", run.Output, StringComparison.Ordinal);
        Assert.Contains("unknown state \"no-such-state\"", run.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// One run of the real script, from a directory that is not the checkout — which holds it to
    /// resolving its sibling modules by its own path rather than by where the caller stood.
    /// </summary>
    private static GuardRun Scan(string assertions, string target)
    {
        var dir = Directory.CreateTempSubdirectory("munin-geometry-scan");

        try
        {
            var start = new ProcessStartInfo(NodeFactAttribute.Node!)
            {
                WorkingDirectory = dir.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            start.ArgumentList.Add(Repo.In("scripts", "geometry-scan.mjs"));
            start.ArgumentList.Add(target);
            start.Environment["GEOMETRY_ASSERTIONS"] = assertions;

            return Guard.Run(start, "geometry-scan.mjs");
        }
        finally
        {
            try
            {
                dir.Delete(recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A temp directory left behind is the lesser problem, and the OS clears it.
            }
        }
    }

    /// <summary>Every assertion the suite defines, against the states it is scoped to or null.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>?> Assertions => KnownAssertions.Value;

    /// <summary>The names <c>check-accessibility.sh</c> passes to the 320px run.</summary>
    private static IReadOnlyList<string> Chosen => Caller.Value.Names;

    /// <summary>The <c>path::state</c> that run measures.</summary>
    private static string ReflowTarget => $"{Caller.Value.Path}::{Caller.Value.State}";

    /// <summary>The state that run drives the page into, which every scoping question is about.</summary>
    private static string ReflowState => Caller.Value.State;

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>?>> KnownAssertions =
        new(ReadAssertions);

    private static readonly Lazy<IReadOnlyList<string>> KnownStates = new(ReadStates);

    private static readonly Lazy<(IReadOnlyList<string> Names, string Path, string State)> Caller = new(ReadCaller);

    /// <summary>
    /// The assertion names and their <c>states:</c> scoping, read out of the source rather than
    /// listed here: a list of my own would go stale exactly when a rename made these checks matter.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>?> ReadAssertions()
    {
        var source = File.ReadAllText(Repo.In("scripts", "geometry-assertions.mjs"));
        var names = Regex.Matches(source, @"^\s*name:\s*(['""])(?<name>.*?)\1,", RegexOptions.Multiline);
        var found = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.Ordinal);

        for (var i = 0; i < names.Count; i++)
        {
            // As far as the next assertion, so a `states:` line is attributed to the one it is in.
            var start = names[i].Index;
            var end = i + 1 < names.Count ? names[i + 1].Index : source.Length;
            var block = source[start..end];
            var scopes = Regex.Match(block, @"^\s*states:\s*\[(?<states>[^\]]*)\]", RegexOptions.Multiline);

            found[names[i].Groups["name"].Value] = scopes.Success
                ? scopes.Groups["states"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(state => state.Trim('\'', '"'))
                    .ToList()
                : null;
        }

        Assert.NotEmpty(found);

        return found;
    }

    /// <summary>Every state <c>axe-states.mjs</c> defines, which is what a target may name.</summary>
    private static IReadOnlyList<string> ReadStates()
    {
        var source = File.ReadAllText(Repo.In("scripts", "axe-states.mjs"));
        var body = source[source.IndexOf("export const states = {", StringComparison.Ordinal)..];
        var found = Regex.Matches(body, @"^  '(?<state>[^']+)':", RegexOptions.Multiline)
            .Select(match => match.Groups["state"].Value)
            .ToList();

        Assert.NotEmpty(found);

        return found;
    }

    /// <summary>
    /// What the 320px run in <c>check-accessibility.sh</c> asks for, read off the script. The
    /// <c>::</c> is part of the pattern rather than something split off after: a target without one
    /// leaves no state, and every scoping check below would then pass having compared nothing.
    /// </summary>
    private static (IReadOnlyList<string> Names, string Path, string State) ReadCaller()
    {
        var source = File.ReadAllText(Repo.In("scripts", "check-accessibility.sh"));
        var assertions = Regex.Match(source, @"GEOMETRY_ASSERTIONS='(?<names>[^']*)'");
        var target = Regex.Match(
            source,
            @"^REFLOW_TARGET=""(?<path>[^"":]*)::(?<state>[^""]+)""",
            RegexOptions.Multiline);

        Assert.True(
            assertions.Success && target.Success,
            "check-accessibility.sh no longer has a GEOMETRY_ASSERTIONS='...' run driving a "
            + "REFLOW_TARGET=\"path::state\", so these checks are reading a script that has moved "
            + "on. A REFLOW_TARGET naming no state would measure the page as it first paints.");

        var state = target.Groups["state"].Value;

        Assert.True(
            KnownStates.Value.Contains(state),
            $"check-accessibility.sh measures the state '{state}', which axe-states.mjs does not "
            + $"define. It defines:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", KnownStates.Value));

        return (
            assertions.Groups["names"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            target.Groups["path"].Value,
            state);
    }
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself where there is no <c>node</c> to run the scanner
/// with.
/// </summary>
/// <remarks>
/// The same shape and the same reason as <see cref="ShellFactAttribute"/>: CI is ubuntu-latest and
/// has node, so these always run where it matters, and a checkout without one is told why instead
/// of being shown a Win32Exception.
/// </remarks>
internal sealed class NodeFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> Resolved = new(FindNode);

    internal static string? Node => Resolved.Value;

    public NodeFactAttribute()
    {
        if (Node is null)
        {
            Skip = "No node on PATH, so scripts/geometry-scan.mjs cannot be run here. CI runs on "
                   + "ubuntu-latest, where there always is one.";
        }
    }

    private static string? FindNode() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .SelectMany(dir => new[] { Path.Combine(dir, "node"), Path.Combine(dir, "node.exe") })
        .FirstOrDefault(File.Exists);
}
