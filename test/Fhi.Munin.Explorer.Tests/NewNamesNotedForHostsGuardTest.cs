namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/assert-new-names-noted-for-hosts.sh</c> against throwaway repositories built to
/// break it and asserts that it goes red, and red for the right reason (Fhi.Metadata-wbm1w).
/// </summary>
/// <remarks>
/// The script cds to its own <c>$0/..</c>, so no seam can aim it at a fixture: each case runs a COPY
/// of it inside the repository under test, making that its checkout root and every path it reads the
/// fixture's own. Only <c>BASE_REF</c> is passed in.
/// </remarks>
public class NewNamesNotedForHostsGuardTest
{
    private const string Script = "assert-new-names-noted-for-hosts.sh";

    private const string Name = "munin-explorer-fixture-new";

    private const string Existing = "src/Fhi.Munin.Explorer/Blazor/Existing.razor";

    private const string Added = "src/Fhi.Munin.Explorer/Blazor/Added.razor";

    private const string Heading = "no 'Notes for hosts' fragment names them:";

    /// <summary>
    /// Above the script's own floor of 10, which applies to the base side as well as the head — so
    /// a fixture carrying only the name under test would exit 2 before reaching any clause.
    /// </summary>
    private const int ExistingNames = 12;

    [ShellFact]
    public void Guard_WhenTheBranchAddsNoNewName_ThenItPasses()
    {
        // The control every other case stands on: without it a red run proves nothing, because the
        // fixture itself — a copied script judging a repository we built — could be what fails.
        var run = RunAgainst(newName: null);

        AssertExit(0, run);
        Assert.Contains("No new munin-explorer class names", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenANewNameIsNotedForHosts_ThenItPasses()
    {
        // The other half of the control: the demand is satisfiable, so a red run below means the
        // clause was reached and answered rather than never reached.
        var run = RunAgainst(new Dictionary<string, string>
        {
            ["changelog.d/thing-vertsstiler.md"] = Note(Name),
        });

        AssertExit(0, run);
        Assert.Contains("All new class names are named", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenANewNameIsOnlyInAnAddedFragment_ThenItIsReported()
    {
        // The defect itself. Only line 1 of a fragment decides whether it counts, so a name written
        // down in the section a host does not read for styling is a name nobody will style.
        var run = RunAgainst(new Dictionary<string, string>
        {
            ["changelog.d/thing.md"] = $"category: Added\n\n- A panel wearing `{Name}`.\n",
        });

        AssertExit(1, run);
        Assert.Contains(Name, Guard.NamesUnder(run.Output, Heading)[0]);
    }

    [ShellFact]
    public void Guard_WhenTheNoteNamesALongerNameThatStartsTheSameWay_ThenItDoesNotCount()
    {
        // Every name here shares the prefix, so a substring test would let a note about one member
        // of a family stand in for the family — the miss this guard exists to prevent.
        var run = RunAgainst(new Dictionary<string, string>
        {
            ["changelog.d/thing-vertsstiler.md"] = Note(Name + "-extra"),
        });

        AssertExit(1, run);
        Assert.Contains(Name, Guard.NamesUnder(run.Output, Heading)[0]);
    }

    [ShellFact]
    public void Guard_WhenTheNotesRunPastAPipeBufferInLength_ThenTheNameStillCountsAsNoted()
    {
        // Pins the here-string (Fhi.Metadata-yvldl, and the sibling's own case). Piping the notes
        // into `grep -q` under `pipefail` lets grep leave while printf is still writing, so printf
        // takes SIGPIPE and a noted name comes back a violation. Past 64 KiB that is a certainty.
        var filler = string.Join('\n', Enumerable.Repeat("- Filler, naming nothing.", 12_000));

        var run = RunAgainst(new Dictionary<string, string>
        {
            ["changelog.d/thing-vertsstiler.md"] = Note(Name) + filler,
        });

        AssertExit(0, run);
    }

    [ShellFact]
    public void Guard_WhenTheBaseRefCannotBeResolved_ThenItSaysSoRatherThanPassing()
    {
        // Exit 2, not 0: an unfetched base is the state a CI job is in before it fetches, and a
        // check that passed there would call every name new or, worse, quietly call none of them.
        using var repo = BaseRepository();

        CommitBranch(repo, Name);

        var run = Run(repo, baseRef: "refs/heads/no-such-base");

        AssertExit(2, run);
        Assert.Contains("is not in this checkout", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenTheExtractionFallsBelowTheNameFloor_ThenItSaysSoRatherThanPassing()
    {
        // The floor is what stands between a stale regex and a green run: extracting nothing makes
        // every branch look clean, which is the one failure a name guard must never have.
        using var repo = BaseRepository(names: 3);

        CommitBranch(repo, Name);

        var run = Run(repo);

        AssertExit(2, run);
        Assert.Contains("below the floor of 10", run.Output, StringComparison.Ordinal);
    }

    /// <summary>A "Notes for hosts" fragment naming <paramref name="name"/>.</summary>
    private static string Note(string name) =>
        $"category: Notes for hosts\n\n- `{name}` needs a rule once the host has room for it.\n";

    /// <summary>Markup carrying <paramref name="names"/> the way the script's extraction reads them.</summary>
    private static string Markup(IEnumerable<string> names) =>
        string.Concat(names.Select(name => $"<div class=\"{name}\"></div>\n"));

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
    /// Builds a repository whose branch commit introduces <paramref name="newName"/> and carries
    /// <paramref name="fragments"/>, then runs the script over it the way CI does.
    /// </summary>
    private static GuardRun RunAgainst(
        IReadOnlyDictionary<string, string>? fragments = null,
        string? newName = Name)
    {
        using var repo = BaseRepository();

        CommitBranch(repo, newName, fragments);

        return Run(repo);
    }

    /// <summary>
    /// A repository holding the base commit, with the script copied in so that its own
    /// <c>$0/..</c> anchor lands here rather than in this checkout.
    /// </summary>
    private static GuardRepository BaseRepository(int names = ExistingNames)
    {
        var repo = new GuardRepository("munin-new-names-guard");

        repo.Write("scripts/" + Script, File.ReadAllText(Repo.In("scripts", Script)));
        repo.Write(Existing, Markup(Enumerable.Range(1, names).Select(i => $"munin-explorer-fixture-{i:D2}")));
        repo.Commit("base");

        return repo;
    }

    /// <summary>The branch commit: <paramref name="newName"/> if there is one, plus any fragments.</summary>
    private static void CommitBranch(
        GuardRepository repo,
        string? newName,
        IReadOnlyDictionary<string, string>? fragments = null)
    {
        // Something has to change or there is no second commit for HEAD~1 to name, and a branch
        // that adds no name is itself a case: the script has to pass it rather than skip it.
        if (newName is null)
        {
            repo.Write("docs/notes.md", "# A branch that names nothing new\n");
        }
        else
        {
            repo.Write(Added, Markup([newName]));
        }

        foreach (var (file, body) in fragments ?? new Dictionary<string, string>())
        {
            repo.Write(file, body);
        }

        repo.Commit("branch");
    }

    /// <summary>Runs the fixture's own copy of the script, with only the base ref passed in.</summary>
    private static GuardRun Run(GuardRepository repo, string baseRef = "HEAD~1") =>
        Guard.RunAt(
            Path.Combine(repo.Path, "scripts", Script),
            new Dictionary<string, string> { ["BASE_REF"] = baseRef });
}
