namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Answers one question about a rendered class name: is there a stylesheet anywhere that draws it?
///
/// The package ships no CSS, so every name it renders is a promise that someone else styles it —
/// either the host, for a name borrowed off helsedata's design system, or the sample stylesheet, for
/// a name this package invented and expects a host to copy. A name in neither is a promise nobody
/// keeps: it renders at raw browser defaults on helsedata.no and looks like a bug in the component.
///
/// "Draws it" rather than "names it", because a rule with an empty block draws exactly what no rule
/// draws. Every question here goes through <see cref="SampleDeclarationsFor"/>, which cuts the
/// selector apart from the declarations, rather than searching the stylesheet as one string — a
/// substring search cannot tell a rule that does something from a rule that does nothing.
///
/// <c>headline-sm</c> was exactly that for months. It reads like one of helsedata's heading classes,
/// it sat beside <c>headline</c> where a real one would, and it is a typo for <c>headline-s</c> that
/// nothing anywhere defines — so nine block headings rendered at the browser's own size inside
/// helsedata. Every check in this repo stayed green, because each was verifying the names the package
/// invents and none was verifying the names it borrows.
///
/// A shell script cannot do this job. It can read class attributes out of the markup, but the views
/// pass class names to helpers as arguments — <c>@Heading(BlockLevel, T.HeadingMetadata, "headline
/// headline-s")</c> — and a name that reaches the DOM through a method parameter is invisible to
/// grep. That is why this lives in the test suite: bUnit renders the component and the check reads
/// the DOM, which is the only place every name actually appears.
/// </summary>
internal static class HostClassNames
{
    /// <summary>
    /// Class names helsedata's own stylesheets define, captured from the live site. See the header
    /// in the file itself for how, and for how to capture it again.
    /// </summary>
    private static readonly Lazy<HashSet<string>> TheirNames = new(() =>
        [.. File.ReadLines(Repo.In("test", "host-class-names.txt"))
                .Where(l => l.Length > 0 && !l.StartsWith('#'))]);

    private static readonly System.Text.RegularExpressions.Regex CssComment =
        new(@"/\*.*?\*/", System.Text.RegularExpressions.RegexOptions.Singleline);

    /// <summary>
    /// Innermost blocks only — <c>[^{}]</c> on both sides — so the rules inside an
    /// <c>@media</c> are matched as themselves rather than swallowed whole by the at-rule.
    ///
    /// No <c>Singleline</c>, unlike its neighbour above: the flag only widens what <c>.</c>
    /// matches, and there is no <c>.</c> here. A negated class crosses newlines on its own.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex CssRule =
        new(@"(?<selector>[^{}]*)\{(?<declarations>[^{}]*)\}");

    /// <summary>
    /// The sample stylesheet, held as text rather than parsed: <see cref="SampleDeclarationsFor"/>
    /// cuts the rules out of it with <see cref="CssRule"/>, and every question here is asked of
    /// those rules rather than of this string.
    ///
    /// Comments are stripped first, and that is load-bearing rather than tidy. This file is heavily
    /// commented, and a comment naming a selector reads to a substring search exactly like a rule
    /// declaring one. The comment a few lines below the badge rule says there is no `tag` class in
    /// helsedata's stylesheets — written with a leading dot, it made this check answer "styled" for
    /// the very name it was documenting as unstyled. A check that a comment can satisfy is a check
    /// that prose can switch off.
    /// </summary>
    private static readonly Lazy<string> SampleStylesheet = new(() =>
        CssComment.Replace(
            File.ReadAllText(Repo.In("samples", "LegacyHost", "wwwroot", "css", "host.css")),
            " "));

