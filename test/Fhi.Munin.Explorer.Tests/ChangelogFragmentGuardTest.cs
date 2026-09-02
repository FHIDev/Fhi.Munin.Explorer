using System.Diagnostics;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/check-changelog-fragment.sh</c> against throwaway repositories built to break it
/// and asserts that it goes red, and red for the right reason.
/// </summary>
/// <remarks>
/// <para>
/// This check had no test at all. A guard that has stopped working passes every branch, which is
/// indistinguishable from a repository where everyone remembered their fragment — so nothing about
/// it could be observed until something was written that it had to refuse.
/// </para>
/// <para>
/// The seam is the script's own two arguments, and the tree it judges is the working directory, so
/// each case is a real git repository with a real base commit rather than a fixture. That is why
/// this class carries a builder instead of a constant.
/// </para>
/// </remarks>
public class ChangelogFragmentGuardTest
{
    private const string Script = "check-changelog-fragment.sh";

    private const string Fragment = "category: Fixed\n\n- Something a host embedding the package sees.\n";

    private const string SourceFile = "src/Fhi.Munin.Explorer/Blazor/Thing.cs";

    [ShellFact]
    public void Guard_WhenTheBranchChangesSrcWithNoFragment_ThenItAsksForOne()
    {
        // The control every other case stands on: without it a red run proves nothing, because the
        // harness itself could be what is failing.
        var run = RunAgainst([SourceFile]);

        AssertExit(1, run);
        Assert.Contains("changes src/ but adds no changelog fragment", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheBranchAddsAFragment_ThenItPasses()
    {
        // The other half of the control: the demand is satisfiable, so a green run below means the
        // clause was reached and answered rather than never reached.
        var run = RunAgainst(
            [SourceFile],
            fragments: new Dictionary<string, string> { ["changelog.d/thing.md"] = Fragment });

        AssertExit(0, run);
        Assert.Contains("Changelog fragment found", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheBranchChangesNothingUnderSrc_ThenItPasses()
    {
        // The narrowness is deliberate, and documented at the top of the script: docs, samples,
        // tests and CI are invisible to someone embedding the package, so they owe nothing.
        var run = RunAgainst(["docs/notes.md", "test/Fhi.Munin.Explorer.Tests/ThingTest.cs"]);

        AssertExit(0, run);
        Assert.Contains("No changes under src/", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheChangedFileListRunsPastAPipeBufferInLength_ThenSrcIsStillSeen()
    {
        // The defect, pinned (Fhi.Metadata-v198s). Padding that sorts after src/ leaves printf
        // still writing when `grep -q` matches and goes, so printf takes SIGPIPE and `pipefail`
        // reports 141 — read here as "no src/ changes". Past the 64 KiB buffer that is certain.
        var padding = Enumerable
            .Range(0, 1_000)
            .Select(i => "zz-padding/" + new string('p', 80) + "-" + i.ToString("D4") + ".cs");

        var run = RunAgainst([.. padding.Prepend(SourceFile)]);

        AssertExit(1, run);
        Assert.Contains("changes src/ but adds no changelog fragment", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheBaseRefCannotBeResolved_ThenItSaysSoRatherThanPassing()
    {
        // Exit 2, not 0: an unfetched base is the state a CI job is in before it fetches, and a
        // check that passed there would be quietly off for every branch that reached it.
        using var repo = new Repository();

        repo.Commit([SourceFile]);

        var run = Guard.RunIn(Script, repo.Path, "refs/heads/no-such-base", "HEAD");

        AssertExit(2, run);
        Assert.Contains("Cannot find a merge base", run.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The exit code, with everything the script printed — stdout and stderr both — in the message,
    /// for the reason spelled out on <see cref="FragmentHostNotesGuardTest"/>: a bare
    /// <c>Assert.Equal</c> leaves a failing run with only a test name to go on.
    /// </summary>
    private static void AssertExit(int expected, GuardRun run) =>
        Assert.True(
            run.ExitCode == expected,
            $"Expected exit {expected} from {Script}, got {run.ExitCode}. It said:"
            + Environment.NewLine
            + (run.Output.Length == 0 ? "(nothing at all)" : run.Output));

    /// <summary>
    /// Builds a repository whose branch commit touches <paramref name="changed"/> and carries
    /// <paramref name="fragments"/>, then runs the script over it the way CI does.
    /// </summary>
    private static GuardRun RunAgainst(
        IReadOnlyCollection<string> changed,
        IReadOnlyDictionary<string, string>? fragments = null)
    {
        using var repo = new Repository();

        repo.Commit(changed, fragments);

        return Guard.RunIn(Script, repo.Path, "HEAD~1", "HEAD");
    }

    /// <summary>A throwaway git repository with one base commit, and a branch commit on request.</summary>
    private sealed class Repository : IDisposable
    {
        internal Repository()
        {
            Path = Directory.CreateTempSubdirectory("munin-changelog-guard").FullName;

            Git("init", "--quiet", "--initial-branch=main");

            Write("README.md", "# Base\n");
            Git("add", "--all");
            Git("commit", "--quiet", "--message", "base");
        }

        internal string Path { get; }

        /// <summary>The branch commit: every path in <paramref name="changed"/>, plus any fragments.</summary>
        internal void Commit(
            IReadOnlyCollection<string> changed,
            IReadOnlyDictionary<string, string>? fragments = null)
        {
            foreach (var file in changed)
            {
                Write(file, "// changed\n");
            }

            foreach (var (file, body) in fragments ?? new Dictionary<string, string>())
            {
                Write(file, body);
            }

            Git("add", "--all");
            Git("commit", "--quiet", "--message", "branch");
        }

        public void Dispose()
        {
            // Never throwing, because this unwinds alongside a failed assertion: a delete that lost
            // a race with a lingering git handle would replace "the guard returned the wrong exit
            // code" with "could not delete a temp directory" (Fhi.Metadata-ze05p).
            try
            {
                // Git marks its loose objects read-only, and Windows will not delete one of those.
                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Path, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A temp directory left behind is the lesser problem, and the OS clears it.
            }
        }

        private void Write(string relative, string body)
        {
            var full = System.IO.Path.Combine(Path, relative);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, body);
        }

        /// <summary>
        /// Git with an identity and no signing of its own, so the fixture neither depends on nor
        /// trips over whatever the machine running the tests has configured globally.
        /// </summary>
        private void Git(params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var setting in new[]
                     {
                         "user.name=Guard Test",
                         "user.email=guard@example.invalid",
                         "commit.gpgsign=false",
                         "core.hooksPath=",
                     })
            {
                start.ArgumentList.Add("-c");
                start.ArgumentList.Add(setting);
            }

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            // Guard's plumbing rather than a bare WaitForExit, for the reason written over it: it
            // budgets the run and kills the tree, so a git blocking on a credential prompt it
            // inherited fails as a stalled fixture instead of hanging the whole xunit run.
            var run = Guard.Run(start, "git " + string.Join(' ', arguments));

            Assert.True(
                run.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed with {run.ExitCode}: {run.Output}");
        }
    }
}
