using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Proves that <see cref="HostClassNames.Orphans"/> answers "undrawn" for a name whose rule
/// declares nothing, and not only for a name no rule mentions.
///
/// Everywhere else in the suite that check is asked of a render, which means it can only fail when
/// the sample stylesheet is actually broken — and the sample stylesheet is not broken, so the
/// strengthening that made the check read declaration blocks landed green and stayed green.
/// A guard that has never been seen to fail is a guard nobody has evidence for: emptying a rule by
/// hand and watching a test go red proves it once, for whoever ran it, and proves nothing
/// afterwards. These tests run that experiment on every build — on a stylesheet held in memory, so
/// the file the rest of the suite reads is never touched.
///
/// The shape being caught is the facet fold's: a selector that is present and correct, with the
/// declaration that would make it mean something missing. The reader who deletes the declaration
/// block and leaves the selector behind gets a failure here rather than an unstyled component on
/// helsedata.no.
///
/// <see cref="HostClassNames.KilderSelectScope"/> is run the same way, for the other half of that
/// shape: not whether a rule draws a name but WHERE the name is, because Stiler's kilder thresholds
/// reach the selection column through the scroll box and cannot see one that left it.
/// </summary>
public class HostClassNamesTest
{
    /// <summary>
    /// The same cut as <c>HostClassNames.CssRule</c>, used in the opposite direction: to write a
    /// stylesheet rather than read one. Every rule whose selector so much as contains the name is
    /// emptied, which is wider than the guard's own matching on purpose — nothing must be left
    /// behind that could answer for the name and make the experiment pass for the wrong reason.
    /// </summary>
    private static readonly Regex Rule = new(@"(?<selector>[^{}]*)\{(?<declarations>[^{}]*)\}");

    /// <summary>
    /// The class-bearing elements of a markup fragment, so the DOM shapes this file has to tell
    /// apart can be written down here whether or not any component renders them — the same reason
    /// <c>AccessibleNameTest</c> parses rather than renders.
    /// </summary>
    private static IReadOnlyList<IElement> Elements(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(response => response.Content(html)).Result;

        return [.. document.QuerySelectorAll("[class]")];
    }

    private static string WithEveryRuleForEmptied(string css, string name) =>
        Rule.Replace(css, m => m.Groups["selector"].Value.Contains('.' + name, StringComparison.Ordinal)
            ? m.Groups["selector"].Value + "{ }"
            : m.Value);

