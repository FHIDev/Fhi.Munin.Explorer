using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

// A datasamling link's code is matched as Munin keeps codes unique: folded ÆØÅ, any case, at any delkilde depth.
public class DatasamlingCodeTest
{
    private static readonly Guid Deep = Guid.NewGuid();

    private static readonly KildeDetail Kilde = new()
    {
        Datasamlinger = [new() { Id = Guid.NewGuid(), Code = "K_KREG.HOVED" }, new() { Id = Guid.NewGuid(), Code = null }],
        Delkilder =
        [
            new()
            {
                Children = [new() { Datasamlinger = [new() { Id = Deep, Code = "K_KREG.SVULST_ØRE" }] }],
            },
        ],
    };

    [Theory]
    [InlineData("K_KREG.SVULST_ØRE")]
    [InlineData("k_kreg.svulst_øre")]
    [InlineData("K_KREG.SVULST_OERE")]
    [InlineData("KREG.SVULST_ØRE")]
    [InlineData("kreg.svulst_oere")]
    public void DatasamlingByCode_WhenTheCodeIsUnderANestedDelkilde_ThenItIsFoundFolded(string linked) =>
        Assert.Equal(Deep, KildeExplorer.DatasamlingByCode(Kilde, linked)?.Id);

    // «K_» is taken off the start of the whole code only (Robin, 2026-11-11): a second one is part of the code.
    [Theory]
    [InlineData("K_KREG.K_SVULST_ØRE")]
    [InlineData("KREG.K_SVULST_ØRE")]
    public void DatasamlingByCode_WhenThePrefixIsRepeatedInsideTheCode_ThenNull(string linked) =>
        Assert.Null(KildeExplorer.DatasamlingByCode(Kilde, linked));

    [Fact]
    public void DatasamlingByCode_WhenNoRowCarriesTheCode_ThenNull() =>
        Assert.Null(KildeExplorer.DatasamlingByCode(Kilde, "K_KREG.ANNET"));

    [Theory]
    [InlineData("K_ALS.INKLUSJON", true)]
    [InlineData("K_ALS INKLUSJON", false)]
    [InlineData("", false)]
    [InlineData("K_ALS.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    public void IsDatasamlingLinkCode_WhenTheValueIsChecked_ThenLettersDigitsUnderscoreHyphenAndDotUpToTheLimitPass(string value, bool code) =>
        Assert.Equal(code, KildeCodes.IsDatasamlingLinkCode(value));
}
