using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

public class AccessLevelsTest
{
    // Munin test's vocabulary entry as /kilder/egenskaper served it on 2026-10-19 (Fhi.Metadata-d7vf4).
    private static readonly PropertyMetadataEntry Vocabulary = new()
    {
        Key = AccessLevels.Key,
        OptionsJson = """
            [{"value":"statistikkbank","label":"Statistikkbank","labelEn":"Statistics bank"},
             {"value":"tilgang-krever-soknad","label":"Tilgang krever søknad","labelEn":"Access requires an application"},
             {"value":"apne-data","label":"Åpne data/statistikk tilgjengelig","labelEn":"Open data/statistics available"},
             {"value":"soknad-via-helsedata","label":"Søknad via helsedata.no","labelEn":"Application via helsedata.no"}]
            """,
    };

    private static Dictionary<string, string?> Bag(string? value) => new() { [AccessLevels.Key] = value };

    [Fact]
    public void Codes_WhenTheBagHoldsAJsonList_ThenEachCodeOnceTrimmed()
    {
        Assert.Equal(["soknad-via-helsedata", "statistikkbank"],
            AccessLevels.Codes(Bag("""[" soknad-via-helsedata","statistikkbank","STATISTIKKBANK",""]""")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Codes_WhenThereIsNothing_ThenNone(string? raw)
    {
        Assert.Empty(AccessLevels.Codes(Bag(raw)));
        Assert.Empty(AccessLevels.Codes(null));
    }

    [Fact]
    public void Codes_WhenTheKeyIsCasedDifferently_ThenItIsStillFound()
    {
        Assert.Equal(["apne-data"], AccessLevels.Codes(new Dictionary<string, string?> { ["tilgangsnivaahelsedata"] = """["apne-data"]""" }));
    }

    [Fact]
    public void Labels_WhenTheReaderIsNorwegian_ThenTheVocabularysOrderAndWordsWithNoLang()
    {
        var labels = AccessLevels.Labels(["soknad-via-helsedata", "statistikkbank"], Vocabulary, "no");

        Assert.Equal([("statistikkbank", "Statistikkbank", null), ("soknad-via-helsedata", "Søknad via helsedata.no", null)],
            labels.Select(l => (l.Code, l.Label, l.Language)));
    }

    [Fact]
    public void Labels_WhenTheReaderIsEnglish_ThenTheEnglishWords()
    {
        var labels = AccessLevels.Labels(["apne-data"], Vocabulary, "en");

        Assert.Equal("Open data/statistics available", Assert.Single(labels).Label);
        Assert.Null(labels[0].Language);
    }

    [Fact]
    public void Labels_WhenACodeIsNotInTheVocabulary_ThenItComesLastAsItself()
    {
        var labels = AccessLevels.Labels(["ny-kode", "statistikkbank"], Vocabulary, "no");

        Assert.Equal(["Statistikkbank", "ny-kode"], labels.Select(l => l.Label));
        Assert.Null(labels[1].Language);
    }
}
