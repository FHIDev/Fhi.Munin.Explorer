using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A datasamling's Frekvens as a word: Munin sends its enum member's camelCase name, and a reader
/// of a Norwegian page should never see "kvartalsvis" or "yearly" (Fhi.Metadata-l9l2n.121).
/// </summary>
public class FrequencyLabelTest
{
    // Munin's merged Frekvens enum, serialised camelCase, then the English names stored before it,
    // mapped the way Munin's FrekvensValueConverter reads them.
    [Theory]
    [InlineData("sjeldnereEnnArlig", "Sjeldnere enn årlig", "Less often than yearly")]
    [InlineData("arlig", "Årlig", "Yearly")]
    [InlineData("halvarlig", "Halvårlig", "Half-yearly")]
    [InlineData("tertialvis", "Tertialvis", "Every four months")]
    [InlineData("kvartalsvis", "Kvartalsvis", "Quarterly")]
    [InlineData("manedlig", "Månedlig", "Monthly")]
    [InlineData("ukentlig", "Ukentlig", "Weekly")]
    [InlineData("daglig", "Daglig", "Daily")]
    [InlineData("hyppigereEnnDaglig", "Hyppigere enn daglig", "More often than daily")]
    [InlineData("daily", "Daglig", "Daily")]
    [InlineData("weekly", "Ukentlig", "Weekly")]
    [InlineData("monthly", "Månedlig", "Monthly")]
    [InlineData("quarterly", "Kvartalsvis", "Quarterly")]
    [InlineData("yearly", "Årlig", "Yearly")]
    [InlineData("continuous", "Hyppigere enn daglig", "More often than daily")]
    public void FrequencyLabel_WhenTheValueIsKnown_ThenItIsTheReadersWord(string wire, string norwegian, string english)
    {
        Assert.Equal(norwegian, Texts.For("nb").FrequencyLabel(wire));
        Assert.Equal(english, Texts.For("en").FrequencyLabel(wire));
    }

    [Theory]
    [InlineData("Kvartalsvis")]
    [InlineData("SjeldnereEnnArlig")]
    [InlineData("Continuous")]
    public void FrequencyLabel_WhenTheCaseDiffers_ThenItIsStillRecognised(string wire)
    {
        // Compared with the lower-cased wire name, since "Kvartalsvis" is already its own nb label.
        Assert.Equal(Texts.For("en").FrequencyLabel(wire.ToLowerInvariant()), Texts.For("en").FrequencyLabel(wire));
        Assert.NotEqual(wire, Texts.For("en").FrequencyLabel(wire));
    }

    [Theory]
    [InlineData("nb")]
    [InlineData("en")]
    public void FrequencyLabel_WhenTheValueIsUnknown_ThenItComesBackAsItArrived(string language)
    {
        // Shown rather than hidden, the StatisticsTypeLabel rule: an unfamiliar word is honest, and a
        // blank would claim the catalogue holds nothing.
        Assert.Equal("Fortløpende", Texts.For(language).FrequencyLabel("Fortløpende"));
    }
}
