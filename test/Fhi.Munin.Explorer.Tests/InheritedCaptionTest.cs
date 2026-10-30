using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Which of a datasamling's values say where they were inherited from, and how that line reads.</summary>
public class InheritedCaptionTest : ExplorerTestContext
{
    private static readonly IReadOnlySet<string> NothingOmitted = new HashSet<string>();

    private static DatasamlingDetail Datasamling() => new()
    {
        Id = Guid.NewGuid(),
        Code = "D",
        PreferredTerm = "D",
        ParentKildeId = Guid.NewGuid(),
        ParentKildeName = "Tromsøundersøkelsen",
        ParentKildeNameEn = "The Tromsø Study",
    };

    private static InheritedValue From(string level, string name, string? nameEn = null) => new()
    {
        Value = "x",
        SourceLevel = level,
        SourceId = Guid.NewGuid(),
        SourceName = name,
        SourceNameEn = nameEn,
    };

    [Fact]
    public void For_WhenAFreeTextFieldIsLeftToTheKilde_ThenItNamesTheKildeAndAnOwnValueNamesNothing()
    {
        var captions = InheritedCaptions.For(Datasamling() with
        {
            EffectiveLegalBasis = "Helseregisterloven",
            DataController = "FHI",
            EffectiveDataController = "FHI",
            EffectiveDataProcessor = "FHI",
        }, Texts.For("nb"), ReaderLanguage.Norwegian, NothingOmitted);

        Assert.Equal(new InheritedCaption("Arvet fra kilden", "Tromsøundersøkelsen", null, "no"),
                     captions[CatalogueColumns.LegalBasis]);
        Assert.True(captions.ContainsKey(CatalogueColumns.DataProcessor));
        Assert.False(captions.ContainsKey(CatalogueColumns.DataController));
    }

    [Fact]
    public void For_WhenNothingIsEffective_ThenThereIsNothingToCaption()
    {
        Assert.Empty(InheritedCaptions.For(Datasamling(), Texts.For("nb"), ReaderLanguage.Norwegian, NothingOmitted));
    }

    [Fact]
    public void For_WhenAPublicKeyComesFromADelkilde_ThenItNamesTheDelkildeInTheReadersLanguage()
    {
        var datasamling = Datasamling() with
        {
            InheritedProperties = new Dictionary<string, InheritedValue>
            {
                [InheritedCaptions.Homepage] = From("delkilde", "Tromsø7", "Tromsø 7"),
                [InheritedCaptions.DataControllerHerId] = From("kilde", "Tromsøundersøkelsen"),
            },
        };

        var english = InheritedCaptions.For(datasamling, Texts.For("en"), ReaderLanguage.English);

        Assert.Equal(new InheritedCaption("Inherited from the sub-source", "Tromsø 7", null, "en"),
                     english[InheritedCaptions.Homepage]);
        // No English name, so the Norwegian one, marked for an English reader.
        Assert.Equal(new InheritedCaption("Inherited from the source", "Tromsøundersøkelsen", "no", "en"),
                     english[InheritedCaptions.DataControllerHerId]);
    }

    [Fact]
    public void For_WhenTheSourceLevelIsUnknown_ThenNoCaptionGuessesOne()
    {
        var captions = InheritedCaptions.For(Datasamling() with
        {
            InheritedProperties = new Dictionary<string, InheritedValue>
            {
                [InheritedCaptions.Homepage] = From("register", "Noe"),
            },
        }, Texts.For("nb"), ReaderLanguage.Norwegian);

        Assert.Empty(captions);
    }

    [Fact]
    public void For_WhenAnInheritedValueIsBlank_ThenItHasNothingToCaption()
    {
        var captions = InheritedCaptions.For(Datasamling() with
        {
            InheritedProperties = new Dictionary<string, InheritedValue>
            {
                [InheritedCaptions.Homepage] = From("kilde", "K") with { Value = "  " },
            },
        }, Texts.For("nb"), ReaderLanguage.Norwegian, NothingOmitted);

        Assert.Empty(captions);
    }

    [Theory]
    [InlineData(CatalogueColumns.Kildetype)]
    [InlineData(CatalogueColumns.LegalBasis)]
    [InlineData(CatalogueColumns.DataController)]
    [InlineData(CatalogueColumns.DataProcessor)]
    [InlineData(InheritedCaptions.DataControllerOrgNumber)]
    [InlineData(InheritedCaptions.DataSourceHerId)]
    public void For_WhenAFieldIsNormallySetOnlyOnTheKilde_ThenItIsLeftUncaptioned(string key)
    {
        var captions = InheritedCaptions.For(Datasamling() with
        {
            EffectiveKildetype = "Register",
            EffectiveLegalBasis = "Helseregisterloven",
            EffectiveDataController = "FHI",
            EffectiveDataProcessor = "FHI",
            InheritedProperties = new Dictionary<string, InheritedValue>
            {
                [InheritedCaptions.DataControllerOrgNumber] = From("kilde", "K"),
                [InheritedCaptions.DataSourceHerId] = From("kilde", "K"),
            },
        }, Texts.For("nb"), ReaderLanguage.Norwegian);

        Assert.False(captions.ContainsKey(key));
    }

    [Theory]
    [InlineData("Tromsø7", "no", "Arvet fra delkilden Tromsø7", "no")]
    [InlineData("", null, "Arvet fra delkilden", null)]
    public void Render_WhenDrawn_ThenItIsAQuietParagraphWithTheNameMarkedApart(
        string name, string? language, string text, string? spanLanguage)
    {
        var caption = new InheritedCaption("Arvet fra delkilden", name, language, "no");

        var cut = Render((RenderFragment)(builder => caption.Render(builder, 0)));

        var paragraph = cut.Find("p");
        Assert.Equal("caption margin--none", paragraph.GetAttribute("class"));
        Assert.Equal("no", paragraph.GetAttribute("lang"));
        Assert.Equal(text, paragraph.TextContent);
        Assert.Equal(spanLanguage, paragraph.QuerySelector("span")?.GetAttribute("lang"));
    }
}
