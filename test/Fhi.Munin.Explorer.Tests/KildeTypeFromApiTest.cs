using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// That the components a host mounts on their own name a kildetype in the API's word, as
/// <c>VariableSearch</c> already does, and fall back to the shipped table (Fhi.Metadata-vcxoc).
/// </summary>
public class KildeTypeFromApiTest : ExplorerTestContext
{
    private const string Token = "sentraltHelseregister";
    private const string ShippedNorwegian = "Sentralt helseregister";
    private const string ShippedEnglish = "Central health registry";
    private const string ApiNorwegian = "Sentralregister fra API";
    private const string ApiEnglish = "Central register from the API";

    /// <summary>Answers a different word per language, and remembers every language it was asked in.</summary>
    private sealed class FiltersClient(Func<string?, string?>? displayName = null, bool fail = false)
        : EmptyMuninExplorerClient
    {
        public List<string?> Languages { get; } = [];

        public bool Empty { get; init; }

        /// <summary>Held until a test lets it go, so an answer can arrive after the first render.</summary>
        public TaskCompletionSource? Gate { get; init; }

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null,
            VariableFilter? filter = null,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            Languages.Add(language);

            return Gate is null ? Answer(language) : Gate.Task.ContinueWith(_ => Answer(language).Result);
        }

        private Task<FilterOptions> Answer(string? language)
        {
            if (fail)
            {
                throw new HttpRequestException("the filters endpoint is down");
            }

            var name = (displayName ?? (l => l == "en" ? ApiEnglish : ApiNorwegian))(language);

            return Task.FromResult(new FilterOptions
            {
                KildeTyper = Empty ? [] : [new KildetypeFacet { Value = Token, DisplayName = name! }],
            });
        }

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
                [new KildeSummary { Id = Guid.NewGuid(), Code = "K_DAR", Name = "Dødsårsaksregisteret", Kildetype = Token }]);
    }

    private static KildeDetail Kilde() =>
        new() { Id = Guid.NewGuid(), Code = "K_DAR", PreferredTerm = "Dødsårsaksregisteret", Kildetype = Token };

    private static VariableDetail Variable() =>
        new() { Id = Guid.NewGuid(), Code = "DAR1", PreferredTerm = "Dødsdato", KildeName = "Dødsårsaksregisteret", KildeType = Token };

    private static DatasamlingDetail Datasamling() =>
        new() { Id = Guid.NewGuid(), PreferredTerm = "Dødsmeldinger", EffectiveKildetype = Token };

    /// <summary>Each view, rendered in a language, as the markup a reader gets.</summary>
    public static TheoryData<string> Views => ["kilde", "variable", "datasamling"];

    /// <summary>A mounted view, reduced to what these tests ask of it: its markup and a new language.</summary>
    private sealed record Mounted(Func<string> Markup, Action<string?> Language);

    private Mounted RenderView(string view, string? language)
    {
        switch (view)
        {
            case "kilde":
                var kilde = Render<KildeView>(b => b.Add(c => c.Kilde, Kilde()).Add(c => c.Language, language));
                return new(() => kilde.Markup, l => kilde.Render(b => b.Add(c => c.Language, l)));
            case "variable":
                var variable = Render<VariableView>(b => b.Add(c => c.Variable, Variable()).Add(c => c.Language, language));
                return new(() => variable.Markup, l => variable.Render(b => b.Add(c => c.Language, l)));
            default:
                var datasamling = Render<DatasamlingView>(
                    b => b.Add(c => c.Datasamling, Datasamling()).Add(c => c.Language, language));
                return new(() => datasamling.Markup, l => datasamling.Render(b => b.Add(c => c.Language, l)));
        }
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void View_WhenTheApiNamesTheKildetype_ThenItShowsTheApiWordInTheReadersLanguage(string view)
    {
        var client = new FiltersClient();
        Services.AddSingleton<IMuninExplorerClient>(client);

        var cut = RenderView(view, "no");

        Assert.Contains(ApiNorwegian, cut.Markup());
        Assert.DoesNotContain(ShippedNorwegian, cut.Markup());

        cut.Language("en");

        Assert.Contains(ApiEnglish, cut.Markup());
        Assert.DoesNotContain(ApiNorwegian, cut.Markup());
        Assert.DoesNotContain(ShippedEnglish, cut.Markup());
        Assert.Equal(["nb", "en"], client.Languages);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void View_WhenRenderedAgain_ThenItAsksForTheFiltersOncePerLanguage(string view)
    {
        var client = new FiltersClient();
        Services.AddSingleton<IMuninExplorerClient>(client);

        var cut = RenderView(view, "no");
        cut.Language("no");
        cut.Language("en");
        cut.Language("en");
        cut.Language("no");

        Assert.Equal(["nb", "en"], client.Languages);
        Assert.Contains(ApiNorwegian, cut.Markup());
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void View_WhenTheAnswerArrivesAfterTheFirstRender_ThenTheShippedWordGivesWayToTheApiWord(string view)
    {
        var client = new FiltersClient { Gate = new TaskCompletionSource() };
        Services.AddSingleton<IMuninExplorerClient>(client);

        var cut = RenderView(view, "no");

        Assert.Contains(ShippedNorwegian, cut.Markup());

        client.Gate.SetResult();

        WaitFor(() => Assert.DoesNotContain(ShippedNorwegian, cut.Markup()));
        Assert.Contains(ApiNorwegian, cut.Markup());
    }

    private static void WaitFor(Action assertion)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (true)
        {
            try
            {
                assertion();

                return;
            }
            catch (Xunit.Sdk.XunitException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }
        }
    }

    /// <summary>Every answer the shipped table is meant for: a throw, no facets, and the three empty or echoed names.</summary>
    public static TheoryData<string, string> Fallbacks()
    {
        var data = new TheoryData<string, string>();

        foreach (var view in new[] { "kilde", "variable", "datasamling" })
        {
            foreach (var answer in new[] { "throws", "no kildetyper", "null", "blank", "value" })
            {
                data.Add(view, answer);
            }
        }

        return data;
    }

    private static FiltersClient Answering(string answer) =>
        answer switch
        {
            "throws" => new FiltersClient(fail: true),
            "no kildetyper" => new FiltersClient { Empty = true },
            "null" => new FiltersClient(_ => null),
            "blank" => new FiltersClient(_ => "  "),
            _ => new FiltersClient(_ => "SentraltHelseregister"),
        };

    [Theory]
    [MemberData(nameof(Fallbacks))]
    public void View_WhenTheApiHasNoWord_ThenItShowsTheShippedWord(string view, string answer)
    {
        Services.AddSingleton<IMuninExplorerClient>(Answering(answer));

        var cut = RenderView(view, "no");

        Assert.Contains(ShippedNorwegian, cut.Markup());
    }

    [Theory]
    [InlineData("variable")]
    [InlineData("datasamling")]
    public void View_WhenTheHostRegisteredNoClient_ThenItRendersTheShippedWord(string view)
    {
        var cut = RenderView(view, "en");

        Assert.Contains(ShippedEnglish, cut.Markup());
    }

    [Fact]
    public void KildeSearch_WhenTheApiNamesTheKildetype_ThenTheFacetAndTheTableCellBothShowIt()
    {
        var client = new FiltersClient();
        Services.AddSingleton<IMuninExplorerClient>(client);

        var cut = Render<KildeSearch>(b => b.Add(c => c.Language, "en"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(cut.FindAll(".munin-explorer-filters li label"),
                            label => label.TextContent.Contains(ApiEnglish, StringComparison.Ordinal));
            Assert.Contains(cut.FindAll(".munin-explorer-kilder tbody td"),
                            cell => cell.TextContent.Trim() == ApiEnglish);
        });
        Assert.DoesNotContain(ShippedEnglish, cut.Markup);
        Assert.Equal(["en"], client.Languages);
    }

    [Fact]
    public void KildeSearch_WhenTheFiltersCallThrows_ThenTheFacetAndTheTableCellShowTheShippedWord()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FiltersClient(fail: true));

        var cut = Render<KildeSearch>(b => b.Add(c => c.Language, "en"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(cut.FindAll(".munin-explorer-filters li label"),
                            label => label.TextContent.Contains(ShippedEnglish, StringComparison.Ordinal));
            Assert.Contains(cut.FindAll(".munin-explorer-kilder tbody td"),
                            cell => cell.TextContent.Trim() == ShippedEnglish);
        });
    }
}
