using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The chassis the three detail views share, rendered directly rather than through one of them.
/// </summary>
/// <remarks>
/// The three view tests reach it the way a reader does, and that is what they are for — but none of
/// them fills <see cref="DetailPage.Contents"/>, because nothing in the package does until the
/// contents nav ships (Fhi.Metadata-35w0p.12). So the fragment and the column it lands in were
/// public API that was invisible when removed: deleting <c>@Contents</c> from the markup, or
/// rendering it into the main column instead, left every test in the suite green. The three
/// placements below are the whole of what the chassis promises, and this is where they are pinned.
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

    [Fact]
    public void Contents_WhenAViewFillsIt_ThenItLandsInTheContentsColumnAndNowhereElse()
    {
        // The fragment nothing in the package sets yet. Asserted by where it lands rather than by
        // its presence: rendered into `__main` instead it would still be on the page.
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
    public void Render_Always_ThenEveryNameItEmitsHasARuleInBothSampleStylesheets()
    {
        // The contents column is the one name here no view renders, so it is the one a host could
        // meet undrawn. Compared against an empty list so a failure names the class.
        var cut = RenderPage(withContents: true);

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
