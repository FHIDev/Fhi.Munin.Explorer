using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Which short form the address may carry for a kilde: only one the API has resolved to it (Fhi.Metadata-9huq2.12).
/// </summary>
public class KildeCodeBookTest
{
    private static readonly Guid Dar = Guid.NewGuid();

    [Fact]
    public void ALearnedCode_IsOfferedForConfirmation_AndWrittenOnlyOnceConfirmed()
    {
        var book = new KildeCodeBook();
        book.Learn(Dar, "K_DAR");

        Assert.Null(book.ShortFormFor(Dar));
        Assert.Equal("dar", book.NextToVerify(Dar));
        Assert.Null(book.NextToVerify(Dar));

        book.Verify(Dar, "dar");
        Assert.Equal("dar", book.ShortFormFor(Dar));
    }

    [Theory]
    [InlineData("K_DAR.SUB")]
    [InlineData("K_D AR")]
    public void ACodeALinkCannotCarry_IsNeverOffered(string code)
    {
        var book = new KildeCodeBook();
        book.Learn(Dar, code);

        Assert.Null(book.NextToVerify(Dar));
        Assert.Null(book.ShortFormFor(Dar));
    }

    [Fact]
    public void TheCodeALinkWasFollowedBy_CountsAsConfirmed_UntilTheShortFormIs()
    {
        var book = new KildeCodeBook();
        book.Verify(Dar, "dår");
        book.Learn(Dar, "K_DAR");

        Assert.Equal("dår", book.ShortFormFor(Dar));
        Assert.Equal("dar", book.NextToVerify(Dar));
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
