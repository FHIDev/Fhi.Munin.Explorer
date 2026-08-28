using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/assert-sample-css-in-step.sh</c> against a stylesheet broken on purpose and
/// asserts that it goes red, and red for the right reason.
///
/// The script is half the class-name guarding in this repository, and it was the half nothing
/// watched. CI only ever runs it against a stylesheet that passes, so everything below its rule
/// extraction — the comment strip, the NAMED/DRAWN split, the rule floor, <c>missing</c> reported
/// apart from <c>empty</c>, clause three's orphans — was dead on every green run. A guard that has
/// never been seen to fail is a guard nobody has evidence for: loosen the perl so every rule is
/// classified as drawn and the floor still passes at 228 rules, the success banner still prints, and
/// the empty-rule check has quietly stopped existing. Its C# twin in <see cref="HostClassNamesTest"/>
/// would keep biting, but only for names a render reaches — which is the blind spot this script
/// exists to cover, since it reads every file under <c>src/</c> whether or not a test renders it.
///
/// So these run the real script, unedited, over a copy of the real stylesheet — comments and all,
/// which is load-bearing: the strip is the one clause here with a named history of failure, and a
/// test handing in pre-stripped text asks nothing of it. The mutations are made around the
/// comments rather than after removing them, so every comment byte reaches the script.
///
/// The seams are <c>SAMPLE_CSS_MODERN</c> / <c>SAMPLE_CSS_LEGACY</c>, which say which two files
/// are "the samples", and <c>HOST_CLASS_NAMES</c>, which says which fixture lists helsedata's own
/// names. A test that reimplemented the extraction and asserted against its own copy would prove
/// the copy and not the script.
/// </summary>
public class SampleCssGuardTest
{
    /// <summary>
    /// The same cut the script's perl makes and <c>HostClassNames.CssRule</c> makes, used here to
    /// write a stylesheet rather than read one.
    /// </summary>
    private static readonly Regex Rule = new(@"(?<selector>[^{}]*)\{(?<declarations>[^{}]*)\}");

