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
    public void Split_WhenANestedLinkLabelHoldsASentenceEnd_ThenTheCutIsNotInsideIt()
    {
        var link = "[Se [1]. Om helse](https://example.org)";
        var (lead, folded) = DescriptionLead.Split(Sentence('a', 300) + " B" + new string('b', 200) + " " + link + " " + new string('c', 200) + ". Slutt.");

        Assert.Equal(Sentence('a', 300), lead);
        Assert.Contains(link, folded, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_WhenTheFirstParagraphIsOneSentenceTooLong_ThenTheOtherParagraphsAreFolded()
    {
        var first = new string('a', 700);
        var (lead, folded) = DescriptionLead.Split(first + "\n\nNeste avsnitt.");

        Assert.Equal(first, lead);
        Assert.Equal("Neste avsnitt.", folded);
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

    /// <summary>A tight list of items that each hold two sentences, the shape a sentence cut would split.</summary>
    private static string Items(int count) =>
        string.Join("\n", Enumerable.Range(1, count).Select(i => $"- {Sentence('p', 40)} {Sentence('q', 30)}"));

    [Fact]
    public void Split_WhenFormattedAndALongListFollowsItsLeadIn_ThenTheCutFallsBeforeTheListNotInsideIt()
    {
        var list = Items(12);

        // No blank line: unformatted, the lead-in and the list are one paragraph and the cut falls inside it.
        var (lead, folded) = DescriptionLead.Split("Liste:\n" + list + "\n\n" + Sentence('b', 50), formatted: true);

        Assert.Equal("Liste:", lead);
        Assert.StartsWith(list, folded, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_WhenFormattedAndTheFirstBlockIsALongList_ThenTheWholeListIsTheLead()
    {
        var list = Items(12);

        var (lead, folded) = DescriptionLead.Split(list + "\n\n" + Sentence('b', 50), formatted: true);

        Assert.Equal(list, lead);
        Assert.Equal(Sentence('b', 50), folded);
    }

    [Fact]
    public void Split_WhenNotFormatted_ThenAListIsCutAsBeforeAfterASentence()
    {
        // The flag-off path is unchanged: the same text is cut after a sentence inside an item.
        var (lead, folded) = DescriptionLead.Split(Items(12) + "\n\n" + Sentence('b', 50));

        Assert.EndsWith(Sentence('p', 40), lead, StringComparison.Ordinal);
        Assert.StartsWith(Sentence('q', 30), folded, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_WhenFormattedAndALongFirstParagraphComesBeforeAList_ThenThatParagraphIsCutAfterASentence()
    {
        var (lead, folded) = DescriptionLead.Split(
            Sentence('a', 300) + " " + Sentence('b', 200) + " " + Sentence('c', 200) + "\n\n" + Items(2), formatted: true);

        Assert.Equal(Sentence('a', 300) + " " + Sentence('b', 200), lead);
        Assert.Equal(Sentence('c', 200) + "\n\n" + Items(2), folded);
    }

    [Fact]
    public void Split_WhenFormattedAndASentenceEndsInsideBold_ThenTheCutIsNotThere()
    {
        // Unformatted, the cut falls after «Viktig.», leaving «**» on each side as text.
        var (lead, _) = DescriptionLead.Split(
            Sentence('a', 300) + " **Viktig. Les dette.** " + new string('b', 300) + ". Slutt.", formatted: true);

        Assert.EndsWith(new string('b', 300) + ".", lead, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_WhenFormattedAndADefinitionSitsBetweenTheCutBlocks_ThenItStaysInTheLead()
    {
        var (lead, folded) = DescriptionLead.Split(
            "Se [nettsiden][1].\n\n[1]: https://a.no\n\n" + Items(12), formatted: true);

        Assert.Equal("Se [nettsiden][1].\n\n[1]: https://a.no", lead);
        Assert.Equal(Items(12), folded);
    }
}
