using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The ordering with no view around it: which pool a placement row reaches into, and what the
/// fallback tail is left holding once it has (Fhi.Metadata-35w0p.22).
/// </summary>
/// <remarks>
/// Reachable here and nowhere else: a curated group whose key is also one of
/// <see cref="SectionKeys"/> is a payload no view can be handed through its own parameters,
/// because the groups are resolved from property metadata rather than passed in.
/// </remarks>
public class DetailLayoutTest
{
    private static readonly RenderFragment Nothing = _ => { };

    private static DetailLayoutSection Section(string? key, string heading) =>
        new(key, key ?? heading, heading, null, Nothing);

    private static SectionPlacement Places(string key, bool builtIn = false) =>
        new() { Key = key, IsBuiltIn = builtIn };

    private static IReadOnlyList<string> Headings(IReadOnlyList<DetailLayoutSection> ordered) =>
        [.. ordered.Select(section => section.Heading)];

    /// <summary>One key standing for a curated group and a built-in block at once.</summary>
    private static (DetailLayoutSection[] Groups, DetailLayoutSection[] Blocks) Colliding() =>
        ([Section(SectionKeys.Statistics, "Statistikk (curated)")],
         [Section(SectionKeys.Statistics, "Statistikk (built-in)")]);

    [Fact]
    public void Order_WhenOneKeyNamesACuratedGroupAndABuiltInBlockAndTheGroupIsPlaced_ThenTheBlockStillDraws()
    {
        // The pools are split so a curated group named statistikk cannot swallow this package's
        // block of that name, and the bookkeeping has to be split with them: one shared set of
        // placed keys takes the block out of its own fallback tail, with every field in it.
        var (groups, blocks) = Colliding();

        Assert.Equal(["Statistikk (curated)", "Statistikk (built-in)"],
                     Headings(DetailLayout.Order([Places(SectionKeys.Statistics)], groups, blocks)));
    }

    [Fact]
    public void Order_WhenOneKeyNamesBothAndTheBuiltInIsPlaced_ThenTheCuratedGroupStillDraws()
    {
        // The same collision from the other side, since the tail draws the groups first: a placed
        // built-in must not delete the curated section of that name either.
        var (groups, blocks) = Colliding();

        Assert.Equal(["Statistikk (built-in)", "Statistikk (curated)"],
                     Headings(DetailLayout.Order(
                         [Places(SectionKeys.Statistics, builtIn: true)], groups, blocks)));
    }

    [Fact]
    public void Order_WhenOneKeyNamesBothAndBothArePlaced_ThenEachIsDrawnOnceInTheBandItWasGiven()
    {
        var (groups, blocks) = Colliding();

        Assert.Equal(["Statistikk (built-in)", "Statistikk (curated)"],
                     Headings(DetailLayout.Order(
                         [Places(SectionKeys.Statistics, builtIn: true), Places(SectionKeys.Statistics)],
                         groups, blocks)));
    }

    [Fact]
    public void Order_WhenTheSameRowArrivesTwice_ThenItsSectionIsDrawnOnceRatherThanAtBothBands()
    {
        // A duplicate row is the API's mistake to make, not a reason to draw one section twice:
        // two elements under one id send the nav's second link to the first of them.
        var ordered = DetailLayout.Order(
            [Places("innhold"), Places("kontakt"), Places("innhold")],
            [Section("innhold", "Innhold"), Section("kontakt", "Kontakt")],
            []);

        Assert.Equal(["Innhold", "Kontakt"], Headings(ordered));
    }

    [Fact]
    public void Order_WhenRowsNameSomeOfTheSections_ThenEverySectionItWasGivenIsStillOneOfTheAnswer()
    {
        // What a fact box's yielding rests on. A view asks DetailLayout.Draws whether the section
        // it is about to yield into is among the ones it built, and that is only the same question
        // as "is it on the page" for as long as this rearranges its input without dropping from it.
        // (Fhi.Metadata-lr6yh)
        DetailLayoutSection[] groups = [Section("innhold", "Innhold"), Section("kontakt", "Kontakt")];

        DetailLayoutSection[] blocks =
            [Section(null, "Metadata"), Section(SectionKeys.Statistics, "Statistikk")];

        var ordered = DetailLayout.Order([Places("kontakt"), Places("mangler")], groups, blocks);

        Assert.Equal(["Kontakt", "Innhold", "Metadata", "Statistikk"], Headings(ordered));
        Assert.All([.. groups, .. blocks], section => Assert.Contains(section, ordered));
        Assert.Equal(groups.Length + blocks.Length, ordered.Count);
    }

    [Fact]
    public void Draws_WhenNoSectionCarriesTheKey_ThenItSaysSoRatherThanMatchingTheKeylessOnes()
    {
        // Null is every view's own "the catalogue placed this nowhere", and the keyless sections
        // are its blocks: answering true for that pair would have a box yield into the Metadata
        // block it is drawn beside.
        DetailLayoutSection[] sections = [Section(null, "Metadata"), Section("innhold", "Innhold")];

        Assert.False(DetailLayout.Draws(sections, null));
        Assert.False(DetailLayout.Draws(sections, "kontakt"));
        Assert.True(DetailLayout.Draws(sections, "innhold"));
    }

    [Fact]
    public void Order_WhenARowNamesASectionNeitherPoolHas_ThenNothingIsDrawnForItAndTheRestKeepTheirOrder()
    {
        // Another page's built-in, or a section whose every property is empty on this payload.
        var ordered = DetailLayout.Order(
            [Places("kriterier"), Places("innhold")],
            [Section("innhold", "Innhold")],
            [Section(null, "Metadata")]);

        Assert.Equal(["Innhold", "Metadata"], Headings(ordered));
    }
}
