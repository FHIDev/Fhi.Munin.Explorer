using System.Text;
using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// That no <c>catch (Exception)</c> in the package throws the exception away.
/// </summary>
/// <remarks>
/// Thirty of them did, and the one that mattered took an afternoon across two repositories, the
/// CMS and the live API to not diagnose (Fhi.Metadata-l9l2n.47). A package that logs one failure
/// in thirty is no more legible than one that logs none, so this is what keeps the next site from
/// being added blind.
/// </remarks>
public class SwallowedExceptionGuardTest
{
    /// <summary>The clause, with the identifier it binds — none, where it binds nothing.</summary>
    private static readonly Regex CatchClause =
        new(@"catch\s*\(\s*Exception\s*(?<name>[A-Za-z_]\w*)?\s*\)", RegexOptions.Compiled);

    /// <summary>What counts as recording it: a log call the caught exception itself is passed to.</summary>
    /// <remarks>
    /// The exception, not merely a call. "Something was logged" passes for code that writes the
    /// reader-facing sentence and drops the stack, which is the defect wearing a hat.
    /// </remarks>
    private static Regex LoggedWith(string name) =>
        new(@"\bLog(Critical|Error|Warning|Information|Debug|Trace)\s*\(\s*" + Regex.Escape(name) + @"\s*,",
            RegexOptions.Compiled);