    private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline);

    /// <summary>
    /// The sample stylesheet as it sits on disk, comments included — deliberately not
    /// <c>HostClassNames.SampleCss</c>, which has already had them taken out for the C# guard's own
    /// use. The script does its own stripping and that step has to be given something to strip.
    /// </summary>
    private static readonly Lazy<string> SampleCss = new(() =>
        File.ReadAllText(Repo.In("samples", "LegacyHost", "wwwroot", "css", "host.css")));

    /// <summary>
    /// A borrowed name — helsedata's, not ours — that the samples style with a rule of its own and
    /// the fixture lists on a line of its own. Clause three's two sources of cover, both movable,
    /// which is what makes it the name the clause-three experiment below is run on.
    /// </summary>
    private const string BorrowedName = "caption";

    [ShellFact]
    public void Guard_WhenTheSamplesArePassedThroughTheSeamUntouched_ThenItStillPasses()
    {
        // The control the others stand on. Without it, a run that exits 1 proves nothing about the
        // mutation: it could be the seam itself — a path that does not arrive, a copy that drifts
        // from its twin — failing every time it is used.
        var run = Guard.RunAgainst(SampleCss.Value);

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
            WithRulesDeleted(SampleCss.Value, "munin-explorer-kodeverk"),
            "munin-explorer-codes");

        var run = Guard.RunAgainst(broken);

        Assert.Equal(1, run.ExitCode);

        var missing = Guard.NamesUnder(run.Output, "has no rule for");
        var empty = Guard.NamesUnder(run.Output, "declares nothing under it");

        Assert.Contains("munin-explorer-kodeverk", missing);
        Assert.DoesNotContain("munin-explorer-codes", missing);

        Assert.Contains("munin-explorer-codes", empty);
        Assert.DoesNotContain("munin-explorer-kodeverk", empty);

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
        var run = Guard.RunAgainst(WithRulesEmptied(SampleCss.Value, "munin-explorer-codes", " ; "));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("munin-explorer-codes", Guard.NamesUnder(run.Output, "declares nothing under it"));
    }

    [ShellFact]
    public void Guard_WhenTheOnlyRuleLeftForANameIsInsideAComment_ThenItIsStillReportedAsHavingNone()
    {
        // The clause with the history. This file carries more prose than CSS, and a comment naming a
        // selector is indistinguishable from a rule declaring one to a substring search: the comment
        // above the kildetype badge was written with a leading dot and made the check answer
        // "styled" for the very name it documented as unstyled. Hence the perl strip — which every
        // other test here would leave untouched if they handed the script text with no comments in
        // it, and a strip nothing exercises is a strip that can be deleted for free.
        //
        // So: take the rule away and leave behind a comment that looks exactly like it. Replace the
        // strip with `cat` and this run goes green, which is the whole point of it.
        var broken = WithRulesDeleted(SampleCss.Value, "munin-explorer-kodeverk")
            + "\n/* Suspended, pending the redesign: .munin-explorer-kodeverk { display: flex; } */\n";

        var run = Guard.RunAgainst(broken);

        Assert.Equal(1, run.ExitCode);

        // The exact name, not a line that contains it. `munin-explorer-kodeverk__item` goes with
        // its parent when the rules are deleted and would satisfy a substring search for the parent
        // — which would let a script that had lost its comment strip pass this test, since the
        // comment rescues only the name it spells.
        Assert.Contains("munin-explorer-kodeverk", Guard.NamesUnder(run.Output, "has no rule for"));
    }

    [ShellFact]
    public void Guard_WhenABorrowedNamesOnlyCoverIsARuleThatDeclaresNothing_ThenClauseThreeReportsIt()
    {
        // Clause three, which nothing could reach through the stylesheet alone: it rescues any name
        // the fixture lists, and the fixture lists every borrowed name the package emits, so a
        // mutated stylesheet changed nothing about its verdict. Taking one line out of the fixture
        // is the lever, and `caption` then has exactly one thing standing between it and the orphan
        // list — the sample rule.
        var fixture = HostNamesWithout(BorrowedName);

        // Which it does, while the rule declares something. Half the experiment, and the half that
        // says the run below failed for the reason claimed rather than because the fixture seam
        // reports everything as an orphan.
        Assert.Equal(0, Guard.RunAgainst(SampleCss.Value, fixture).ExitCode);

        // And an empty block is not that. This is what "reads DRAWN" means at clause three, and
        // until now it was a claim in a comment: the selector is still in the file, still spelled
        // the same way, and the name is an orphan anyway.
        var broken = WithRulesEmptied(SampleCss.Value, BorrowedName);
        var run = Guard.RunAgainst(broken, fixture);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(BorrowedName, Guard.NamesUnder(run.Output, "styled by nothing"));

        Assert.Contains('.' + BorrowedName, broken, StringComparison.Ordinal);
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
    /// The captured host names with <paramref name="name"/>'s own line taken out, and nothing else
    /// touched — the fixture is matched line for line (<c>grep -qxF</c>), so a line that merely
    /// contains the name is a different name and stays.
    /// </summary>
    private static string HostNamesWithout(string name)
    {
        var lines = File.ReadAllLines(Repo.In("test", "host-class-names.txt"));

        // Asserted rather than assumed: a fixture recapture that dropped this name would otherwise
        // turn the experiment below into two runs of the same thing, both green, proving nothing.
        Assert.Contains(name, lines);

        return string.Join('\n', lines.Where(l => !string.Equals(l, name, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Every rule whose selector so much as contains <paramref name="name"/>, left with
    /// <paramref name="block"/> between its braces. Wider than the script's own matching on purpose:
    /// nothing must be left behind that could answer for the name and make the experiment pass for
    /// the wrong reason.
    /// </summary>
    private static string WithRulesEmptied(string css, string name, string block = " ") =>
        MutateRules(css, m => m.Groups["selector"].Value.Contains('.' + name, StringComparison.Ordinal)
            ? m.Groups["selector"].Value + "{" + block + "}"
            : m.Value);

    /// <summary>The same rules taken out altogether, which is the other failure: no rule at all.</summary>
    private static string WithRulesDeleted(string css, string name) =>
        MutateRules(css, m => m.Groups["selector"].Value.Contains('.' + name, StringComparison.Ordinal)
            ? string.Empty
            : m.Value);

    /// <summary>
    /// <see cref="Rule"/> applied to the stylesheet's rules and to nothing else, leaving every
    /// comment exactly as it was.
    /// </summary>
    /// <remarks>
    /// The comments here hold braces — a paragraph quoting `p { margin: 0 }` is a normal thing for
    /// this file to say — so <see cref="Rule"/> run over the raw text would cut a "rule" out of the
    /// middle of one and put it back mangled. Stripping first is the other way out and the wrong
    /// one: it hands the script a stylesheet with nothing to strip, and the strip is a clause under
    /// test. So cut around them: the text between comments is rules, and the comments themselves are
    /// copied through untouched.
    /// </remarks>
    private static string MutateRules(string css, MatchEvaluator mutate)
    {
        var kept = new StringBuilder();
        var at = 0;

        foreach (Match comment in Comment.Matches(css))
        {
            kept.Append(Rule.Replace(css[at..comment.Index], mutate));
            kept.Append(comment.Value);
            at = comment.Index + comment.Length;
        }

        return kept.Append(Rule.Replace(css[at..], mutate)).ToString();
    }
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
    /// them, with <paramref name="hostNames"/> standing in for the captured fixture where one is
    /// given.
    /// </summary>
    /// <remarks>
    /// Both copies get the same bytes, so the first clause — the two files are one file — passes and
    /// the run reaches the clause under test. Run from a directory that is not the checkout, which
    /// also holds the script to its claim that it anchors itself on its own location rather than on
    /// where the caller happens to be standing.
    /// </remarks>
    internal static GuardRun RunAgainst(string stylesheet, string? hostNames = null)
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

            if (hostNames is not null)
            {
                var fixture = Path.Combine(dir.FullName, "host-class-names.txt");

                File.WriteAllText(fixture, hostNames);
                start.Environment["HOST_CLASS_NAMES"] = fixture;
            }

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
    /// The names the script listed under <paramref name="heading"/> — one per indented line, as
    /// <c>printf '  %s\n'</c> writes them, up to the next <c>::error::</c>.
    ///
    /// Whole names rather than a substring of the section, because the names come in families and a
    /// substring search cannot tell a member from the family. `munin-explorer-kodeverk__item` in the
    /// list answers a search for `munin-explorer-kodeverk`, so a script that had stopped reporting
    /// the parent would still look as though it did.
    ///
    /// The heading is asserted before it is cut on, so a run that reports the wrong failure — or no
    /// failure — fails on the sentence saying which message was expected rather than on a string
    /// index.
    /// </summary>
    internal static IReadOnlyList<string> NamesUnder(string output, string heading)
    {
        Assert.Contains(heading, output, StringComparison.Ordinal);

        var section = output[(output.IndexOf(heading, StringComparison.Ordinal) + heading.Length)..];
        var next = section.IndexOf("::error::", StringComparison.Ordinal);

        return [.. (next < 0 ? section : section[..next])
                   .Split('\n')
                   .Select(line => line.TrimEnd('\r'))
                   .Where(line => line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
                   .Select(line => line.Trim())];
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
