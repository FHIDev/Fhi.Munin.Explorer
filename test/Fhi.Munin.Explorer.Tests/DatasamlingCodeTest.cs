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
    public void DatasamlingByCode_WhenTheCodeIsUnderANestedDelkilde_ThenItIsFoundFolded(string linked) =>
        Assert.Equal(Deep, KildeExplorer.DatasamlingByCode(Kilde, linked)?.Id);

    [Fact]
    public void DatasamlingByCode_WhenNoRowCarriesTheCode_ThenNull() =>
        Assert.Null(KildeExplorer.DatasamlingByCode(Kilde, "K_KREG.ANNET"));

    [Theory]
    [InlineData("K_ALS.INKLUSJON", true)]
    [InlineData("K_ALS INKLUSJON", false)]
    [InlineData("", false)]
    public void IsDatasamlingLinkCode_WhenTheValueHasADot_ThenItIsACode(string value, bool code) =>
        Assert.Equal(code, KildeCodes.IsDatasamlingLinkCode(value));
}
