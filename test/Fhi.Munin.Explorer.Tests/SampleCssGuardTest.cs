using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/assert-sample-css-in-step.sh</c> against a stylesheet broken on purpose and
/// asserts that it goes red, and red for the right reason.
///
/// The script is half the class-name guarding in this repository, and it was the half nothing
/// watched. CI only ever runs it against a stylesheet that passes, so everything below its rule
/// extraction — the NAMED/DRAWN split, the rule floor, <c>missing</c> reported apart from
/// <c>empty</c> — was dead on every green run. A guard that has never been seen to fail is a guard
/// nobody has evidence for: loosen the perl so every rule is classified as drawn and the floor
/// still passes at 228 rules, the success banner still prints, and the empty-rule check has quietly
/// stopped existing. Its C# twin in <see cref="HostClassNamesTest"/> would keep biting, but only for
/// names a render reaches — which is the blind spot this script exists to cover, since it reads
/// every file under <c>src/</c> whether or not a test renders it.
///
/// So these run the real script, unedited, over a copy of the real stylesheet with one rule
/// mutated. The only seam is <c>SAMPLE_CSS_MODERN</c> / <c>SAMPLE_CSS_LEGACY</c>, which say which
/// two files are "the samples": a test that reimplemented the extraction and asserted against its
/// own copy would prove the copy and not the script.
/// </summary>
public class SampleCssGuardTest
{
    /// <summary>
    /// The same cut the script's perl makes and <c>HostClassNames.CssRule</c> makes, used here to
    /// write a stylesheet rather than read one.
    /// </summary>
    private static readonly Regex Rule = new(@"(?<selector>[^{}]*)\{(?<declarations>[^{}]*)\}");

