using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public sealed class DescriptionLeadHeldOutTest
{
    [Fact]
    public void Split_WhenLongDescriptionHoldsAList_ThenEveryListItemStaysWholeOnOneSide()
    {
        var items = new[]
        {
            "Registeret dekker alle fødsler i Norge siden 1967 og følger opp mor og barn. Dataene hentes fra meldinger fra fødeinstitusjonene hvert år.",
            "Dødsårsaksregisteret samler dødsmeldinger fra leger over hele landet hver måned. Opplysningene kodes av fagpersoner før de lagres i registeret.",
            "Reseptregisteret får månedlige leveranser fra alle apotek i landet og fra legemiddelgrossister. Tallene brukes til å følge forbruket av legemidler over tid.",
            "Hjerte- og karregisteret bygger på meldinger fra sykehusene om innleggelser og prosedyrer. Registeret oppdateres årlig med nye diagnoser og behandlinger.",
        };
        var text = "Kilden samler flere registre som beskrives under.\n" + string.Join("\n", items.Select(item => "- " + item));

        var (lead, folded) = DescriptionLead.Split(text, formatted: true);

        Assert.NotNull(folded);
        Assert.All(items, item => Assert.True(
            lead.Contains(item, StringComparison.Ordinal) || folded.Contains(item, StringComparison.Ordinal),
            $"List item was cut in two between the lead and the folded part: «{item}»{Environment.NewLine}Lead: «{lead}»{Environment.NewLine}Folded: «{folded}»"));
    }
}
