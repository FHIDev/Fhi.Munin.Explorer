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
        // The second false green: an assertion scoped away from a state is an assertion that never
        // runs there. Asked of every target, because a run that measures in one state and prints
        // n/a in the other still exits 0 having measured nothing in the state somebody added.
        Assert.NotEmpty(ReflowTargets);

        foreach (var (path, state) in ReflowTargets)
        {
            foreach (var name in Chosen)
            {
                var scopes = Assertions[name];

                Assert.True(
                    scopes is null || scopes.Contains(state),
                    $"check-accessibility.sh measures {path}::{state} at 320px and asks for "
                    + $"'{name}', which is only measured in {string.Join(", ", scopes ?? [])}. It "
                    + "would print n/a there and measure nothing.");
            }
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

    [NodeFact]
    public void Scan_WhenLeavingOutANameNothingDefines_ThenItExitsTwo()
    {
        // A misspelt exception would leave out nothing and still be read as a named, known gap.
        var run = Scan(null, $"{Target}::kilder-list", except: "no such assertion");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("unknown assertion \"no such assertion\"", run.Output, StringComparison.Ordinal);
    }

    [NodeFact]
    public void Scan_WhenBothRunAndLeaveOutListsAreGiven_ThenItExitsTwo()
    {
        var run = Scan("hidden means hidden", $"{Target}::kilder-list", except: "no horizontal overflow");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("GEOMETRY_ASSERTIONS and GEOMETRY_EXCEPT are both set", run.Output, StringComparison.Ordinal);
    }

    [NodeFact]
    public void Scan_WhenLeavingOutOneName_ThenTheBannerCountsEveryOtherAssertion()
    {
        var run = Scan(null, $"{Target}::no-such-state", except: "hidden means hidden");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains(
            $"==> ASSERTIONS: {Assertions.Count - 1} of {Assertions.Count}, leaving out: hidden means hidden{Environment.NewLine}",
            run.Output.ReplaceLineEndings(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void HostileHost_WhenItLeavesAssertionsOutAt320_ThenEveryNameAndStateExists()
    {
        // Only the credentialed CI job runs that script, so a rename would otherwise surface there
        // alone. Every call is parsed or the count below disagrees, so none is skipped unread.
        var source = File.ReadAllText(Repo.In("scripts", "check-hostile-host.sh"));
        var calls = Regex.Matches(
            source,
            @"^reflow ""(?<except>[^""]*)""(?<targets>(?:[ \t]*\\?\r?\n?[ \t]*""[^""]+"")+)",
            RegexOptions.Multiline);

        Assert.NotEmpty(calls);
        Assert.Equal(Regex.Matches(source, @"^reflow ", RegexOptions.Multiline).Count, calls.Count);

        foreach (Match call in calls)
        {
            var names = call.Groups["except"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var name in names)
            {
                Assert.True(
                    Assertions.ContainsKey(name),
                    $"check-hostile-host.sh leaves out '{name}' at 320px, which geometry-assertions.mjs "
                    + "does not define.");
            }

            var states = Regex.Matches(call.Groups["targets"].Value, @"""[^"":]*::(?<state>[^""]+)""")
                .Select(match => match.Groups["state"].Value)
                .ToList();

            Assert.NotEmpty(states);

            foreach (var state in states)
            {
                Assert.True(
                    KnownStates.Value.Contains(state),
                    $"check-hostile-host.sh measures the state '{state}' at 320px, which axe-states.mjs "
                    + "does not define.");
            }
        }
    }

    [Fact]
    public void HostileHost_WhenMeasuringAt320_ThenEveryTargetIsInExactlyOneCall()
    {
        // A state added to TARGETS and not to a reflow call would never be measured at 320, and
        // both the gate and the test above would stay green.
        var source = File.ReadAllText(Repo.In("scripts", "check-hostile-host.sh"));
        var array = Regex.Match(source, @"^TARGETS=\((?<items>[^)]*)^\)", RegexOptions.Multiline);

        Assert.True(array.Success, "check-hostile-host.sh no longer declares TARGETS=( ... ).");

        var targets = Regex.Matches(array.Groups["items"].Value, @"""(?<target>[^""]+)""")
            .Select(match => match.Groups["target"].Value)
            .Order(StringComparer.Ordinal)
            .ToList();
        var measured = Regex.Matches(
                source,
                @"^reflow ""[^""]*""(?<targets>(?:[ \t]*\\?\r?\n?[ \t]*""[^""]+"")+)",
                RegexOptions.Multiline)
            .SelectMany(call => Regex.Matches(call.Groups["targets"].Value, @"""(?<target>[^""]+)"""))
            .Select(match => match.Groups["target"].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(targets);
        Assert.Equal(targets, measured);
    }

    [Fact]
    public void TheCaller_WhenItMeasures320_ThenAnExportedExceptListCannotStopIt()
    {
        // geometry-scan.mjs exits 2 when both lists are set, so an exported GEOMETRY_EXCEPT would stop
        // check-accessibility.sh's 320px scan before it measured anything.
        var source = File.ReadAllText(Repo.In("scripts", "check-accessibility.sh"));

        Assert.Matches(@"(?m)^GEOMETRY_EXCEPT= \\\r?\n(?:GEOMETRY_[A-Z_]+=[^\n]*\\\r?\n)*\s+node [^\n]*geometry-scan\.mjs", source);
    }

    [Fact]
    public void HostileHost_WhenA320ScanFails_ThenTheRunFailsAndAnExportedListCannotStopOrNarrowIt()
    {
        // Read, not run: the step needs a browser and the feed. Without the status lines a real run
        // printed a 320px FAIL and exited 0; without clearing, an exported list exits 2 or narrows a scan.
        var source = File.ReadAllText(Repo.In("scripts", "check-hostile-host.sh"));
        Assert.Matches(@"(?m)^GEOMETRY_EXCEPT= [^\n]*geometry-scan\.mjs"" ""\$\{urls\[@\]\}""$", source);

        var body = Regex.Match(source, @"^reflow\(\) \{(?<body>.*?)^\}", RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.True(body.Success, "check-hostile-host.sh no longer defines reflow() { ... }.");
        Assert.Matches(@"GEOMETRY_ASSERTIONS=\s", body.Groups["body"].Value);
        Assert.Matches(@"\[ ""\$status"" -ne 0 \] && reflow_status=1", body.Groups["body"].Value);

        var verdict = Regex.Match(
            source,
            @"^if (?<condition>[^\n]*); then\s+cat >&2 <<'EOF'\s+The component does not render correctly",
            RegexOptions.Multiline);

        Assert.True(verdict.Success, "check-hostile-host.sh no longer has the failing verdict this reads.");
        Assert.Contains(@"[ ""$reflow_status"" -ne 0 ]", verdict.Groups["condition"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void HostileHost_WhenTheNegativeControlFails_ThenItExitsThreeAndNotOne()
    {
        // The exit code is this script's only channel to an unattended caller, and 1 there means
        // "renders wrong" while this means "nobody measured". Sharing 1 put a control failure and a
        // real defect under one title for a month (Fhi.Metadata-pvwzl, Fhi.Munin.Explorer#345).
        var source = File.ReadAllText(Repo.In("scripts", "check-hostile-host.sh"));

        var control = Regex.Match(
            source,
            @"^if \[ ""\$control_status"" -ne 0 \]; then\r?\n(?<body>.*?)^fi$",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.True(control.Success, "check-hostile-host.sh no longer branches on control_status.");

        // Both halves of the name. Asserting only that a 3 is present would let a later edit leave
        // an `exit 1` earlier in the branch: the shell returns on the first one it reaches, so the
        // guard would pass while the script still called a control failure a measured defect.
        var body = control.Groups["body"].Value;
        Assert.Matches(@"(?m)^  exit 3$", body);
        Assert.DoesNotMatch(@"(?m)^\s*exit 1$", body);
    }

    [Fact]
    public void HostileHost_WhenTheTabWalkFailsOrIsUnmeasured_ThenTheRunSaysSo()
    {
        // Read, not run, for the reason the two above are. Without the status in the verdict a Tab
        // stop inside a hidden panel prints FAIL and exits 0; without its own 3, a walk that never
        // reached its planted stop reads as a measured defect (Fhi.Metadata-w8sms).
        var source = File.ReadAllText(Repo.In("scripts", "check-hostile-host.sh"));

        Assert.Matches(@"(?m)node ""\$ROOT/scripts/tab-stop-scan\.mjs""", source);
        Assert.Matches(@"(?m)^tab_stop_status=\$\?$", source);
        Assert.Matches(@"(?m)^\[ ""\$tab_stop_status"" -eq 2 \] && exit 2$", source);

        var unmeasured = Regex.Match(
            source,
            @"^if \[ ""\$tab_stop_status"" -eq 3 \]; then\r?\n(?<body>.*?)^fi$",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.True(unmeasured.Success, "check-hostile-host.sh no longer branches on the Tab walk's 3.");
        Assert.Matches(@"(?m)^  exit 3$", unmeasured.Groups["body"].Value);
        Assert.DoesNotMatch(@"(?m)^\s*exit 1$", unmeasured.Groups["body"].Value);

        var verdict = Regex.Match(
            source,
            @"^if (?<condition>[^\n]*); then\s+cat >&2 <<'EOF'\s+The component does not render correctly",
            RegexOptions.Multiline);

        Assert.True(verdict.Success, "check-hostile-host.sh no longer has the failing verdict this reads.");
        Assert.Contains(@"[ ""$tab_stop_status"" -ne 0 ]", verdict.Groups["condition"].Value, StringComparison.Ordinal);
        Assert.True(
            unmeasured.Index < verdict.Index,
            "the Tab walk's 3 has to be answered before the verdict, or an unmeasured walk exits 1.");
    }

    /// <summary>
    /// One run of the real script, from a directory that is not the checkout — which holds it to
    /// resolving its sibling modules by its own path rather than by where the caller stood.
    /// </summary>
    private static GuardRun Scan(string? assertions, string target, string? except = null)
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
            start.Environment["GEOMETRY_EXCEPT"] = except;

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

    /// <summary>Every <c>path::state</c> that run measures, in the order the script declares them.</summary>
    private static IReadOnlyList<(string Path, string State)> ReflowTargets => Caller.Value.Targets;

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>?>> KnownAssertions =
        new(ReadAssertions);

    private static readonly Lazy<IReadOnlyList<string>> KnownStates = new(ReadStates);

    private static readonly Lazy<(IReadOnlyList<string> Names, IReadOnlyList<(string Path, string State)> Targets)>
        Caller = new(ReadCaller);

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
    ///
    /// Every <c>REFLOW*_TARGET</c> the script declares, not the first: the run grew a second one
    /// under <c>Fhi.Metadata-kvgu7</c> and a pattern pinned to <c>REFLOW_TARGET</c> alone left it
    /// outside every check here, which is the false green this class exists for.
    /// </summary>
    private static (IReadOnlyList<string> Names, IReadOnlyList<(string Path, string State)> Targets) ReadCaller()
    {
        var source = File.ReadAllText(Repo.In("scripts", "check-accessibility.sh"));
        var assertions = Regex.Match(source, @"GEOMETRY_ASSERTIONS='(?<names>[^']*)'");
        var targets = Regex.Matches(
            source,
            @"^REFLOW[A-Z_]*_TARGET=""(?<path>[^"":]*)::(?<state>[^""]+)""",
            RegexOptions.Multiline);

        Assert.True(
            assertions.Success && targets.Count > 0,
            "check-accessibility.sh no longer has a GEOMETRY_ASSERTIONS='...' run driving a "
            + "REFLOW_TARGET=\"path::state\", so these checks are reading a script that has moved "
            + "on. A REFLOW_TARGET naming no state would measure the page as it first paints.");

        var found = targets
            .Select(match => (Path: match.Groups["path"].Value, State: match.Groups["state"].Value))
            .ToList();

        foreach (var (_, state) in found)
        {
            Assert.True(
                KnownStates.Value.Contains(state),
                $"check-accessibility.sh measures the state '{state}', which axe-states.mjs does "
                + $"not define. It defines:{Environment.NewLine}  "
                + string.Join(Environment.NewLine + "  ", KnownStates.Value));
        }

        return (
            assertions.Groups["names"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            found);
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
