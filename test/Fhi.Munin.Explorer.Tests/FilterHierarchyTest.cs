using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The filter panel's hierarchy, head-on: which node hangs where, and which group the standalone
/// facet may offer.
/// </summary>
/// <remarks>
/// Tested apart from the markup because the rules are about the payload rather than about drawing:
/// a group hung off its <c>parentId</c> rather than off its owner, or an opted-out one offered as a
/// checkbox, renders perfectly and narrows by something else than the row says.
/// </remarks>
public class FilterHierarchyTest
{
    private static readonly Guid Mfr = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Fodsel = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Registrering = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Oppfolging = new("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid Bakgrunn = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Levekaar = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Diagnoser = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid NotInThePayload = new("ffffffff-0000-0000-0000-000000000001");

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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering), filter: "1"),
                Gruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering),
                       filter: VariabelgruppeFacet.StandaloneFacetOptOut),
                Gruppe(Diagnoser, "Diagnoser", Under(Mfr, datasamling: Registrering))
            ]
        };

        var datasamling = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal(["Bakgrunn", "Levekår", "Diagnoser"], datasamling.Children.Select(node => node.Name));
        Assert.All(datasamling.Children, node => Assert.True(node.Offered));
        Assert.All(datasamling.Children, node => Assert.Equal(HierarchyLevel.Variabelgruppe, node.Level));
    }

    [Fact]
    public void StandaloneVariabelgrupper_WhenAGroupIsOptedOut_ThenItNestsWhatIsOfferedWithoutBeingOffered()
    {
        // Both halves, because either alone passes against the other's failure: a checkbox there
        // offers a filter the API withholds, and dropping the row strands the group under it.
        var facets = Answer() with
        {
            Variabelgrupper =
            [
                Gruppe(Bakgrunn, "Bakgrunn", [], filter: VariabelgruppeFacet.StandaloneFacetOptOut),
                Gruppe(Levekaar, "Levekår", [], parent: Bakgrunn, filter: "1"),
                Gruppe(Diagnoser, "Diagnoser", [], parent: Bakgrunn)
            ]
        };

        var trunk = Assert.Single(FilterHierarchy.StandaloneVariabelgrupper(facets));

        Assert.False(trunk.Offered);
        Assert.Equal(["Levekår", "Diagnoser"], trunk.Children.Select(node => node.Name));
        Assert.All(trunk.Children, node => Assert.True(node.Offered));
    }

    [Fact]
    public void StandaloneVariabelgrupper_WhenAnOptedOutGroupStandsAlone_ThenNothingOffersItBack()
    {
        // The shape the answer takes when the request already selects the group: it is listed for
        // the selection's sake and is still not a choice the facet may make. (Fhi.Metadata-fbe3w)
        // The method takes no selection at all, which is what makes that structural.
        var facets = Answer() with
        {
            Variabelgrupper =
            [
                Gruppe(Bakgrunn, "Bakgrunn", [], filter: VariabelgruppeFacet.StandaloneFacetOptOut)
            ]
        };

        Assert.False(Assert.Single(FilterHierarchy.StandaloneVariabelgrupper(facets)).Offered);
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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering))
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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, delkilde: Fodsel, datasamling: Registrering)),
                Gruppe(Levekaar, "Levekår", Under(Mfr, delkilde: Fodsel))
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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering)),
                Gruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Oppfolging), parent: Bakgrunn)
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
                Gruppe(Bakgrunn, "Bakgrunn",
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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: NotInThePayload))
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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering), count: 4),
                Gruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering), count: 5)
            ]
        };

        var kilde = Assert.Single(FilterHierarchy.Build(facets));
        var datasamling = Assert.Single(kilde.Children);

        Assert.Equal(9, kilde.Count);
        Assert.Equal(0, datasamling.Count);
        Assert.Equal([4, 5], datasamling.Children.Select(node => node.Count));
    }

    [Fact]
    public void Build_WhenTheAnswerCarriesNothing_ThenBothSurfacesAreEmpty()
    {
        Assert.Empty(FilterHierarchy.Build(Answer()));
        Assert.Empty(FilterHierarchy.StandaloneVariabelgrupper(Answer()));
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
            Variabelgrupper = [Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering))]
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
                Gruppe(Bakgrunn, "Bakgrunn", Under(Mfr, datasamling: Registrering), parent: Levekaar),
                Gruppe(Levekaar, "Levekår", Under(Mfr, datasamling: Registrering), parent: Bakgrunn)
            ]
        };

        var datasamling = Assert.Single(Assert.Single(FilterHierarchy.Build(facets)).Children);

        Assert.Equal([Bakgrunn, Levekaar], Flatten(datasamling.Children).Select(node => node.Id));
    }

    [Fact]
    public void Build_WhenTheAnswerIsAWholeCatalogue_ThenEveryPlacementIsDrawnExactlyOnce()
    {
        // The shape a real answer has — two kilder, delkilder, datasamlinger under both levels and
        // a group per datasamling — so a rule that only holds for a single branch fails here.
        var facets = Catalogue(kilder: 2, delkilderPerKilde: 2, datasamlingerPerDelkilde: 3, grupperPerDatasamling: 4);

        var tree = FilterHierarchy.Build(facets);
        var nodes = Flatten(tree).ToList();

        Assert.Equal(2, tree.Count);
        Assert.Equal(2, nodes.Count(node => node.Level == HierarchyLevel.Kilde));
        Assert.Equal(4, nodes.Count(node => node.Level == HierarchyLevel.Delkilde));
        Assert.Equal(12, nodes.Count(node => node.Level == HierarchyLevel.Datasamling));
        Assert.Equal(48, nodes.Count(node => node.Level == HierarchyLevel.Variabelgruppe));
        Assert.Equal(nodes.Count, nodes.Select(node => node.Path).Distinct().Count());
    }

    /// <summary>Every node of the tree, parents before what hangs under them.</summary>
    private static IEnumerable<HierarchyNode> Flatten(IEnumerable<HierarchyNode> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

    /// <summary>An answer carrying nothing, which every fixture here narrows to what it is about.</summary>
    private static FilterOptions Answer() => new();

    private static KildeFacet Kilde(Guid id, int count = 0) =>
        new() { Id = id, Name = $"Kilde {id:N}", ShortName = "", Count = count };

    private static DelkildeFacet Delkilde(Guid id, Guid kilde, Guid? parent = null, int count = 0) =>
        new() { Id = id, Name = $"Delkilde {id:N}", KildeId = kilde, ParentDelkildeId = parent, Count = count };

    private static DatasamlingFacet Datasamling(Guid id, Guid kilde, Guid? delkilde = null, int count = 0) =>
        new() { Id = id, Name = $"Datasamling {id:N}", KildeId = kilde, DelkildeId = delkilde, Count = count };

    private static VariabelgruppeFacet Gruppe(
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
        int kilder, int delkilderPerKilde, int datasamlingerPerDelkilde, int grupperPerDatasamling)
    {
        List<KildeFacet> kildeFacets = [];
        List<DelkildeFacet> delkildeFacets = [];
        List<DatasamlingFacet> datasamlingFacets = [];
        List<VariabelgruppeFacet> gruppeFacets = [];

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

                    for (var gruppe = 0; gruppe < grupperPerDatasamling; gruppe++)
                    {
                        var gruppeId = Id("c", (index * grupperPerDatasamling) + gruppe);

                        // Every third group opted out of the standalone facet, so a tree that read
                        // the opt-out would come up short of the count below rather than empty.
                        gruppeFacets.Add(Gruppe(gruppeId, $"Gruppe {gruppeId:N}",
                                                Under(kildeId, delkildeId, datasamlingId),
                                                filter: gruppe % 3 == 0
                                                    ? VariabelgruppeFacet.StandaloneFacetOptOut
                                                    : null));
                    }
                }
            }
        }

        return Answer() with
        {
            Kilder = kildeFacets,
            Delkilder = delkildeFacets,
            Datasamlinger = datasamlingFacets,
            HierarchyVariabelgrupper = gruppeFacets
        };
    }

    private static Guid Id(string prefix, int index) =>
        new($"{prefix}{prefix}{prefix}{prefix}{prefix}{prefix}{prefix}{prefix}-0000-0000-0000-{index:d12}");
}
