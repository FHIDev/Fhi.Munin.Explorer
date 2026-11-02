using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The kilde code a link carries (Fhi.Metadata-9huq2.12): written short, read in any form the API takes.</summary>
public class KildeCodesTest
{
    [Theory]
    [InlineData("K_DAR", "dar")]
    [InlineData("k_dar", "dar")]
    [InlineData("K_40ARINGSUNDERSOKELSENE", "40aringsundersokelsene")]
    [InlineData("MFR", "mfr")]
    [InlineData(" K_MFR ", "mfr")]
    [InlineData("K_", "k_")]
    public void ShortForm_DropsThePrefixAndLowerCases(string code, string expected) =>
        Assert.Equal(expected, KildeCodes.ShortForm(code));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ShortForm_OfNoCode_IsNull(string? code) => Assert.Null(KildeCodes.ShortForm(code));

    [Theory]
    [InlineData("dar", true)]
    [InlineData("dår", true)]
    [InlineData("K_DAR", true)]
    [InlineData("k-dar", true)]
    [InlineData("d ar", false)]
    [InlineData("dar/x", false)]
    [InlineData("", false)]
    public void IsLinkCode_TakesWhatAKildeCodeCanBe(string value, bool expected) =>
        Assert.Equal(expected, KildeCodes.IsLinkCode(value));

    [Fact]
    public void IsLinkCode_RefusesACodeLongerThanAnyKildeCode() =>
        Assert.False(KildeCodes.IsLinkCode(new string('A', KildeCodes.MaxLength + 1)));
}
