using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The chosen codes as a summary (Fhi.Metadata-emsc0.3.7): one line per code list, text only.</summary>
public class DesiredCodeSummaryTest : ExplorerTestContext
{
    private static string Render(BunitContext context, IReadOnlyList<DesiredCodeView> codes, string language = "no")
    {
        var cut = context.Render(DesiredCodeSummary.For(Texts.For(language), codes, "no"));
        return System.Text.RegularExpressions.Regex.Replace(cut.Markup.Length == 0 ? "" : cut.Find("div").TextContent, @"\s+", " ").Trim();
    }

    [Fact]
    public void For_WhenCodesComeFromTwoLists_ThenEachListIsOneLineNamedByTheApiOrByItsKind()
    {
        var text = Render(this,
        [
            new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "node", Code = "E11", Count = 10 },
            new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "range", Code = "E10", To = "E14", Count = 50 },
            new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "1569", Kind = "kode", Code = "9", Name = "Ukjent", Expired = true },
            new DesiredCodeView { KodeverkType = "AdministrativtKodeverk", KodeverkReference = "2.16.578", KodeverkName = "Kommunenummer", Kind = "kode", Code = "0301", Name = "Oslo" },
        ]);

        Assert.Contains("ICD-10: E11 med alt under (10 koder), E10–E14 (50 koder)", text);
        Assert.Contains("Kildekodeverk: 9 Ukjent (utgått)", text);
        Assert.Contains("Kommunenummer: 0301 Oslo", text);
    }

    [Fact]
    public void For_WhenThereAreNoCodes_ThenNothingIsDrawn()
    {
        var cut = Render(DesiredCodeSummary.For(Texts.For("no"), [], "no"));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void For_WhenATermLooksLikeMarkup_ThenItIsShownAsTextMarkedNorwegian()
    {
        var cut = Render(DesiredCodeSummary.For(Texts.For("en"),
            [new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "1", Kind = "kode", Code = "1", Name = "<img src=x onerror=alert(1)>" }], "no"));

        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("<img src=x", cut.Find("span[lang=no]").TextContent);
    }

    [Fact]
    public void For_WhenTheListIsNamedByThePackage_ThenOnlyTheApisNameAndTheTermsAreMarkedNorwegian()
    {
        var cut = Render(DesiredCodeSummary.For(Texts.For("en"),
        [
            new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "1", Kind = "kode", Code = "1", Name = "Ugift" },
            new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "node", Code = "E11", Count = 10 },
            new DesiredCodeView { KodeverkType = "AdministrativtKodeverk", KodeverkReference = "2", KodeverkName = "Kommunenummer", Kind = "kode", Code = "0301", Name = "Oslo" },
        ], "no"));

        var marked = cut.FindAll("span[lang=no]").Select(s => s.TextContent).ToList();
        Assert.Equal(["Ugift", "Kommunenummer", "Oslo"], marked);
        Assert.Empty(cut.FindAll("ul, li"));
    }

    [Fact]
    public void For_WhenAHealthCodeSystemHasAName_ThenItIsStillLabelledByItsReference()
    {
        var cut = Render(DesiredCodeSummary.For(Texts.For("no"),
            [new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", KodeverkName = "Den internasjonale sykdomsklassifikasjonen", Kind = "node", Code = "E11", Count = 10 }], "no"));

        Assert.StartsWith("ICD-10:", cut.Find("div > div").TextContent.Trim());
        Assert.DoesNotContain(cut.FindAll("span[lang=no]"), s => s.TextContent == "ICD-10");
    }

    [Fact]
    public void For_WhenOnlyALaterChoiceCarriesTheName_ThenTheListIsNamedByIt()
    {
        var text = Render(this,
        [
            new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", Kind = "kode", Code = "1", Name = "Ugift" },
            new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", KodeverkName = "Sivilstand", Kind = "kode", Code = "2", Name = "Gift" },
        ]);

        Assert.StartsWith("Sivilstand: 1 Ugift, 2 Gift", text);
    }

    [Fact]
    public void For_WhenTheKindIsMissing_ThenTheListIsLabelledByItsReference()
    {
        var text = Render(this, [new DesiredCodeView { KodeverkType = "", KodeverkReference = "1569", Kind = "kode", Code = "9", Name = "Ukjent" }]);

        Assert.StartsWith("1569: 9 Ukjent", text);
    }

    [Fact]
    public void For_WhenASavedRangeIsExactlyABlock_ThenItReadsAsThatBlockAndAnotherRangeAsARange()
    {
        var text = Render(this,
        [
            new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "range", Code = "E10", To = "E14", Count = 50, Ancestors = ["IV", "E10-E14"] },
            new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "range", Code = "E10", To = "E12", Count = 30, Ancestors = ["IV", "E10-E14"] },
        ]);

        Assert.Contains("ICD-10: E10-E14 med alt under (50 koder), E10–E12 (30 koder)", text);
    }
}