    [Fact]
    public void Orphans_WhenTheSampleRulesForARenderedNameAreEmptied_ThenTheNameIsReported()
    {
        // The acceptance experiment for this guard, run against the real stylesheet: take a name
        // the component renders, confirm the samples draw it today, then blank every rule that
        // names it and confirm the guard notices. `munin-explorer-codes` is the block that was
        // emptied by hand to check this, so it is the one pinned here.
        string[] rendered = ["munin-explorer-codes"];

        Assert.Equal([], HostClassNames.OrphansIn(HostClassNames.SampleCss, rendered));

        var emptied = WithEveryRuleForEmptied(HostClassNames.SampleCss, "munin-explorer-codes");

        // The selector survives the mutation — that is the whole point. A check that searched the
        // stylesheet for the name would still find it here and still answer "styled".
        Assert.Contains(".munin-explorer-codes", emptied, StringComparison.Ordinal);

        var orphans = HostClassNames.OrphansIn(emptied, rendered);

        Assert.Single(orphans);
        Assert.StartsWith("munin-explorer-codes ", orphans[0], StringComparison.Ordinal);
        Assert.Contains("every one of them empty", orphans[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Orphans_WhenEveryRuleNamingAClassIsEmpty_ThenItIsReportedApartFromAMissingRule()
    {
        // Two failures, two messages. "Unstyled" alone sends the reader looking for a rule that is
        // sitting right there in the file with nothing in it, which is the slowest way to find a
        // one-line deletion.
        var css = """
            .munin-explorer-alpha { }
            .munin-explorer-alpha:focus { }
            """;

        var orphans = HostClassNames.OrphansIn(css, ["munin-explorer-alpha", "munin-explorer-beta"]);

        Assert.Equal(2, orphans.Count);
        Assert.Equal(
            "munin-explorer-alpha (named by 2 rule(s) in the stylesheet, every one of them empty)",
            orphans[0]);
        Assert.Equal("munin-explorer-beta", orphans[1]);
    }

    [Fact]
    public void Orphans_WhenOneOfSeveralRulesDeclaresSomething_ThenTheNameIsNotReported()
    {
        // An empty rule beside a real one is not a fault. The skip link is written that way — a
        // resting rule and a `:focus` twin — and a responsive override left empty while a media
        // query is being worked on styles the name no less than it did before.
        var css = """
            .munin-explorer-alpha { }
            @media (min-width: 60rem) { .munin-explorer-alpha { display: grid; } }
            """;

        Assert.Equal([], HostClassNames.OrphansIn(css, ["munin-explorer-alpha"]));
    }

    [Fact]
    public void Orphans_WhenABlockHoldsOnlyWhitespaceAndSemicolons_ThenTheNameIsReported()
    {
        // `{ ; }` is as silent as `{}` and reads as more deliberate, which is exactly why it is
        // worth pinning: the leftover of a declaration deleted without its semicolon.
        var css = "\n.munin-explorer-alpha {\n  ;\n}\n";

        Assert.Single(HostClassNames.OrphansIn(css, ["munin-explorer-alpha"]));
    }

    [Fact]
    public void Orphans_WhenOnlyALongerNameIsDeclared_ThenTheShorterNameIsStillReported()
    {
        // A rule for the part is not a rule for the whole: the period bar's `__fill` is drawn and
        // its wrapper is not, and the wrapper is the one that has to be reported. This is the
        // boundary handling in `Mentions`, asked now of a selector rather than of the whole file —
        // moving to rule-level extraction must not have loosened it.
        var css = ".munin-explorer-period__fill { background: red; }";

        Assert.Equal(["munin-explorer-period"], HostClassNames.OrphansIn(css, ["munin-explorer-period"]));
    }

    [Fact]
    public void Orphans_WhenAnEmptyRuleIsWrittenAsAComment_ThenTheCommentDoesNotAnswerForTheName()
    {
        // Comments go before the rules are cut, so prose about a name cannot switch the check off —
        // and prose about a name whose rule is empty cannot either. The sample stylesheet documents
        // its own selectors constantly, so this is not a hypothetical shape.
        var css = """
            /* .munin-explorer-alpha { display: grid; } — what this used to do */
            .munin-explorer-alpha { }
            """;

        Assert.Single(HostClassNames.OrphansIn(css, ["munin-explorer-alpha"]));
    }

    [Fact]
    public void Orphans_WhenTheNameIsTheKilderScrollCountModifier_ThenItIsNotAskedAbout()
    {
        // The one exemption, and the reason it is not a hole: the three shell guards already drop
        // `munin-explorer-kilder-scroll--cols-` because it is a stem C# finishes, and this check
        // reads the DOM so it sees the finished name. Undrawn the box keeps what its base class
        // gives it, which is why the modifier could ship before the rule that selects on it.
        Assert.Equal([], HostClassNames.OrphansIn("", ["munin-explorer-kilder-scroll--cols-15"]));
    }

    [Fact]
    public void Orphans_WhenANameMerelyLooksLikeTheCountModifier_ThenItIsStillReported()
    {
        // The exemption is anchored on the one stem and on digits, so it cannot quietly spread to
        // any name ending in a modifier: a second count modifier is a decision somebody makes here.
        Assert.Equal(
            [
                "munin-explorer-kilder-scroll--cols-15-extra",
                "munin-explorer-kilder-scroll--cols-wide",
                "munin-explorer-list-scroll--cols-9",
            ],
            HostClassNames.OrphansIn(
                "",
                [
                    "munin-explorer-kilder-scroll--cols-wide",
                    "munin-explorer-list-scroll--cols-9",
                    "munin-explorer-kilder-scroll--cols-15-extra",
                ]));
    }

    [Fact]
    public void KilderSelectScope_WhenTheSelectColumnLeavesTheScrollBox_ThenItIsCountedAsOutside()
    {
        // The acceptance experiment for the containment half of the kilder guard. A check that
        // asked whether the class is in the document answers the same for both fragments below,
        // which is exactly why the guard asks where it is instead. (Fhi.Metadata-l9l2n.110)
        var inside = HostClassNames.KilderSelectScope(Elements("""
            <div class="munin-explorer-kilder-scroll">
              <table class="munin-explorer-kilder">
                <tr><td class="munin-explorer-kilder__select"></td></tr>
              </table>
            </div>
            """));

        Assert.Equal((1, 1, 0), inside);

        var escaped = HostClassNames.KilderSelectScope(Elements("""
            <table class="munin-explorer-kilder">
              <tr><td class="munin-explorer-kilder__select"></td></tr>
            </table>
            <div class="munin-explorer-kilder-scroll"></div>
            """));

        Assert.Equal((1, 0, 1), escaped);
    }

    [Fact]
    public void KilderSelectScope_WhenTheWrapperIsRenamed_ThenThereIsNoBoxToScopeTo()
    {
        // Told apart from "the column is gone" on purpose: both leave Inside at zero, and a guard
        // that reported only that would send the reader after the wrong class.
        var scope = HostClassNames.KilderSelectScope(Elements("""
            <div class="munin-explorer-kilder-overflow">
              <table class="munin-explorer-kilder">
                <tr><td class="munin-explorer-kilder__select"></td></tr>
              </table>
            </div>
            """));

        Assert.Equal((0, 0, 1), scope);
    }
}
