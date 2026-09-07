using System.Globalization;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/assert-drift-ran.sh</c> against results broken on purpose, so its diagnostics
/// have been read at least once: a guard nothing has ever watched fail can say anything, and this
/// one said something untrue for four nights (<c>Fhi.Metadata-wpcb3</c>, docs/contract-drift.md).
/// </summary>
public class DriftRanGuardTest
{
    private const string Category = "ContractDrift";

    /// <summary>What the authenticated arm says when it skips — the attribute's own words.</summary>
    private static readonly string TokenReason =
        $"Writes to a signed-in reader's own list. Set {LiveApi.TokenVariable} to an explorer " +
        "access token for https://runa.munin.skytest.fhi.no to run it.";

    private const string LiveReason =
        "Calls the live API. Set MUNIN_EXPLORER_LIVE=1 to run it (against https://runa.munin.skytest.fhi.no).";

    [ShellFact]
    public void Guard_WhenEveryTestRan_ThenItPasses()
    {
        // The control the others stand on. Without it a red run proves nothing about the mutation:
        // it could be the seam — an unreadable TRX, a bash that cannot find the script — failing
        // every time it is used.
        var run = RunAgainst(Trx(executed: 10), minimum: 10, authenticated: 1);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("10 found, 10 executed, 0 skipped", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("UNCHECKED", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheAuthenticatedArmSkipped_ThenItPassesAndSaysThatHalfWasNotChecked()
    {
        // The state every unattended run is in and will stay in: no schedule can hold an ID-porten
        // access token. Green, because a nightly that is red forever gets muted and takes the read
        // half with it — but never green quietly, which is the whole trade.
        var run = RunAgainst(
            Trx(executed: 9, ("DesiredData_WhenWrittenToTheLiveApi_ThenItSurvivesAReadBack", TokenReason)),
            minimum: 10,
            authenticated: 1);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("UNCHECKED", run.Output, StringComparison.Ordinal);
        Assert.Contains("DesiredData_WhenWrittenToTheLiveApi", run.Output, StringComparison.Ordinal);
        Assert.Contains(LiveApi.TokenVariable, run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenATestIsGone_ThenTheFloorFiresEvenThoughEveryRemainingTestRan()
    {
        // Read off `total`, not `executed`: with the old guard this exact run passed its floor,
        // because eight executed tests clear a floor of eight however many used to exist.
        var run = RunAgainst(
            Trx(executed: 8, ("DesiredData_WhenWrittenToTheLiveApi_ThenItSurvivesAReadBack", TokenReason)),
            minimum: 10,
            authenticated: 1);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("Expected 10 ContractDrift tests to exist; the run found 9", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheLiveGateNeverReachedTheJob_ThenItFailsAndQuotesTheGateTheTestsNamed()
    {
        // The failure the guard was written for. It has to stay red and stay distinguishable from
        // the token skip, which is why the two are counted apart rather than as one skip column.
        var run = RunAgainst(
            Trx(executed: 0, Enumerable.Range(1, 10).Select(i => ($"Test{i}", LiveReason)).ToArray()),
            minimum: 10,
            authenticated: 1);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("10 of 10 ContractDrift tests were skipped", run.Output, StringComparison.Ordinal);
        Assert.Contains("MUNIN_EXPLORER_LIVE=1", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenATestSkippedForSomeOtherReason_ThenItFailsAndRepeatsTheReasonTheTestGave()
    {
        // The case the old message described as "a Skip= somebody left on a [Fact]" without ever
        // saying which fact or what it said, so a reader had ten tests and no reason to start from.
        var run = RunAgainst(
            Trx(executed: 9, ("KildeDetail_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt", "Flaky against the test API, back on Monday")),
            minimum: 10,
            authenticated: 1);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("KildeDetail_WhenReadFromTheLiveApi", run.Output, StringComparison.Ordinal);
        Assert.Contains("Flaky against the test API, back on Monday", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenMoreTestsWantTheTokenThanTheCallerDeclared_ThenItFails()
    {
        // Otherwise the token reason would be a phrase anybody could paste into a Skip= to make a
        // test disappear from the count. The declared number is the bound, the same way the floor is.
        var run = RunAgainst(
            Trx(executed: 8, ("DesiredData_A", TokenReason), ("DesiredData_B", TokenReason)),
            minimum: 10,
            authenticated: 1);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("the caller declared 1", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheCountersSaySkippedAndTheResultsCannotSayWhich_ThenItFailsClosed()
    {
        // A logger that stops writing per-test results would otherwise leave the guard reporting a
        // skip count it can no longer explain, which is the state it exists to make impossible.
        var run = RunAgainst(Trx(executed: 9, total: 10, skipped: []), minimum: 10, authenticated: 1);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("the results list 0", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenVariable_WhenTheGuardSortsSkipsByIt_ThenTheScriptSpellsItTheSameWay()
    {
        // Written in C# and matched by bash, so nothing but this connects them: rename the constant
        // and every authenticated skip silently becomes "somebody left a Skip= on a [Fact]".
        var script = File.ReadAllText(Repo.In("scripts", "assert-drift-ran.sh"));

        Assert.Contains($"TOKEN_VARIABLE={LiveApi.TokenVariable}", script, StringComparison.Ordinal);
    }

    private static GuardRun RunAgainst(string trx, int minimum, int authenticated)
    {
        var dir = Directory.CreateTempSubdirectory("munin-drift-guard");

        try
        {
            var path = Path.Combine(dir.FullName, "results.trx");

            File.WriteAllText(path, trx);

            return Guard.RunIn(
                "assert-drift-ran.sh",
                dir.FullName,
                path,
                minimum.ToString(CultureInfo.InvariantCulture),
                Category,
                authenticated.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            Guard.Discard(dir);
        }
    }

    /// <summary>
    /// A results file holding <paramref name="executed"/> passing tests plus one
    /// <c>NotExecuted</c> result per <paramref name="skipped"/> entry.
    /// </summary>
    private static string Trx(int executed, params (string Name, string Reason)[] skipped) =>
        Trx(executed, executed + skipped.Length, skipped);

    /// <summary>
    /// The same, with a <c>total</c> of its own so a run can claim more tests than it recorded —
    /// the only way to reach the fail-closed clause.
    /// </summary>
    /// <remarks>
    /// Copied from what VSTest really writes: the skip reason in an <c>ErrorInfo/Message</c>, and
    /// <c>notExecuted</c> at 0 however many skipped, because an xUnit skip decided at construction
    /// never reaches the logger as a test — the quirk the guard subtracts around.
    /// </remarks>
    private static string Trx(int executed, int total, (string Name, string Reason)[] skipped)
    {
        var results = string.Concat(Enumerable.Range(1, executed).Select(i =>
            $"""<UnitTestResult testName="Fhi.Munin.Explorer.Tests.ContractDriftTest.Ran{i}" outcome="Passed" />"""));

        results += string.Concat(skipped.Select(test =>
            $"""
             <UnitTestResult testName="Fhi.Munin.Explorer.Tests.ContractDriftTest.{test.Name}" outcome="NotExecuted">
               <Output><ErrorInfo><Message>{test.Reason.Replace("&", "&amp;", StringComparison.Ordinal)}</Message></ErrorInfo></Output>
             </UnitTestResult>
             """));

        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <TestRun>
                  <Results>{results}</Results>
                  <ResultSummary outcome="Completed">
                    <Counters total="{total}" executed="{executed}" passed="{executed}" failed="0" notExecuted="0" />
                  </ResultSummary>
                </TestRun>
                """;
    }
}
