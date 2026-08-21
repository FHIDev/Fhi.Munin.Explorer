namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Answers one question about a rendered class name: is there a stylesheet anywhere that defines it?
///
/// The package ships no CSS, so every name it renders is a promise that someone else styles it —
/// either the host, for a name borrowed off helsedata's design system, or the sample stylesheet, for
/// a name this package invented and expects a host to copy. A name in neither is a promise nobody
/// keeps: it renders at raw browser defaults on helsedata.no and looks like a bug in the component.
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
    private static readonly Lazy<string> RepoRoot = new(() =>
    {
        // Walk up from the test binary rather than trusting the working directory, which differs
        // between `dotnet test`, the IDE runner and CI.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Fhi.Munin.Explorer.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                $"No Fhi.Munin.Explorer.slnx above '{AppContext.BaseDirectory}', so the stylesheets this " +
                "check reads cannot be found. Running the tests from outside the checkout?");
    });

    /// <summary>
    /// Class names helsedata's own stylesheets define, captured from the live site. See the header
    /// in the file itself for how, and for how to capture it again.
    /// </summary>
    private static readonly Lazy<HashSet<string>> TheirNames = new(() =>
        [.. File.ReadLines(Path.Combine(RepoRoot.Value, "test", "host-class-names.txt"))
                .Where(l => l.Length > 0 && !l.StartsWith('#'))]);

    private static readonly System.Text.RegularExpressions.Regex CssComment =
        new(@"/\*.*?\*/", System.Text.RegularExpressions.RegexOptions.Singleline);

    /// <summary>
    /// The sample stylesheet, read as text rather than parsed: the question here is only whether a
    /// rule mentions the name, not what the rule does.
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
            File.ReadAllText(Path.Combine(RepoRoot.Value, "samples", "LegacyHost", "wwwroot", "css", "host.css")),
            " "));

    /// <summary>
    /// The names among <paramref name="rendered"/> that no stylesheet defines. Empty is the passing
    /// answer; anything else names a class that will render unstyled on helsedata.no.
    /// </summary>
    internal static IReadOnlyList<string> Orphans(IEnumerable<string> rendered) =>
        [.. rendered.Distinct(StringComparer.Ordinal)
                    .Where(name => !TheirNames.Value.Contains(name) && !SampleStyles(name))
                    .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Anchored on the right so a rule for <c>.munin-explorer-period__fill</c> does not answer for
    /// <c>.munin-explorer-period</c> — a rule for the part is not a rule for the whole.
    /// </summary>
    private static bool SampleStyles(string name)
    {
        var css = SampleStylesheet.Value;
        for (var i = css.IndexOf('.' + name, StringComparison.Ordinal); i >= 0;
             i = css.IndexOf('.' + name, i + 1, StringComparison.Ordinal))
        {
            var after = i + 1 + name.Length;
            if (after >= css.Length || !(char.IsAsciiLetterOrDigit(css[after]) || css[after] is '-' or '_'))
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
