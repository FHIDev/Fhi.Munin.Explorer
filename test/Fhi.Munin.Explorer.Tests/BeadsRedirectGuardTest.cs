using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The tracked <c>.beads/redirect</c>, which is the only thing tying this repository's beads to the
/// shared Munin pool.
/// </summary>
/// <remarks>
/// <para>
/// Something in the forge's branch flow replaces this file with an absolute path to the parent
/// anvil, which is right in a repository that owns its bead store and wrong here, where the file is
/// hand-written and deliberately points at a different repository. What does it is not established:
/// <c>bd worktree create</c> is ruled out, having lost that behaviour upstream before either build
/// in use. So this guard is deliberately aimed at the outcome rather than the cause — it has
/// reached a pull request four times, and each repair restored the file and added nothing that
/// would notice the next one (Fhi.Metadata-l9l2n.46).
/// </para>
/// <para>
/// Neither failure it causes is loud. An absolute path aimed at an empty store makes <c>bd</c>
/// create a second database, so work leaves the shared board rather than erroring; and the path
/// resolves from the project root, so every non-Linux checkout looks for the container path under
/// its own drive and warns that the target is missing. With auto-merge on forge pull requests,
/// CI is the last gate before main, so the guard has to be a failing build.
/// </para>
/// </remarks>
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

    /// <summary>
    /// The lines that are neither comments nor blank: the redirect target, and nothing else.
    /// </summary>
    /// <remarks>
    /// Split on <c>'\n'</c> with the carriage return trimmed rather than on
    /// <c>Environment.NewLine</c>: the file is LF on disk and arrives CRLF in a checkout with
    /// autocrlf, and the test has to pass on Windows and on CI's Linux both.
    /// </remarks>
    private static IReadOnlyList<string> PathLines() =>
        [.. Text()
            .Split('\n')
            .Select(line => line.TrimEnd('\r').Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))];
}
