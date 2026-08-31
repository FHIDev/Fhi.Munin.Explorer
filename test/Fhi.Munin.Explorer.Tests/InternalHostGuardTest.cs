using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

// That the checkout names the Munin test API only through a host published outside FHI's network
// (Fhi.Metadata-ip02g). Hostnames only: a private IP or an internal repository name in the docs is
// the same leak and goes unread, so docs/running-locally.md describes that split in prose.
public class InternalHostGuardTest
{
    // The preceding label is part of the match on purpose: searching for the bare name alone also
    // matches the corrected runa./kelda. forms, and would call an uncorrected file clean.
    private static readonly Regex AnyMuninTestHost =
        new(@"(?<prefix>[A-Za-z0-9.-]*)munin\.skytest\.fhi\.no", RegexOptions.IgnoreCase);

    private static readonly string[] ExternalPrefixes = ["runa.", "kelda."];

    /// <summary>Put on a line whose point is the internal host, such as an assertion that a message
    /// does not contain it. Per line, so a file is never exempt as a whole, and the test below
    /// keeps it out of every root but <c>test/</c>.</summary>
    private const string ExemptMarker = "internal-host-on-purpose";

    /// <summary>The internal host, written once here so the cases below can be read literally.</summary>
    private const string Internal = "https://munin.skytest.fhi.no"; // internal-host-on-purpose

    /// <summary>Walked in full. Files at the top of the checkout are added separately, so a new one
    /// there is covered without being named.</summary>
    private static readonly string[] Roots =
        ["src", "samples", "test", "docs", "scripts", ".github", "changelog.d"];

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
                if (InternalHostsIn(lines[i]) > 0)
                {
                    offenders.Add(
                        $"{Path.GetRelativePath(Repo.Root, file)}:{i + 1}: {lines[i].Trim()}");
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

    [Fact]
    public void Sources_WhenTheyAreEnumerated_ThenOneFileFromEveryRootIsAmongThem()
    {
        // The check above passes by reading nothing, so the roots are anchored here and this is the
        // test that fails when one of them moves.
        var found = Sources()
            .Select(file => Path.GetRelativePath(Repo.Root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("src/Fhi.Munin.Explorer/Client/ServiceCollectionExtensions.cs", found);
        Assert.Contains("samples/LegacyHost/Program.cs", found);
        Assert.Contains("test/Fhi.Munin.Explorer.Tests/LiveApi.cs", found);
        Assert.Contains("docs/running-locally.md", found);
        Assert.Contains("scripts/check-accessibility.sh", found);
        Assert.Contains(".github/workflows/contract-drift.yml", found);
        Assert.Contains("changelog.d/README.md", found);
        Assert.Contains("README.md", found);
    }

    [Fact]
    public void Sources_WhenTheyCarryTheExemptMarker_ThenOnlyUnderTheTestProject()
    {
        // Per line is not per place: the marker pasted into a copy target silences the guard over
        // exactly the files it exists to keep copyable, and the ordinary way there is fixing a red
        // test by carrying the comment that made it green along with the snippet.
        var elsewhere = Sources()
            .Where(file => File.ReadAllText(file).Contains(ExemptMarker, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Repo.Root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.StartsWith("test/", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            elsewhere.Count == 0,
            "The exemption is only for lines under test/ whose subject is the internal host. These "
            + "are copy targets, so a line exempted here ships the host to a reader who cannot "
            + "reach it:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, elsewhere));
    }

    [Theory]
    // A guard CI only ever runs against clean input asserts nothing: an empty result is what a
    // broken matcher gives too. So the offending forms are fed in here directly.
    [InlineData(Internal, 1)]
    [InlineData("https://runa.munin.skytest.fhi.no", 0)]
    [InlineData("https://kelda.munin.skytest.fhi.no", 0)]
    [InlineData("https://api.runa.munin.skytest.fhi.no", 0)]
    [InlineData("https://notruna.munin.skytest.fhi.no", 1)] // internal-host-on-purpose
    [InlineData("https://xkelda.munin.skytest.fhi.no", 1)] // internal-host-on-purpose
    [InlineData("MuninExplorer__ApiBaseUrl: https://runa.munin.skytest.fhi.no", 0)]
    [InlineData(Internal + " and https://runa.munin.skytest.fhi.no on one line", 1)]
    [InlineData(Internal + " twice " + Internal, 2)]
    [InlineData(Internal + " // " + ExemptMarker, 0)]
    [InlineData("nothing here names a host", 0)]
    public void Line_WhenItIsMatched_ThenOnlyTheLabelsPublishedOutsideFhiCountAsExternal(
        string line, int expected) =>
        Assert.Equal(expected, InternalHostsIn(line));

    /// <summary>How many times a line names the internal host: the seam the theory above pins.</summary>
    private static int InternalHostsIn(string line) =>
        line.Contains(ExemptMarker, StringComparison.Ordinal)
            ? 0
            : AnyMuninTestHost.Matches(line).Count(match => !IsExternal(match.Groups["prefix"].Value));

    // Whole labels only. Bare EndsWith would wave through notruna., which is a name of its own.
    private static bool IsExternal(string prefix) =>
        ExternalPrefixes.Any(external =>
            prefix.Equals(external, StringComparison.OrdinalIgnoreCase)
            || prefix.EndsWith("." + external, StringComparison.OrdinalIgnoreCase));

    // A missing root throws rather than reading nothing, which is how the file-or-directory fallback
    // this replaced would have turned a renamed docs/ into an empty walk and a green test.
    private static IEnumerable<string> Sources() =>
        Roots.SelectMany(root => Directory.Exists(Repo.In(root))
                ? Directory.EnumerateFiles(Repo.In(root), "*", SearchOption.AllDirectories)
                : throw new InvalidOperationException(
                    $"'{root}' is not a directory under '{Repo.Root}', so this guard would pass by "
                    + "reading nothing. Renamed? Update Roots."))
            .Concat(Directory.EnumerateFiles(Repo.Root, "*", SearchOption.TopDirectoryOnly))
            .Where(file => Readable.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
}
