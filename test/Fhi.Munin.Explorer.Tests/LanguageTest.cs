using System.Reflection;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one choice every user-facing string in this package hangs off: which language the host asked
/// for.
/// </summary>
/// <remarks>
/// Tested head-on rather than only through rendered markup because both of its failure modes are
/// silent. A token this does not recognise renders a whole page in the wrong language with nothing
/// thrown, and a string translated in one language and not the other renders as the wrong one in
/// place — neither shows up as an exception, a warning, or a failing render.
/// </remarks>
public class LanguageTest : BunitContext
{
    private static Page<VariableSummary> OnePage() =>
        new()
        {
            Items =
            [
                new VariableSummary
                {
                    Id = Guid.NewGuid(),
                    Code = "V_ALS.F1.ALSFRSR1TALE",
                    PreferredTerm = "1. Tale",
                    KildeName = "Als registeret"
                }
            ],
            TotalCount = 1,
            PageNumber = 1,
            Size = 25,
            TotalPages = 1
        };

    /// <summary>Answers one page, and remembers the language the facet call asked for.</summary>
    private sealed class RecordingClient : EmptyMuninExplorerClient
    {
        public string? FacetLanguage { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OnePage());

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default)
        {
            FacetLanguage = language;
            return Task.FromResult(new FilterOptions());
        }
    }

    private IRenderedComponent<VariableExplorer> RenderWith(IMuninExplorerClient client, string language)
    {
        Services.AddSingleton(client);
        return Render<VariableExplorer>(b => b.Add(c => c.Language, language));
    }

    [Theory]
    // helsedata's CMS reports the branch name, which is "no" and not "nb".
    [InlineData("no", "no")]
    [InlineData("NO", "no")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    // The second representation the same solution holds: LanguageExtensions returns nb-NO/en-GB and
    // the PDF generator builds full CultureInfos from them. Which of the two reaches our mount point
    // is the host's choice, so both have to mean the same thing here.
    [InlineData("en-GB", "en")]
    [InlineData("en-US", "en")]
    [InlineData("En-gb", "en")]
    [InlineData("nb-NO", "no")]
    [InlineData("nb", "no")]
    [InlineData("nn", "no")]
    // Not what BCP 47 says, but what resource file names and hand-written configuration use.
    [InlineData("en_US", "en")]
    [InlineData("  en-GB  ", "en")]
    // Norwegian is the fallback rather than an error: the catalogue is Norwegian and so are most of
    // its readers, and a component that refused to render would take the host's whole page with it.
    [InlineData(null, "no")]
    [InlineData("", "no")]
    [InlineData("   ", "no")]
    [InlineData("de", "no")]
    [InlineData("e", "no")]
    [InlineData("-en", "no")]
    public void Of_WhenGivenALanguageToken_ThenItResolvesToOneOfTheTwoLanguages(
        string? language, string expected)
    {
        Assert.Equal(expected, ReaderLanguage.Of(language));
        Assert.Equal(expected == "en", ReaderLanguage.IsEnglish(language));
    }

    [Fact]
    public void For_WhenTheTokenCarriesARegion_ThenTheWordsFollowItsLanguageRatherThanFallingBack()
    {
        // The bead's own criterion. An exact match on "en" left an English page's chrome in
        // Norwegian with nothing thrown and no test failing, which is why this one exists.
        Assert.Same(Texts.For("en"), Texts.For("en-GB"));
        Assert.Equal("Variable explorer", Texts.For("en-GB").Title);
        Assert.Equal("Variabelutforsker", Texts.For("nb-NO").Title);
    }

    [Theory]
    [InlineData("en-GB", "en", "January")]
    [InlineData("nb-NO", "nb", "januar")]
    public void Culture_WhenTheTokenCarriesARegion_ThenDatesAreWrittenTheWayThatLanguageWritesThem(
        string language, string expectedCulture, string expectedMonth)
    {
        // Dates resolve the language a second time, separately from the words, so a token that
        // fixed one and not the other would give an English page Norwegian dates. That halfway
        // state is worse than either language on its own, because it reads as bad data rather than
        // as a setting.
        var culture = CatalogueProperties.Culture(language);

        Assert.Equal(expectedCulture, culture.TwoLetterISOLanguageName);
        Assert.Equal(
            expectedMonth,
            new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("MMMM", culture));
    }

    [Fact]
    public void Render_WhenTheTokenCarriesARegion_ThenTheComponentRendersInThatLanguage()
    {
        var cut = RenderWith(new RecordingClient(), "en-GB");

        Assert.Contains("Variable explorer", cut.Markup);
        Assert.DoesNotContain("Variabelutforsker", cut.Markup);
    }

    [Theory]
    // The API's own two tags, not ours: it documents "nb" or "en" and sends the value verbatim as
    // Accept-Language. Our "no" is not one of them, and it has no parent culture the API's request
    // localization can fall back from, so it would quietly take the API's default culture instead.
    [InlineData("en-GB", "en")]
    [InlineData("en", "en")]
    [InlineData("nb-NO", "nb")]
    [InlineData("no", "nb")]
    [InlineData("de", "nb")]
    public void Render_WhenGivenALanguageToken_ThenTheFacetCallAsksForTheApisSpellingOfIt(
        string language, string expected)
    {
        // The datatype facet's names are resolved server side, so they follow Accept-Language
        // rather than the texts here. Passing the host's raw token through would leave the filter
        // panel as the one Norwegian block on an English page if the API did not know "en-GB".
        var client = new RecordingClient();

        RenderWith(client, language);

        Assert.Equal(expected, client.FacetLanguage);
        Assert.Equal(expected, ReaderLanguage.ForApi(language));
    }

    [Fact]
    public void Render_WhenTheHostRegistersNoLocalisationServices_ThenTheComponentStillRenders()
    {
        // The constraint the whole approach exists for: no host in helsedata's estate calls
        // AddLocalization(), so an IStringLocalizer injected anywhere in here would throw at render
        // time rather than fail the build. Nothing but the client is registered below.
        var cut = RenderWith(new RecordingClient(), "en");

        Assert.Contains("Variable explorer", cut.Markup);
    }

    [Fact]
    public void Package_WhenBuilt_ThenItReferencesNoLocalisationAssembly()
    {
        // The render test above passes whether or not a localiser is injected somewhere that
        // happens not to run — an unopened panel, an error path. This one does not: the reference
        // is in the assembly's metadata the moment anyone types IStringLocalizer.
        var referenced = typeof(VariableExplorer).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(name => name.Contains("Localization", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(referenced);
    }

    [Fact]
    public void Texts_WhenAStringIsAddedInOneLanguage_ThenItIsAlsoAddedInTheOther()
    {
        // A half-translated release is what this catches. The record's positional parameters make a
        // *missing* string a compile error, but an empty one, and a vocabulary that gained a token
        // in Norwegian only, are both silent: the value renders as nothing, or as the raw API
        // token, in one language and not in the other.
        var no = Texts.For("no");
        var en = Texts.For("en");

        foreach (var property in typeof(Texts).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.GetValue(no))
            {
                case string norwegian:
                    Assert.False(
                        string.IsNullOrWhiteSpace(norwegian), $"{property.Name} is empty in Norwegian.");
                    Assert.False(
                        string.IsNullOrWhiteSpace((string?)property.GetValue(en)),
                        $"{property.Name} is empty in English.");
                    break;

                case IReadOnlyDictionary<string, string> norwegianVocabulary:
                    var englishVocabulary = (IReadOnlyDictionary<string, string>)property.GetValue(en)!;

                    Assert.Equal(
                        norwegianVocabulary.Keys.OrderBy(key => key, StringComparer.Ordinal),
                        englishVocabulary.Keys.OrderBy(key => key, StringComparer.Ordinal));
                    Assert.All(
                        norwegianVocabulary.Concat(englishVocabulary),
                        entry => Assert.False(
                            string.IsNullOrWhiteSpace(entry.Value),
                            $"{property.Name}[{entry.Key}] is empty."));
                    break;

                default:
                    // The sentence-building delegates. There is nothing to compare without
                    // arguments, and each is asserted where it is rendered — PageOf and
                    // ResultSummary by the English pager test, NoResults by the English empty
                    // state. The type assertion is what keeps that comment true: a member added
                    // later in some other shape — a list of strings, a nested record — would
                    // otherwise fall through here and be reported as checked with nothing about
                    // its translation compared, which is the half-translated release this test
                    // exists to catch.
                    Assert.True(
                        typeof(Delegate).IsAssignableFrom(property.PropertyType),
                        $"{property.Name} is a {property.PropertyType.Name}, which no arm here "
                        + "compares. Give it an arm rather than letting it pass unchecked.");
                    Assert.NotNull(property.GetValue(en));
                    Assert.NotNull(property.GetValue(no));
                    break;
            }
        }
    }

    [Fact]
    public void Texts_WhenAControlIsNamedAfterAVariable_ThenBothLanguagesPutTheNameInTheSentence()
    {
        // The parity guard above has one arm for the sentence-building delegates, and it can only
        // ask that both languages have one — a translation that dropped the {name} placeholder
        // would satisfy it and give every row in the list an identical accessible name, which is
        // the defect these three strings exist to fix. So each is called here, in both languages.
        const string variable = "Alder ved diagnose";

        foreach (var language in new[] { "no", "en" })
        {
            var texts = Texts.For(language);

            foreach (var (name, sentence) in new[]
            {
                (nameof(texts.SaveToListLabel), texts.SaveToListLabel(variable)),
                (nameof(texts.RemoveFromListLabel), texts.RemoveFromListLabel(variable)),
                (nameof(texts.RemoveFromThisListLabel), texts.RemoveFromThisListLabel(variable))
            })
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(sentence), $"{name} is empty in {language}.");
                Assert.Contains(variable, sentence, StringComparison.Ordinal);
            }

            // Each accessible name contains the words on the button, so a speech-input user saying
            // what they can see still hits the control. WCAG 2.5.3, and the reason the variable's
            // name is appended rather than dropped into the middle of the phrase.
            Assert.Contains(texts.SaveToList, texts.SaveToListLabel(variable), StringComparison.Ordinal);
            Assert.Contains(texts.RemoveFromList, texts.RemoveFromListLabel(variable), StringComparison.Ordinal);
            Assert.Contains(
                texts.RemoveFromThisList, texts.RemoveFromThisListLabel(variable), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Texts_WhenTheApiRateLimits_ThenTheSentenceDiffersFromTheGenericFailureInBothLanguages()
    {
        // The whole point of the 429 branch is that a throttled reader is told something they can
        // act on. Two strings that happened to say the same thing would satisfy every other test
        // here — the parity guard only asks that both languages have one — and leave the reader
        // being told to try again by the failure that trying again causes.
        foreach (var language in new[] { "no", "en" })
        {
            var texts = Texts.For(language);

            Assert.NotEqual(texts.Error, texts.RateLimitError);
            Assert.NotEqual(texts.KildeListError, texts.RateLimitError);
            Assert.NotEqual(texts.CodesError, texts.RateLimitError);
            Assert.NotEqual(texts.DetailError, texts.RateLimitError);
            Assert.NotEqual(texts.KildeError, texts.RateLimitError);

            // No number and no countdown. Retry-After is read and carried on the exception for a
            // host that logs it, and never rendered: the window is shared, so a moment named on
            // the page may already be spent by somebody else's request.
            Assert.False(
                texts.RateLimitError.Any(char.IsDigit),
                $"The rate-limit text in {language} names a number: \"{texts.RateLimitError}\".");
        }
    }
}
