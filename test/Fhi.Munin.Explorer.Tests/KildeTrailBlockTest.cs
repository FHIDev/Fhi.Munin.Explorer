using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilde trail's steps, asked of the block directly: the row drawer no longer draws the trail,
/// so nothing else pins them. (Fhi.Metadata-l9l2n.101)
/// </summary>
public class KildeTrailBlockTest
{
    private static VariableDetail Variable() => new()
    {
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        KildeType = "nasjonaltMedisinskKvalitetsregister",
        DatasamlingName = "Inklusjon",
    };

    private static IReadOnlyList<string> StepTexts(VariableDetail detail, string language = "no") =>
        [.. KildeTrailBlock.Steps(detail, Blazor.Texts.For(language), null).Select(c => c.Text)];

    [Fact]
    public void Steps_Always_ThenTheyRunWidestFirstWithTheShortNameBesideTheKilde()
    {
        Assert.Equal(["Nasjonalt medisinsk kvalitetsregister", "Als registeret (ALS)", "Inklusjon"],
                     StepTexts(Variable()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("als registeret")]
    public void Steps_WhenTheShortNameIsMissingOrRestatesTheName_ThenTheKildeIsWrittenOnce(string shortName)
    {
        Assert.Equal("Als registeret", StepTexts(Variable() with { KildeShortName = shortName })[1]);
    }

    [Fact]
    public void Steps_WhenTheKildeHasNoKildetype_ThenTheTrailStartsAtTheKilde()
    {
        Assert.Equal(["Als registeret (ALS)", "Inklusjon"], StepTexts(Variable() with { KildeType = null }));
    }

    [Fact]
    public void Steps_WhenNoFacetNamesTheKildetype_ThenTheTokenIsKeptRatherThanEmptied()
    {
        var steps = StepTexts(Variable() with { KildeType = "ukjentKildetype" });

        Assert.Equal("ukjentKildetype", steps[0]);
    }

    [Fact]
    public void Steps_WhenAFacetNamesTheKildetype_ThenItsWordIsUsed()
    {
        var steps = KildeTrailBlock.Steps(Variable(), Blazor.Texts.For("no"), "Sentralt helseregister (nytt)");

        Assert.Equal("Sentralt helseregister (nytt)", steps[0].Text);
        Assert.False(steps[0].Norwegian);
    }

    [Theory]
    [InlineData("no", "2 datasamlinger")]
    [InlineData("en", "2 data collections")]
    public void Steps_WhenTheVariableIsInSeveralDatasamlinger_ThenTheLastStepCountsTheNamedOnes(
        string language, string counted)
    {
        // An unnamed entry is dropped from the count, as it is from the list the count stands over.
        var detail = Variable() with
        {
            AllDatasamlinger =
            [
                new() { Id = Guid.NewGuid(), Name = "  " },
                new() { Id = Guid.NewGuid(), Name = "MS-oppfølging" },
                new() { Id = Guid.NewGuid(), Name = "Inklusjon" },
            ],
        };

        var steps = KildeTrailBlock.Steps(detail, Blazor.Texts.For(language), null);

        Assert.Equal(counted, steps[^1].Text);
        Assert.False(steps[^1].Norwegian);
        Assert.Equal(["MS-oppfølging", "Inklusjon"], KildeTrailBlock.NamedDatasamlinger(detail).Select(d => d.Name));
    }

    [Fact]
    public void Named_WhenThePayloadCarriesNoLists_ThenThePrimaryNamesStandIn()
    {
        var detail = Variable() with { VariabelgruppeName = "Funksjonsscore" };

        Assert.Equal(["Inklusjon"], KildeTrailBlock.NamedDatasamlinger(detail).Select(d => d.Name));
        Assert.Equal(["Funksjonsscore"], KildeTrailBlock.NamedVariabelgrupper(detail).Select(g => g.Name));
    }
}
