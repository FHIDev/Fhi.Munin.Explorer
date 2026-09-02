using System.Diagnostics;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A throwaway git repository for the guard tests, with no commit of its own until a caller makes
/// one.
/// </summary>
/// <remarks>
/// Shared rather than one copy per guard class. The two subtle halves — an identity git will not
/// prompt over, and a delete Windows can perform over git's read-only loose objects — are the same
/// wherever a script is driven over history, and a second copy is a second place for them to rot.
/// </remarks>
internal sealed class GuardRepository : IDisposable
{
    internal GuardRepository(string prefix)
    {
        Path = Directory.CreateTempSubdirectory(prefix).FullName;

        Git("init", "--quiet", "--initial-branch=main");
    }

    internal string Path { get; }

    internal void Write(string relative, string body)
    {
        var full = System.IO.Path.Combine(Path, relative);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, body);
    }

    /// <summary>Commits everything written since the last one.</summary>
    internal void Commit(string message)
    {
        Git("add", "--all");
        Git("commit", "--quiet", "--message", message);
    }

    public void Dispose()
    {
        // Never throwing, because this unwinds alongside a failed assertion: a delete that lost a
        // race with a lingering git handle would replace "the guard returned the wrong exit code"
        // with "could not delete a temp directory" (Fhi.Metadata-ze05p).
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

    /// <summary>
    /// Git with an identity and no signing of its own, so the fixture neither depends on nor trips
    /// over whatever the machine running the tests has configured globally.
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
        // budgets the run and kills the tree, so a git blocking on a credential prompt it inherited
        // fails as a stalled fixture instead of hanging the whole xunit run.
        var run = Guard.Run(start, "git " + string.Join(' ', arguments));

        Assert.True(
            run.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed with {run.ExitCode}: {run.Output}");
    }
}
