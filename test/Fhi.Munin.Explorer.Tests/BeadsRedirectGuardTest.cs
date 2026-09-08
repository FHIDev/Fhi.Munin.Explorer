using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The tracked <c>.beads/redirect</c>, which ties this repo's beads to the shared Munin
/// pool. Something keeps replacing it with an absolute path — four times, cause unestablished — and
/// both failures are silent: a second database, and a broken Windows checkout (Fhi.Metadata-l9l2n.46).</summary>
public class BeadsRedirectGuardTest
{
    /// <summary>Where the Munin checkout sits relative to this one, lower-case for Linux.</summary>
    private const string Target = "../fhi.metadata/.beads";

    /// <summary>The drive-absolute form, which is the half of "absolute" a leading slash misses.</summary>
    private static readonly Regex DriveLetter = new("^[A-Za-z]:");

    [Fact]
    public void Redirect_WhenItIsRead_ThenItsOnlyPathLineIsTheRelativeMuninStore()
    {
        var paths = PathLines();

        Assert.True(
            paths.Count == 1,
            $"Expected exactly one non-comment line in .beads/redirect, found {paths.Count}: "
            + string.Join(" | ", paths));

        Assert.Equal(Target, paths[0]);
    }

    [Fact]
    public void Redirect_WhenItIsRead_ThenThePathIsRelativeRatherThanAContainerOrDrivePath()
    {
        var path = Assert.Single(PathLines());

        Assert.False(
            path.StartsWith('/') || DriveLetter.IsMatch(path),
            $"'{path}' is absolute, so it resolves on the box that wrote it and nowhere else. "
            + "The redirect resolves from the project root and has to stay relative.");
    }

    [Fact]
    public void Redirect_WhenItIsRead_ThenTheCommentExplainingTheTwoTrapsSurvives()
    {
        var text = Text();

        // Asserted apart from the path because both regressions deleted the comment with it, and a
        // check on the path alone goes green again the moment someone restores only the path.
        Assert.Contains("Do NOT run `bd init` here", text, StringComparison.Ordinal);
        Assert.Contains("Lower-case on purpose", text, StringComparison.Ordinal);
    }

    private static string Text() => File.ReadAllText(Repo.In(".beads", "redirect"));

    /// <summary>The redirect target: lines that are neither comment nor blank. Split on '\n' with
    /// the return trimmed, not Environment.NewLine — LF on disk, CRLF under autocrlf, and this runs
    /// on Windows and on CI's Linux both.</summary>
    private static IReadOnlyList<string> PathLines() =>
        [.. Text()
            .Split('\n')
            .Select(line => line.TrimEnd('\r').Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))];
}