    [ShellFact]
    public void Guard_WhenTheSamplesArePassedThroughTheSeamUntouched_ThenItStillPasses()
    {
        // The control the other three stand on. Without it, a run that exits 1 proves nothing about
        // the mutation: it could be the seam itself — a path that does not arrive, a copy that
        // drifts from its twin — failing every time it is used.
        var run = Guard.RunAgainst(HostClassNames.SampleCss);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("declare something", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenOneNameIsEmptiedAndAnotherLosesItsRules_ThenBothAreReportedAndReportedApart()
    {
        // The experiment `Orphans_WhenTheSampleRulesForARenderedNameAreEmptied_…` runs in memory for
        // the C# guard, run here through the script itself — and doubled, because the two failures
        // have to arrive under different headings. "Unstyled" alone sends the reader looking for a
        // rule that is sitting right there in the file with nothing in it, which is the slowest way
        // to find a one-line deletion, and that is the whole reason the `empty` bucket exists.
        //
        // Both names are the Data tab's, and no rule in the samples names them both, so the two
        // mutations cannot reach into each other's result.
        var broken = WithRulesEmptied(
            WithRulesDeleted(HostClassNames.SampleCss, "munin-explorer-kodeverk"),
            "munin-explorer-codes");

        var run = Guard.RunAgainst(broken);

        Assert.Equal(1, run.ExitCode);

        var missing = Guard.SectionAfter(run.Output, "has no rule for");
        var empty = Guard.SectionAfter(run.Output, "declares nothing under it");

        Assert.Contains("munin-explorer-kodeverk", missing, StringComparison.Ordinal);
        Assert.DoesNotContain("munin-explorer-codes", missing, StringComparison.Ordinal);

        Assert.Contains("munin-explorer-codes", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("munin-explorer-kodeverk", empty, StringComparison.Ordinal);

        // The selector survives the emptying — that is the point of the mutation. A check that
        // searched the stylesheet for the name would still find it and still answer "styled".
        Assert.Contains(".munin-explorer-codes", broken, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenAnEmptiedBlockHoldsOnlyASemicolon_ThenItStillCountsAsDeclaringNothing()
    {
        // `{ ; }` is as silent as `{}` and reads as more deliberate: the leftover of a declaration
        // deleted without its semicolon. The perl's verdict is `/[^\s;]/` for exactly this, and this
        // is what would notice the day somebody simplifies it to a test for a non-empty block.
        var run = Guard.RunAgainst(WithRulesEmptied(HostClassNames.SampleCss, "munin-explorer-codes", " ; "));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(
            "munin-explorer-codes",
            Guard.SectionAfter(run.Output, "declares nothing under it"),
            StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheRuleExtractionYieldsAlmostNothing_ThenItStopsInsteadOfReportingEveryName()
    {
        // The guard on the guard. A stale extraction reads as "no name has a rule", which is loud
        // about the wrong thing — a reader would go looking for ~75 missing rules that are all still
        // in the file. Exit 2 rather than 1, because nothing has been checked rather than something
        // having failed.
        var run = Guard.RunAgainst(
            """
            .munin-explorer-codes { color: red; }
            .munin-explorer-kodeverk { color: red; }
            """);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("below the floor", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("has no rule for", run.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every rule whose selector so much as contains <paramref name="name"/>, left with
    /// <paramref name="block"/> between its braces. Wider than the script's own matching on purpose:
    /// nothing must be left behind that could answer for the name and make the experiment pass for
    /// the wrong reason.
    /// </summary>
    private static string WithRulesEmptied(string css, string name, string block = " ") =>
        Rule.Replace(css, m => m.Groups["selector"].Value.Contains('.' + name, StringComparison.Ordinal)
            ? m.Groups["selector"].Value + "{" + block + "}"
            : m.Value);

    /// <summary>The same rules taken out altogether, which is the other failure: no rule at all.</summary>
    private static string WithRulesDeleted(string css, string name) =>
        Rule.Replace(css, m => m.Groups["selector"].Value.Contains('.' + name, StringComparison.Ordinal)
            ? string.Empty
            : m.Value);
}

/// <summary>What one run of the script said.</summary>
internal readonly record struct GuardRun(int ExitCode, string Output);

/// <summary>Runs the sample-stylesheet guard the way CI runs it, against a stylesheet of our own.</summary>
internal static class Guard
{
    /// <summary>
    /// <c>bash</c> as PATH resolves it, or null where there is none — a Windows checkout without
    /// Git Bash on PATH, which is the only case the tests above skip for.
    /// </summary>
    internal static string? Bash { get; } =
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .SelectMany(dir => new[] { Path.Combine(dir, "bash"), Path.Combine(dir, "bash.exe") })
        .FirstOrDefault(File.Exists);

    /// <summary>
    /// Writes <paramref name="stylesheet"/> out as both sample copies and runs the script over
    /// them.
    /// </summary>
    /// <remarks>
    /// Both copies get the same bytes, so the first clause — the two files are one file — passes and
    /// the run reaches the clause under test. Run from a directory that is not the checkout, which
    /// also holds the script to its claim that it anchors itself on its own location rather than on
    /// where the caller happens to be standing.
    /// </remarks>
    internal static GuardRun RunAgainst(string stylesheet)
    {
        var dir = Directory.CreateTempSubdirectory("munin-css-guard");

        try
        {
            var modern = Path.Combine(dir.FullName, "modern.css");
            var legacy = Path.Combine(dir.FullName, "legacy.css");

            File.WriteAllText(modern, stylesheet);
            File.WriteAllText(legacy, stylesheet);

            var start = new ProcessStartInfo(Bash!)
            {
                WorkingDirectory = dir.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            start.ArgumentList.Add(Repo.In("scripts", "assert-sample-css-in-step.sh"));
            start.Environment["SAMPLE_CSS_MODERN"] = modern;
            start.Environment["SAMPLE_CSS_LEGACY"] = legacy;

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException($"'{Bash}' did not start.");

            // Both pipes are read before waiting: a script that filled one of them while we waited
            // on the other would deadlock, and this one prints a whole diff on its first clause.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            Task.WaitAll([stdout, stderr], TimeSpan.FromMinutes(2));
            process.WaitForExit(TimeSpan.FromMinutes(2));

            return new GuardRun(process.ExitCode, stdout.Result + stderr.Result);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// What the script printed under <paramref name="heading"/>, up to the next <c>::error::</c>.
    ///
    /// The heading is asserted before it is cut on, so a run that reports the wrong failure — or no
    /// failure — fails on the sentence saying which message was expected rather than on a string
    /// index.
    /// </summary>
    internal static string SectionAfter(string output, string heading)
    {
        Assert.Contains(heading, output, StringComparison.Ordinal);

        var section = output[(output.IndexOf(heading, StringComparison.Ordinal) + heading.Length)..];
        var next = section.IndexOf("::error::", StringComparison.Ordinal);

        return next < 0 ? section : section[..next];
    }
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself where there is no <c>bash</c> to run the script
/// with.
/// </summary>
/// <remarks>
/// Skipped rather than failed, and only for the shell that runs the guard: CI is ubuntu-latest, so
/// these always run where it matters, and a Windows checkout without Git Bash on PATH is told why
/// instead of being shown a Win32Exception. The same shape as
/// <see cref="LiveApiFactAttribute"/>, for the same reason — the reason is written where whoever
/// reads the test output can see it.
/// </remarks>
internal sealed class ShellFactAttribute : FactAttribute
{
    public ShellFactAttribute()
    {
        if (Guard.Bash is null)
        {
            Skip = "No bash on PATH, so scripts/assert-sample-css-in-step.sh cannot be run here. " +
                   "CI runs on ubuntu-latest, where it always can.";
        }
    }
}
