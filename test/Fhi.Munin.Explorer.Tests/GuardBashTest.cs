using System.Diagnostics;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What <see cref="Guard"/> assumes before any script runs: the bash, its PATH, its cleanup. None
/// can go red on the ubuntu-latest runner; the box they are for is the developer's, where all
/// three were false at once (Fhi.Metadata-ze05p).
/// </summary>
public class GuardBashTest
{
    [ShellFact]
    public void Bash_WhenResolvedFromPath_ThenItRunsAScriptNamedByAnAbsolutePath()
    {
        // The contract every other guard test rests on, and the one that was not held: the first
        // bash on PATH can be WSL's app execution alias, which reads a Windows path as a filename
        // with the separators gone and reports 127 for a script that is sitting right there.
        var script = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sh");

        try
        {
            File.WriteAllText(script, "exit 37\n");

            var start = new ProcessStartInfo(Guard.Bash!)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            start.ArgumentList.Add(script);

            var run = Guard.Run(start, Path.GetFileName(script));

            Assert.True(
                run.ExitCode == 37,
                $"'{Guard.Bash}' returned {run.ExitCode} for a script that exits 37. It said:"
                + Environment.NewLine
                + (run.Output.Length == 0 ? "(nothing at all)" : run.Output));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [ShellFact]
    public void Shell_WhenTheParentIsNotItself_ThenTheToolsTheGuardsShellOutToAreOnItsPath()
    {
        // Every guard script goes through perl, grep and sed. On Windows those are installed
        // beside bash and nowhere a Windows PATH points, so a run started from anything but a
        // Git Bash shell reached the scripts and then died inside them.
        var start = Guard.Shell(Path.GetTempPath());

        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("command -v perl && command -v grep && command -v sed");

        var run = Guard.Run(start, "command -v perl grep sed");

        Assert.True(
            run.ExitCode == 0,
            $"'{Guard.Bash}' cannot see all of perl, grep and sed. It said:"
            + Environment.NewLine
            + (run.Output.Length == 0 ? "(nothing at all)" : run.Output));
    }

    [Fact]
    public void Discard_WhenSomethingElseStillHoldsTheDirectory_ThenItLeavesItRatherThanThrowing()
    {
        // The delete runs in a finally beside the assertions, so one that throws replaces the
        // guard's verdict with a cleanup error. The open handle stands in for whatever still has
        // the directory; on Linux the delete simply succeeds and this pins the contract instead.
        var dir = Directory.CreateTempSubdirectory("munin-discard");
        var held = Path.Combine(dir.FullName, "held.txt");

        using (new FileStream(held, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            Guard.Discard(dir);
        }

        Guard.Discard(dir);

        Assert.False(Directory.Exists(dir.FullName));
    }
}
