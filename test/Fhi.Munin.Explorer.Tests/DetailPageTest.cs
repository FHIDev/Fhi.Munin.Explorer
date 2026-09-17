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
public class DetailPageTest : ExplorerTestContext
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
    public void Facts_WhenAViewNamesThem_ThenTheRowSitsBetweenTheNameBlockAndTheBody()
    {
        // Where it sits is the whole of what the chassis promises about this fragment. Rendered
        // inside the body it would be a cell of the two-track grid rather than a row across the
        // page, and every count of it would still pass.
        var cut = Render<DetailPage>(parameters => parameters
            .Add(p => p.ViewRoot, "munin-explorer-kilde")
            .Add(p => p.ViewMain, "munin-explorer-kilde__main")
            .Add(p => p.Facts, (IReadOnlyList<DetailFact>)[new DetailFact("Type", "Kvalitetsregister")])
            .Add(p => p.Header, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>the name block</p>")))
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>the sections</p>"))));

        var children = cut.Find(".munin-explorer-page").Children.ToList();

        var nameBlock = children.FindIndex(child => child.TextContent.Contains("the name block", StringComparison.Ordinal));
        var facts = children.FindIndex(child => child.ClassList.Contains("munin-explorer-page__facts"));
        var body = children.FindIndex(child => child.ClassList.Contains("munin-explorer-page__body"));

        Assert.True(nameBlock < facts && facts < body);
        Assert.Empty(cut.Find(".munin-explorer-page__body").QuerySelectorAll(".munin-explorer-page__facts"));
    }

    [Fact]
    public void Facts_WhenAViewNamesNone_ThenNoRowIsDrawnAtAll()
    {
        // Stiler rules the row with a border above and below it, so an empty one is two lines
        // across a page that led with nothing — and the saved-list view leads with nothing.
        Assert.Empty(RenderPage(withContents: false).FindAll(".munin-explorer-page__facts"));
    }

    // -----------------------------------------------------------------------
    // The sticky fact bar. A bUnit render IS the JS-absent case — the module is never really
    // imported here — so every assertion below is about the page a reader without it gets.

    /// <summary>Six named facts, one of them blank, as a view that has not been filled in names them.</summary>
    private static IReadOnlyList<DetailFact> SixFacts() =>
    [
        new DetailFact("Kildetype", "Helseundersøkelse"),
        // The note is on a fact the BAR repeats, so the assertion that the bar leaves notes out is
        // about something: on the fourth it would hold whether the bar dropped them or not.
        new DetailFact("Dataansvarlig", "UiT", "no", NoteLabel: "Databehandler", Note: "Norsk helsenett"),
        new DetailFact("Grad av personidentifikasjon", "Avidentifisert"),
        new DetailFact("Dataperiode", "1974–", NoteLabel: "Gyldighet", Note: "1974–2026"),
        new DetailFact("Totalt antall variabler", "630", Note: "i 6 datasamlinger"),
        new DetailFact("Lovverk", null),
    ];

    private IRenderedComponent<DetailPage> RenderSticky(string? name = "Tromsøundersøkelsen", bool withActions = false) =>
        Render<DetailPage>(parameters =>
        {
            parameters
                .Add(p => p.ViewRoot, "munin-explorer-kilde")
                .Add(p => p.ViewMain, "munin-explorer-kilde__main")
                .Add(p => p.StickyName, name)
                .Add(p => p.StickyNameLang, "no")
                .Add(p => p.StickyCode, "K_TR (Tromsø)")
                .Add(p => p.Facts, SixFacts())
                .Add(p => p.Header, (RenderFragment)(builder => builder.AddMarkupContent(0, "<h2>Tromsøundersøkelsen</h2>")))
                .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>the sections</p>")));

            if (withActions)
            {
                parameters.Add(p => p.Actions,
                    (RenderFragment)(builder => builder.AddMarkupContent(0, "<button type=\"button\">Vis variabler</button>")));
            }
        });

    [Fact]
    public void Stuckbar_WhenTheModuleNeverRuns_ThenItIsInertAndThePageIsStillWhole()
    {
        // bUnit never runs the module, so this render is the no-JS case whole: the bar must be
        // there and reach nobody, and nothing on the page may depend on the observer having run.
        // "Uten JS dukker den bare aldri opp - ingenting går tapt."
        var cut = RenderSticky();

        var bar = cut.Find(".munin-explorer-page__stuckbar");

        Assert.True(bar.HasAttribute("hidden"));
        Assert.Equal("true", bar.GetAttribute("aria-hidden"));
        Assert.DoesNotContain("munin-explorer-page__stuckbar--on", bar.ClassList);

        // And the page it summarises, unchanged: the name block, the hero row with all five facts
        // the catalogue filled in, and the body below it.
        Assert.Equal("Tromsøundersøkelsen", cut.Find("h2").TextContent.Trim());
        Assert.Equal(5, cut.Find(".munin-explorer-page__facts").Children.Length);
        Assert.Equal("the sections", cut.Find(".munin-explorer-page__main").TextContent.Trim());
    }

    [Fact]
    public void Stuckbar_Always_ThenItRepeatsRatherThanReplacingAndIsNeverASecondHeading()
    {
        // The bar duplicates what is already on the page, so the one thing it must not do is join
        // the outline: a heading here is the page's title announced twice.
        var cut = RenderSticky(withActions: true);

        var bar = cut.Find(".munin-explorer-page__stuckbar");

        Assert.Empty(bar.QuerySelectorAll("h1, h2, h3, h4, h5, h6"));

        var name = bar.QuerySelector(".munin-explorer-page__stuckbar-name")!;

        Assert.Equal("SPAN", name.TagName);
        Assert.Contains("Tromsøundersøkelsen", name.TextContent);
        Assert.Contains("K_TR (Tromsø)", name.TextContent);
        Assert.Equal("no", name.QuerySelector("span")!.GetAttribute("lang"));

        // Three of the hero row's facts, label and value only: the <small> qualifier is what the
        // hero row has the width for and a one-line bar does not.
        Assert.Equal(
            ["Kildetype", "Dataansvarlig", "Grad av personidentifikasjon"],
            bar.QuerySelectorAll("dt").Select(dt => dt.TextContent.Trim()));
        Assert.Empty(bar.QuerySelectorAll("dl small"));
        Assert.DoesNotContain("Norsk helsenett", bar.TextContent, StringComparison.Ordinal);

        // The action row again, which is what the reader came to the bar for.
        Assert.Equal(
            "Vis variabler",
            bar.QuerySelector(".munin-explorer-page__actions")!.TextContent.Trim());
    }

    [Fact]
    public void Stuckbar_WhenAViewNamesNothingToCondenseTo_ThenNoBarIsDrawnAtAll()
    {
        // Both halves, because both are real: the saved-list view names no hero facts, and a view
        // that has not been given a name has nothing to put in the bar's one unrepeated slot.
        Assert.Empty(RenderSticky(name: null).FindAll(".munin-explorer-page__stuckbar"));
        Assert.Empty(RenderPage(withContents: false).FindAll(".munin-explorer-page__stuckbar"));
    }

    [Fact]
    public void Stuckbar_WhenTwoPagesAreMountedTogether_ThenEachDrivesItsOwn()
    {
        // Two explorers on one host page must not fight over one bar. The ids are what keeps them
        // apart, so the assertion is that each observe call pairs a bar with ITS OWN hero row —
        // two calls naming one bar would pass any count of them.
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);

        var cut = Render(builder =>
        {
            for (var page = 0; page < 2; page++)
            {
                builder.OpenComponent<DetailPage>(page * 10);
                builder.AddComponentParameter(page * 10 + 1, nameof(DetailPage.ViewRoot), "munin-explorer-kilde");
                builder.AddComponentParameter(page * 10 + 2, nameof(DetailPage.ViewMain), "munin-explorer-kilde__main");
                builder.AddComponentParameter(page * 10 + 3, nameof(DetailPage.StickyName), "Tromsøundersøkelsen");
                builder.AddComponentParameter(page * 10 + 4, nameof(DetailPage.Facts), SixFacts());
                builder.CloseComponent();
            }
        });

        var bars = cut.FindAll(".munin-explorer-page__stuckbar").Select(bar => bar.Id ?? "").ToList();
        var rows = cut.FindAll(".munin-explorer-page__facts").Select(row => row.Id ?? "").ToList();

        Assert.Equal(2, bars.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, rows.Distinct(StringComparer.Ordinal).Count());

        // Sorted on both sides: which of the two renders first is not the claim, and the ids are
        // fresh guids, so a stable order would be an assumption rather than an assertion.
        string[] wanted = [$"{bars[0]} watches {rows[0]}", $"{bars[1]} watches {rows[1]}"];

        cut.WaitForAssertion(() => Assert.Equal(
            wanted.Order(StringComparer.Ordinal),
            Observed(module).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Stuckbar_WhenThePageGoesAway_ThenItsOwnObserverIsDisconnected()
    {
        // An observer outliving its component holds the elements it watches, and a host swapping a
        // view out of the render tree leaves the circuit — and the observer — alive.
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        var cut = RenderSticky();

        var bar = cut.Find(".munin-explorer-page__stuckbar").Id ?? "";
        var row = cut.Find(".munin-explorer-page__facts").Id ?? "";

        cut.WaitForAssertion(() => Assert.Equal([$"{bar} watches {row}"], Observed(module)));

        DisposeComponents();

        Assert.Equal(
            [bar],
            module.Invocations["disconnectHeroFacts"].Select(call => (call.Arguments[0] as string) ?? ""));
    }

    [Fact]
    public void Stuckbar_Always_ThenEveryNameItEmitsHasARuleInBothSampleStylesheets()
    {
        // The bar's four names are new, and the samples are the only stylesheet they have here:
        // neither sample carries Stiler, so a name with no rule renders at browser defaults in both.
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(RenderSticky(withActions: true).FindAll("[class]"))));
    }

    /// <summary>Which bar each observe call was given, paired with the row it was told to watch.</summary>
    /// <remarks>
    /// One string per call rather than a pair, so a failure prints which bar was pointed at which
    /// row: two calls naming one bar is the defect, and a count of them cannot say so.
    /// </remarks>
    private static IEnumerable<string> Observed(BunitJSModuleInterop module) =>
        module.Invocations["observeHeroFacts"]
            .Select(call => $"{call.Arguments[0]} watches {call.Arguments[1]}");

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
