using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>That nothing a host developer can copy — the sources, the docs, the README that ships
/// inside the package — names the unprefixed Munin test host, internal to FHI (Fhi.Metadata-ip02g).</summary>
public class InternalHostGuardTest
{
    // The preceding label is part of the match on purpose: searching for the bare name alone also
    // matches the corrected runa./kelda. forms, and would call an uncorrected file clean.
    private static readonly Regex AnyMuninTestHost =
        new(@"(?<prefix>[A-Za-z0-9.-]*)munin\.skytest\.fhi\.no", RegexOptions.IgnoreCase);

    private static readonly string[] ExternalPrefixes = ["runa.", "kelda."];

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
                    if (!IsExternal(match.Groups["prefix"].Value))
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

    // EndsWith rather than equality, so a deeper name below runa. is still external.
    private static bool IsExternal(string prefix) =>
        ExternalPrefixes.Any(external => prefix.EndsWith(external, StringComparison.OrdinalIgnoreCase));

    // README.md is here because it ships inside the package as PackageReadmeFile and is rendered on
    // the feed's package page, which makes its registration snippet the most direct copy target of all.
    private static IEnumerable<string> Sources() =>
        new[] { Repo.In("src"), Repo.In("samples"), Repo.In("docs"), Repo.In("README.md") }
            .SelectMany(path => Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                : new[] { path })
            .Where(file => Readable.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
}
