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
        return System.Text.RegularExpressions.Regex.Replace(cut.Markup.Length == 0 ? "" : cut.Find("ul").TextContent, @"\s+", " ").Trim();
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
}
