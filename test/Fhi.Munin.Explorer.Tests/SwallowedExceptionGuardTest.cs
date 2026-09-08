using System.Text;
using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// That no <c>catch</c> in the package the explorer's own failures reach throws the exception away.
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
    /// <remarks>
    /// The package's own exception types as well as <c>Exception</c>. Fifteen typed handlers carry
    /// the <c>Warning</c> half of the split, and reading only <c>catch (Exception)</c> left every
    /// one of them free to discard a 429 — the distinction the incident actually needed.
    /// </remarks>
    private static readonly Regex CatchClause =
        new(@"catch\s*\(\s*(?<type>Exception|MuninExplorer\w*Exception)\s*(?<name>[A-Za-z_]\w*)?\s*\)",
            RegexOptions.Compiled);

    /// <summary>What counts as recording it: a log call the caught exception itself is passed to.</summary>
    /// <remarks>
    /// The exception, not merely a call. "Something was logged" passes for code that writes the
    /// reader-facing sentence and drops the stack, which is the defect wearing a hat.
    /// </remarks>
    private static Regex LoggedWith(string name) =>
        LoggedAt("Critical|Error|Warning|Information|Debug|Trace", name);

    /// <summary>The same, narrowed to the levels a clause of that kind is allowed to write at.</summary>
    private static Regex LoggedAt(string levels, string name) =>
        new(@"\bLog(" + levels + @")\s*\(\s*" + Regex.Escape(name) + @"\s*,", RegexOptions.Compiled);

    /// <summary>
    /// What the package's own exception types may be written at: <c>Warning</c>, and only that.
    /// </summary>
    /// <remarks>
    /// A 429 and a 401 are expected, handled outcomes and the catalogue is up in both — reading
    /// either as <c>Error</c> is the reading that wasted an afternoon on the incident this came
    /// from. Fifteen typed handlers carry that half of the split, and one of them is tested.
    /// </remarks>
    private static Regex ExpectedOutcome(string name) => LoggedAt("Warning", name);

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
    // Half-logged is the shape these are one edit away from, so both of its faces are fed in too:
    // a log the early return jumps over, and a rethrow that only one branch reaches.
    [InlineData("catch (Exception ex) { if (stale) { return; } Log?.LogError(ex, \"x\"); }", 1)]
    [InlineData("catch (Exception ex) { Log?.LogError(ex, \"x\"); if (stale) { return; } _e = 1; }", 0)]
    [InlineData("catch (Exception ex) { if (stale) { return; } _error = T.Error; }", 1)]
    [InlineData("catch (Exception ex) { if (handled) { throw; } _error = T.Error; }", 1)]
    [InlineData("catch (Exception ex) { if (handled) { throw; } Log?.LogError(ex, \"x\"); }", 0)]
    [InlineData("catch (Exception) { if (stale) { return; } throw; }", 1)]
    [InlineData("catch (Exception ex) { Log?.LogError(ex, \"a } brace\"); }", 0)]
    [InlineData("catch (JsonException) { return null; }", 0)]
    // The package's own types are walked as well, so the Warning half of the split cannot be
    // dropped silently — and a discarding one of those is an offender like any other.
    [InlineData("catch (MuninExplorerRateLimitedException) { _error = T.RateLimitError; }", 1)]
    [InlineData("catch (MuninExplorerRateLimitedException ex) { Log?.LogWarning(ex, \"x\"); }", 0)]
    [InlineData("catch (MuninExplorerUnauthorizedException ex) { Log?.LogWarning(ex, \"x\"); }", 0)]
    // And the level is the whole point of keeping them their own branch, so Error there is a
    // discard of the one distinction the incident needed rather than a note in the wrong column.
    [InlineData("catch (MuninExplorerRateLimitedException ex) { Log?.LogError(ex, \"x\"); }", 1)]
    [InlineData("nothing here catches anything", 0)]
    public void Source_WhenItIsScanned_ThenOnlyADiscardedExceptionIsReported(string source, int expected) =>
        Assert.Equal(expected, Offenders(source).Count);

    /// <summary>
    /// That every call to the host-callback helper hands it the component's logger.
    /// </summary>
    /// <remarks>
    /// The one place the logger is threaded by hand rather than read off the component, at fourteen
    /// call sites across three files. The catch inside the helper satisfies the walk above whatever
    /// its callers pass, so a site written <c>RaiseAsync(SearchChanged, _search, null)</c> — or a new
    /// one that simply forgets — restores the blindness on the path most likely to break.
    /// </remarks>
    [Fact]
    public void RaiseAsync_WhereItIsCalled_ThenTheLoggerIsPassedRatherThanLeftOut()
    {
        var offenders = Sources()
            .SelectMany(file => Unlogged(File.ReadAllText(file))
                .Select(call => $"{Relative(file)}: {call}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These raise a host callback without handing the helper a logger, so a handler that "
            + "throws there is swallowed as silently as before. Pass Log as the last argument:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData("await RaiseAsync(SortChanged, _sort, Log);", 0)]
    [InlineData("return RaiseAsync(SearchChanged, _search, Log);", 0)]
    [InlineData("RaiseAsync(ExploreVariablesRequested, Handover(visible), Log);", 0)]
    [InlineData("await RaiseAsync(SortChanged, _sort, null);", 1)]
    [InlineData("await RaiseAsync(SortChanged, _sort);", 1)]
    // The backing field, not the property: it is null until the property has resolved it once, so
    // a site passing it logs nothing on the first raise — which is the mount, and the initial URL.
    [InlineData("await RaiseAsync(SortChanged, _sort, _log);", 1)]
    [InlineData(
        "private static async Task RaiseAsync<TValue>("
        + "EventCallback<TValue> callback, TValue value, ILogger? log)", 0)]
    [InlineData("// see the RaiseAsync remarks in VariableSearch.Querying.cs", 0)]
    public void RaiseAsyncCalls_WhenTheyAreScanned_ThenOnlyOneWithoutALoggerIsReported(
        string source, int expected) =>
        Assert.Equal(expected, Unlogged(source).Count);

    private static readonly Regex RaiseCall =
        new(@"\bRaiseAsync\s*(?:<[^<>()]*>)?\s*\(", RegexOptions.Compiled);

    /// <summary>Every call to the helper whose last argument is not the component's logger.</summary>
    private static List<string> Unlogged(string source)
    {
        var found = new List<string>();

        foreach (Match call in RaiseCall.Matches(source))
        {
            // The helper's own declaration reads as a call and is not one, and the prose about it
            // has no parentheses to walk at all.
            var arguments = Arguments(source, call.Index + call.Length - 1);

            if (arguments is null || arguments.Contains("ILogger", StringComparison.Ordinal))
            {
                continue;
            }

            if (!arguments.TrimEnd().EndsWith("Log", StringComparison.Ordinal))
            {
                found.Add($"RaiseAsync({arguments})");
            }
        }

        return found;
    }

    /// <summary>The argument list at <paramref name="open"/>, or null where it never closes.</summary>
    private static string? Arguments(string source, int open)
    {
        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '(')
            {
                depth++;
            }
            else if (source[i] == ')' && --depth == 0)
            {
                return source[(open + 1)..i];
            }
        }

        return null;
    }

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

    /// <summary>Whether every path out of the body writes the exception down or hands it on.</summary>
    /// <remarks>
    /// A rethrow is not a discard: the exception travels on to whoever swallows it, and logging
    /// here as well would put the same stack in the host's log twice. Both halves ask about a path
    /// rather than a token, because half-logged is the shape these are one edit away from.
    /// </remarks>
    private static bool Records(Match clause, string body)
    {
        var leaves = Leaves.Match(body);

        // TopLevel blanks rather than removes, so an index into it is an index into the body.
        return Reached(Rethrows.Match(TopLevel(body)), leaves)
            || (clause.Groups["name"].Success
                && Reached(Level(clause)(clause.Groups["name"].Value).Match(body), leaves));
    }

    /// <summary>Whether the body gets here on every path: it is there, and no return precedes it.</summary>
    /// <remarks>
    /// Several of these already branch on a stale generation, so a log or a rethrow the early
    /// return jumps over is the near miss worth refusing — the real sites log first, and this is
    /// what holds them there.
    /// </remarks>
    private static bool Reached(Match found, Match leaves) =>
        found.Success && (!leaves.Success || found.Index < leaves.Index);

    private static Func<string, Regex> Level(Match clause) =>
        clause.Groups["type"].Value == "Exception" ? LoggedWith : ExpectedOutcome;

    private static readonly Regex Rethrows = new(@"(^|[^.\w])throw\s*;", RegexOptions.Compiled);

    private static readonly Regex Leaves = new(@"\breturn\b", RegexOptions.Compiled);

    /// <summary>The body with every nested block blanked, so a token in it is one no branch guards.</summary>
    private static string TopLevel(string body)
    {
        var top = new StringBuilder(body.Length);
        var depth = 0;

        foreach (var c in body)
        {
            var outer = c == '{' ? depth++ : c == '}' ? --depth : depth;

            top.Append(outer <= 1 ? c : ' ');
        }

        return top.ToString();
    }

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
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && Path.GetFileName(file) != TheSeam);

    /// <summary>The one file that may swallow, and the test below is why it is only that one.</summary>
    private const string TheSeam = "ExplorerLog.cs";

    [Fact]
    public void TheSeam_WhenItIsExcused_ThenItIsBecauseItIsTheLoggerAndNothingElseIsExcused()
    {
        // The excuse is narrow and has to be shown to be: what ExplorerLog catches is the host's
        // logging failing, which by definition cannot be written down, and letting it out would
        // take the circuit the catch below it exists to keep. Any second file is a hole.
        var excused = Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), TheSeam, SearchOption.AllDirectories)
            .ToList();

        var file = Assert.Single(excused);

        Assert.NotEmpty(Offenders(File.ReadAllText(file)));
    }
}
