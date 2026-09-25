using System.Text.Json;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one sibling ordering every mixed delkilde/datasamling list is meant to share, and the
/// <c>displayOrder</c> field it reads on all three payloads that carry the two kinds.
/// </summary>
/// <remarks>
/// Ordering each kind by its own rank and concatenating looks right on a kilde holding one kind
/// only, and puts K_KK's datasamlinger before its waves (Fhi.Metadata-oty1h).
/// </remarks>
public class SiblingOrderTest
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");

    private static IReadOnlyList<Sibling> Merge(IEnumerable<Sibling> delkilder, IEnumerable<Sibling> datasamlinger) =>
        SiblingOrder.Merge(delkilder, datasamlinger, s => s.DisplayOrder, s => s.Name, s => s.Id);

    private static string[] Names(IEnumerable<Sibling> siblings) => [.. siblings.Select(s => s.Name)];

    // ------------------------------------------------------------------------------- the helper

    [Fact]
    public void Merge_WhenRanksInterleaveTheKinds_ThenADatasamlingSitsBetweenTwoDelkilder()
    {
        var merged = Merge(
            [new(true, "Delkilde 1", A, 1), new(true, "Delkilde 2", B, 3)],
            [new(false, "Datasamling", C, 2)]);

        Assert.Equal(["Delkilde 1", "Datasamling", "Delkilde 2"], Names(merged));
    }

    [Fact]
    public void Merge_WhenNoSiblingHasARank_ThenPayloadOrderIsKeptRatherThanSortedByName()
    {
        // A server predating displayOrder already sends the imported order; re-sorting by name here
        // would put K_KK's alphabetic datasamlinger ahead of the waves.
        var merged = Merge(
            [new(true, "Wave 2", A, null), new(true, "Wave 1", B, null)],
            [new(false, "Death", C, null), new(false, "Cancer", Guid.Empty, null)]);

        Assert.Equal(["Wave 2", "Wave 1", "Death", "Cancer"], Names(merged));
    }

    [Fact]
    public void Merge_WhenSomeSiblingsLackARank_ThenTheyFollowTheRankedOnesInPayloadOrder()
    {
        var merged = Merge(
            [new(true, "Unranked delkilde", A, null), new(true, "Second", B, 2)],
            [new(false, "Unranked datasamling", C, null), new(false, "First", Guid.Empty, 1)]);

        Assert.Equal(["First", "Second", "Unranked delkilde", "Unranked datasamling"], Names(merged));
    }

    [Fact]
    public void Merge_WhenRanksAreEqual_ThenNameOrdinalDecidesWhateverThePayloadOrder()
    {
        // Ordinal, so "Zeta" sorts before "alpha" — the same on every culture a host runs under.
        var merged = Merge(
            [new(true, "alpha", A, 1)],
            [new(false, "Zeta", B, 1)]);

        Assert.Equal(["Zeta", "alpha"], Names(merged));
    }

    [Fact]
    public void Merge_WhenRanksAndNamesAreEqual_ThenIdDecides()
    {
        var merged = Merge(
            [new(true, "Same", C, 4)],
            [new(false, "Same", A, 4), new(false, "Same", B, 4)]);

        Assert.Equal([A, B, C], merged.Select(s => s.Id));
    }

    [Fact]
    public void Merge_WhenBothInputsAreEmpty_ThenTheResultIsEmpty() =>
        Assert.Empty(Merge([], []));

    [Theory]
    [InlineData("delkilder")]
    [InlineData("datasamlinger")]
    [InlineData("displayOrder")]
    [InlineData("name")]
    [InlineData("id")]
    public void Merge_WhenAnArgumentIsNull_ThenItThrowsNamingThatArgument(string argument)
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => SiblingOrder.Merge<Sibling>(
            argument == "delkilder" ? null! : [],
            argument == "datasamlinger" ? null! : [],
            argument == "displayOrder" ? null! : s => s.DisplayOrder,
            argument == "name" ? null! : s => s.Name,
            argument == "id" ? null! : s => s.Id));

        Assert.Equal(argument, thrown.ParamName);
    }

    // ------------------------------------------------------------ the resolver's agreed outputs

    [Theory]
    [InlineData(nameof(SiblingOrderFixtures.KildeKK))]
    [InlineData(nameof(SiblingOrderFixtures.ManualCancerFirst))]
    [InlineData(nameof(SiblingOrderFixtures.NewChildAfterManualPrefix))]
    [InlineData(nameof(SiblingOrderFixtures.ResetToSource))]
    public void Merge_WhenGivenTheResolversOutput_ThenSiblingsAreInTheResolvedOrder(string fixture)
    {
        var scope = SiblingOrderFixtures.Named(fixture);

        Assert.Equal(scope.Expected, Names(Merge(scope.Delkilder, scope.Datasamlinger)));
    }

    [Fact]
    public void Merge_WhenReadFromAHierarchyPayload_ThenKildeKKIsInWorkbookOrder()
    {
        var hierarchy = Read<KildeHierarchy>(Hierarchy(ranked: true));

        var merged = SiblingOrder.Merge(
            hierarchy.Delkilder.Select(d => new Sibling(true, d.Name, d.Id, d.DisplayOrder)),
            hierarchy.DirectDatasamlinger.Select(d => new Sibling(false, d.Name, d.Id, d.DisplayOrder)),
            s => s.DisplayOrder, s => s.Name, s => s.Id);

        Assert.Equal(SiblingOrderFixtures.KildeKK().Expected, Names(merged));
    }

    // -------------------------------------------------------------------- the three payloads

    [Fact]
    public void Filters_WhenDisplayOrderIsSent_ThenBothFacetKindsCarryIt()
    {
        var filters = Read<FilterOptions>(Filters(ranked: true));

        Assert.Equal(2, filters.Delkilder.Single().DisplayOrder);
        Assert.Equal(62, filters.Datasamlinger.Single().DisplayOrder);
    }

    [Fact]
    public void Filters_WhenAnOlderServerSendsNoDisplayOrder_ThenBothFacetKindsReadNull()
    {
        var filters = Read<FilterOptions>(Filters(ranked: false));

        Assert.Null(filters.Delkilder.Single().DisplayOrder);
        Assert.Null(filters.Datasamlinger.Single().DisplayOrder);
    }

    [Fact]
    public void Hierarchy_WhenDisplayOrderIsSent_ThenEveryDelkildeAndDatasamlingCarriesIt()
    {
        var hierarchy = Read<KildeHierarchy>(Hierarchy(ranked: true));

        Assert.Equal(new int?[] { 2, 36, 49, 57 }, hierarchy.Delkilder.Select(d => d.DisplayOrder));
        Assert.Equal(new int?[] { 60, 62, 61 }, hierarchy.DirectDatasamlinger.Select(d => d.DisplayOrder));
        Assert.Equal(1, hierarchy.Delkilder[0].Datasamlinger.Single().DisplayOrder);
    }

    [Fact]
    public void Hierarchy_WhenAnOlderServerSendsNoDisplayOrder_ThenEveryNodeReadsNull()
    {
        var hierarchy = Read<KildeHierarchy>(Hierarchy(ranked: false));

        Assert.All(hierarchy.Delkilder, d => Assert.Null(d.DisplayOrder));
        Assert.All(hierarchy.DirectDatasamlinger, d => Assert.Null(d.DisplayOrder));
        Assert.Null(hierarchy.Delkilder[0].Datasamlinger.Single().DisplayOrder);
    }

    [Fact]
    public void Detail_WhenDisplayOrderIsSent_ThenDelkilderAndDatasamlingerCarryIt()
    {
        var kilde = Read<KildeDetail>(Detail(ranked: true));

        Assert.Equal(2, kilde.Delkilder.Single().DisplayOrder);
        Assert.Equal(1, kilde.Delkilder.Single().Datasamlinger.Single().DisplayOrder);
        Assert.Equal(62, kilde.Datasamlinger.Single().DisplayOrder);
    }

    [Fact]
    public void Detail_WhenAnOlderServerSendsNoDisplayOrder_ThenDelkilderAndDatasamlingerReadNull()
    {
        var kilde = Read<KildeDetail>(Detail(ranked: false));

        Assert.Null(kilde.Delkilder.Single().DisplayOrder);
        Assert.Null(kilde.Delkilder.Single().Datasamlinger.Single().DisplayOrder);
        Assert.Null(kilde.Datasamlinger.Single().DisplayOrder);
    }

    [Fact]
    public void DisplayOrder_WhenSerialised_ThenItKeepsTheApisWireName()
    {
        var json = JsonSerializer.Serialize(new HierarchyDatasamling { DisplayOrder = 7 }, MuninExplorerClient.Json);

        Assert.Equal(7, JsonDocument.Parse(json).RootElement.GetProperty("displayOrder").GetInt32());
    }

    // ------------------------------------------------------------------------------ payloads

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, MuninExplorerClient.Json)
        ?? throw new InvalidOperationException($"The payload did not read as a {typeof(T).Name}.");

    private static string Rank(bool ranked, int rank) => ranked ? $", \"displayOrder\": {rank}" : "";

    private static string Filters(bool ranked) =>
        $$"""
          {
            "delkilder": [
              { "id": "{{SiblingOrderFixtures.Wave1}}", "name": "Wave 1", "parentDelkildeId": null,
                "kildeId": "4bbb0000-0000-0000-0000-000000000000", "count": 3{{Rank(ranked, 2)}} }
            ],
            "datasamlinger": [
              { "id": "{{SiblingOrderFixtures.Cancer}}", "name": "Cancer", "delkildeId": null,
                "kildeId": "4bbb0000-0000-0000-0000-000000000000", "count": 5, "categories": []{{Rank(ranked, 62)}} }
            ]
          }
          """;

    private static string Hierarchy(bool ranked) =>
        $$"""
          {
            "kildeId": "4bbb0000-0000-0000-0000-000000000000",
            "kildeName": "K_KK",
            "delkilder": [
              { "id": "{{SiblingOrderFixtures.Wave1}}", "name": "Wave 1", "variableCount": 1{{Rank(ranked, 2)}},
                "datasamlinger": [
                  { "id": "4bd50000-0000-0000-0000-0000000000ff", "name": "Questionnaire", "variableCount": 1{{Rank(ranked, 1)}} }
                ] },
              { "id": "{{SiblingOrderFixtures.Wave2}}", "name": "Wave 2", "variableCount": 1{{Rank(ranked, 36)}} },
              { "id": "{{SiblingOrderFixtures.Wave3}}", "name": "Wave 3", "variableCount": 1{{Rank(ranked, 49)}} },
              { "id": "{{SiblingOrderFixtures.Wave4}}", "name": "Wave 4", "variableCount": 1{{Rank(ranked, 57)}} }
            ],
            "directDatasamlinger": [
              { "id": "{{SiblingOrderFixtures.AllWaves}}", "name": "All waves - derived variables", "variableCount": 1{{Rank(ranked, 60)}} },
              { "id": "{{SiblingOrderFixtures.Cancer}}", "name": "Cancer", "variableCount": 1{{Rank(ranked, 62)}} },
              { "id": "{{SiblingOrderFixtures.Death}}", "name": "Death", "variableCount": 1{{Rank(ranked, 61)}} }
            ]
          }
          """;

    private static string Detail(bool ranked) =>
        $$"""
          {
            "id": "4bbb0000-0000-0000-0000-000000000000",
            "name": "K_KK",
            "delkilder": [
              { "id": "{{SiblingOrderFixtures.Wave1}}", "code": "K_KK.W1", "name": "Wave 1"{{Rank(ranked, 2)}},
                "datasamlinger": [
                  { "id": "4bd50000-0000-0000-0000-0000000000ff", "name": "Questionnaire"{{Rank(ranked, 1)}} }
                ] }
            ],
            "datasamlinger": [
              { "id": "{{SiblingOrderFixtures.Cancer}}", "name": "Cancer"{{Rank(ranked, 62)}} }
            ]
          }
          """;
}