    /// <summary>
    /// The names among <paramref name="rendered"/> that no stylesheet draws — the ones no rule
    /// names at all, and the ones a rule names and then says nothing about. Empty is the passing
    /// answer; anything else names a class that will render unstyled on helsedata.no.
    ///
    /// The second case is the reason this asks <see cref="SampleDeclarationsFor"/> instead of
    /// searching the stylesheet for the name. An empty block draws what no block draws, so a check
    /// that stops at the selector calls it styled: the facet fold is the standing example of that
    /// shape — the selector was never the missing half, the declaration that undoes the fold was.
    /// Nothing in the sample stylesheet fails this today, which is the point of adding it now
    /// rather than after the next empty rule ships.
    ///
    /// An entry says which of the two failures it is, because "unstyled" alone sends the reader
    /// looking for a rule that is sitting right there with nothing in it.
    /// </summary>
    internal static IReadOnlyList<string> Orphans(IEnumerable<string> rendered) =>
        [.. rendered.Distinct(StringComparer.Ordinal)
                    .Where(name => !TheirNames.Value.Contains(name))
                    .Select(SampleVerdict)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)];

    /// <summary>
    /// True for the structural names this package invented, as opposed to the ones it took over from
    /// helsedata's variable page.
    ///
    /// Before the rename the split was legible in the prefix itself: `variable-explorer*` was ours,
    /// `variable-data-list*` / `variable-dataitem*` / `variable-meta*` were theirs. Everything now
    /// sits under one prefix we own - the point of the rename - so the distinction has to be spelled
    /// out, and it is still worth drawing: the assertions that use it are exact lists, and they say
    /// what they mean only while the scope stays what it was.
    /// </summary>
    internal static bool IsOwnStructureName(string cls) =>
        cls.StartsWith("munin-explorer", StringComparison.Ordinal)
        && !cls.StartsWith("munin-explorer-data-list", StringComparison.Ordinal)
        && !cls.StartsWith("munin-explorer-dataitem", StringComparison.Ordinal)
        && !cls.StartsWith("munin-explorer-meta", StringComparison.Ordinal);

    /// <summary>
    /// The declaration block of every rule in the sample stylesheet whose selector names
    /// <paramref name="name"/> — <c>.munin-explorer-skiplink-pagination</c> and its <c>:focus</c>
    /// twin come back as two entries, in the order the file writes them.
    ///
    /// This is the only way into the stylesheet, and that is deliberate: a search for the name
    /// alone answers "styled" for a rule with nothing in it, and a rule with nothing in it draws
    /// nothing. <see cref="Orphans"/> reads these blocks to rule out the empty ones for every name
    /// at once; the two named regression tests read them to assert a particular declaration — the
    /// skip link's offset, and the pair that undoes the facet fold on a host with room for a
    /// sidebar. Both halves of that failure are the same shape: the selector was there, and the
    /// declaration that made it mean something was not.
    /// </summary>
    internal static IReadOnlyList<(string Selector, string Declarations)> SampleDeclarationsFor(string name) =>
        [.. CssRule.Matches(SampleStylesheet.Value)
                   .Where(m => Mentions(m.Groups["selector"].Value, name))
                   .Select(m => (m.Groups["selector"].Value.Trim(), m.Groups["declarations"].Value))];

    /// <summary>
    /// Null when the sample stylesheet really draws <paramref name="name"/>; otherwise the line
    /// <see cref="Orphans"/> reports for it.
    /// </summary>
    private static string? SampleVerdict(string name)
    {
        var rules = SampleDeclarationsFor(name);

        if (rules.Count == 0)
        {
            return name;
        }

        return rules.Any(rule => Declares(rule.Declarations))
            ? null
            : $"{name} (named by {rules.Count} rule(s) in the sample stylesheet, every one of them empty)";
    }

    /// <summary>
    /// Whether a declaration block says anything at all. Comments are gone from the text these
    /// blocks are cut out of before <see cref="CssRule"/> ever sees it, so what is left to discount
    /// is whitespace and stray semicolons — <c>{ ; }</c> is as silent as <c>{}</c>.
    ///
    /// Deliberately not a check of WHICH declarations: that question belongs to a named regression
    /// test for the one invariant it is about, the way the skip link's offset and the facet fold's
    /// two rules each have one. This one is asked of all ~75 names, so it can only ask the question
    /// that is the same for every name.
    /// </summary>
    private static bool Declares(string declarations) =>
        declarations.Any(c => !char.IsWhiteSpace(c) && c != ';');

    /// <summary>
    /// Whether a selector names this class. Hard-anchored on the left — the search is for
    /// <c>'.' + name</c>, so nothing with a prefix in front of the name answers for it. What the
    /// right-hand check does is accept any character that cannot continue a class name, which is
    /// how the two things that can follow a name are told apart: <c>_</c> continues it, so a rule for
    /// <c>.munin-explorer-period__fill</c> does not answer for <c>.munin-explorer-period</c> — a
    /// rule for the part is not a rule for the whole — while <c>:</c> ends it, so
    /// <c>.munin-explorer-skiplink-pagination:focus</c> does answer for the name it qualifies.
    /// </summary>
    private static bool Mentions(string selector, string name)
    {
        for (var i = selector.IndexOf('.' + name, StringComparison.Ordinal); i >= 0;
             i = selector.IndexOf('.' + name, i + 1, StringComparison.Ordinal))
        {
            var after = i + 1 + name.Length;
            if (after >= selector.Length
                || !(char.IsAsciiLetterOrDigit(selector[after]) || selector[after] is '-' or '_'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every class name in a rendered fragment.
    ///
    /// Taken from <c>ClassList</c> rather than split off <c>ClassName</c>: the attribute separates
    /// tokens with any ASCII whitespace, not only spaces, and a class attribute broken across lines
    /// in the markup would otherwise arrive here as one long token that no stylesheet defines — a
    /// false orphan, reported against a name nobody wrote.
    /// </summary>
    internal static IEnumerable<string> Of(IEnumerable<AngleSharp.Dom.IElement> elements) =>
        elements.SelectMany(e => e.ClassList);
}
