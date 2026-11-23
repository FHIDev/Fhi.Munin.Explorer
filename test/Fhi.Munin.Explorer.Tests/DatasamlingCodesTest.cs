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
        new() { Id = Plain, Code = "KREG.SIDKRG" },
        new() { Id = Norwegian, Code = "MFR.FØDSEL" },
        new() { Id = Ascii, Code = "NPR.AVDOD" },
        new() { Id = Guid.NewGuid(), Code = null },
        new() { Id = Guid.NewGuid(), Code = "" },
    ];

    [Theory]
    [InlineData("KREG.SIDKRG")]
    [InlineData("kreg.sidkrg")]
    [InlineData(" KREG.SIDKRG ")]
    public void Find_WhenTheCodeIsTheStoredOneInAnyCase_ThenItIsFound(string code) =>
        Assert.Equal(Plain, DatasamlingCodes.Find(Datasamlinger, code));

    [Theory]
    [InlineData("mfr.fødsel")]
    [InlineData("MFR.FOEDSEL")]
    public void Find_WhenTheCodeFoldsToTheStoredSpelling_ThenItIsFound(string code) =>
        Assert.Equal(Norwegian, DatasamlingCodes.Find(Datasamlinger, code));

    [Fact]
    public void Find_WhenTheLinksLetterFoldsOneLetterToTheStoredAsciiCode_ThenItIsFound() =>
        // The second tier: Ø read as O, as the import reads it.
        Assert.Equal(Ascii, DatasamlingCodes.Find(Datasamlinger, "NPR.AVDØD"));

    [Fact]
    public void Find_WhenOnlyTheStoredCodeHasTheNorwegianLetter_ThenItIsNotFound() =>
        // The import folds the row's letters, not the stored ones: «fodsel» never meant FØDSEL there either.
        Assert.Null(DatasamlingCodes.Find(Datasamlinger, "MFR.FODSEL"));

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
    [InlineData("KREG SIDKRG")]
    [InlineData("KREG.FINNES_IKKE")]
    public void Find_WhenTheCodeIsBlankMalformedOrUnknown_ThenNothingIsFound(string? code) =>
        Assert.Null(DatasamlingCodes.Find(Datasamlinger, code));
}
