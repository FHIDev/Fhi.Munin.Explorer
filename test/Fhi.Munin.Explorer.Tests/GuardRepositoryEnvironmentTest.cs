namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Holds <see cref="GuardRepository"/> to seeing only its own repository, whatever the shell that
/// started the tests points git at (Fhi.Metadata-fj5vv).
/// </summary>
/// <remarks>
/// A worktree session exports GIT_DIR and GIT_WORK_TREE, and an inherited pair outranks the child's
/// working directory — so the guard fixtures drove the developer's own checkout instead, landing a
/// commit named "base" on the branch under test. Asserting that the other guard classes pass would
/// not catch a regression: they pass on any machine that exports nothing, CI included, which is why
/// this one exports a decoy of its own rather than trusting the ambient environment.
/// </remarks>
[Collection(GuardScripts.Name)]
public class GuardRepositoryEnvironmentTest
{
    [Fact]
    public void Git_WhenTheEnvironmentPointsAtAnotherRepository_ThenItActsOnTheTempOneAnyway()
    {
        using var decoy = new GuardRepository("munin-guard-decoy");

        decoy.Write("decoy.txt", "decoy");
        decoy.Commit("decoy");

        var restore = Redirect(decoy);

        try
        {
            using var subject = new GuardRepository("munin-guard-subject");

            // Leaf names rather than whole paths: git answers with forward slashes on Windows too,
            // and a temp directory can be reached through a symlink on macOS.
            var subjectLeaf = Path.GetFileName(subject.Path);
            var decoyLeaf = Path.GetFileName(decoy.Path);

            foreach (var question in new[] { "--absolute-git-dir", "--show-toplevel" })
            {
                var answer = subject.Read("rev-parse", question);

                Assert.Contains(subjectLeaf, answer, StringComparison.Ordinal);
                Assert.DoesNotContain(decoyLeaf, answer, StringComparison.Ordinal);
            }

            subject.Write("subject.txt", "subject");
            subject.Commit("base");

            Assert.Contains("base", subject.Read("log", "--oneline"), StringComparison.Ordinal);

            // The half that says the damage was avoided rather than merely misreported: the commit
            // has to be absent from the repository the environment named.
            Assert.DoesNotContain("base", decoy.Read("log", "--oneline"), StringComparison.Ordinal);
            Assert.Empty(decoy.Read("status", "--porcelain"));
        }
        finally
        {
            restore.Dispose();
        }
    }

    /// <summary>
    /// Points all four at <paramref name="decoy"/> for the duration, never at the real checkout: a
    /// test that aimed them there would reproduce the defect it exists to forbid.
    /// </summary>
    private static IDisposable Redirect(GuardRepository decoy)
    {
        var previous = Guard.InheritedGit.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);

        var gitDir = Path.Combine(decoy.Path, ".git");

        Environment.SetEnvironmentVariable("GIT_DIR", gitDir);
        Environment.SetEnvironmentVariable("GIT_WORK_TREE", decoy.Path);
        Environment.SetEnvironmentVariable("GIT_INDEX_FILE", Path.Combine(gitDir, "index"));
        Environment.SetEnvironmentVariable("GIT_COMMON_DIR", gitDir);

        return new Restore(previous);
    }

    private sealed class Restore(IReadOnlyDictionary<string, string?> previous) : IDisposable
    {
        public void Dispose()
        {
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
