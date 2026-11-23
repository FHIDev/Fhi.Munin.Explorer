using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A link's datasamling code is matched as Munin's list import matches one (ExplorerService.ListImport.cs), so a shared
/// link and an imported row with the same code open the same datasamling (Fhi.Metadata-l9l2n.204).
/// </summary>
public class DatasamlingCodesTest
{
    private static readonly Guid Plain = Guid.NewGuid();
    private static readonly Guid Norwegian = Guid.NewGuid();
    private static readonly Guid Ascii = Guid.NewGuid();

    private static readonly DatasamlingReference[] Datasamlinger =
    [
        new() { Id = Plain, Code = "K_KREG.SIDKRG" },
        new() { Id = Norwegian, Code = "K_MFR.FØDSEL" },
        new() { Id = Ascii, Code = "K_NPR.AVDOD" },
        new() { Id = Guid.NewGuid(), Code = null },
        new() { Id = Guid.NewGuid(), Code = "" },
    ];

    [Theory]
    [InlineData("K_KREG.SIDKRG")]
    [InlineData("k_kreg.sidkrg")]
    [InlineData(" K_KREG.SIDKRG ")]
    public void Find_WhenTheCodeIsTheStoredOneInAnyCase_ThenItIsFound(string code) =>
        Assert.Equal(Plain, DatasamlingCodes.Find(Datasamlinger, code));

    [Theory]
    [InlineData("k_mfr.fødsel")]
    [InlineData("K_MFR.FOEDSEL")]
    public void Find_WhenTheCodeFoldsToTheStoredSpelling_ThenItIsFound(string code) =>
        Assert.Equal(Norwegian, DatasamlingCodes.Find(Datasamlinger, code));

    [Fact]
    public void Find_WhenTheLinksLetterFoldsOneLetterToTheStoredAsciiCode_ThenItIsFound() =>
        // The second tier: Ø read as O, as the import reads it.
        Assert.Equal(Ascii, DatasamlingCodes.Find(Datasamlinger, "K_NPR.AVDØD"));

    [Fact]
    public void Find_WhenOnlyTheStoredCodeHasTheNorwegianLetter_ThenItIsNotFound() =>
        // The import folds the row's letters, not the stored ones: «fodsel» never meant FØDSEL there either.
        Assert.Null(DatasamlingCodes.Find(Datasamlinger, "K_MFR.FODSEL"));

    [Fact]
    public void Find_WhenTwoDatasamlingerShareATier_ThenNeitherIsNamed()
    {
        DatasamlingReference[] twins = [new() { Id = Guid.NewGuid(), Code = "A.B" }, new() { Id = Guid.NewGuid(), Code = "a.b" }];

        Assert.Null(DatasamlingCodes.Find(twins, "A.B"));
    }

    [Fact]
    public void Find_WhenTheStoredSpellingAndAFoldBothMatch_ThenTheStoredSpellingWins()
    {
        var stored = Guid.NewGuid();
        DatasamlingReference[] both = [new() { Id = stored, Code = "X.ÆR" }, new() { Id = Guid.NewGuid(), Code = "X.ER" }];

        Assert.Equal(stored, DatasamlingCodes.Find(both, "X.ÆR"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K_KREG SIDKRG")]
    [InlineData("K_KREG.FINNES_IKKE")]
    public void Find_WhenTheCodeIsBlankMalformedOrUnknown_ThenNothingIsFound(string? code) =>
        Assert.Null(DatasamlingCodes.Find(Datasamlinger, code));

    [Fact]
    public void Find_WhenTheLinkLeavesOutK_ThenItIsNotFound() =>
        // Stricter than the kilde page on purpose: the list import takes no «K_» as optional, and a link and an imported
        // row with the same code must open the same datasamling.
        Assert.Null(DatasamlingCodes.Find(Datasamlinger, "KREG.SIDKRG"));

    [Fact]
    public void Find_WhenTheLinksÆFoldsToE_ThenTheStoredSpellingWithEIsFound()
    {
        var stored = Guid.NewGuid();

        Assert.Equal(stored, DatasamlingCodes.Find([new() { Id = stored, Code = "K_X.ER" }], "K_X.ÆR"));
    }

    [Theory]
    [InlineData("K_ÆÆÆÆÆÆ", "K_AEAEAEAEAEAE")]
    [InlineData("K_ÆÆÆÆÆÆ", "K_EEEEEE")]
    public void Find_WhenTheLinkHasMoreÆThanFoldIndependently_ThenOnlyTheAllAEAndAllESpellingsAreTried(string link, string stored)
    {
        var id = Guid.NewGuid();
        DatasamlingReference[] one = [new() { Id = id, Code = stored }];
        DatasamlingReference[] mixed = [new() { Id = id, Code = "K_AEEEEEE" }];

        Assert.Equal(id, DatasamlingCodes.Find(one, link));
        Assert.Null(DatasamlingCodes.Find(mixed, link));
    }

    [Fact]
    public void Find_WhenTheVariableListsOneDatasamlingTwice_ThenItIsNotATie()
    {
        DatasamlingReference[] twice = [new() { Id = Plain, Code = "K_KREG.SIDKRG" }, new() { Id = Plain, Code = "K_KREG.SIDKRG" }];

        Assert.Equal(Plain, DatasamlingCodes.Find(twice, "K_KREG.SIDKRG"));
    }

    [Fact]
    public void IsLinkCode_AtTheLengthBound_ThenOneHundredIsReadAndOneMoreIsNot()
    {
        Assert.True(DatasamlingCodes.IsLinkCode(new string('A', DatasamlingCodes.MaxLength)));
        Assert.False(DatasamlingCodes.IsLinkCode(new string('A', DatasamlingCodes.MaxLength + 1)));
    }
}
