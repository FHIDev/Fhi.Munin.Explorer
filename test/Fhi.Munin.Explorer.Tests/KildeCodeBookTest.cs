using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Which short form the address may carry for a kilde: only one that reads back to it (Fhi.Metadata-9huq2.12).</summary>
public class KildeCodeBookTest
{
    private static readonly Guid Dar = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    [Fact]
    public void ShortFormFor_AKnownCode_IsTheShortForm()
    {
        var book = new KildeCodeBook();
        book.Learn(Dar, "K_DAR");

        Assert.Equal("dar", book.ShortFormFor(Dar));
        Assert.Null(book.ShortFormFor(Other));
    }

    [Fact]
    public void ShortFormFor_TwoKilderSharingAShortForm_IsNullForBoth()
    {
        // K_X and X both shorten to «x», which the API refuses as ambiguous: the address keeps their ids.
        var book = new KildeCodeBook();
        book.Learn(Dar, "K_X");
        book.Learn(Other, "X");

        Assert.Null(book.ShortFormFor(Dar));
        Assert.Null(book.ShortFormFor(Other));
    }

    [Theory]
    [InlineData("K_DAR.SUB")]
    [InlineData("K_D AR")]
    public void ShortFormFor_ACodeALinkCannotCarry_IsNull(string code)
    {
        var book = new KildeCodeBook();
        book.Learn(Dar, code);

        Assert.Null(book.ShortFormFor(Dar));
    }

    [Fact]
    public void Learn_RaisesOnlyWhenTheCodeIsNewOrChanged()
    {
        var book = new KildeCodeBook();
        var raised = 0;
        book.Learned += _ => raised++;

        book.Learn(Dar, "K_DAR");
        book.Learn(Dar, "K_DAR");
        book.Learn(Dar, "");
        book.Learn(Guid.Empty, "K_X");

        Assert.Equal(1, raised);
    }
}
