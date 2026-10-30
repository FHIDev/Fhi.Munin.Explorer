using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Share codes have 6 or 10 ASCII letters or digits, and nothing else reaches the API (Fhi.Metadata-ayqlp.7).</summary>
public class SharedListCodeTest
{
    [Theory]
    [InlineData("AB12CD", "AB12CD")]
    [InlineData(" ab12cd ", "AB12CD")]
    [InlineData("AB12CD34EF", "AB12CD34EF")]
    [InlineData("\tab12cd34ef\n", "AB12CD34EF")]
    public void NormalizeCode_WhenSixOrTenAsciiLettersOrDigits_ThenItIsTrimmedAndUpperCased(string code, string expected)
    {
        Assert.Equal(expected, SharedList.NormalizeCode(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("AB12CD34E")]
    [InlineData("AB12CD34EF5")]
    [InlineData("AB12CD34E-")]
    [InlineData("ÆØÅ123ABCD")]
    [InlineData("AB12 CD34E")]
    public void NormalizeCode_WhenAnyOtherLengthOrCharacter_ThenItIsNull(string? code)
    {
        Assert.Null(SharedList.NormalizeCode(code));
    }
}
