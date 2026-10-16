using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Where a kilde's description is cut under its name (Fhi.Metadata-nebok).</summary>
public class DescriptionLeadTest
{
    private static string Sentence(char letter, int length) => char.ToUpperInvariant(letter) + new string(letter, length - 2) + ".";

    [Fact]
    public void Split_WhenTheWholeTextFits_ThenNothingIsFolded()
    {
        var text = Sentence('a', 280) + "\n\n" + Sentence('b', 280);

        Assert.Equal((text, null), DescriptionLead.Split(text));
    }

    [Fact]
    public void Split_WhenParagraphsOverflow_ThenWholeParagraphsAreShownWhileTheyFit()
    {
        var (lead, folded) = DescriptionLead.Split(
            Sentence('a', 200) + "<br><br>" + Sentence('b', 300) + "\n\n" + Sentence('c', 100));

        Assert.Equal(Sentence('a', 200) + "\n\n" + Sentence('b', 300), lead);
        Assert.Equal(Sentence('c', 100), folded);
    }

    [Fact]
    public void Split_WhenTheFirstParagraphIsTooLong_ThenItIsCutAfterTheLastSentenceThatFits()
    {
        var (lead, folded) = DescriptionLead.Split(
            Sentence('a', 300) + " " + Sentence('b', 200) + " " + Sentence('c', 200) + "\n\nNeste.");

        Assert.Equal(Sentence('a', 300) + " " + Sentence('b', 200), lead);
        Assert.Equal(Sentence('c', 200) + "\n\nNeste.", folded);
    }

    [Fact]
    public void Split_WhenAFullStopIsNotASentenceEnd_ThenItIsNotCutThere()
    {
        // «1. januar» and «f.eks. en» are followed by a small letter, so neither ends a sentence.
        var text = "Fra 1. januar " + new string('a', 400) + " f.eks. en " + new string('b', 300) + ".";

        Assert.Equal((text, null), DescriptionLead.Split(text));
    }

    [Fact]
    public void Split_WhenALinkIsLong_ThenItCountsByItsLabel()
    {
        var text = Sentence('a', 500) + " [Lenke](https://example.org/" + new string('x', 200) + ").";

        Assert.Equal((text, null), DescriptionLead.Split(text));
    }

    [Theory]
    [InlineData("bl.a. Statistisk")]
    [InlineData("jf. Lov")]
    [InlineData("Dr. Hansen")]
    [InlineData("O. Hansen")]
    public void Split_WhenAnAbbreviationIsFollowedByACapital_ThenItIsNotASentenceEnd(string abbreviated)
    {
        var (lead, _) = DescriptionLead.Split(
            Sentence('a', 300) + " Mer tekst " + abbreviated + " " + new string('b', 300) + ". Siste.");

        Assert.Equal(Sentence('a', 300), lead);
    }

    [Fact]
    public void Split_WhenALinkLabelHoldsASentenceEnd_ThenTheCutIsNotInsideTheLink()
    {
        var link = "[Se Lov. Om helse](https://example.org)";
        var (lead, folded) = DescriptionLead.Split(Sentence('a', 300) + " B" + new string('b', 200) + " " + link + " " + new string('c', 200) + ". Slutt.");

        Assert.Equal(Sentence('a', 300), lead);
        Assert.Contains(link, folded, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_WhenTheFirstParagraphHasNoSentenceEnd_ThenNothingIsFolded()
    {
        var text = new string('a', 900);

        Assert.Equal((text, null), DescriptionLead.Split(text));
    }

    [Fact]
    public void Split_WhenTheTextIsPastTheParseCap_ThenItIsLeftWhole()
    {
        var text = string.Concat(Enumerable.Repeat(Sentence('a', 100) + "\n\n", 250));

        Assert.Equal((text, null), DescriptionLead.Split(text));
    }
}
