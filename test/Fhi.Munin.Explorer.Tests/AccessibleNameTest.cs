using AngleSharp;
using AngleSharp.Dom;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The oracle the accessibility assertions are written against, asserted against itself.
/// </summary>
/// <remarks>
/// A test helper does not usually get its own tests. This one does, because it is the thing every
/// other accessibility assertion in this suite trusts, and because its failures are silent in one
/// direction only: an arm that is more generous than the real naming rules reports an unnamed
/// control as named, and every test standing on it goes green over the defect it was written to
/// catch. Parsed from markup strings rather than rendered from a component, so the shapes it has to
/// refuse can be written down here whether or not the package emits them.
/// </remarks>
public class AccessibleNameTest
{
    private static IElement Parse(string html, string selector)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(response => response.Content(html)).Result;

        return document.QuerySelector(selector)!;
    }

    [Fact]
    public void Of_WhenAControlIsWrappedInItsLabel_ThenTheLabelNamesIt()
    {
        // The shape the kodeverk checkbox uses. No `for`, no id — the wrap is the association.
        var element = Parse(
            "<label><input type=\"checkbox\"/> Inkluder kodeverk</label>", "input");

        Assert.Equal("Inkluder kodeverk", AccessibleName.Of(element));
    }

    [Fact]
    public void Of_WhenAnInlineChildOpensWithASpace_ThenThatSpaceIsNotAnnounced()
    {
        // The facet count shape cgk85 shipped. accname computes each ELEMENT's alternative and
        // trims it, so the span's leading space is discarded and a browser announces "Biobank(1)".
        // Flattening the label instead invents a separator nobody hears — Fhi.Metadata-ueiq6.
        var element = Parse(
            "<label><input type=\"checkbox\"/>Biobank<span> (1)</span></label>", "input");

        Assert.Equal("Biobank(1)", AccessibleName.Of(element));
    }

    [Fact]
    public void Of_WhenTheLabelItselfSeparatesItsWords_ThenTheSpaceSurvives()
    {
        // The other direction, and the reason the fix is not "trim everything": this space belongs
        // to the label's own text node rather than to the element beside it, so it is announced.
        // A fix that dropped it would turn "Velg liste" into "Velgliste" and pass the test above.
        var element = Parse(
            "<label><select><option>A</option></select>Velg <b>liste</b></label>", "select");

        Assert.Equal("Velg liste", AccessibleName.Of(element));
    }

    [Fact]
    public void Of_WhenRazorIndentationSurroundsTheControl_ThenItIsStillCollapsed()
    {
        // What the old implementation got right and this must keep: the newlines and indentation
        // Razor writes around a control are not part of the name.
        var element = Parse(
            "<label>\n    <input type=\"checkbox\"/>\n    Inkluder kodeverk\n</label>", "input");

        Assert.Equal("Inkluder kodeverk", AccessibleName.Of(element));
    }

    [Fact]
    public void Of_WhenALabelWrapsASecondControl_ThenOnlyTheFirstIsNamedByIt()
    {
        // HTML names the FIRST labelable descendant of a label and no other. A browser leaves the
        // text field here unnamed, so this helper has to as well: reporting "Inkluder kodeverk"
        // for it would be a silent pass on exactly the unnamed-field defect the helper exists to
        // catch, which is the direction that matters.
        const string html =
            "<label><input type=\"checkbox\" id=\"a\"/> Inkluder kodeverk <input type=\"text\" id=\"b\"/></label>";

        Assert.Equal("Inkluder kodeverk", AccessibleName.Of(Parse(html, "#a")));
        Assert.Equal("", AccessibleName.Of(Parse(html, "#b")));
    }

    [Fact]
    public void Of_WhenSomethingUnlabelableSitsInsideALabel_ThenItTakesNoNameFromIt()
    {
        // A <span> is not labelable, so the words around it name nothing. Its own content does not
        // rescue it either — only buttons, links and summaries are named by what is in them.
        var element = Parse("<label>Velg liste <span>Liste A</span></label>", "span");

        Assert.Equal("", AccessibleName.Of(element));
    }

    [Fact]
    public void Of_WhenAControlOnlyCarriesAPlaceholderOrATitle_ThenItHasNoName()
    {
        // The whole reason this helper exists rather than an assertion on attributes. Both are
        // accepted as names by several checking tools and neither is one.
        Assert.Equal("", AccessibleName.Of(
            Parse("<input type=\"text\" placeholder=\"Navn på ny liste\"/>", "input")));
        Assert.Equal("", AccessibleName.Of(
            Parse("<input type=\"text\" title=\"Navn på ny liste\"/>", "input")));
    }

    [Fact]
    public void Of_WhenAButtonPointsAtItselfAndThenAtAName_ThenItReadsAsBoth()
    {
        // The shape the save and remove buttons use: the control's own words first, then the
        // catalogue's name for the row, each in its own element so each keeps its own language.
        var element = Parse(
            "<div><button id=\"save\" aria-labelledby=\"save name\">Lagre i liste</button>"
            + "<span id=\"name\" lang=\"no\">Alder ved diagnose</span></div>",
            "button");

        Assert.Equal("Lagre i liste Alder ved diagnose", AccessibleName.Of(element));
    }

    [Fact]
    public void Of_WhenTheNameItPointsAtIsEmpty_ThenItFallsBackToItsOwnWords()
    {
        // A variable with no PreferredTerm. The empty half contributes nothing rather than leaving
        // the button announcing a phrase with a hole on the end.
        var element = Parse(
            "<div><button id=\"save\" aria-labelledby=\"save name\">Lagre i liste</button>"
            + "<span id=\"name\"></span></div>",
            "button");

        Assert.Equal("Lagre i liste", AccessibleName.Of(element));
    }
}
