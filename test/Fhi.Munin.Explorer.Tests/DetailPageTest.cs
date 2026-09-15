using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The chassis the three detail views share, rendered directly rather than through one of them.
/// </summary>
/// <remarks>
/// The three view tests reach it the way a reader does, and that is what they are for — but they
/// reach it through <see cref="DetailToc"/> now that the contents nav fills
/// <see cref="DetailPage.Contents"/>, so what the chassis promises about the fragment is still only
/// pinned here: that it lands in the contents column and not the main one, and that a view passing
/// nothing gets no column at all. The three placements below are the whole of that promise.
/// </remarks>
public class DetailPageTest : BunitContext
{
    private IRenderedComponent<DetailPage> RenderPage(bool withContents) =>
        Render<DetailPage>(parameters =>
        {
            parameters
                .Add(p => p.ViewRoot, "munin-explorer-kilde")
                .Add(p => p.ViewMain, "munin-explorer-kilde__main")
                .Add(p => p.Header, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>the name block</p>")))
                .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>the sections</p>")));

            if (withContents)
            {
                parameters.Add(p => p.Contents, (RenderFragment)(builder => builder.AddMarkupContent(0, "<nav>the contents nav</nav>")));
            }
        });

    private IRenderedComponent<DetailPage> RenderChrome(
        IReadOnlyList<DetailTrailStep>? trail = null,
        string? eyebrow = null,
        bool withActions = false) =>
        Render<DetailPage>(parameters =>
        {
            parameters
                .Add(p => p.ViewRoot, "munin-explorer-kilde")
                .Add(p => p.ViewMain, "munin-explorer-kilde__main")
                .Add(p => p.Eyebrow, eyebrow)
                .Add(p => p.Trail, trail)
                .Add(p => p.TrailLabel, "Brødsmulesti")
                .Add(p => p.Header, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>the name block</p>")));

            if (withActions)
            {
                parameters.Add(p => p.Actions,
                    (RenderFragment)(builder => builder.AddMarkupContent(0, "<button type=\"button\">an action</button>")));
            }
        });

    /// <summary>A trail of the shape a caller with addresses supplies: ancestors, then the page.</summary>
    private static IReadOnlyList<DetailTrailStep> Targeted() =>
    [
        new DetailTrailStep("Kildeutforsker", "/kilder"),
        new DetailTrailStep("Als registeret", "/kilder?kilde=1", "no"),
        new DetailTrailStep("Inklusjon", null, "no"),
    ];

    [Fact]
    public void Contents_WhenAViewFillsIt_ThenItLandsInTheContentsColumnAndNowhereElse()
    {
        // Asserted by where it lands rather than by its presence: rendered into `__main` instead,
        // the nav would still be on the page and every count of it would still pass.
        var cut = RenderPage(withContents: true);

        var toc = cut.Find(".munin-explorer-page__body > .munin-explorer-page__toc");

        Assert.Equal("the contents nav", toc.TextContent.Trim());
        Assert.Equal("NAV", toc.Children[0].TagName);
        Assert.DoesNotContain("the contents nav", cut.Find(".munin-explorer-page__main").TextContent);
    }

    [Fact]
    public void Contents_WhenNoViewFillsIt_ThenTheColumnIsNotDrawnAtAll()
    {
        // An empty column is not free: its track is a fixed 250px above 1025px and the row gap is
        // 40px below it, so a body that always had two children cost a rail or a blank row.
        var cut = RenderPage(withContents: false);

        Assert.Empty(cut.FindAll(".munin-explorer-page__toc"));

        var body = cut.Find(".munin-explorer-page__body");

        Assert.Contains("munin-explorer-page__main", Assert.Single(body.Children).ClassList);
    }

    [Fact]
    public void Body_Always_ThenItHoldsTheColumnsAndNothingElse()
    {
        // The track count is declared, not counted, so a third child wraps to a second row in the
        // 250px track. Both shapes, because only the filled one has a track to wrap out of.
        foreach (var withContents in (bool[])[true, false])
        {
            var body = RenderPage(withContents).Find(".munin-explorer-page__body");

            Assert.Equal(
                withContents
                    ? ["munin-explorer-page__toc", "munin-explorer-page__main munin-explorer-kilde__main"]
                    : (string[])["munin-explorer-page__main munin-explorer-kilde__main"],
                body.Children.Select(child => child.ClassName));
        }
    }

    [Fact]
    public void Header_Always_ThenItSitsAboveTheBodyRatherThanInsideIt()
    {
        // The grid below holds the columns and nothing else, so the name block is a sibling of the
        // body. Inside it, it would be a child the track count does not allow for.
        var cut = RenderPage(withContents: true);

        var root = cut.Find(".munin-explorer-page");

        Assert.Equal("the name block", root.Children[0].TextContent.Trim());
        Assert.Contains("munin-explorer-page__body", root.Children[1].ClassList);
        Assert.DoesNotContain("the name block", cut.Find(".munin-explorer-page__body").TextContent);
    }

    [Fact]
    public void ChildContent_Always_ThenItLandsInTheMainColumn()
    {
        // The third placement, and the one every section of every detail view arrives through.
        var main = RenderPage(withContents: true).Find(".munin-explorer-page__body > .munin-explorer-page__main");

        Assert.Equal("the sections", main.TextContent.Trim());
    }

    [Fact]
    public void Classes_WhenAViewPassesNoNameOfItsOwn_ThenTheChassisNameStandsAlone()
    {
        // Both parameters are EditorRequired and all three views pass one, so this is the shape a
        // future caller gets wrong. Interpolated straight it wrote `class="munin-explorer-page "`,
        // which the exact class-name lists elsewhere tokenise over for no reason.
        var cut = Render<DetailPage>(parameters => parameters
            .Add(p => p.ViewRoot, "")
            .Add(p => p.ViewMain, ""));

        Assert.Equal("munin-explorer-page", cut.Find(".munin-explorer-page").ClassName);
        Assert.Equal("munin-explorer-page__main", cut.Find(".munin-explorer-page__main").ClassName);
    }

    [Fact]
    public void Classes_WhenAViewPassesItsOwnNames_ThenTheyAreWornBesideTheChassisNames()
    {
        // The chassis name first on both, which is the order the three view tests read.
        var cut = RenderPage(withContents: false);

        Assert.Equal("munin-explorer-page munin-explorer-kilde", cut.Find(".munin-explorer-page").ClassName);
        Assert.Equal(
            "munin-explorer-page__main munin-explorer-kilde__main",
            cut.Find(".munin-explorer-page__main").ClassName);
    }

    [Fact]
    public void Attributes_WhenACallerWritesOne_ThenItLandsOnTheRootBesideTheClassList()
    {
        // What this is for: the saved-list view's version marker has to stay on the root element,
        // and the root is the chassis's. Splatted onto anything inside it, a host reading the
        // deployed version off the mount point finds nothing.
        var cut = Render<DetailPage>(parameters => parameters
            .Add(p => p.ViewRoot, "")
            .Add(p => p.ViewMain, "")
            .AddUnmatched("data-munin-explorer-version", "0.1.0-alpha.1"));

        var root = cut.Find(".munin-explorer-page");

        Assert.Equal("0.1.0-alpha.1", root.GetAttribute("data-munin-explorer-version"));
        Assert.Equal("munin-explorer-page", root.ClassName);
    }

    [Fact]
    public void Attributes_WhenACallerWritesAClass_ThenTheChassisNameIsStillOnTheRoot()
    {
        // Blazor settles a duplicated attribute by source order, so this holds only because the
        // splat is written before `class` on the element. Swapped — the splat reads more naturally
        // first — a host's own name would replace the root every layout rule keys on, silently.
        var cut = Render<DetailPage>(parameters => parameters
            .Add(p => p.ViewRoot, "munin-explorer-kilde")
            .Add(p => p.ViewMain, "")
            .AddUnmatched("class", "the-host-own-name"));

        var root = cut.Find(".munin-explorer-page");

        Assert.Equal("munin-explorer-page munin-explorer-kilde", root.ClassName);
    }

    [Fact]
    public void Eyebrow_WhenAViewSetsIt_ThenItIsAParagraphAboveTheNameBlockAndNeverAHeading()
    {
        // The whole of AC3, and the reason this is a <p>: a word above the title rendered as an
        // <h*> is a second title in the outline a screen reader navigates by, naming a category
        // rather than the thing on screen.
        var cut = RenderChrome(eyebrow: "Datakilde");

        var eyebrow = cut.Find(".munin-explorer-page__eyebrow");

        Assert.Equal("P", eyebrow.TagName);
        Assert.Equal("Datakilde", eyebrow.TextContent.Trim());
        Assert.Null(eyebrow.GetAttribute("role"));
        Assert.Empty(cut.FindAll("h1, h2, h3, h4, h5, h6"));

        var children = cut.Find(".munin-explorer-page").Children.ToList();

        Assert.True(
            children.FindIndex(child => child.ClassList.Contains("munin-explorer-page__eyebrow"))
            < children.FindIndex(child => child.TextContent.Contains("the name block", StringComparison.Ordinal)));
    }

    [Fact]
    public void Eyebrow_WhenAViewSetsNothing_ThenNoElementIsDrawnForIt()
    {
        // An empty one is not free: Stiler gives the name a pill and 1rem of margin above it, so a
        // blank paragraph would be a gap over the title on a page that asked for none.
        foreach (var empty in (string?[])[null, "", "   "])
        {
            Assert.Empty(RenderChrome(eyebrow: empty).FindAll(".munin-explorer-page__eyebrow"));
        }
    }

    [Fact]
    public void Trail_WhenStepsCarryTargets_ThenItIsANamedNavAroundAnOrderedListOfLinks()
    {
        // AC4 whole. The <ol> is the half a row of links cannot claim: the order is what a
        // breadcrumb tells a screen reader, and a <ul> says the steps are a set.
        var cut = RenderChrome(Targeted());

        var nav = cut.Find("nav.breadcrumbs");

        Assert.Equal("Brødsmulesti", AccessibleName.Of(nav));

        var list = Assert.Single(nav.Children);

        Assert.Equal("OL", list.TagName);
        Assert.Contains("breadcrumbs__list", list.ClassList);
        Assert.All(list.Children, step => Assert.Equal("LI", step.TagName));

        Assert.Equal(
            ["/kilder", "/kilder?kilde=1"],
            nav.QuerySelectorAll("a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void Trail_Always_ThenTheLastStepIsTheCurrentPageAndIsNotALink()
    {
        // Marked rather than merely last, and plain text rather than a link to where the reader
        // already is. The href on it is deliberate: the chassis drops one, so no call site can put
        // the current page back into the tab order one view at a time.
        var cut = RenderChrome([
            new DetailTrailStep("Kildeutforsker", "/kilder"),
            new DetailTrailStep("Inklusjon", "/kilder?kilde=1&datasamling=2"),
        ]);

        var steps = cut.FindAll("nav.breadcrumbs li");
        var last = steps[^1];

        Assert.Equal("page", last.GetAttribute("aria-current"));
        Assert.Contains("breadcrumbs__last-crumb", last.ClassList);
        Assert.Empty(last.QuerySelectorAll("a"));
        Assert.Equal("Inklusjon", last.TextContent.Trim());
        Assert.Null(steps[0].GetAttribute("aria-current"));
    }

    [Fact]
    public void Trail_WhenNoStepCarriesATarget_ThenTheTrailIsPlainTextAndNotOneDeadLink()
    {
        // The degradation AC2 asks about, one level down from "no trail at all": a caller may know
        // where a page sits and have no address for it. An <a> with no href is not a link a browser
        // will follow, and an href="#" is a control that reloads the page it is on.
        var cut = RenderChrome([
            new DetailTrailStep("Kildeutforsker", null),
            new DetailTrailStep("Als registeret", null),
            new DetailTrailStep("Inklusjon", null),
        ]);

        Assert.Empty(cut.FindAll("nav.breadcrumbs a"));
        Assert.Equal(
            ["Kildeutforsker", "Als registeret", "Inklusjon"],
            cut.FindAll("nav.breadcrumbs li").Select(step => step.TextContent.Trim()));
    }

    [Fact]
    public void Trail_WhenACallerPassesNone_ThenNoNavIsDrawnAtAll()
    {
        // What a caller with no addresses of its own gets — the variable explorer's drill-ins, and
        // Kelda before a host wires KilderHref. A landmark with one step names nowhere to go.
        foreach (var nothing in (IReadOnlyList<DetailTrailStep>?[])[null, []])
        {
            Assert.Empty(RenderChrome(nothing).FindAll("nav.breadcrumbs"));
        }
    }

    [Fact]
    public void Trail_WhenAStepIsTheCataloguesNorwegian_ThenTheLangIsOnTheWordsRatherThanOnTheLink()
    {
        // An accessible name is announced in the computed language of the element that owns it, so
        // a langed <a> would switch voice for the whole control rather than for the name in it.
        var step = RenderChrome(Targeted())
            .FindAll("nav.breadcrumbs a")
            .Single(link => link.TextContent.Trim() == "Als registeret");

        Assert.Null(step.GetAttribute("lang"));
        Assert.Equal("no", Assert.Single(step.Children).GetAttribute("lang"));
    }

    [Fact]
    public void Actions_WhenAViewFillsIt_ThenTheRowIsDrawnAboveTheNameBlock()
    {
        var cut = RenderChrome(withActions: true);

        var row = cut.Find(".munin-explorer-page__actions");

        Assert.Equal("an action", row.TextContent.Trim());

        var children = cut.Find(".munin-explorer-page").Children.ToList();

        Assert.True(
            children.FindIndex(child => child.ClassList.Contains("munin-explorer-page__actions"))
            < children.FindIndex(child => child.TextContent.Contains("the name block", StringComparison.Ordinal)));
    }

    [Fact]
    public void Actions_WhenNoViewFillsIt_ThenTheRowIsNotDrawnAtAll()
    {
        // Stiler gives the row 24px of margin above it, so an empty one is a gap under the chrome
        // of every page with no page-level action — which is every page in this package today.
        Assert.Empty(RenderChrome(withActions: false).FindAll(".munin-explorer-page__actions"));
    }

    [Fact]
    public void Chrome_Always_ThenEveryNameItEmitsHasARuleSomeStylesheetSupplies()
    {
        // The trail's five names are helsedata's own rather than ours, so this is where that claim
        // is checked: they have to be in the capture of their live page, since neither sample
        // stands a borrowed rule in and a partial copy of one is a divergence rather than a
        // stand-in.
        var cut = RenderChrome(Targeted(), eyebrow: "Datakilde", withActions: true);

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_Always_ThenEveryNameItEmitsHasARuleInBothSampleStylesheets()
    {
        // The contents column is the one name here no view renders, so it is the one a host could
        // meet undrawn. Compared against an empty list so a failure names the class.
        var cut = RenderPage(withContents: true);

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
