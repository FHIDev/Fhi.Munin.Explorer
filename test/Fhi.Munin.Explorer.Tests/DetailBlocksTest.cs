using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The shared fact list on its own: which of the three ways it can spend a value wins, and whether
/// a list already on screen follows a row that changed underneath it.
/// </summary>
/// <remarks>
/// The three detail views fill it and their own tests pin the facts each of them chooses. What is
/// only reachable here is the combination no view produces yet — an authored list that also carries
/// a target — and a second render of one list instance, which no view's own tests perform.
/// </remarks>
public class DetailBlocksTest : ExplorerTestContext
{
    /// <summary>A receiver for the fragment under test, which is not a component.</summary>
    /// <remarks>
    /// A component rather than a bare fragment because a list that has to follow a change needs a
    /// second render of one instance, and a fragment has no parameters to change.
    /// </remarks>
    private sealed class Host : ComponentBase
    {
        [Parameter]
        public IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> Facts { get; set; } = [];

        [Parameter]
        public bool Authored { get; set; }

        [Parameter]
        public string Language { get; set; } = ReaderLanguage.Norwegian;

        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, DetailBlocks.LinkedFacts(Facts, Language, Authored));
    }

    private IRenderedComponent<Host> RenderList(
        bool authored, params (string Label, string? Value, bool Norwegian, string? Href)[] facts) =>
        Render<Host>(p => p.Add(c => c.Authored, authored).Add(c => c.Facts, facts));

    [Fact]
    public void LinkedFacts_WhenNoRowHasAValue_ThenNoListIsDrawnRatherThanARunOfNone()
    {
        // The half of the absence rule the views never reach, because each gates its section on
        // AnyFacts first. The list answers it too, so a caller that forgets the gate draws nothing.
        var cut = RenderList(authored: false, ("Lovverk", null, true, null), ("Frekvens", "  ", true, "/x"));

        Assert.Empty(cut.FindAll("dl"));
    }

    [Fact]
    public void LinkedFacts_WhenARowHasNoValueForAnEnglishReader_ThenItReadsNoneUnmarkedMutedAndGoesNowhere()
    {
        // "None" is this package's word, so it must not carry the catalogue's lang="no" that the row
        // would wear with a value, nor the target. English, because a Norwegian reader marks nothing
        // and could not tell. The filled sibling shows the marking is on for this reader.
        var cut = Render<Host>(p => p
            .Add(c => c.Language, "en")
            .Add(c => c.Facts, [("Legal basis", null, true, "/lovverk"), ("Data controller", "St. Olavs hospital HF", true, null)]));

        var absent = Cell(cut, "Legal basis");

        Assert.Equal("None", absent.TextContent);
        Assert.Equal(DetailBlocks.Absent, absent.ClassName);
        Assert.Null(absent.GetAttribute("lang"));
        Assert.Empty(absent.QuerySelectorAll("a"));
        Assert.Equal("no", Cell(cut, "Data controller").GetAttribute("lang"));
    }

    /// <summary>One row's value cell, asked for by the label beside it.</summary>
    private static IElement Cell(IRenderedComponent<Host> cut, string label) =>
        cut.FindAll("dl.munin-explorer-page__fields > div")
           .FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
           ?.QuerySelector("dd")
        ?? throw new InvalidOperationException($"No '{label}' row in the list.");

    [Fact]
    public void LinkedFacts_WhenAnAuthoredListCarriesATargetToo_ThenTheLinkWinsAndItsSiblingStillRendersMarkdown()
    {
        // The rule is the helper's rather than one caller's: a row with somewhere to go is drawn as
        // that link, and its value stays the literal text it carries. No view passes both today,
        // and the next one that does would otherwise lose the renderer with nothing failing.
        var cut = RenderList(authored: true,
                             ("Kilde", "[Als registeret](https://als.example)", false, "/kilder?kilde=1"),
                             ("Beskrivelse", "[Skjemaet](https://skjema.example)", false, null));

        var link = Assert.Single(Cell(cut, "Kilde").QuerySelectorAll("a"));

        Assert.Equal("/kilder?kilde=1", link.GetAttribute("href"));
        Assert.Equal("[Als registeret](https://als.example)", link.TextContent);

        // The sibling, so the assertion above says the branch was taken rather than that the
        // renderer was off for the whole list.
        var prose = Assert.Single(Cell(cut, "Beskrivelse").QuerySelectorAll("a"));

        Assert.Equal("https://skjema.example", prose.GetAttribute("href"));
        Assert.Equal("Skjemaet", prose.TextContent);
    }

    [Fact]
    public void LinkedFacts_WhenATargetIsAnAbsoluteAddress_ThenOnlyThatAnchorCarriesRel()
    {
        // A catalogue URL is guarded as the metadata rows guard theirs; a host's own path is not.
        var cut = RenderList(authored: false,
                             ("Lovverk", "Helseregisterloven", true, "https://lovdata.no/lov"),
                             ("Kilde", "Als registeret", false, "/kilder?kilde=1"));

        Assert.Equal("noopener noreferrer", Cell(cut, "Lovverk").QuerySelector("a")!.GetAttribute("rel"));
        Assert.Null(Cell(cut, "Kilde").QuerySelector("a")!.GetAttribute("rel"));
    }

    [Fact]
    public void LinkedFacts_WhenTheRowsChangeUnderAListAlreadyOnScreen_ThenTheDomFollowsThemRatherThanTheFirstDraw()
    {
        // Not a test of the per-row stride, which nothing can see: measured at 10 and at 20, every
        // re-render shape tried — a row removed, inserted, switching branch — renders identical
        // markup, because the diff falls back to a sequential match when the numbers repeat.
        var cut = RenderList(authored: false,
                             ("Kilde", "Als registeret", false, "/kilder?kilde=1"),
                             ("Type datakilde", "Kvalitetsregister", false, null),
                             ("Dataansvarlig", "St. Olavs hospital HF", false, null));

        cut.Render(p => p.Add(
            c => c.Facts,
            new (string Label, string? Value, bool Norwegian, string? Href)[]
            {
                ("Kilde", "Annet register", false, "/kilder?kilde=2"),
                ("Type datakilde", "Helseregister", false, null),
                ("Dataansvarlig", "Hemit HF", false, null),
            }));

        var link = Assert.Single(cut.FindAll("dd a"));

        Assert.Equal("/kilder?kilde=2", link.GetAttribute("href"));
        Assert.Equal("Annet register", link.TextContent);
        Assert.Equal(["Annet register", "Helseregister", "Hemit HF"],
                     cut.FindAll("dd").Select(cell => cell.TextContent));
    }

    [Fact]
    public void LinkedFacts_WhenARowGainsATargetOnARerender_ThenItIsRedrawnAsALinkRatherThanKeptAsText()
    {
        // The branch switching under a list already on screen: the host wires an address after the
        // first draw, and the row has to stop being a text node and become an anchor in the same
        // position. This is what KildeSearch does to the Kilde row when its parameter arrives.
        var cut = RenderList(authored: false, ("Kilde", "Als registeret", false, null));

        Assert.Empty(cut.FindAll("dd a"));

        cut.Render(p => p.Add(
            c => c.Facts,
            new (string Label, string? Value, bool Norwegian, string? Href)[]
            {
                ("Kilde", "Als registeret", false, "/kilder?kilde=1"),
            }));

        Assert.Equal("/kilder?kilde=1", Assert.Single(cut.FindAll("dd a")).GetAttribute("href"));
        Assert.Equal("Als registeret", cut.Find("dd").TextContent);
    }
}
