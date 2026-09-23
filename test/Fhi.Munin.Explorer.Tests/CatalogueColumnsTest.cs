using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The three rules the merge carries, asked of it directly rather than through a rendered page.
/// </summary>
/// <remarks>
/// Each of them is a behaviour a later simplification would remove without breaking a compile: a
/// plain indexer assignment in place of the guard, a dropped IsNullOrWhiteSpace, a null check read
/// as redundant against a non-nullable contract. Two of the three would put the empty section this
/// bead exists to fix back on the page from the other direction — a blank key present in the bag
/// keeps its group alive and draws a labelled row with nothing in it (Fhi.Metadata-bct95).
/// <para>
/// <see cref="SeededPlacementRenderingTest"/> counts what reaches a page; this says why.
/// </para>
/// </remarks>
public class CatalogueColumnsTest
{
    private const string Curated = "Det kuraterte svaret.";
    private const string Column = "Kolonneverdien.";

    // The bag is declared non-nullable and still arrives null, which is one of the three rules
    // under test — so the null goes in through the same forgiving assignment a host's client would.
    private static VariableDetail Variable(Dictionary<string, string?>? bag, string description) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = "V_ALS.F1.TALE",
            PreferredTerm = "1. Tale",
            Description = description,
            AdditionalProperties = bag!,
        };

    [Fact]
    public void Merge_WhenTheBagAlreadyHoldsTheKey_ThenTheCuratedValueStandsAndTheColumnIsDropped()
    {
        // What the payload curated is the payload's own answer, and a column written over it would
        // change a row that renders correctly today.
        var values = CatalogueColumns.Values(
            Variable(new Dictionary<string, string?> { [CatalogueColumns.Description] = Curated }, Column));

        Assert.Equal(Curated, values[CatalogueColumns.Description]);
    }

    [Fact]
    public void Merge_WhenTheBagHoldsTheKeyBlank_ThenTheColumnFillsItRatherThanLeavingTheBlank()
    {
        // A key present and empty is the shape that draws a labelled row with nothing in it, so the
        // bag only wins where it has something to say.
        var values = CatalogueColumns.Values(
            Variable(new Dictionary<string, string?> { [CatalogueColumns.Description] = "   " }, Column));

        Assert.Equal(Column, values[CatalogueColumns.Description]);
    }

    [Fact]
    public void Merge_WhenTheColumnIsBlank_ThenNoKeyIsAddedAtAll()
    {
        // Absent rather than present-and-empty: absent is what lets Rows keep skipping the key and
        // its group keep collapsing.
        var values = CatalogueColumns.Values(Variable([], "   "));

        Assert.False(values.ContainsKey(CatalogueColumns.Description));
    }

    [Fact]
    public void Merge_WhenTheBagIsNull_ThenTheColumnsStillMergeRatherThanThrowing()
    {
        // AdditionalProperties is declared non-nullable and System.Text.Json writes an explicit JSON
        // null straight over it, so a host substituting its own client can hand this one.
        var values = CatalogueColumns.Values(Variable(bag: null, Column));

        Assert.Equal(Column, values[CatalogueColumns.Description]);
    }

    [Fact]
    public void Merge_Always_ThenTheBagsOwnKeysSurviveBesideTheMergedColumns()
    {
        // The merge adds to the bag rather than replacing it: the curated keys are most of what the
        // sections draw, and a bag rebuilt from the columns alone would empty every one of them.
        var values = CatalogueColumns.Values(
            Variable(new Dictionary<string, string?> { ["Formaal"] = Curated }, Column));

        Assert.Equal((Curated, Column), (values["Formaal"], values[CatalogueColumns.Description]));
    }

    [Fact]
    public void Merge_WhenADateColumnIsSet_ThenItIsMergedAsTheDayTheReaderReadsItAndNotAsAnInstant()
    {
        // The column holds an instant and the section renders whatever the bag says verbatim, so the
        // day is written here — in the language the fact box one line away writes the same field in.
        KildeDetail kilde = new()
        {
            Id = Guid.NewGuid(),
            Code = "K_ALS",
            PreferredTerm = "Als registeret",
            ValidFrom = new DateTimeOffset(2023, 2, 3, 0, 0, 0, TimeSpan.Zero),
        };

        Assert.Equal(("3. februar 2023", "3 February 2023"),
                     (CatalogueColumns.Values(kilde, "no")[CatalogueColumns.ValidFrom],
                      CatalogueColumns.Values(kilde, "en")[CatalogueColumns.ValidFrom]));
    }

    [Fact]
    public void Merge_WhenADateColumnIsUnset_ThenNoKeyIsAddedForIt()
    {
        // The same absence rule as any other empty column, reached through the date formatter, which
        // answers null for both of the ways the payload carries no date.
        KildeDetail kilde = new()
        {
            Id = Guid.NewGuid(),
            Code = "K_ALS",
            PreferredTerm = "Als registeret",
        };

        Assert.False(CatalogueColumns.Values(kilde, "no").ContainsKey(CatalogueColumns.ValidFrom));
    }

    // The four identity columns a kilde carries with no group and no bag entry: a section the
    // catalogue places them in came out empty until they were merged (Fhi.Metadata-zg89n).
    private static KildeDetail Kilde(Dictionary<string, string?>? bag = null, string? shortName = "ALS") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = "K_ALS",
            ShortName = shortName,
            PreferredTerm = "Als registeret",
            Kildetype = "helseregister",
            AdditionalProperties = bag!,
        };

    private static DatasamlingDetail Datasamling(Dictionary<string, string?>? bag = null, string? shortName = "INKL") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = "K_ALS.INKLUSJON",
            ShortName = shortName,
            PreferredTerm = "Inklusjon",
            AdditionalProperties = bag!,
        };

    [Fact]
    public void Values_WhenAKildeCarriesItsIdentityColumns_ThenTheRawKildetypeCodeShortNameAndNameAreMerged()
    {
        // The raw token and not this package's label for it: the catalogue curates its own words.
        var values = CatalogueColumns.Values(Kilde(), "no");

        Assert.Equal(("helseregister", "K_ALS", "ALS", "Als registeret"),
                     (values[CatalogueColumns.Kildetype], values[CatalogueColumns.Code],
                      values[CatalogueColumns.ShortName], values[CatalogueColumns.PreferredTerm]));
    }

    [Fact]
    public void Values_WhenAKildeBagHoldsKortNavn_ThenTheCuratedValueStands()
    {
        var values = CatalogueColumns.Values(
            Kilde(new Dictionary<string, string?> { [CatalogueColumns.ShortName] = Curated }), "no");

        Assert.Equal(Curated, values[CatalogueColumns.ShortName]);
    }

    [Fact]
    public void Values_WhenAKildeHasNoShortName_ThenNoKortNavnKeyIsAdded()
    {
        Assert.False(CatalogueColumns.Values(Kilde(shortName: null), "no").ContainsKey(CatalogueColumns.ShortName));
        Assert.False(CatalogueColumns.Values(Kilde(shortName: "  "), "no").ContainsKey(CatalogueColumns.ShortName));
    }

    [Fact]
    public void Values_WhenADatasamlingCarriesItsIdentityColumns_ThenCodeShortNameAndNameAreMerged()
    {
        var values = CatalogueColumns.Values(Datasamling(), "no");

        Assert.Equal(("K_ALS.INKLUSJON", "INKL", "Inklusjon"),
                     (values[CatalogueColumns.Code], values[CatalogueColumns.ShortName],
                      values[CatalogueColumns.PreferredTerm]));
    }

    [Fact]
    public void Values_WhenADatasamlingBagHoldsPreferredTerm_ThenTheCuratedValueStands()
    {
        var values = CatalogueColumns.Values(
            Datasamling(new Dictionary<string, string?> { [CatalogueColumns.PreferredTerm] = Curated }), "no");

        Assert.Equal(Curated, values[CatalogueColumns.PreferredTerm]);
    }

    [Fact]
    public void Values_WhenADatasamlingHasNoShortName_ThenNoKortNavnKeyIsAdded()
    {
        Assert.False(CatalogueColumns.Values(Datasamling(shortName: null), "no").ContainsKey(CatalogueColumns.ShortName));
        Assert.False(CatalogueColumns.Values(Datasamling(shortName: " "), "no").ContainsKey(CatalogueColumns.ShortName));
    }

    [Fact]
    public void Values_WhenAVariableHasAName_ThenPreferredTermIsMerged()
    {
        Assert.Equal("1. Tale", CatalogueColumns.Values(Variable([], Column))[CatalogueColumns.PreferredTerm]);
    }

    [Fact]
    public void Values_WhenAVariableBagHoldsPreferredTerm_ThenTheCuratedValueStands()
    {
        var values = CatalogueColumns.Values(
            Variable(new Dictionary<string, string?> { [CatalogueColumns.PreferredTerm] = Curated }, Column));

        Assert.Equal(Curated, values[CatalogueColumns.PreferredTerm]);
    }

    [Fact]
    public void Values_WhenAVariableNameIsBlank_ThenNoPreferredTermKeyIsAdded()
    {
        var variable = Variable([], Column) with { PreferredTerm = "  " };

        Assert.False(CatalogueColumns.Values(variable).ContainsKey(CatalogueColumns.PreferredTerm));
    }
}