    [Fact]
    public void Catches_WhenTheyAreInThePackage_ThenEveryOneRecordsTheExceptionOrLetsItTravelOn()
    {
        var offenders = Sources()
            .SelectMany(file => Offenders(File.ReadAllText(file))
                .Select(offender => $"{Relative(file)}:{offender.Line}: {offender.Clause}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These catch every exception and neither log it nor rethrow it, so a fault on a host's "
            + "server is diagnosable only by elimination. Pass the exception to an ILogger — the "
            + "first argument, so the stack survives — or let it travel on:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Catches_WhenTheyAreCounted_ThenTheWalkFoundTheOnesTheIncidentWasAbout()
    {
        // The check above passes by reading nothing, and a scanner that matched no clause at all
        // would look exactly as green. So the walk is anchored: these are two of the sites the
        // incident named, and the total is a floor rather than a tally to keep up to date.
        var found = Sources()
            .ToDictionary(Relative, file => Clauses(File.ReadAllText(file)).Count);

        Assert.True(
            found.GetValueOrDefault("src/Fhi.Munin.Explorer/Blazor/KildeSearch.razor.cs") >= 4,
            "KildeSearch is where the incident landed and it held five of these. Fewer than four "
            + "means the scanner has stopped matching rather than that the file has changed.");

        Assert.True(
            found.GetValueOrDefault("src/Fhi.Munin.Explorer/Blazor/VariableListView.razor.cs") >= 8,
            "VariableListView held eleven of the thirty. Fewer than eight means the same.");

        Assert.True(
            found.Values.Sum() >= 24,
            $"Only {found.Values.Sum()} clauses walked, and thirty were there when this was written. "
            + "A floor rather than a tally — well under it is a broken matcher, not a tidier package.");
    }

    [Theory]
    // A guard only ever run over clean input asserts nothing, so the offending shapes are fed in
    // directly — including the two near misses: a log call that drops the exception, and a brace
    // inside a message template, which naive matching reads as the end of the body.
    [InlineData("catch (Exception) { _error = T.Error; }", 1)]
    [InlineData("catch (Exception ex) { _error = T.Error; }", 1)]
    [InlineData("catch (Exception ex) { Log?.LogError(\"failed\"); }", 1)]
    [InlineData("catch (Exception ex) { Log?.LogError(\"failed {Id}\", id); }", 1)]
    [InlineData("catch (Exception ex) { Log?.LogError(ex, \"failed\"); }", 0)]
    [InlineData("catch (Exception ex) { Log?.LogError(ex, \"failed {Id}\", id); }", 0)]
    [InlineData("catch (Exception ex) { _logger?.LogWarning(ex, \"refused\"); }", 0)]
    [InlineData("catch (Exception cause) when (cause is not X) { _logger?.LogWarning(cause, \"x\"); }", 0)]
    [InlineData("catch (Exception cause) when (cause is not X) { return null; }", 1)]
    [InlineData("catch (Exception) { _retained = false; throw; }", 0)]
    [InlineData("catch (Exception ex) { if (stale) { return; } Log?.LogError(ex, \"x\"); }", 0)]
    [InlineData("catch (Exception ex) { if (stale) { return; } _error = T.Error; }", 1)]
    [InlineData("catch (Exception ex) { Log?.LogError(ex, \"a } brace\"); }", 0)]
    [InlineData("catch (JsonException) { return null; }", 0)]
    [InlineData("catch (MuninExplorerRateLimitedException) { _error = T.RateLimitError; }", 0)]
    [InlineData("nothing here catches anything", 0)]
    public void Source_WhenItIsScanned_ThenOnlyADiscardedExceptionIsReported(string source, int expected) =>
        Assert.Equal(expected, Offenders(source).Count);

    private sealed record Offender(int Line, string Clause);

    /// <summary>Every <c>catch (Exception …)</c> in a source, with the body it guards.</summary>
    private static List<(Match Clause, string Body)> Clauses(string source)
    {
        var found = new List<(Match, string)>();

        foreach (Match clause in CatchClause.Matches(source))
        {
            var open = source.IndexOf('{', clause.Index + clause.Length);

            if (open < 0)
            {
                continue;
            }

            found.Add((clause, Block(source, open)));
        }

        return found;
    }

    private static List<Offender> Offenders(string source) =>
        [.. Clauses(source)
            .Where(found => !Records(found.Clause, found.Body))
            .Select(found => new Offender(
                source.Take(found.Clause.Index).Count(c => c == '\n') + 1,
                found.Clause.Value))];

    // A rethrow is not a discard: the exception travels on to whoever swallows it, and that one
    // logs it. Logging here as well would put the same stack in the host's log twice.
    private static bool Records(Match clause, string body) =>
        Rethrows.IsMatch(body)
        || (clause.Groups["name"].Success && LoggedWith(clause.Groups["name"].Value).IsMatch(body));

    private static readonly Regex Rethrows = new(@"(^|[^.\w])throw\s*;", RegexOptions.Compiled);

    /// <summary>
    /// The block starting at <paramref name="open"/>, brace-matched past strings and comments.
    /// </summary>
    /// <remarks>
    /// Past them because the message templates are full of <c>{Placeholder}</c>: counting braces
    /// naively ends the body at the first one and calls a logged site unlogged.
    /// </remarks>
    private static string Block(string source, int open)
    {
        var body = new StringBuilder();
        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            var c = source[i];

            switch (c)
            {
                case '/' when i + 1 < source.Length && source[i + 1] == '/':
                    i = Next(source, source.IndexOf('\n', i));
                    continue;

                case '/' when i + 1 < source.Length && source[i + 1] == '*':
                    i = Next(source, source.IndexOf("*/", i + 2, StringComparison.Ordinal)) + 1;
                    continue;

                case '@' when i + 1 < source.Length && source[i + 1] == '"':
                    i = Verbatim(source, i + 2);
                    continue;

                case '"':
                case '\'':
                    i = Quoted(source, i + 1, c);
                    continue;
            }

            body.Append(c);

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth == 0)
            {
                break;
            }
        }

        return body.ToString();
    }

    /// <summary>Where an unterminated construct leaves the walk: at the end, not off it.</summary>
    private static int Next(string source, int index) => index < 0 ? source.Length : index;

    private static int Quoted(string source, int from, char quote)
    {
        for (var i = from; i < source.Length; i++)
        {
            if (source[i] == '\\')
            {
                i++;
            }
            else if (source[i] == quote)
            {
                return i;
            }
        }

        return source.Length;
    }

    private static int Verbatim(string source, int from)
    {
        for (var i = from; i < source.Length; i++)
        {
            if (source[i] != '"')
            {
                continue;
            }

            if (i + 1 < source.Length && source[i + 1] == '"')
            {
                i++;
                continue;
            }

            return i;
        }

        return source.Length;
    }

    private static string Relative(string file) =>
        Path.GetRelativePath(Repo.Root, file).Replace(Path.DirectorySeparatorChar, '/');

    // The package only. The samples and the tests are free to swallow: neither ships to a host
    // whose logs are the one place a fault can be read.
    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                Repo.In("src", "Fhi.Munin.Explorer"), "*.razor", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
}
