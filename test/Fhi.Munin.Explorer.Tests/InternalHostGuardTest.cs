using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// That nothing a host developer can copy out of <c>src/</c> or <c>samples/</c> names the
/// unprefixed <c>munin.skytest.fhi.no</c>, which answers only from inside FHI's network.
/// </summary>
/// <remarks>
/// Reads the sources, because the way this went wrong was a site nobody thought to look at
/// (Fhi.Metadata-ip02g). Matching the preceding label is load-bearing: searching for the bare
/// name alone also matches the corrected form, and calls a file clean that never was.
/// </remarks>
public class InternalHostGuardTest
{
    private static readonly Regex AnyMuninTestHost =
        new(@"(?<prefix>[A-Za-z0-9.-]*)munin\.skytest\.fhi\.no");

    private static readonly string[] Prefixed = ["runa.", "kelda."];

    // Text a developer can copy from. Build output is skipped because it is a copy of what is
    // already checked, and would report every offender twice.
    private static readonly string[] Readable =
        [".cs", ".razor", ".cshtml", ".html", ".css", ".js", ".json", ".md", ".txt", ".xml",
         ".csproj", ".props", ".targets", ".yml", ".yaml", ".config", ".sh"];

    [Fact]
    public void Sources_WhenTheyNameTheMuninTestApi_ThenAlwaysThroughAnExternallyPublishedHost()
    {
        var offenders = new List<string>();

        foreach (var file in Sources())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in AnyMuninTestHost.Matches(lines[i]))
                {
                    if (!Prefixed.Contains(match.Groups["prefix"].Value))
                    {
                        offenders.Add(
                            $"{Path.GetRelativePath(Repo.Root, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These name the internal Munin host, which is unreachable outside FHI's network and fails "
            + "silently when copied. Use runa.munin.skytest.fhi.no (or kelda.) instead:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> Sources() =>
        new[] { Repo.In("src"), Repo.In("samples") }
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(file => Readable.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
}
