namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runs <c>scripts/assert-fragment-names-noted-for-hosts.sh</c> against changelog fragments broken
/// on purpose and asserts that it goes red, and red for the right reason.
/// </summary>
/// <remarks>
/// <para>
/// On a correct tree the script prints a banner and exits 0, which is exactly what a guard that had
/// stopped working would also do — so a run against the tree as it stands proves nothing about any
/// clause in it. The mutations below are the proof.
/// </para>
/// <para>
/// The seams are <c>CHANGELOG_DIR</c> and <c>CHANGELOG_FILE</c>, and they are the only ones:
/// <c>src/</c> is read from the checkout, so the staleness floor under test is the real extraction
/// over the real component.
/// </para>
/// </remarks>
public class FragmentHostNotesGuardTest
{
    private const string Script = "assert-fragment-names-noted-for-hosts.sh";

    /// <summary>
    /// The facet panel's toggle — the name this guard exists for. It reached `main` naming its own
    /// host requirement in an `Added` bullet, three weeks before the branch-diff check that would
    /// have asked about it, so it was never new on a branch that check could see.
    /// </summary>
    private const string Name = "munin-explorer-filters__toggle";

    private const string Added = "category: Added\n\n- The panel folds, and a host styles "
                                 + $"`{Name}` to undo the fold.\n";

    private const string Noted = $"category: Notes for hosts\n\n- `{Name}` needs `display: none` "
                                 + "once the host has room for a sidebar.\n";

    [ShellFact]
    public void Guard_WhenTheRealFragmentsArePassedThroughTheSeam_ThenTheyPass()
    {
        // The control the others stand on. Without it a run that exits 1 proves nothing about the
        // mutation: it could be the seam itself failing every time it is used.
        var run = Guard.RunScript(Script, new Dictionary<string, string>
        {
            ["CHANGELOG_DIR"] = Repo.In("changelog.d"),
            ["CHANGELOG_FILE"] = Repo.In("CHANGELOG.md"),
        });

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("is named for hosts", run.Output, StringComparison.Ordinal);
    }

    [ShellFact]
    public void Guard_WhenAnAddedFragmentNamesAClassNoHostNoteNames_ThenItIsReported()
    {
        // The defect itself, and the case the branch diff cannot reach: the name is not new, it is
        // simply written down in the section a host does not read for styling.
        var run = RunAgainst(new Dictionary<string, string> { ["facets.md"] = Added });

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(Name, Guard.NamesUnder(run.Output, "no 'Notes for hosts' fragment names:")[0]);
    }

    [ShellFact]
    public void Guard_WhenAHostNoteNamesIt_ThenItPasses()
    {
        // The pairing, not merely "some fragment mentions it": the same Added fragment goes green
        // the moment a Notes for hosts fragment names the same name.
        var run = RunAgainst(new Dictionary<string, string>
        {
            ["facets.md"] = Added,
            ["facets-vertsstiler.md"] = Noted,
        });

        Assert.Equal(0, run.ExitCode);
    }

    [ShellFact]
    public void Guard_WhenTheNoteHasAlreadyBeenReleased_ThenItStillCounts()
    {
        // assemble-changelog.ps1 empties changelog.d at release and folds the notes into
        // CHANGELOG.md. A later fragment naming an already-noted name is not a defect, and a guard
        // that called it one would fire on every release.
        var run = RunAgainst(
            new Dictionary<string, string> { ["facets.md"] = Added },
            changelog: $"# Changelog\n\n## 0.2.0 — 2026-09-01\n\n### Added\n\n- Something else.\n\n"
                       + $"### Notes for hosts\n\n- `{Name}` needs `display: none`.\n");

        Assert.Equal(0, run.ExitCode);
    }

    [ShellFact]
    public void Guard_WhenTheNoteNamesALongerNameThatStartsTheSameWay_ThenItDoesNotCount()
    {
        // Every name here shares the prefix, so a substring test would let a note about one member
        // of a family stand in for the family — the miss this guard exists to prevent.
        var run = RunAgainst(new Dictionary<string, string>
        {
            ["facets.md"] = Added,
            ["facets-vertsstiler.md"] = Noted.Replace(Name, Name + "-extra", StringComparison.Ordinal),
        });

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(Name, Guard.NamesUnder(run.Output, "no 'Notes for hosts' fragment names:")[0]);
    }

    [ShellFact]
    public void Guard_WhenTheFragmentDirectoryIsGone_ThenItSaysSoRatherThanPassing()
    {
        // Deleting the directory is the easiest way to make a nagging check quiet, and the exit
        // code is what makes it loud instead: 2 rather than 0, so it fails CI rather than passing.
        var run = Guard.RunScript(Script, new Dictionary<string, string>
        {
            ["CHANGELOG_DIR"] = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            ["CHANGELOG_FILE"] = Repo.In("CHANGELOG.md"),
        });

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("is missing, so there are no fragments", run.Output, StringComparison.Ordinal);
    }

    /// <summary>Runs the script over a fragment directory written for the case under test.</summary>
    private static GuardRun RunAgainst(IReadOnlyDictionary<string, string> fragments, string? changelog = null)
    {
        var dir = Directory.CreateTempSubdirectory("munin-fragment-guard");

        // Outside the fragment directory on purpose: a released changelog left inside it would be
        // read as one more fragment, and the case under test would not be the one written down.
        var file = changelog is null
            ? Repo.In("CHANGELOG.md")
            : Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".md");

        try
        {
            foreach (var (name, body) in fragments)
            {
                File.WriteAllText(Path.Combine(dir.FullName, name), body);
            }

            if (changelog is not null)
            {
                File.WriteAllText(file, changelog);
            }

            return Guard.RunScript(Script, new Dictionary<string, string>
            {
                ["CHANGELOG_DIR"] = dir.FullName,
                ["CHANGELOG_FILE"] = file,
            });
        }
        finally
        {
            dir.Delete(recursive: true);

            if (changelog is not null)
            {
                File.Delete(file);
            }
        }
    }
}
