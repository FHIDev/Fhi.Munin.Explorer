namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/assert-class-names-listed.sh</c> against a README broken on purpose and asserts
/// that it goes red, and red for the right reason.
/// </summary>
/// <remarks>
/// <para>
/// The script reconciles every <c>munin-explorer*</c> name under <c>src/</c> against the README's
/// inventory table. On a correct tree it prints a banner and exits 0, which is exactly what a guard
/// that had stopped working would also do — so a run against the tree as it stands proves nothing
/// about any clause in it. The mutations below are the proof.
/// </para>
/// <para>
/// The seam is <c>README_FILE</c>, and it is the only one: <c>src/</c> is read from the checkout so
/// the extraction under test is the real one, over the real component. Every mutation is therefore
/// on the README side, which is also where the defect this guard exists for lived — three
/// hand-written counts that had all gone stale, and eight names in no markdown file at all.
/// </para>
/// </remarks>
public class ClassNameInventoryGuardTest
{
    private const string Script = "assert-class-names-listed.sh";

    /// <summary>
    /// A row from the inventory, chosen because it is one of the eight <c>munin-explorer-whole*</c>
    /// names that were missing from every markdown file in the repository until this guard existed.
    /// Indented as the README has it: the table sits inside a list item, and the script allows the
    /// leading whitespace for that reason.
    /// </summary>
    private const string SampleRow = "  | `munin-explorer-whole__list` | handle |";

    private static readonly Lazy<string> Readme = new(() => File.ReadAllText(Repo.In("README.md")));

    [ShellFact]
    public void Guard_WhenTheReadmeIsPassedThroughTheSeamUntouched_ThenItStillPasses()
    {
        // The control the others stand on. Without it a run that exits 1 proves nothing about the
        // mutation: it could be the seam itself failing every time it is used.
        var run = RunAgainst(Readme.Value);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("lists every munin-explorer name", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenOneEmittedNameIsMissingFromTheInventory_ThenItIsReported()
    {
        // The whole point of the script, and the case the branch diff in
        // assert-new-names-noted-for-hosts.sh cannot reach: the name is not new, it is simply not
        // written down. Nothing about the branch says so, so nothing that reads the branch can tell.
        Assert.Contains(SampleRow, Readme.Value, StringComparison.Ordinal);

        var run = RunAgainst(Readme.Value.Replace(SampleRow + "\n", string.Empty, StringComparison.Ordinal));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("munin-explorer-whole__list", Guard.NamesUnder(run.Output, "does not list them:"));
    }

    [ShellFact]
    public void Guard_WhenTheInventoryListsANameNothingEmits_ThenItIsReportedToo()
    {
        // The same defect read backwards: a row left behind by a rename sends a host looking for an
        // element that is not there. A one-directional check would call this green.
        var run = RunAgainst(Readme.Value.Replace(
            SampleRow,
            SampleRow + "\n  | `munin-explorer-whole__ghost` | handle |",
            StringComparison.Ordinal));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("munin-explorer-whole__ghost", Guard.NamesUnder(run.Output, "no longer writes them:"));
    }

    [ShellFact]
    public void Guard_WhenARowNamesAKindThatDoesNotExist_ThenItIsReported()
    {
        // A misspelled kind reads as an answer to "what does an undefined one cost a host" and is
        // not one. No script can tell a handle from a name that carries meaning, but it can tell
        // that a row claims neither.
        var run = RunAgainst(Readme.Value.Replace(SampleRow, "  | `munin-explorer-whole__list` | handel |", StringComparison.Ordinal));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("handel", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheInventoryYieldsAlmostNothing_ThenItStopsInsteadOfReportingEveryName()
    {
        // The guard on the guard. A table the row pattern no longer matches reads as "no name is
        // listed", which is loud about the wrong thing — the reader would go looking for 108
        // missing rows that are all still there. Exit 2, because nothing has been checked.
        var run = RunAgainst(
            """
            <!-- class-names:start -->
            | `munin-explorer` | handle |
            <!-- class-names:end -->
            """);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("below the floor", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("does not list them", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheMarkersAreGone_ThenItSaysSoRatherThanReportingEveryName()
    {
        // Deleting the block is the easiest way to make a nagging check quiet, and the exit code is
        // what makes it loud instead: 2 rather than 0, so a README with no inventory in it fails CI
        // rather than passing it.
        var run = RunAgainst("# Fhi.Munin.Explorer\n\nNo inventory here.\n");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("no inventory block", run.Output, StringComparison.Ordinal);
    }

    private static GuardRun RunAgainst(string readme)
    {
        var file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".md");

        try
        {
            File.WriteAllText(file, readme);

            return Guard.RunScript(Script, new Dictionary<string, string> { ["README_FILE"] = file });
        }
        finally
        {
            File.Delete(file);
        }
    }
}
