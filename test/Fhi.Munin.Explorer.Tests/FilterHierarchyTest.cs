using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Which node hangs where. Tested apart from the markup because a group hung off its
/// <c>parentId</c> rather than off its owner renders perfectly and narrows by the wrong id.</summary>
public class FilterHierarchyTest
{
    private static readonly Guid Mfr = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Npr = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Fodsel = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Svangerskap = new("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Registrering = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Oppfolging = new("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid Bakgrunn = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Levekaar = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Diagnoser = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid NotInThePayload = new("ffffffff-0000-0000-0000-000000000001");
    private static readonly Guid Kontroll = new("dddddddd-0000-0000-0000-000000000003");
    private static readonly Guid Utskriving = new("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid Tvilling = new("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly Guid Adopsjon = new("bbbbbbbb-0000-0000-0000-000000000004");

    [Fact]
    public void Build_WhenGroupsCarryEveryFilterValue_ThenTheTreeDrawsAllThree()
    {
        // The opt-out belongs to the standalone facet and to nothing else, so a tree reading it
        // would hide a group the reader can see in Munin's own explorer. (Fhi.Metadata-fbe3w)
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering), filter: "1"),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering),
                               filter: VariabelgruppeFacet.StandaloneFacetOptOut),
                Variabelgruppe(Diagnoser, "Diagnoser", Under(Mfr, datasamling: Registrering))
            ]
        };

        var datasamling = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal(["Bakgrunn", "Levekår", "Diagnoser"], datasamling.Children.Select(node => node.Name));
        Assert.All(datasamling.Children, node => Assert.Equal(HierarchyLevel.Variabelgruppe, node.Level));
    }

    [Fact]
    public void Build_WhenADatasamlingHangsStraightOffItsKilde_ThenSoDoTheGroupsInIt()
    {
        // Most kilder have no delkilde at all, so the level below a kilde is usually this one.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering))
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));
        var datasamling = Assert.Single(kilde.Children);

        Assert.Equal(HierarchyLevel.Datasamling, datasamling.Level);
        Assert.Equal("Bakgrunn", Assert.Single(datasamling.Children).Name);
    }

    [Fact]
    public void Build_WhenAGroupIsInNoDatasamling_ThenItIsItsOwnBranchBesideThemUnderItsDelkilde()
    {
        // A delkilde holds variables its datasamlinger do not, and such a group has nowhere else to
        // be drawn: folding it into a datasamling would say it is in one.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder = [Delkilde(Fodsel, Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr, Fodsel)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, delkilde: Fodsel, datasamling: Registrering)),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, delkilde: Fodsel))
            ]
        };

        var delkilde = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal(HierarchyLevel.Delkilde, delkilde.Level);
        Assert.Equal([HierarchyLevel.Datasamling, HierarchyLevel.Variabelgruppe],
                     delkilde.Children.Select(node => node.Level));
        Assert.Equal("Levekår", delkilde.Children[1].Name);
        Assert.Equal("Bakgrunn", Assert.Single(delkilde.Children[0].Children).Name);
    }

    [Fact]
    public void Build_WhenAGroupsParentIdPointsOutsideItsOwner_ThenTheOwnerDecidesWhereItHangs()
    {
        // parentId is another group and never the catalogue owner. Reading it as ownership draws a
        // group under a datasamling its variables are not in, and the count beside it is then a
        // number the rows a tick produces do not match.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr), Datasamling(Oppfolging, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering)),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Oppfolging), parent: Bakgrunn)
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));

        Assert.Equal("Bakgrunn", Assert.Single(kilde.Children[0].Children).Name);
        Assert.Equal("Levekår", Assert.Single(kilde.Children[1].Children).Name);
    }

    [Fact]
    public void Build_WhenAGroupIsPlacedUnderTwoOwners_ThenItIsDrawnUnderBothAndKeepsOneId()
    {
        // Where a node is drawn and what ticking it selects are different questions: the paths
        // differ so a disclosure belongs to one position, and the id is one so both tick one filter.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr), Datasamling(Oppfolging, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn",
                               [.. Under(Mfr, datasamling: Registrering), .. Under(Mfr, datasamling: Oppfolging)])
            ]
        };

        var drawn = Assert.Single(FilterHierarchy.Build(facets))
            .Children
            .Select(datasamling => Assert.Single(datasamling.Children))
            .ToList();

        Assert.Equal([Bakgrunn, Bakgrunn], drawn.Select(node => node.Id));
        Assert.Equal(2, drawn.Select(node => node.Path).Distinct().Count());
    }

    [Fact]
    public void Build_WhenTheOwningDatasamlingIsNotInTheAnswer_ThenTheGroupFallsBackToItsKilde()
    {
        // The facets are cross-filtered, so a datasamling with no matching variables of its own is
        // genuinely absent from an answer a group under it is in. Dropping the group there would
        // take a filter off the panel over a level that is merely empty.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: NotInThePayload))
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));

        Assert.Equal("Bakgrunn", Assert.Single(kilde.Children).Name);
    }

    [Fact]
    public void Build_WhenAParentHoldsFewerVariablesThanItsChildren_ThenEveryCountIsTheAnswersOwn()
    {
        // The API cross-filters each count and never rolls a subtree up, so a container reads 0
        // while the groups under it carry the numbers. Summing here would put a number beside a
        // checkbox that the rows ticking it do not add up to.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr, count: 9)],
            Datasamlinger = [Datasamling(Registrering, Mfr, count: 0)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering), count: 4),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering), count: 5)
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));
        var datasamling = Assert.Single(kilde.Children);

        Assert.Equal(9, kilde.Count);
        Assert.Equal(0, datasamling.Count);
        Assert.Equal([4, 5], datasamling.Children.Select(node => node.Count));
    }

    [Fact]
    public void Build_WhenTheAnswerCarriesNothing_ThenTheTreeIsEmpty()
    {
        Assert.Empty(FilterHierarchy.Build(Answer()));
    }

    [Fact]
    public void Build_WhenTheAnswerPredatesTheTreeCollection_ThenTheTreeHasNoGroupsRatherThanTheFacets()
    {
        // hierarkiVariabelgrupper is the tree's collection and variabelgrupper is the facet's own,
        // scoped differently: falling back to the second would draw a curated shortlist as a
        // kilde's contents.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr)],
            Variabelgrupper = [Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering))]
        };

        Assert.Empty(Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children).Children);
    }

    [Fact]
    public void Build_WhenTwoGroupsNameEachOtherAsParent_ThenBothAreStillDrawn()
    {
        // A cycle the catalogue should never produce has no root to be reached from, and silently
        // losing one takes a filter the reader can neither see nor clear off the panel.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering), parent: Levekaar),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering), parent: Bakgrunn)
            ]
        };

        var datasamling = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal([Bakgrunn, Levekaar], Flatten(datasamling.Children).Select(node => node.Id));
    }

    [Fact]
    public void Build_WhenAKildeHasNothingUnderIt_ThenItIsStillARootAndNoRootIdRepeats()
    {
        // The panel keys these roots by id and reaches a kilde's whole subtree through that key, so
        // a listed kilde missing from them loses its datasamlinger and its groups from the facet —
        // and its search — with no error anywhere. (Fhi.Metadata-g51gg)
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr), Kilde(Npr), Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Npr)]
        };

        var roots = FilterHierarchy.Build(facets);

        Assert.Equal([Mfr, Npr], roots.Select(node => node.Id));
        Assert.All(roots, node => Assert.Equal(HierarchyLevel.Kilde, node.Level));
    }

    [Fact]
    public void Build_WhenTheAnswerIsAWholeCatalogue_ThenEveryPlacementIsDrawnExactlyOnce()
    {
        // The shape a real answer has — two kilder, delkilder, datasamlinger under both levels and
        // a group per datasamling — so a rule that only holds for a single branch fails here.
        var facets = Catalogue(
            kilder: 2, delkilderPerKilde: 2, datasamlingerPerDelkilde: 3, variabelgrupperPerDatasamling: 4);

        var tree = FilterHierarchy.Build(facets);
        var nodes = Flatten(tree).ToList();

        Assert.Equal(2, tree.Count);
        Assert.Equal(2, nodes.Count(node => node.Level == HierarchyLevel.Kilde));
        Assert.Equal(4, nodes.Count(node => node.Level == HierarchyLevel.Delkilde));
        Assert.Equal(12, nodes.Count(node => node.Level == HierarchyLevel.Datasamling));
        Assert.Equal(48, nodes.Count(node => node.Level == HierarchyLevel.Variabelgruppe));
        Assert.Equal(nodes.Count, nodes.Select(node => node.Path).Distinct().Count());
    }

    [Fact]
    public void Build_WhenADelkildeNamesAnotherAsItsParent_ThenItIsDrawnUnderItWithItsOwnLevelsBeneath()
    {
        // A delkilde hangs off a delkilde as readily as off its kilde, and drawing the child beside
        // its parent instead would say the two are peers of one kilde. No displayOrder here, so the
        // siblings keep SiblingOrder's legacy order: delkilder as sent, then datasamlinger.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder = [Delkilde(Fodsel, Mfr), Delkilde(Svangerskap, Mfr, parent: Fodsel)],
            Datasamlinger = [Datasamling(Registrering, Mfr, Fodsel), Datasamling(Oppfolging, Mfr, Svangerskap)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, delkilde: Fodsel)),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, delkilde: Svangerskap))
            ]
        };

        var tree = FilterHierarchy.Build(facets);
        var fodsel = Assert.Single(Assert.Single(tree).Children);
        var svangerskap = fodsel.Children[0];

        Assert.Equal([HierarchyLevel.Delkilde, HierarchyLevel.Datasamling, HierarchyLevel.Variabelgruppe],
                     fodsel.Children.Select(node => node.Level));
        Assert.Equal(Svangerskap, svangerskap.Id);
        Assert.Equal($"Kilde:{Mfr}/Delkilde:{Fodsel}/Delkilde:{Svangerskap}", svangerskap.Path);
        Assert.Equal([Oppfolging, Levekaar], svangerskap.Children.Select(node => node.Id));
        Assert.Equal(1, Flatten(tree).Count(node => node.Id == Svangerskap));
    }

    [Fact]
    public void Build_WhenADelkildesParentIsNotInTheAnswer_ThenItStandsAtKildeLevelWithWhatIsUnderIt()
    {
        // The parent delkilde is cross-filtered away as readily as any other facet, and losing its
        // children with it would take a whole branch of filters off the panel.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder = [Delkilde(Svangerskap, Mfr, parent: NotInThePayload)],
            Datasamlinger = [Datasamling(Registrering, Mfr, Svangerskap)]
        };

        var delkilde = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal(HierarchyLevel.Delkilde, delkilde.Level);
        Assert.Equal(Svangerskap, delkilde.Id);
        Assert.Equal(Registrering, Assert.Single(delkilde.Children).Id);
    }

    [Fact]
    public void Build_WhenThePayloadRepeatsAVariabelgruppeId_ThenTheParentedCopyIsTheOneDrawn()
    {
        // Two copies of one id differ in owner, parent and name alike, so keeping the first listed
        // would draw the group somewhere else, under nothing, and call it something else.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr), Datasamling(Oppfolging, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering)),
                Variabelgruppe(Levekaar, "Levekår først", Under(Mfr, datasamling: Oppfolging)),
                Variabelgruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering), parent: Bakgrunn)
            ]
        };

        var tree = FilterHierarchy.Build(facets);
        var kilde = Assert.Single(tree);
        var levekaar = Assert.Single(Assert.Single(kilde.Children[0].Children).Children);

        Assert.Equal("Levekår", levekaar.Name);
        Assert.Empty(kilde.Children[1].Children);
        Assert.Equal(1, Flatten(tree).Count(node => node.Id == Levekaar));
    }

    [Fact]
    public void Build_WhenAGroupNamesOneOwnerTwice_ThenItIsDrawnOnceUnderIt()
    {
        // One press ticks one filter, so a repeated owner drawing two rows would put two chips over
        // the results for it. (Fhi.Metadata-l9l2n.82)
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn",
                               [.. Under(Mfr, datasamling: Registrering), .. Under(Mfr, datasamling: Registrering)])
            ]
        };

        var datasamling = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal(Bakgrunn, Assert.Single(datasamling.Children).Id);
    }

    [Fact]
    public void Build_WhenThePayloadRepeatsACatalogueId_ThenOneNodeIsDrawnCarryingTheFirstListedCount()
    {
        // Every level of the tree keys something by these ids, so a repeated one is both a row drawn
        // twice and a dictionary that throws while building the panel. Here every copy names the same
        // parents; the two below disagree about a delkilde, which each level settles its own way.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr, count: 9), Kilde(Mfr, count: 3)],
            Delkilder = [Delkilde(Fodsel, Mfr, count: 7), Delkilde(Fodsel, Mfr, count: 2)],
            Datasamlinger =
            [
                Datasamling(Registrering, Mfr, Fodsel, count: 4), Datasamling(Registrering, Mfr, Fodsel, count: 1)
            ],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, delkilde: Fodsel, datasamling: Registrering))
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));
        var delkilde = Assert.Single(kilde.Children);
        var datasamling = Assert.Single(delkilde.Children);

        Assert.Equal([9, 7, 4], new[] { kilde.Count, delkilde.Count, datasamling.Count });
        Assert.Equal("Bakgrunn", Assert.Single(datasamling.Children).Name);
    }

    [Fact]
    public void Build_WhenARepeatedDelkildeIdNamesAParentInOnlyOneCopy_ThenItIsDrawnUnderThatParent()
    {
        // The copy hanging off a present parent wins, first listed or not — the one rule the delkilde
        // level does not share with the levels above and below it, whose parents are not of their own
        // kind and cannot be preferred. (Fhi.Metadata-l9l2n.82)
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder =
            [
                Delkilde(Svangerskap, Mfr),
                Delkilde(Fodsel, Mfr),
                Delkilde(Svangerskap, Mfr, parent: Fodsel)
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));
        var fodsel = Assert.Single(kilde.Children);

        Assert.Equal(Fodsel, fodsel.Id);
        Assert.Equal(Svangerskap, Assert.Single(fodsel.Children).Id);
    }

    [Fact]
    public void Build_WhenARepeatedDatasamlingIdDisagreesAboutItsDelkilde_ThenItIsStillDrawnOnce()
    {
        // The two copies are split across the two lookups the kilde level keeps, so settling the id
        // inside either bucket still draws it at both paths — one press, two chips. (Fhi.Metadata-l9l2n.82)
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder = [Delkilde(Fodsel, Mfr)],
            Datasamlinger = [Datasamling(Registrering, Mfr, Fodsel), Datasamling(Registrering, Mfr)]
        };

        var tree = FilterHierarchy.Build(facets);
        var delkilde = Assert.Single(Assert.Single(tree).Children);

        Assert.Equal(1, Flatten(tree).Count(node => node.Id == Registrering));
        Assert.Equal(Registrering, Assert.Single(delkilde.Children).Id);
    }

    [Fact]
    public void Build_WhenARepeatedDelkildeIdDisagreesAboutItsKilde_ThenItIsStillDrawnOnce()
    {
        // Each kilde is handed its own bucket of delkilder, so settling the id inside one bucket
        // still draws it under both kilder — one press, two chips, and the second kilde offering a
        // filter none of its variables are behind. (Fhi.Metadata-l9l2n.82)
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr), Kilde(Npr)],
            Delkilder = [Delkilde(Fodsel, Mfr), Delkilde(Fodsel, Npr)]
        };

        var tree = FilterHierarchy.Build(facets);

        Assert.Equal(1, Flatten(tree).Count(node => node.Id == Fodsel));
        Assert.Equal(Fodsel, Assert.Single(tree[0].Children).Id);
        Assert.Empty(tree[1].Children);
    }

    [Fact]
    public void Build_WhenTheOwningDatasamlingIdIsAnotherKildes_ThenTheGroupStaysAtTheKildeItNames()
    {
        // The id spaces are independent Guids off the wire, so a datasamling id that is in the
        // answer settles nothing until its kilde agrees: filing the group there would draw it under
        // a kilde whose variables it has none of, with no error anywhere.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr), Kilde(Npr)],
            Datasamlinger = [Datasamling(Registrering, Npr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering))
            ]
        };

        var tree = FilterHierarchy.Build(facets);

        Assert.Equal("Bakgrunn", Assert.Single(tree[0].Children).Name);
        Assert.Empty(Assert.Single(tree[1].Children).Children);
    }

    [Fact]
    public void Build_WhenTheOwningDelkildeIdIsAnotherKildes_ThenTheGroupStaysAtTheKildeItNames()
    {
        // The delkilde arm of the same hazard, and its own clause in the builder: a delkilde id in
        // the answer is not a delkilde of the kilde the placement names.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr), Kilde(Npr)],
            Delkilder = [Delkilde(Fodsel, Npr)],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, delkilde: Fodsel))
            ]
        };

        var tree = FilterHierarchy.Build(facets);

        Assert.Equal("Bakgrunn", Assert.Single(tree[0].Children).Name);
        Assert.Empty(Assert.Single(tree[1].Children).Children);
    }

    // ------------------------------------------------------ sibling order (Fhi.Metadata-vc789)

    [Theory]
    [InlineData(nameof(SiblingOrderFixtures.KildeKK))]
    [InlineData(nameof(SiblingOrderFixtures.ManualCancerFirst))]
    [InlineData(nameof(SiblingOrderFixtures.NewChildAfterManualPrefix))]
    [InlineData(nameof(SiblingOrderFixtures.ResetToSource))]
    public void Build_WhenTheAnswerRanksAKildesChildren_ThenTheyAreInTheResolvedOrder(string fixture)
    {
        // The rank the API resolved is the only way a curator's manual move, a new child or a reset
        // reaches the tree, so each payload must draw exactly in that rank and in no kind-first order.
        var scope = SiblingOrderFixtures.Named(fixture);

        var kilde = Assert.Single(FilterHierarchy.Build(Ranked(scope)));

        Assert.Equal(scope.Expected, kilde.Children.Select(node => node.Name));
    }

    [Fact]
    public void Build_WhenRanksInterleaveTheKindsAtRootAndNested_ThenNeitherKindIsGroupedFirst()
    {
        // Either kind first, at either level, fails here: the kilde's delkilde sits between its two
        // datasamlinger, and under that delkilde a nested delkilde sits between two datasamlinger.
        var kilde = Assert.Single(FilterHierarchy.Build(Interleaved(ranked: true)));
        var fodsel = kilde.Children[1];

        Assert.Equal([Registrering, Fodsel, Oppfolging], kilde.Children.Select(node => node.Id));
        Assert.Equal([Kontroll, Svangerskap, Utskriving, Bakgrunn], fodsel.Children.Select(node => node.Id));
    }

    [Fact]
    public void Build_WhenTwoParentsRankTheirChildrenOppositely_ThenEachParentIsOrderedByItsOwnRanks()
    {
        // Ranks are per parent and reuse the same numbers, so ordering across parents — or letting
        // one parent's kind order leak into the next — would put one of these two the wrong way up.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder =
            [
                Delkilde(Fodsel, Mfr, rank: 1), Delkilde(Svangerskap, Mfr, rank: 2),
                Delkilde(Tvilling, Mfr, parent: Fodsel, rank: 2), Delkilde(Adopsjon, Mfr, parent: Svangerskap, rank: 1)
            ],
            Datasamlinger =
            [
                Datasamling(Registrering, Mfr, Fodsel, rank: 1), Datasamling(Oppfolging, Mfr, Svangerskap, rank: 2)
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));

        Assert.Equal([Fodsel, Svangerskap], kilde.Children.Select(node => node.Id));
        Assert.Equal([Registrering, Tvilling], kilde.Children[0].Children.Select(node => node.Id));
        Assert.Equal([Adopsjon, Oppfolging], kilde.Children[1].Children.Select(node => node.Id));
    }

    [Fact]
    public void Build_WhenAChildIsMovedUpPastAnAbsentParent_ThenItsOldParentsRankDoesNotPlaceIt()
    {
        // Both carry rank 1 from a parent the answer left out, which beside Fodsel's 2 is a
        // stranger's number: taken at face value, either would jump ahead of the ranked sibling.
        var facets = Answer() with
        {
            Kilder = [Kilde(Mfr)],
            Delkilder =
            [
                Delkilde(Svangerskap, Mfr, parent: NotInThePayload, rank: 1), Delkilde(Fodsel, Mfr, rank: 2)
            ],
            Datasamlinger = [Datasamling(Registrering, Mfr, NotInThePayload, rank: 1)]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));

        Assert.Equal([Fodsel, Svangerskap, Registrering], kilde.Children.Select(node => node.Id));
        Assert.Equal([2, null, null], kilde.Children.Select(node => node.DisplayOrder));
    }

    [Fact]
    public void Build_WhenTheAnswerPredatesDisplayOrder_ThenSiblingsKeepPayloadOrderDelkilderFirst()
    {
        // An older API sends no rank, and its payload order is the imported order it already
        // applied: re-sorting by name, or falling back to datasamlinger first, would invent one.
        var kilde = Assert.Single(FilterHierarchy.Build(Interleaved(ranked: false)));
        var fodsel = kilde.Children[0];

        Assert.Equal([Fodsel, Oppfolging, Registrering], kilde.Children.Select(node => node.Id));
        Assert.Equal([Svangerskap, Utskriving, Kontroll, Bakgrunn], fodsel.Children.Select(node => node.Id));
        Assert.All(Flatten([kilde]), node => Assert.Null(node.DisplayOrder));
    }

    [Fact]
    public void Build_WhenTheAnswerIsRanked_ThenOnlyTheOrderChangesAndNotWhereOrWhatAnythingIs()
    {
        // Paths are what the panel's disclosure and selection keys are made of, and counts are the
        // answer's own: ordering that rebuilt a node could move either while every order test passes.
        static IEnumerable<(string, Guid, HierarchyLevel, int)> Shape(FilterOptions facets) =>
            Flatten(FilterHierarchy.Build(facets))
                .Select(node => (node.Path, node.Id, node.Level, node.Count))
                .OrderBy(entry => entry.Path, StringComparer.Ordinal);

        Assert.Equal(Shape(Interleaved(ranked: false)), Shape(Interleaved(ranked: true)));
        Assert.Equal(
            [2, 3, 5, 7, 11, 13, 17],
            Flatten(FilterHierarchy.Build(Interleaved(ranked: true)))
                .Where(node => node.Level is not HierarchyLevel.Kilde)
                .Select(node => node.Count)
                .Order());
    }

    /// <summary>One sibling-order scope as the /filters answer carries it, every child straight off K_KK.</summary>
    private static FilterOptions Ranked(SiblingScope scope) => Answer() with
    {
        Kilder = [Kilde(SiblingOrderFixtures.KildeKkId)],
        Delkilder =
        [
            .. scope.Delkilder.Select(sibling =>
                Delkilde(sibling.Id, SiblingOrderFixtures.KildeKkId, rank: sibling.DisplayOrder)
                    with { Name = sibling.Name })
        ],
        Datasamlinger =
        [
            .. scope.Datasamlinger.Select(sibling =>
                Datasamling(sibling.Id, SiblingOrderFixtures.KildeKkId, rank: sibling.DisplayOrder)
                    with { Name = sibling.Name })
        ]
    };

    /// <summary>Both kinds under a kilde and under one of its delkilder, with a group beside them.</summary>
    /// <remarks>Payload order is deliberately not the ranked order, at either level.</remarks>
    private static FilterOptions Interleaved(bool ranked)
    {
        int? Rank(int rank) => ranked ? rank : null;

        return Answer() with
        {
            Kilder = [Kilde(Mfr, count: 19)],
            Delkilder =
            [
                Delkilde(Fodsel, Mfr, count: 2, rank: Rank(2)),
                Delkilde(Svangerskap, Mfr, parent: Fodsel, count: 3, rank: Rank(2))
            ],
            Datasamlinger =
            [
                Datasamling(Oppfolging, Mfr, count: 5, rank: Rank(3)),
                Datasamling(Registrering, Mfr, count: 7, rank: Rank(1)),
                Datasamling(Utskriving, Mfr, Fodsel, count: 11, rank: Rank(3)),
                Datasamling(Kontroll, Mfr, Fodsel, count: 13, rank: Rank(1))
            ],
            HierarchyVariabelgrupper =
            [
                Variabelgruppe(Bakgrunn, "Bakgrunn", Under(Mfr, delkilde: Fodsel), count: 17)
            ]
        };
    }

    /// <summary>Every node of the tree, parents before what hangs under them.</summary>
    private static IEnumerable<HierarchyNode> Flatten(IEnumerable<HierarchyNode> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

    /// <summary>An answer carrying nothing, which every fixture here narrows to what it is about.</summary>
    private static FilterOptions Answer() => new();

    private static KildeFacet Kilde(Guid id, int count = 0) =>
        new() { Id = id, Name = $"Kilde {id:N}", ShortName = "", Count = count };

    private static DelkildeFacet Delkilde(
        Guid id, Guid kilde, Guid? parent = null, int count = 0, int? rank = null) =>
        new()
        {
            Id = id,
            Name = $"Delkilde {id:N}",
            KildeId = kilde,
            ParentDelkildeId = parent,
            Count = count,
            DisplayOrder = rank
        };

    private static DatasamlingFacet Datasamling(
        Guid id, Guid kilde, Guid? delkilde = null, int count = 0, int? rank = null) =>
        new()
        {
            Id = id,
            Name = $"Datasamling {id:N}",
            KildeId = kilde,
            DelkildeId = delkilde,
            Count = count,
            DisplayOrder = rank
        };

    private static VariabelgruppeFacet Variabelgruppe(
        Guid id,
        string name,
        IReadOnlyList<VariabelgruppeOwner> owners,
        Guid? parent = null,
        string? filter = null,
        int count = 0) =>
        new() { Id = id, Name = name, ParentId = parent, Filter = filter, Count = count, Owners = owners };

    /// <summary>One placement, named the way the payload names it: a kilde, and how far down it reaches.</summary>
    private static IReadOnlyList<VariabelgruppeOwner> Under(
        Guid kilde, Guid? delkilde = null, Guid? datasamling = null) =>
        [new() { KildeId = kilde, DelkildeId = delkilde, DatasamlingId = datasamling }];

    /// <summary>An answer the size of a real one, every level of it populated.</summary>
    private static FilterOptions Catalogue(
        int kilder, int delkilderPerKilde, int datasamlingerPerDelkilde, int variabelgrupperPerDatasamling)
    {
        List<KildeFacet> kildeFacets = [];
        List<DelkildeFacet> delkildeFacets = [];
        List<DatasamlingFacet> datasamlingFacets = [];
        List<VariabelgruppeFacet> variabelgruppeFacets = [];

        for (var kilde = 0; kilde < kilder; kilde++)
        {
            var kildeId = Id("a", kilde);
            kildeFacets.Add(Kilde(kildeId, count: kilde));

            for (var delkilde = 0; delkilde < delkilderPerKilde; delkilde++)
            {
                var delkildeId = Id("b", (kilde * delkilderPerKilde) + delkilde);
                delkildeFacets.Add(Delkilde(delkildeId, kildeId));

                for (var datasamling = 0; datasamling < datasamlingerPerDelkilde; datasamling++)
                {
                    var index = (kilde * delkilderPerKilde * datasamlingerPerDelkilde)
                                + (delkilde * datasamlingerPerDelkilde) + datasamling;
                    var datasamlingId = Id("d", index);
                    datasamlingFacets.Add(Datasamling(datasamlingId, kildeId, delkildeId));

                    for (var variabelgruppe = 0; variabelgruppe < variabelgrupperPerDatasamling; variabelgruppe++)
                    {
                        var variabelgruppeId = Id("c", (index * variabelgrupperPerDatasamling) + variabelgruppe);

                        // Every third group opted out of the standalone facet, so a tree that read
                        // the opt-out would come up short of the count below rather than empty.
                        variabelgruppeFacets.Add(Variabelgruppe(
                            variabelgruppeId,
                            $"Variabelgruppe {variabelgruppeId:N}",
                            Under(kildeId, delkildeId, datasamlingId),
                            filter: variabelgruppe % 3 == 0 ? VariabelgruppeFacet.StandaloneFacetOptOut : null));
                    }
                }
            }
        }

        return Answer() with
        {
            Kilder = kildeFacets,
            Delkilder = delkildeFacets,
            Datasamlinger = datasamlingFacets,
            HierarchyVariabelgrupper = variabelgruppeFacets
        };
    }

    private static Guid Id(string prefix, int index) =>
        new($"{prefix}{prefix}{prefix}{prefix}{prefix}{prefix}{prefix}{prefix}-0000-0000-0000-{index:d12}");
}
