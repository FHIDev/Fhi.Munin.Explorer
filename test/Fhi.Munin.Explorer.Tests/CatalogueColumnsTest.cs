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

    [Fact]
    public void Values_WhenAKildeHasANumericIdentificationValue_ThenTheDatasamlingRepairDoesNotApply()
    {
        var kilde = new KildeDetail
        {
            Id = Guid.NewGuid(),
            Code = "K_TEST",
            PreferredTerm = "Testkilde",
            PersonIdentificationLevel = "deIdentified",
            AdditionalProperties = new Dictionary<string, string?> { [CatalogueColumns.PersonIdentification] = "2" },
            PropertyMetadata = [new()
            {
                Key = CatalogueColumns.PersonIdentification,
                Type = "SingleSelect",
                OptionsJson = """[{"value":"deIdentified","label":"Avidentifiserte data"}]""",
            }],
        };

        Assert.Equal("2", CatalogueColumns.Values(kilde, "nb")[CatalogueColumns.PersonIdentification]);
    }

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
}
