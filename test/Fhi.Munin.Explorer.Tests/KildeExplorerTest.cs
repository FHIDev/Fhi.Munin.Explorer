using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Kelda's shell: the search field, the count, the result table and what happens when a kilde is
/// opened.
/// </summary>
/// <remarks>
/// Three of the things asserted here are the ones a component that merely renders would get wrong
/// silently, and each has already cost this repository something once.
/// <para>
/// The first is the search. A test that only checks that searching narrows the list passes against
/// exactly the implementation <c>Fhi.Metadata-l9l2n.26</c> had to undo — one round-trip per
/// keystroke on helsedata's Blazor Server circuit, dropping characters out of a fast paste. So the
/// count of calls is asserted beside the result, and the field is asked to accept an
/// <c>input</c> event it must not have.
/// </para>
/// <para>
/// The second is <see cref="KildeView.Sections"/>. Kelda's own sections have to reach that
/// component through its parameter, because it is a shared core with slots and not a view with
/// flags; an implementation that instead put Kelda-specific markup inside it would pass any
/// assertion that only looks for the text on screen. The assertion here is on the parameter, and
/// what those sections actually are — and that Runa's view of the same kilde has none of them —
/// is <c>KildeSectionsTest</c>'s.
/// </para>
/// <para>
/// The third is the class names. Both sample hosts style every name this component writes, so
/// looking at a sample proves nothing about a host that has only Stiler — the guard is what
/// catches it, and it is run over a render that has the list on screen and over one that has a
/// kilde open, because the two states share almost no markup.
/// </para>
/// </remarks>
public class KildeExplorerTest : BunitContext
{
    private static KildeSummary Kilde(
        string name,
        string code,
        string? shortName = null,
        string kildetype = "sentraltHelseregister",
        bool active = true,
        string? dataProcessor = "Folkehelseinstituttet",
        int datasamlinger = 3,
        int variables = 42,
        string? established = null,
        string? category = null,
        string? accessRights = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ShortName = shortName,
            Kildetype = kildetype,
            IsActive = active,
            DataProcessor = dataProcessor,
            DatasamlingCount = datasamlinger,
            TotalVariables = variables,
            AdditionalProperties = Properties(established, category, accessRights),
        };

    /// <summary>
    /// The curated bag two of the facets and the Opprettet column read from, holding only the keys
    /// a test asked for.
    /// </summary>
    /// <remarks>
    /// A key is left out entirely rather than set to null or to an empty string, because that is
    /// what the API does for a kilde nobody filled the field in on — and "the key is absent" is the
    /// state the empty-facet rule has to survive. <paramref name="category"/> is passed as the API
    /// writes it, a JSON array inside a string, so a test can hand over a malformed one as easily
    /// as a good one.
    /// </remarks>
    private static IReadOnlyDictionary<string, string?> Properties(
        string? established, string? category, string? accessRights)
    {
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (established is not null)
        {
            properties["Opprettet"] = established;
        }

        if (category is not null)
        {
            properties["healthCategory"] = category;
        }

        if (accessRights is not null)
        {
            properties["accessRights"] = accessRights;
        }

        return properties;
    }

    /// <summary>
    /// The EHDS categories the catalogue's own vocabulary lists, value, Norwegian and English.
    /// </summary>
    /// <remarks>
    /// Transcribed off the <c>healthCategory</c> vocabulary in
    /// <c>Testdata/kilde-med-delkilder.json</c>, which is what the API really serves. It belongs
    /// here and nowhere else: the package itself used to hold this table and translate the facet
    /// out of it, and that copy going stale is what the tests below are here to keep from coming
    /// back.
    /// </remarks>
    private static readonly (string Value, string Norwegian, string? English)[] Categories =
    [
        ("ehds-cat:health-registries", "Helseregistre", "Health registries"),
        ("ehds-cat:registries-quality-of-healthcare", "Kvalitetsregistre", "Quality-of-healthcare registries"),
        ("ehds-cat:population-health-surveys", "Befolkningsbaserte helseundersøkelser", "Population health surveys"),
        ("ehds-cat:provesamling", "Prøvesamling", "Sample collection"),
        ("ehds-cat:biodata", "Biodata (DNA/omikk)", "Biodata (DNA/omics)"),
        ("ehds-cat:biobanks", "Biobanker", "Biobanks"),
        ("ehds-cat:other", "Annet", "Other")
    ];

    /// <summary>The access-rights values the same vocabulary lists. <inheritdoc cref="Categories"/></summary>
    private static readonly (string Value, string Norwegian, string? English)[] AccessLevels =
    [
        ("eu-access:NON_PUBLIC", "Ikke-offentlig", "Non-public"),
        ("eu-access:RESTRICTED", "Begrenset", "Restricted"),
        ("eu-access:PUBLIC", "Offentlig", "Public")
    ];

    /// <summary>
    /// One property's vocabulary, in the shape the API sends it.
    /// </summary>
    /// <remarks>
    /// The options are a JSON string rather than a JSON array, which is the API's own doing and
    /// the reason the component parses them: the field carries both labels so that one response can
    /// be rendered to readers in either language without being fetched again.
    /// </remarks>
    private static PropertyMetadataEntry Vocabulary(
        string key, params (string Value, string Norwegian, string? English)[] options) =>
        new()
        {
            Key = key,
            Type = "MultiSelect",
            OptionsJson = JsonSerializer.Serialize(
                options.Select(option => new { value = option.Value, label = option.Norwegian, labelEn = option.English })),
        };

    /// <summary>The vocabulary the catalogue serves today, for both of the coded facets.</summary>
    private static IReadOnlyList<PropertyMetadataEntry> CatalogueVocabulary() =>
        [Vocabulary("healthCategory", Categories), Vocabulary("accessRights", AccessLevels)];

    private static KildeDetail Detail(KildeSummary summary) =>
        new()
        {
            Id = summary.Id,
            Code = summary.Code,
            PreferredTerm = summary.Name,
            ShortName = summary.ShortName,
            Description = "Norsk register for ALS og andre motonevronsykdommer.",
            Kildetype = summary.Kildetype,
            DataController = summary.DataController,
            DataProcessor = summary.DataProcessor,
            LastUpdated = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
            TotalVariables = summary.TotalVariables,
        };

    /// <summary>
    /// Answers with a fixed list, and remembers what it was asked and how often.
    /// </summary>
    /// <remarks>
    /// The call count is the point of this fake rather than a detail of it: the component is
    /// supposed to ask for the list exactly once and never again, so almost every assertion about
    /// searching is partly an assertion about <see cref="Calls"/>.
    /// </remarks>
    private sealed class FakeClient(params KildeSummary[] kilder) : EmptyMuninExplorerClient
    {
        private readonly Dictionary<Guid, KildeDetail> _details = [];
        private readonly List<TaskCompletionSource<KildeDetail?>> _stalls = [];

        public string? LastSearch { get; private set; }
        public string? LastKildeType { get; private set; }
        public int Calls { get; private set; }
        public int DetailCalls { get; private set; }
        public int VocabularyCalls { get; private set; }

        /// <summary>
        /// The vocabulary the API serves beside the list, which the coded facets draw their words
        /// from. Whatever the catalogue holds now — a test that wants a value the catalogue added
        /// since says so with <see cref="Serving"/>.
        /// </summary>
        private IReadOnlyList<PropertyMetadataEntry> _vocabulary = CatalogueVocabulary();

        /// <summary>Fail the vocabulary fetch — the sibling endpoint being down, with the list up.</summary>
        public bool FailVocabulary { get; set; }

        /// <summary>How many detail fetches have been left hanging.</summary>
        public int Stalls => _stalls.Count;

        /// <summary>Fail every detail fetch from the next one on — the API being down, not an id it does not know.</summary>
        public bool FailDetail { get; set; }

        /// <summary>
        /// Refuse every detail fetch from the next one on with the API's 429 — the API being up and
        /// this reader having asked too often.
        /// </summary>
        /// <remarks>
        /// Its own switch beside <see cref="FailDetail"/>, because the point of the tests using it
        /// is that the view tells the three answers apart: refused, down, and not published.
        /// </remarks>
        public bool RateLimitDetail { get; set; }

        /// <summary>
        /// Never answer a detail fetch from the next one on, so a test can decide when — and
        /// whether — it lands.
        /// </summary>
        /// <remarks>
        /// Without this every fetch here completes before the click handler returns, so no fetch is
        /// ever in flight across an open or a close and the component's generation guard is never
        /// reached. It was possible to delete that guard and keep the whole suite green.
        /// </remarks>
        public bool StallDetail { get; set; }

        /// <summary>Publish a detail for a kilde; anything not published answers null, as the API does.</summary>
        public FakeClient Publishing(params KildeSummary[] summaries)
        {
            foreach (var summary in summaries)
            {
                _details[summary.Id] = Detail(summary);
            }

            return this;
        }

        /// <summary>Publish a detail written by the caller, for the fields Detail() leaves empty.</summary>
        public FakeClient Describing(KildeDetail detail)
        {
            _details[detail.Id] = detail;

            return this;
        }

        /// <summary>Serve this vocabulary in place of the catalogue's current one.</summary>
        public FakeClient Serving(params PropertyMetadataEntry[] vocabulary)
        {
            _vocabulary = vocabulary;

            return this;
        }

        /// <summary>Answer the oldest detail fetch still hanging.</summary>
        public void AnswerStalled(KildeDetail detail) => Oldest().TrySetResult(detail);

        /// <summary>Fail the oldest detail fetch still hanging.</summary>
        public void FailStalled() => Oldest().TrySetException(new HttpRequestException("the API is down"));

        /// <summary>Refuse the oldest detail fetch still hanging with the API's 429.</summary>
        public void RateLimitStalled() =>
            Oldest().TrySetException(new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)));

        private TaskCompletionSource<KildeDetail?> Oldest() =>
            _stalls.First(stall => !stall.Task.IsCompleted);

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default)
        {
            LastSearch = search;
            LastKildeType = kildeType;
            Calls++;

            return Task.FromResult<IReadOnlyList<KildeSummary>>(kilder);
        }

        public override Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(
            CancellationToken cancellationToken = default)
        {
            VocabularyCalls++;

            return FailVocabulary
                ? Task.FromException<IReadOnlyList<PropertyMetadataEntry>>(
                    new HttpRequestException("the API is down"))
                : Task.FromResult(_vocabulary);
        }

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DetailCalls++;

            if (RateLimitDetail)
            {
                return Task.FromException<KildeDetail?>(
                    new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)));
            }

            if (FailDetail)
            {
                // A faulted task rather than a throw from the call itself: that is the shape an
                // HttpClient failure arrives in, and it is the await that has to catch it.
                return Task.FromException<KildeDetail?>(new HttpRequestException("the API is down"));
            }

            if (StallDetail)
            {
                var stall = new TaskCompletionSource<KildeDetail?>();
                _stalls.Add(stall);

                return stall.Task;
            }

            return Task.FromResult(_details.TryGetValue(id, out var detail) ? detail : null);
        }
    }

    /// <summary>
    /// Never answers the list call, so a test can see the render before the list arrives.
    /// </summary>
    /// <remarks>
    /// <see cref="FakeClient"/> answers from <see cref="Task.FromResult{TResult}"/>, so its await
    /// never yields and no test using it renders while the list is in flight. An unresolved task is
    /// the shape a real HttpClient call has, and the state behind it — a host-named kilde whose
    /// detail fetch has not started yet — is one the drilldown is already on screen for.
    /// </remarks>
    private sealed class StallingListClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<KildeSummary>> _never = new();

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            _never.Task;
    }

    /// <summary>
    /// Answers the list and the vocabulary from a <see cref="TaskCompletionSource{TResult}"/>
    /// apiece, so a test can land one while the other is still in flight.
    /// </summary>
    /// <remarks>
    /// <see cref="FakeClient"/> answers both from <see cref="Task.FromResult{TResult}"/>, so its
    /// awaits never yield and every test using it renders with the two calls already finished —
    /// the one ordering that cannot happen there is the one the component's two-call design is
    /// about. A real <c>HttpClient</c> yields on both, and a host pointed at an API that has not
    /// deployed <c>api/explorer/kilder/egenskaper</c> yet, or a slow one, gets exactly this: the
    /// list in hand and the vocabulary still outstanding.
    /// </remarks>
    private sealed class StagedClient(params KildeSummary[] kilder) : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<KildeSummary>> _list = new();
        private readonly TaskCompletionSource<IReadOnlyList<PropertyMetadataEntry>> _vocabulary = new();
        private readonly TaskCompletionSource<KildeDetail?> _detail = new();

        /// <summary>
        /// How many detail fetches have been issued, which is the point of this counter rather
        /// than a detail of it: the question a deep link asks is whether the fetch was made at
        /// all while the vocabulary was still outstanding, and an unmade one and a made one that
        /// has not answered look identical on screen.
        /// </summary>
        public int DetailCalls { get; private set; }

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            _list.Task;

        public override Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(
            CancellationToken cancellationToken = default) =>
            _vocabulary.Task;

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DetailCalls++;

            return _detail.Task;
        }

        /// <summary>Land the list.</summary>
        public void AnswerList() => _list.TrySetResult(kilder);

        /// <summary>Land the vocabulary the catalogue serves today.</summary>
        public void AnswerVocabulary() => _vocabulary.TrySetResult(CatalogueVocabulary());

        /// <summary>Land the detail for the first kilde, which is the one a deep link opens here.</summary>
        public void AnswerDetail() => _detail.TrySetResult(Detail(kilder[0]));
    }

    /// <summary>Fails the list call, which is the API being down rather than the catalogue being empty.</summary>
    private sealed class FailingClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("the API is down");
    }

    /// <summary>
    /// Refuses the list call with the API's 429, which is neither the API being down nor the
    /// catalogue being empty — it is this reader having asked too often.
    /// </summary>
    private sealed class RateLimitedClient : EmptyMuninExplorerClient
    {
        public int Calls { get; private set; }

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default)
        {
            Calls++;

            throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
        }
    }

    private IRenderedComponent<KildeExplorer> RenderWith(
        IMuninExplorerClient client,
        Action<ComponentParameterCollectionBuilder<KildeExplorer>>? parameters = null)
    {
        Services.AddSingleton(client);

        return parameters is null ? Render<KildeExplorer>() : Render<KildeExplorer>(parameters);
    }

    private static IReadOnlyList<string> RowNames(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder tbody th button").Select(b => b.TextContent.Trim())];

    /// <summary>
    /// The facet headings on screen, in the order the panel draws them.
    /// </summary>
    /// <remarks>
    /// <c>h4</c> because the component's own title defaults to <c>h2</c>: the panel's heading is one
    /// level below it and a facet's is one below that. Selected as an element rather than by a class
    /// on purpose — the empty-facet assertions are about what is in the DOM, and a heading with no
    /// class would slip past a selector that asked for one.
    /// </remarks>
    private static IReadOnlyList<string> FacetHeadings(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-filters__facets [role=group] h4").Select(h => h.TextContent.Trim())];

    /// <summary>One facet's group, found by the heading over it.</summary>
    private static IElement Facet(IRenderedComponent<KildeExplorer> cut, string heading) =>
        cut.FindAll(".munin-explorer-filters__facets [role=group]")
           .Single(group => group.QuerySelector("h4")!.TextContent.Trim() == heading);

    /// <summary>The visible text of every choice in a facet, count and all.</summary>
    private static IReadOnlyList<string> Choices(IElement facet) =>
        [.. facet.QuerySelectorAll("label").Select(label => label.TextContent.Trim())];

    /// <summary>
    /// The <c>lang</c> on every choice in a facet, in the order they are drawn, with null for a
    /// choice carrying none.
    /// </summary>
    private static IReadOnlyList<string?> Languages(IElement facet) =>
        [.. facet.QuerySelectorAll("label").Select(label => label.GetAttribute("lang"))];

    /// <summary>
    /// Tick the choice whose visible text begins with <paramref name="choice"/>.
    /// </summary>
    /// <remarks>
    /// By prefix rather than by whole text because every choice carries its count — a test naming
    /// the value would otherwise have to name the number beside it, and would then break whenever a
    /// fixture gained a row that has nothing to do with what it is asserting.
    /// <para>
    /// The facet is looked up again on every call rather than held: ticking re-renders, and an
    /// element found before that belongs to the markup as it was.
    /// </para>
    /// </remarks>
    private static void Tick(IRenderedComponent<KildeExplorer> cut, string heading, string choice) =>
        Facet(cut, heading)
            .QuerySelectorAll("label")
            .First(label => label.TextContent.Trim().StartsWith(choice, StringComparison.Ordinal))
            .QuerySelector("input")!
            .Change(true);

    // ---------------------------------------------------------------------------------
    // The list.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenTheCatalogueHasThreeKilder_ThenAllThreeAreListed()
    {
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR"),
            Kilde("Reseptregisteret", "K_NORPD"));

        var cut = RenderWith(client);

        Assert.Equal(["Als registeret", "Dødsårsaksregisteret", "Reseptregisteret"], RowNames(cut));
    }

    [Fact]
    public void Render_WhenTheCatalogueIsEmpty_ThenItSaysSoRatherThanThrowing()
    {
        // The whole list arrives in one array, so "no kilder" is an empty array and not a page with
        // no items — there is no total to fall back on and nothing to page to. A component that
        // reached into the first row anyway would throw here rather than on helsedata's site.
        var cut = RenderWith(new FakeClient());

        Assert.Contains("Ingen kilder er registrert ennå.", cut.Markup);
        Assert.Empty(cut.FindAll(".munin-explorer-kilder"));
    }

    [Fact]
    public void Render_WhenTheListIsOnScreen_ThenTheCountSaysHowManyKilderAreInIt()
    {
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR"),
            Kilde("Reseptregisteret", "K_NORPD")));

        Assert.Contains("3 kilder", cut.Markup);
    }

    [Fact]
    public void Render_WhenOneKildeIsOnScreen_ThenTheCountIsNotWrittenInThePlural()
    {
        // "1 kilder" is the kind of thing that ships because the count was interpolated at the call
        // site. The plural belongs to the language, which is why Texts assembles the whole phrase.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        Assert.Contains("1 kilde", cut.Markup);
        Assert.DoesNotContain("1 kilder", cut.Markup);
    }

    [Fact]
    public void Render_Always_ThenTheListIsAskedForOnceAndUnfiltered()
    {
        // The endpoint is not paged and the list is small, so it is fetched whole and everything the
        // reader does afterwards happens over what is already in hand. Sending a search or a
        // kildetype would fetch a narrower list that the client-side filter would then narrow
        // again — and the facets count over this list.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS"));

        RenderWith(client);

        Assert.Equal(1, client.Calls);
        Assert.Null(client.LastSearch);
        Assert.Null(client.LastKildeType);
    }

    [Fact]
    public void Render_WhenTheListCannotBeFetched_ThenTheFailureIsReportedRatherThanThrown()
    {
        var cut = RenderWith(new FailingClient());

        var alert = cut.Find("[role=alert]");

        Assert.Contains("Kunne ikke laste kilder", alert.TextContent);
        // Not the empty state as well: the catalogue is not empty, it is unreachable, and saying
        // both would tell the reader two different things about the same blank screen.
        Assert.DoesNotContain("Ingen kilder er registrert", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheApiRateLimits_ThenTheReaderIsToldTheyAskedTooOftenAndNothingIsRetried()
    {
        // The kilde list is one call on load, so a reader meets the limiter here through the site
        // rather than through their own clicking — helsedata's cluster shares one address bucket.
        // Telling them the sources could not be loaded, and inviting a retry, aims them straight
        // back at it.
        var client = new RateLimitedClient();

        var cut = RenderWith(client);

        var alert = cut.Find("[role=alert]");

        Assert.Contains("for mange forespørsler", alert.TextContent);
        Assert.DoesNotContain("Kunne ikke laste kilder", alert.TextContent);
        Assert.DoesNotContain("Ingen kilder er registrert", cut.Markup);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Render_WhenTwoInstancesShareAPage_ThenTheirDomIdsDoNotCollide()
    {
        // Duplicate ids break label association and fail WCAG 4.1.1. helsedata can legitimately put
        // more than one explorer on a page.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var a = Render<KildeExplorer>();
        var b = Render<KildeExplorer>();

        var idA = a.Find(".searchbox__freetext").Id;
        var idB = b.Find(".searchbox__freetext").Id;

        Assert.False(string.IsNullOrWhiteSpace(idA));
        Assert.NotEqual(idA, idB);
    }

    // ---------------------------------------------------------------------------------
    // The columns.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Always_ThenTheTableHasKeldasDefaultVisibleColumnsInItsOwnOrder()
    {
        // Read off Munin's own Kelda rather than off this component: Navn, Status and Opprettet are
        // always visible there (kelda.tsx:61) and DEFAULT_VISIBLE (:86-100) turns on Kildetype,
        // Datasamlinger and Variabler. The set is asserted whole and in order, so promoting one of
        // Kelda's off-by-default columns back into this table cannot happen unnoticed again —
        // Dataansvarlig, Databehandler and Delkilder were here for a while and are three of the
        // seven Kelda keeps behind its column picker (Fhi.Metadata-ay3zz).
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        // The control columns in front are excluded on purpose: this asserts Kelda DEFAULT_VISIBLE
        // data columns, and the expand toggle and the checkbox are neither.
        var headers = cut
            .FindAll(".munin-explorer-kilder thead th:not(.munin-explorer-kilder__expand):not(.munin-explorer-kilder__select)")
            .Select(th => th.TextContent.Trim());

        Assert.Equal(
        [
            "Navn",
            "Kildetype",
            "Status",
            "Datasamlinger",
            "Variabler",
            "Opprettet",
        ], headers);
    }

    [Fact]
    public void Render_Always_ThenARowCarriesAValueForEveryColumnItHasOne()
    {
        var cut = RenderWith(new FakeClient(Kilde(
            "Dødsårsaksregisteret", "K_DAR",
            shortName: "DÅR",
            kildetype: "sentraltHelseregister",
            datasamlinger: 7,
            variables: 312,
            established: "1951")));

        var row = cut.Find(".munin-explorer-kilder tbody tr");
        // The expand control is skipped, as in the header assertion above: it is a control column
        // and not one of the data columns this pins.
        var cells = row
            .QuerySelectorAll("th, td:not(.munin-explorer-kilder__expand)")
            .Select(c => c.TextContent.Trim())
            .ToList();

        // The name cell carries the code under the name, the way Kelda does: it is how a reader who
        // knows K_DAR finds the row whose name they do not know.
        Assert.StartsWith("Dødsårsaksregisteret", cells[0]);
        Assert.Contains("K_DAR", cells[0]);

        Assert.Equal("Sentralt helseregister", cells[1]);
        Assert.Equal("Aktiv", cells[2]);
        Assert.Equal("7", cells[3]);
        Assert.Equal("312", cells[4]);
        Assert.Equal("1951", cells[5]);
    }

    // A kilde with one datasamling of its own and one hanging off a delkilde, which is the shape
    // that tells a flattened panel from a grouped one.
    private static KildeDetail DetailWithCollections(KildeSummary summary) =>
        Detail(summary) with
        {
            Datasamlinger = [Collection("Hoveddatasamling")],
            Delkilder =
            [
                new() { Name = "Bølge 4", Datasamlinger = [Collection("Bølge 4 - serie 49")] }
            ]
        };

    private static KildeDatasamling Collection(string name) =>
        new() { Name = name, VariableCount = 12 };

    private static IElement ExpandToggle(IRenderedComponent<KildeExplorer> cut, string kilde) =>
        cut.FindAll(".munin-explorer-kilder tbody tr")
           .First(row => row.TextContent.Contains(kilde, StringComparison.Ordinal))
           .QuerySelector(".munin-explorer-kilder__expand-toggle")!;

    [Fact]
    public void Render_WhenAKildeHasNoDatasamlinger_ThenItHasNoExpandToggle()
    {
        // Kelda draws no toggle where there is nothing to open (canExpand = datasamlingCount > 0),
        // and a control that expands to an empty panel is worse than no control.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", datasamlinger: 3),
            Kilde("Tomt register", "K_TOM", datasamlinger: 0)));

        var rows = cut.FindAll(".munin-explorer-kilder tbody tr");

        Assert.NotNull(rows[0].QuerySelector(".munin-explorer-kilder__expand-toggle"));
        Assert.Null(rows[1].QuerySelector(".munin-explorer-kilder__expand-toggle"));
    }

    [Fact]
    public void Render_WhenTheToggleIsPressed_ThenTheRowOpensOnItsDatasamlingerGrouped()
    {
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var cut = RenderWith(new FakeClient(als).Describing(DetailWithCollections(als)));

        ExpandToggle(cut, "Als registeret").Click();

        var panel = cut.Find(".munin-explorer-kilder__expanded");

        Assert.Contains("Hoveddatasamling", panel.TextContent);
        Assert.Contains("Bølge 4 - serie 49", panel.TextContent);

        // The panel names itself and the groups hang under it, so a group is never announced as a
        // peer of the filter panel and the kilde's own datasamlinger are not left unowned.
        Assert.Equal("Datasamlinger for Als registeret", panel.QuerySelector("h3")!.TextContent);

        var heading = panel.QuerySelector("h4.munin-explorer-kilde__delkilde-name")!;

        Assert.Equal("Bølge 4", heading.TextContent);
        Assert.Equal(2, panel.QuerySelectorAll("table.munin-explorer-kilde__datasamlinger").Length);
    }

    [Fact]
    public void Render_WhenADelkildeIsNested_ThenItsDatasamlingerAreDrawnToo()
    {
        // The count that decides whether a toggle is drawn at all includes datasamlinger under
        // delkilder at any depth, so stopping at the first level opens a row on nothing while the
        // Datasamlinger column still promises them (KildeDetail.cs: "walk it recursively").
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 1);
        var detail = Detail(als) with
        {
            Delkilder =
            [
                new()
                {
                    Name = "Bølge 4",
                    Children = [new() { Name = "Serie 49", Datasamlinger = [Collection("Dypt nede")] }]
                }
            ]
        };

        var cut = RenderWith(new FakeClient(als).Describing(detail));

        ExpandToggle(cut, "Als registeret").Click();

        var panel = cut.Find(".munin-explorer-kilder__expanded");

        Assert.Contains("Dypt nede", panel.TextContent);

        // A step deeper in the outline than a first-level delkilde, which is what says it is inside
        // one rather than beside it.
        Assert.Equal("Serie 49", panel.QuerySelector("h5.munin-explorer-kilde__delkilde-name")!.TextContent);
    }

    [Fact]
    public void Render_WhenTheKildeHasNoDatasamlingerAfterAll_ThenTheRowSaysSoRatherThanOpeningBlank()
    {
        // The toggle is drawn from the list endpoint's count and the panel is filled from a second,
        // separate request; they can disagree. An empty panel with an empty status line says
        // nothing at all, which is the state this branch's other tests exist to prevent.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 3);
        var cut = RenderWith(new FakeClient(als).Describing(Detail(als)));

        ExpandToggle(cut, "Als registeret").Click();

        var panel = cut.Find(".munin-explorer-kilder__expanded");

        Assert.Equal("Ingen datasamlinger registrert", panel.QuerySelector("p[role=status]")!.TextContent);
        Assert.Empty(panel.QuerySelectorAll("table"));
    }

    [Fact]
    public void Render_WhenTheDatasamlingerArrive_ThenTheLiveRegionSaysHowMany()
    {
        // A region that empties on success never announces the one outcome the reader pressed for.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var cut = RenderWith(new FakeClient(als).Describing(DetailWithCollections(als)));

        ExpandToggle(cut, "Als registeret").Click();

        var status = cut.Find(".munin-explorer-kilder__expanded p[role=status]");

        Assert.Equal("2 datasamlinger", status.TextContent);
        Assert.Equal("polite", status.GetAttribute("aria-live"));
    }

    [Fact]
    public void Render_WhenTheCatalogueCuratesAnOrder_ThenThePanelFollowsItAsTheDrilldownDoes()
    {
        // DatasamlingTable was shared so the two views agree about the same kilde; order is part of
        // that agreement, and KildeView sorts every level by PresentationOrder then by name.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 3);
        var detail = Detail(als) with
        {
            Datasamlinger =
            [
                Collection("Siste") with { PresentationOrder = 3 },
                Collection("Første") with { PresentationOrder = 1 },
                Collection("Midten") with { PresentationOrder = 2 }
            ]
        };

        var cut = RenderWith(new FakeClient(als).Describing(detail));

        ExpandToggle(cut, "Als registeret").Click();

        var names = cut.FindAll(".munin-explorer-kilder__expanded tbody th").Select(c => c.TextContent);

        Assert.Equal(["Første", "Midten", "Siste"], names);
    }

    [Fact]
    public void Render_WhenTheToggleIsPressed_ThenItSaysSoToAScreenReader()
    {
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var cut = RenderWith(new FakeClient(als).Describing(DetailWithCollections(als)));

        var toggle = ExpandToggle(cut, "Als registeret");

        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal("Vis datasamlinger for Als registeret", toggle.GetAttribute("aria-label"));

        // No aria-controls while closed: the panel is not in the DOM, so the IDREF would point at
        // nothing at all rather than at a collapsed region.
        Assert.Null(toggle.GetAttribute("aria-controls"));

        toggle.Click();

        var opened = ExpandToggle(cut, "Als registeret");

        Assert.Equal("true", opened.GetAttribute("aria-expanded"));
        Assert.Equal("Skjul datasamlinger for Als registeret", opened.GetAttribute("aria-label"));

        // aria-controls has to name the panel that actually arrived, or it points at nothing.
        Assert.Equal(
            opened.GetAttribute("aria-controls"),
            cut.Find(".munin-explorer-kilder__expanded").GetAttribute("id"));
    }

    [Fact]
    public void Render_WhenAFacetIsTickedWhileARowIsOpen_ThenTheSameKildeIsStillTheOpenOne()
    {
        // THE TRAP this bead names, and it names the facet path specifically. An earlier version of
        // this test filtered with the search box instead — which passes even when a facet tick
        // throws the expansion away, because Choose is a different handler entirely.
        var dar = Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "sentraltHelseregister", datasamlinger: 1);
        var als = Kilde("Als registeret", "K_ALS", kildetype: "biobank", datasamlinger: 2);
        var client = new FakeClient(dar, als).Describing(DetailWithCollections(als));
        var cut = RenderWith(client);

        ExpandToggle(cut, "Als registeret").Click();

        Assert.Contains("Hoveddatasamling", cut.Find(".munin-explorer-kilder__expanded").TextContent);

        // Ticking removes the row ABOVE the open one, so an expansion held by position would shift
        // the panel onto its neighbour or off the end of the list.
        Tick(cut, "Kildetype", "Biobank");

        var rows = cut.FindAll(".munin-explorer-kilder tbody tr");
        var panel = cut.Find(".munin-explorer-kilder__expanded");

        // Still open, still the same kilde, and still holding that kilde's datasamlinger.
        Assert.Contains("Als registeret", rows[0].TextContent);
        Assert.Equal("true", ExpandToggle(cut, "Als registeret").GetAttribute("aria-expanded"));
        Assert.Contains("Hoveddatasamling", panel.TextContent);
    }

    [Fact]
    public void Render_WhenARowIsOpenedTwice_ThenTheDatasamlingerAreFetchedOnce()
    {
        // Cached per kilde, the way Kelda caches it. Without this every open is a request for
        // something the component already has.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var client = new FakeClient(als).Describing(DetailWithCollections(als));
        var cut = RenderWith(client);

        ExpandToggle(cut, "Als registeret").Click();
        var afterFirst = client.DetailCalls;

        ExpandToggle(cut, "Als registeret").Click();   // collapse
        ExpandToggle(cut, "Als registeret").Click();   // and open it again

        Assert.Equal(1, afterFirst);
        Assert.Equal(afterFirst, client.DetailCalls);
        Assert.Contains("Hoveddatasamling", cut.Find(".munin-explorer-kilder__expanded").TextContent);
    }

    [Fact]
    public async Task Render_WhenARowIsReopenedWhileItsFetchIsInFlight_ThenNoSecondRequestIsMade()
    {
        // The cache is keyed on the answer, which does not exist yet while the first call is out —
        // so without treating an in-flight load as cached, a quick collapse and re-expand pays for
        // the same kilde twice against a rate limit this client already has to apologise for.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var client = new FakeClient(als).Describing(DetailWithCollections(als));
        var cut = RenderWith(client);

        client.StallDetail = true;

        ExpandToggle(cut, "Als registeret").Click();   // starts the fetch
        ExpandToggle(cut, "Als registeret").Click();   // collapse, fetch still out
        ExpandToggle(cut, "Als registeret").Click();   // and open it again

        Assert.Equal(1, client.DetailCalls);
        Assert.Equal(1, client.Stalls);

        await cut.InvokeAsync(() => client.AnswerStalled(DetailWithCollections(als)));

        // The first answer still lands, because nothing newer was started to supersede it.
        Assert.Contains("Hoveddatasamling", cut.Find(".munin-explorer-kilder__expanded").TextContent);
        Assert.Equal(1, client.DetailCalls);
    }

    [Fact]
    public void Render_WhenTheFetchFails_ThenTheRowSaysSoRatherThanOpeningEmpty()
    {
        // An open panel with nothing in it reads as "this kilde has no datasamlinger", which is a
        // different fact from "we could not fetch them" — and the row's own count says otherwise.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var client = new FakeClient(als).Describing(DetailWithCollections(als));

        client.FailDetail = true;

        var cut = RenderWith(client);

        ExpandToggle(cut, "Als registeret").Click();

        var status = cut.Find(".munin-explorer-kilder__expanded p[role=status]");

        // The same live region the drilldown uses, so a failure that arrives after the press is
        // announced rather than only drawn.
        Assert.Equal("polite", status.GetAttribute("aria-live"));
        Assert.Contains("infobox", status.ClassName ?? "");
        Assert.Empty(cut.Find(".munin-explorer-kilder__expanded").QuerySelectorAll("table"));
    }

    [Fact]
    public async Task Render_WhileTheDatasamlingerAreStillComing_ThenTheOpenRowSaysSoRatherThanSittingEmpty()
    {
        // "No datasamlinger" and "still coming" are different facts, and the row's own count says
        // which. Stalled on purpose: with a fake that answers at once the loading state has no
        // window to be seen in, and a test of it passes for the wrong reason.
        var als = Kilde("Als registeret", "K_ALS", datasamlinger: 2);
        var client = new FakeClient(als).Describing(DetailWithCollections(als));
        var cut = RenderWith(client);

        client.StallDetail = true;

        ExpandToggle(cut, "Als registeret").Click();

        var waiting = cut.Find(".munin-explorer-kilder__expanded");

        Assert.Equal("Henter datakilden …", waiting.TextContent.Trim());
        Assert.Empty(waiting.QuerySelectorAll("table"));
        Assert.Equal("status", waiting.QuerySelector("p")!.GetAttribute("role"));

        await cut.InvokeAsync(() => client.AnswerStalled(DetailWithCollections(als)));

        var arrived = cut.Find(".munin-explorer-kilder__expanded");

        Assert.Contains("Hoveddatasamling", arrived.TextContent);
        Assert.Equal(2, arrived.QuerySelectorAll("table").Length);
    }

    [Fact]
    public void Render_WhenAKildeNoLongerCollectsData_ThenTheStatusColumnSaysSo()
    {
        // A kilde kept for historical reference is still in the list. Hiding the distinction would
        // let a reader take a closed register for an open one.
        var cut = RenderWith(new FakeClient(Kilde("Gammelt register", "K_OLD", active: false)));

        Assert.Contains("Passiv", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheKildeHasNoFoundingYear_ThenTheCellSaysSoRatherThanGoingBlank()
    {
        // The ordinary case, not the edge case: which keys the property bag carries varies per
        // kilde and per environment, so an Opprettet column built only against a kilde that has one
        // renders a blank cell — or throws — for the ones that do not. "Ikke oppgitt" rather than
        // Kelda's em dash, because that is what every other empty cell in this table says.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cells = cut.FindAll(".munin-explorer-kilder tbody td:not(.munin-explorer-kilder__expand)").Select(c => c.TextContent.Trim()).ToList();

        Assert.Equal("Ikke oppgitt", cells[4]);
    }

    [Fact]
    public void Render_WhenTheCatalogueStatesAFoundingYear_ThenTheCellShowsItVerbatim()
    {
        // Kelda renders this one through no date formatter at all, and neither does this: the
        // import file holds a '2916' typo, a '1900' and a literal '0', and showing them as they
        // stand is what gets them fixed at source. A formatter would blank them or invent a day.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", established: "1994"),
            Kilde("Kreftregisteret", "K_KREG", established: "2916")));

        var cells = cut.FindAll(".munin-explorer-kilder tbody td:not(.munin-explorer-kilder__expand)").Select(c => c.TextContent.Trim()).ToList();

        Assert.Equal("1994", cells[4]);
        Assert.Equal("2916", cells[9]);
    }

    [Fact]
    public void Render_Always_ThenOpprettetIsTheFoundingYearAndNotWhenMuninRegisteredTheKilde()
    {
        // The whole of Fhi.Metadata-bc4x1. KildeSummary.Created is JSON "opprettet" too, and binding
        // this column to it reproduces Kelda's header over Kelda's Importert data — which Kelda
        // demoted out of this slot and keeps off by default. Same word, different fact, and any
        // assertion that only reads the header text passes either way.
        var kilde = Kilde("Als registeret", "K_ALS", established: "1994") with
        {
            Created = new DateTimeOffset(2026, 5, 19, 12, 58, 37, TimeSpan.Zero),
        };

        var cut = RenderWith(new FakeClient(kilde));

        var row = cut.Find(".munin-explorer-kilder tbody tr");

        Assert.Equal("1994", row.QuerySelectorAll("td")[^1].TextContent.Trim());
        Assert.DoesNotContain("2026", row.TextContent);
    }

    [Fact]
    public void Render_WhenTheListIsTheCapturedPayload_ThenTheColumnShowsTheYearsTheApiSent()
    {
        // The one Opprettet test that does not write the key it reads: Properties() spells it the
        // way the component looks it up, so the rest pass just as well against a key the API never
        // sends and a column of "Ikke oppgitt" ships. These years are the captured payload's own.
        var kilder = JsonSerializer.Deserialize<IReadOnlyList<KildeSummary>>(
                TestData.Read("kilder.json"), MuninExplorerClient.Json)
            ?? throw new InvalidOperationException("kilder.json no longer reads as a kilde list.");

        var cut = RenderWith(new FakeClient([.. kilder]));

        var years = cut.FindAll(".munin-explorer-kilder tbody tr")
            .Select(row => row.QuerySelectorAll("td")[^1].TextContent.Trim());

        Assert.Equal(["2023", "2006", "2020"], years);
    }

    [Fact]
    public void Render_Always_ThenTheResultsAreATableAndTheNameIsAButton()
    {
        // The shape rule, pinned. Neither Stiler nor helsedata's own stylesheets have a kilde list
        // to read a shape back off, so the markup leans on elements that dress themselves: an
        // unstyled table still lines its columns up and an unstyled <button> still looks and
        // behaves like a control, where a class name nobody defines renders as nothing at all.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var name = cut.Find(".munin-explorer-kilder tbody th button");

        Assert.Equal("BUTTON", name.TagName);
        Assert.Equal("button", name.GetAttribute("type"));
    }

    // ---------------------------------------------------------------------------------
    // Searching, which never leaves the browser.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Search_WhenTheUserTypesInTheField_ThenNoRoundTripIsMade()
    {
        // The regression guard the whole search design exists for. value + @oninput means one
        // round-trip per keystroke on helsedata's Blazor Server circuit whatever the handler does
        // with it, and the re-render each one triggers rewrites the element while more input is
        // still arriving — "svelging" arrived as "sng" the last time. No registered oninput handler
        // means the browser event never reaches the circuit, and bUnit says so by refusing to
        // dispatch it.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS"));
        var cut = RenderWith(client);

        var input = cut.Find(".searchbox__freetext");

        Assert.Throws<MissingEventHandlerException>(() => input.Input("als"));
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Search_WhenATermIsEntered_ThenTheListNarrowsWithoutAskingTheApiAgain()
    {
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR"),
            Kilde("Norsk pasientregister", "K_NPR"));

        var cut = RenderWith(client);

        // Two of the three survive, not one: a filter that narrowed to a single row would look the
        // same as a lookup, and this is a filter.
        cut.Find(".searchbox__freetext").Change("registeret");
        cut.Find("form").Submit();

        Assert.Equal(["Als registeret", "Dødsårsaksregisteret"], RowNames(cut));

        // The trap. A component that sent the term to the API would satisfy the line above and
        // still be the implementation this design exists to avoid: the list is fetched once, and
        // searching is a filter over what is already here.
        Assert.Equal(1, client.Calls);
        Assert.Null(client.LastSearch);
    }

    [Fact]
    public void Search_WhenTheTermIsTheCode_ThenTheKildeIsFound()
    {
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Reseptregisteret", "K_NORPD")));

        cut.Find(".searchbox__freetext").Change("norpd");

        Assert.Equal(["Reseptregisteret"], RowNames(cut));
    }

    [Fact]
    public void Search_WhenTheTermIsTheShortName_ThenTheKildeIsFound()
    {
        // The third of the three fields Kelda matches on, and the one a reader is most likely to
        // know without knowing either of the others.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", shortName: "ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR", shortName: "DÅR")));

        cut.Find(".searchbox__freetext").Change("dår");

        Assert.Equal(["Dødsårsaksregisteret"], RowNames(cut));
    }

    [Fact]
    public void Search_WhenTheTermIsInAnotherCase_ThenItStillMatches()
    {
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        cut.Find(".searchbox__freetext").Change("ALS REGISTERET");

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Search_WhenNothingMatches_ThenTheEmptyStateNamesWhatWasSearchedFor()
    {
        // A different sentence from the one an empty catalogue gets: this one tells the reader the
        // catalogue has kilder and that their words were the problem, which is the difference
        // between trying again and giving up.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        cut.Find(".searchbox__freetext").Change("hjortedyr");

        Assert.Contains("Ingen kilder samsvarer med søket «hjortedyr»", cut.Markup);
        Assert.Empty(cut.FindAll(".munin-explorer-kilder"));
    }

    [Fact]
    public void ClearSearch_WhenThereIsASearch_ThenTheControlIsInsideTheFieldAheadOfTheSubmit()
    {
        // The matched half of the variable explorer's. These two were built as a pair by 5ghur and
        // half of this shipped would leave the two explorers disagreeing about the same control on
        // the same page. (Fhi.Metadata-ag4n7)
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        cut.Find(".searchbox__freetext").Change("als");

        var controls = cut.Find(".searchbox__freetext-container").QuerySelectorAll("button");

        // DOM order and identity, not the order of tokens inside a class attribute: reshuffling
        // those changes nothing a reader can tell and must not fail this.
        Assert.Equal(2, controls.Length);
        Assert.True(controls[0].ClassList.Contains("munin-explorer-search__clear"),
                    "The clear control is not the first button inside the field.");
        Assert.True(controls[1].ClassList.Contains("searchbox__freetext-submit-button"),
                    "The submit button is not the second button inside the field.");

        Assert.Equal("text", cut.Find(".searchbox__freetext").GetAttribute("type"));
        Assert.Equal("Tøm søket", AccessibleName.Of(cut.Find(".munin-explorer-search__clear")));
    }

    [Fact]
    public void ClearSearch_WhenPressed_ThenFocusGoesToTheFieldRatherThanTheDocument()
    {
        // Same reason as next door: the control takes itself off the page as it acts, so without
        // this the reader's focus lands on <body> and their next Tab starts at the top of the
        // host's page. (Fhi.Metadata-ag4n7)
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        cut.Find(".searchbox__freetext").Change("als");
        cut.Find(".munin-explorer-search__clear").Click();

        JSInterop.VerifyInvoke("Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void ClearSearch_WhenThereIsNoSearch_ThenThereIsNoControlAtAll()
    {
        // Inside the field now, so it is drawn only when it has something to do — an x sitting in
        // a box with nothing in it is an invitation to press something inert. The greyed
        // always-present button this replaces was the right answer while it stood OUTSIDE the
        // field, where its coming and going would have shifted the row. (Fhi.Metadata-ag4n7)
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Norsk pasientregister", "K_NPR"));

        var cut = RenderWith(client);

        Assert.Empty(cut.FindAll(".munin-explorer-search__clear"));
    }

    [Fact]
    public void ClearSearch_WhenPressed_ThenTheWholeListIsBackWithoutTypingOrEnter()
    {
        // The control that replaces the user-agent ✕. That one emptied the box without applying
        // it, so the reader was left with a search still in force behind a box reading empty, and
        // everything downstream - velg-alle, Nullstill utvalg, the handover - then worked on rows
        // they believed they had cleared. Reported 2026-08-27. This asserts the whole round trip:
        // the button appears with a search, one press restores the list, and it goes away again.
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Norsk pasientregister", "K_NPR"));

        var cut = RenderWith(client);

        cut.Find(".searchbox__freetext").Change("als");

        Assert.Equal(["Als registeret"], RowNames(cut));

        cut.Find(".munin-explorer-search__clear").Click();

        Assert.Equal(["Als registeret", "Norsk pasientregister"], RowNames(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-search__clear"));

        // The box on screen has to agree with the list under it - that is the whole bug.
        Assert.Equal(string.Empty, cut.Find(".searchbox__freetext").GetAttribute("value") ?? string.Empty);

        // Still the one fetch from initialisation: clearing a client-side filter is not a reload.
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Search_WhenTheTermIsCleared_ThenTheWholeListComesBackWithoutARefetch()
    {
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Reseptregisteret", "K_NORPD"));

        var cut = RenderWith(client);

        cut.Find(".searchbox__freetext").Change("als");
        cut.Find(".searchbox__freetext").Change("");

        Assert.Equal(["Als registeret", "Reseptregisteret"], RowNames(cut));
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Render_WhenTheHostSetsTheSearch_ThenTheListOpensNarrowed()
    {
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Reseptregisteret", "K_NORPD"));

        var cut = RenderWith(client, b => b.Add(c => c.Search, "resept"));

        Assert.Equal(["Reseptregisteret"], RowNames(cut));
        Assert.Equal(1, client.Calls);
    }

    // ---------------------------------------------------------------------------------
    // Opening a kilde.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Select_WhenAKildeIsChosen_ThenKildeViewRendersThatKilde()
    {
        var als = Kilde("Als registeret", "K_ALS", shortName: "ALS");
        var client = new FakeClient(als, Kilde("Reseptregisteret", "K_NORPD")).Publishing(als);

        var cut = RenderWith(client);

        cut.FindAll(".munin-explorer-kilder tbody th button")[0].Click();

        var view = cut.FindComponent<KildeView>();

        Assert.Equal(als.Id, view.Instance.Kilde?.Id);
        Assert.Equal(1, client.DetailCalls);

        // The list is gone rather than sitting under it: with no router the kilde is a view this
        // component swaps to, and the reader gets the full width to read in.
        Assert.Empty(cut.FindAll(".munin-explorer-kilder"));
    }

    [Fact]
    public void Select_WhenAKildeIsChosen_ThenKildeViewIsGivenItsSectionsThroughTheParameter()
    {
        // The trap this test exists for. KildeView is a shared core with slots precisely so that
        // Kelda's own sections go INTO it rather than being added to it — an implementation that
        // put Kelda-specific markup inside that component would satisfy any assertion that only
        // looked for text on screen, and would take down the separation the component is built to
        // hold up. So the assertion is on the parameter as well as on the output.
        //
        // It is no longer the host's fragment by reference: Kelda's own three sections are markup
        // in this component, and what reaches the core is those plus whatever the host passed. The
        // host's own is still asserted, because a composition that dropped it would otherwise read
        // exactly like one that never had it.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        RenderFragment sections = builder => builder.AddMarkupContent(0, "<p>Fra verten</p>");

        var cut = RenderWith(client, b => b.Add(c => c.Sections, sections));

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var view = cut.FindComponent<KildeView>();

        Assert.NotNull(view.Instance.Sections);
        Assert.Contains("Fra verten", cut.Markup);

        // Kelda's own, in the same slot. The datasamling section is not one of them: the core
        // draws it and reads its heading off the source (Fhi.Metadata-rhybi).
        Assert.Contains("Kriterier for tilgang til data", cut.Markup);
        Assert.Contains("Priser", cut.Markup);
    }

    [Fact]
    public void Select_WhenTheHostPassesNoSections_ThenKeldasOwnAreStillThere()
    {
        // The sections are the component's, not the host's: an embedding that passes nothing gets
        // the same kilde page as one that passes something. Worth its own test because the natural
        // way to write the composition — pass the host's fragment when there is one — reads as
        // correct and leaves a kilde with three sections missing whenever a host stays silent.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.NotNull(cut.FindComponent<KildeView>().Instance.Sections);
        Assert.Contains("Kriterier for tilgang til data", cut.Markup);
    }

    [Fact]
    public void Select_WhenAKildeIsChosen_ThenKeldaNamesNoHeadingForTheDatasamlinger()
    {
        // Kelda used to pass "Delkilder og datasamlinger" over every source, delkilder or not, and
        // 61 of the 66 sources the API serves have none (Fhi.Metadata-rhybi). The word belongs to
        // the source, so the core reads it there — passing nothing is what lets it.
        //
        // The heading that results is asserted on the rendered page in KildeSectionsTest, on the
        // captured payloads: this source is hand-written and carries no delkilder either way,
        // which is how the wrong heading stayed invisible here.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Null(cut.FindComponent<KildeView>().Instance.DataCollectionsHeading);
    }

    [Fact]
    public void Select_WhenTheDrilldownIsOpen_ThenTheWayBackIsBlueRatherThanAPlainGhost()
    {
        // The plain ghost has no border and no background until :hover, so it reads as bold text
        // to a reader without a mouse. --ghost-blue is Stiler's own and says link. (Fhi.Metadata-l9l2n.34)
        var als = Kilde("Als registeret", "K_ALS");

        var cut = RenderWith(new FakeClient(als).Publishing(als));

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var classes = cut.Find(".munin-explorer-drilldown button").ClassList;

        Assert.Contains("button-square--ghost-blue", classes);
        Assert.DoesNotContain("button-square--ghost", classes);
    }

    [Fact]
    public void Select_WhenTheReaderGoesBack_ThenTheListIsThereAsTheyLeftIt()
    {
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als, Kilde("Reseptregisteret", "K_NORPD")).Publishing(als);

        var cut = RenderWith(client);

        cut.Find(".searchbox__freetext").Change("als");
        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();

        // The search survives, because nothing was torn down and nothing was refetched.
        Assert.Equal(["Als registeret"], RowNames(cut));
        Assert.Equal(1, client.Calls);
        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void Select_WhenTheCatalogueDoesNotPublishTheKilde_ThenTheViewSaysSoRatherThanThrowing()
    {
        // Null from the client is "no such published kilde", which is not a fault — an id in a URL
        // somebody edited is a normal event on a public page.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS"));

        var cut = RenderWith(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Contains("Fant ingen detaljer for denne datakilden.", cut.Markup);
        Assert.Empty(cut.FindComponents<KildeView>());
    }

    [Fact]
    public void Select_WhenTheDetailFetchFails_ThenItSaysSoRatherThanEscapingTheHandler()
    {
        // Two things at once. An exception out of a Blazor Server event handler tears down the
        // circuit for helsedata's whole CMS page rather than for this component, so the fetch has
        // to be caught where it is awaited. And the sentence has to stay the API's rather than the
        // catalogue's: "kunne ikke hente" is a fault worth trying again after, where "fant ingen
        // detaljer" tells the reader there is nothing to come back for. Only the second of those
        // had a test, so swapping the two — or collapsing them onto one — was invisible.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        client.FailDetail = true;
        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Equal("Kunne ikke hente datakilden nå. Prøv igjen om litt.", status.TextContent.Trim());
        Assert.Equal("infobox infobox--bg-yellow", status.GetAttribute("class"));
        Assert.DoesNotContain("Fant ingen detaljer", cut.Markup);
        Assert.Empty(cut.FindComponents<KildeView>());
    }

    [Fact]
    public void Select_WhenTheDetailFetchIsRateLimited_ThenItSaysTheReaderAskedTooOften()
    {
        // The list arrives and the detail is refused, which is what a reader opening one kilde after
        // another meets: the catalogue is up, and they have asked too often. All three answers this
        // status line can carry have to stay apart — "kunne ikke hente" invites the retry the
        // limiter is counting, and "fant ingen detaljer" says there is nothing to come back for.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        client.RateLimitDetail = true;
        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Contains("for mange forespørsler", status.TextContent);
        Assert.Equal("infobox infobox--bg-yellow", status.GetAttribute("class"));
        Assert.DoesNotContain("Kunne ikke hente datakilden", cut.Markup);
        Assert.DoesNotContain("Fant ingen detaljer", cut.Markup);
        Assert.Empty(cut.FindComponents<KildeView>());

        // Nothing asks again by itself: one click, one request.
        Assert.Equal(1, client.DetailCalls);
    }

    [Fact]
    public async Task Select_WhenAReopenedKildesAbandonedFetchIsRateLimited_ThenItIsNotReportedInTheNewView()
    {
        // The generation guard on the 429 path. It is written once for both failures now, but this
        // pins the throttled sentence specifically: a fetch the reader has already left must not put
        // a warning box over a kilde that loaded perfectly.
        //
        // Ordering as in the generic test below: the abandoned fetch is refused only after the
        // owning fetch has landed, because DetailStatus reads the loading flag before the error and
        // a stale error behind "Henter datakilden …" is invisible to any assertion on the view.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        client.StallDetail = true;
        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();

        client.StallDetail = false;
        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);

        await cut.InvokeAsync(client.RateLimitStalled);

        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Equal(string.Empty, status.TextContent.Trim());
        Assert.Equal("caption", status.GetAttribute("class"));
        Assert.DoesNotContain("for mange forespørsler", cut.Markup);
        Assert.Equal("false", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));
        Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);
    }

    [Fact]
    public async Task Select_WhenTheReaderGoesBackBeforeTheDetailArrives_ThenTheLateAnswerIsDropped()
    {
        // What the fetch's generation counter is for. Without it the answer to a fetch nobody is
        // waiting for any more writes itself into a component that is showing the list again —
        // on helsedata, a drilldown re-opening itself over the list after the reader pressed Back.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        client.StallDetail = true;
        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();

        Assert.Equal(1, client.Stalls);

        await cut.InvokeAsync(() => client.AnswerStalled(Detail(als)));

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Empty(cut.FindComponents<KildeView>());
        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public async Task Select_WhenAReopenedKildesAbandonedFetchAnswers_ThenItDoesNotStandInForTheNewOne()
    {
        // Closing a kilde and opening the same one again is two fetches carrying one id, so a guard
        // written on the id rather than on the generation would let the first — already thrown
        // away — answer for the second: the view would stop saying it was loading, and show a
        // detail fetched before the reader's second click, while the fetch that owns it runs on.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        client.StallDetail = true;
        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();
        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Equal(2, client.Stalls);

        await cut.InvokeAsync(() => client.AnswerStalled(Detail(als)));

        Assert.Equal("true", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));
        Assert.Empty(cut.FindComponents<KildeView>());

        // And the fetch that does own the view still gets to fill it.
        await cut.InvokeAsync(() => client.AnswerStalled(Detail(als)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));
            Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);
        });
    }

    [Fact]
    public async Task Select_WhenAReopenedKildesAbandonedFetchFails_ThenItsFailureIsNotReportedInTheNewView()
    {
        // The same guard on the other path out of the fetch. A failure belonging to a request the
        // reader has already left is a warning box for a fetch that never had anything to do with
        // what is on screen — here, over a kilde that loaded perfectly.
        //
        // The abandoned fetch is failed *after* the one that owns the view has landed, and that
        // ordering is the whole test. Failing it while the owning fetch is still in flight proves
        // nothing about this guard: DetailStatus reads the loading flag before the error, so a
        // stale _detailError sits behind "Henter datakilden …" where no assertion on the rendered
        // view can see it, and the guard can be deleted with the suite still green. Only with the
        // loading flag down does the error reach the status line and its warning class.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        client.StallDetail = true;
        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();

        // Reopened, and answered straight away this time, so the view is settled and not loading.
        client.StallDetail = false;
        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);

        await cut.InvokeAsync(client.FailStalled);

        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Equal(string.Empty, status.TextContent.Trim());
        Assert.Equal("caption", status.GetAttribute("class"));
        Assert.DoesNotContain("Kunne ikke hente datakilden", cut.Markup);
        Assert.Equal("false", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));
        Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);
    }

    [Fact]
    public void Select_WhenTheHostBindsTheSelection_ThenItIsToldWhichKildeIsOpenAndWhenItCloses()
    {
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var reported = new List<Guid?>();

        var cut = RenderWith(client, b => b.Add(
            c => c.SelectedKildeIdChanged, EventCallback.Factory.Create<Guid?>(this, reported.Add)));

        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();

        Assert.Equal([als.Id, null], reported);
    }

    [Fact]
    public async Task Select_WhenTheHostHandlesTheSelectionAsynchronously_ThenTheOpenViewIsLoadingBeforeTheFetchStarts()
    {
        // The render between the click and the fetch, where the open view once said aria-busy
        // "false" over an empty status line for a request not yet issued. No other test reaches it:
        // the callbacks here are synchronous, so RaiseAsync never yields. (Fhi.Metadata-74cbp)
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        // RunContinuationsAsynchronously so landing it here resumes SelectAsync the way a real
        // host's callback does, rather than inline on the thread that completed it.
        var host = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var cut = RenderWith(client, b => b.Add(
            c => c.SelectedKildeIdChanged, EventCallback.Factory.Create<Guid?>(this, _ => host.Task)));

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var region = cut.Find(".munin-explorer-drilldown");

        Assert.Equal(0, client.DetailCalls);
        Assert.Equal("true", region.GetAttribute("aria-busy"));
        Assert.Equal(
            "Henter datakilden …",
            cut.Find(".munin-explorer-drilldown p[role=status]").TextContent.Trim());

        // And the settled view, so the fix reads as "busy until it lands" rather than "busy always".
        await cut.InvokeAsync(host.SetResult);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));
            Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);
        });
    }

    [Fact]
    public async Task Select_WhenTheReaderGoesBackBeforeTheHostHasHandledTheSelection_ThenNoDetailIsFetched()
    {
        // Back is drawn inside the drilldown while it loads, so it is clickable in the gap an
        // asynchronous host leaves — and the fetch resuming after that gap used to reopen the kilde
        // the reader had just closed. With a synchronous callback the gap does not exist at all.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        // Asynchronous the way the sample hosts are, which write the URL from this callback.
        var host = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var cut = RenderWith(client, b => b.Add(
            c => c.SelectedKildeIdChanged, EventCallback.Factory.Create<Guid?>(this, _ => host.Task)));

        var opening = cut.Find(".munin-explorer-kilder tbody th button")
            .TriggerEventAsync("onclick", new MouseEventArgs());
        var closing = cut.Find(".munin-explorer-drilldown button")
            .TriggerEventAsync("onclick", new MouseEventArgs());

        await cut.InvokeAsync(host.SetResult);

        // Awaited rather than polled: both handlers have run to their end, so a fetch asserted
        // never to have been issued cannot still be on its way.
        await opening;
        await closing;

        Assert.Equal(0, client.DetailCalls);
        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Render_WhenTheHostNamesAKilde_ThenItIsAlreadyOpenOnTheFirstRender()
    {
        // The one piece of state worth putting in a host's URL, per the Kelda parity decision, so
        // it has to survive being handed back in.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als, Kilde("Reseptregisteret", "K_NORPD")).Publishing(als);

        var cut = RenderWith(client, b => b.Add(c => c.SelectedKildeId, als.Id));

        Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);
    }

    [Fact]
    public void Select_WhenTheKildeIsOpen_ThenTheRegionIsNamedByTheHeadingInsideIt()
    {
        // A landmark is only useful if a screen reader can say which kilde it just entered, and the
        // name it points at has to be the view's own rather than a second heading outside it saying
        // the same thing.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var region = cut.Find(".munin-explorer-drilldown");
        var labelledBy = region.GetAttribute("aria-labelledby");

        Assert.Equal("region", region.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(labelledBy));
        Assert.Equal("Als registeret", cut.Find($"#{labelledBy}").TextContent.Trim());
    }

    [Fact]
    public void Render_WhenTheHostNamesAKildeTheListCannotName_ThenTheHeadingStopsSayingItIsLoading()
    {
        // The list is what knows a kilde's name, so an id it does not carry — one the catalogue
        // does not publish, or any id at all when the list itself failed to load — leaves the
        // view's own heading with nothing of the catalogue's to say. That heading is what
        // aria-labelledby points at, so one left on "Henter datakilden …" tells a screen reader
        // entering the landmark that the source is still loading, for as long as the reader stays
        // in it, while the status line underneath says the fetch is finished and found nothing.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS"));

        var cut = RenderWith(client, b => b.Add(c => c.SelectedKildeId, Guid.NewGuid()));

        var region = cut.Find(".munin-explorer-drilldown");
        var heading = cut.Find($"#{region.GetAttribute("aria-labelledby")}");

        Assert.Equal("false", region.GetAttribute("aria-busy"));
        Assert.Equal("Fant ingen detaljer for denne datakilden.", heading.TextContent.Trim());

        // The package's own words, so not marked as the catalogue's language.
        Assert.Null(heading.GetAttribute("lang"));
    }

    [Fact]
    public void Render_WhenTheHostNamesAKildeTheListCannotNameAndTheFetchFails_ThenTheHeadingCarriesTheFailure()
    {
        // The second of the three states this heading has to follow, and the one the test above
        // cannot tell apart: with the list unable to name the kilde, dropping DetailStatus from the
        // fallback chain leaves "Fant ingen detaljer for denne datakilden." — which that test still
        // passes on, while a screen reader entering the landmark hears the fetch found nothing over
        // a status line saying it failed and is worth retrying. The two sentences ask the reader to
        // do different things, so the landmark's name has to be the one the status line carries.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS")) { FailDetail = true };

        var cut = RenderWith(client, b => b.Add(c => c.SelectedKildeId, Guid.NewGuid()));

        var region = cut.Find(".munin-explorer-drilldown");
        var heading = cut.Find($"#{region.GetAttribute("aria-labelledby")}");
        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Equal("false", region.GetAttribute("aria-busy"));
        Assert.Equal("Kunne ikke hente datakilden nå. Prøv igjen om litt.", heading.TextContent.Trim());
        Assert.Equal(heading.TextContent.Trim(), status.TextContent.Trim());
        Assert.DoesNotContain("Fant ingen detaljer", cut.Markup);

        // The package's own words, so not marked as the catalogue's language.
        Assert.Null(heading.GetAttribute("lang"));
    }

    [Fact]
    public void Render_WhenTheHostNamesAKildeTheListCannotNameAndTheFetchIsStillRunning_ThenTheHeadingSaysItIsLoading()
    {
        // The third state, and the one the heading is allowed to say "Henter datakilden …" in: the
        // fetch really is in flight. The fix above is "stop standing on loading forever", so this
        // is what keeps it from becoming "never say loading at all".
        var client = new FakeClient(Kilde("Als registeret", "K_ALS")) { StallDetail = true };

        var cut = RenderWith(client, b => b.Add(c => c.SelectedKildeId, Guid.NewGuid()));

        var region = cut.Find(".munin-explorer-drilldown");
        var heading = cut.Find($"#{region.GetAttribute("aria-labelledby")}");

        Assert.Equal("true", region.GetAttribute("aria-busy"));
        Assert.Equal("Henter datakilden …", heading.TextContent.Trim());
        Assert.Equal(1, client.Stalls);
    }

    [Fact]
    public void Render_WhenTheHostNamesAKildeAndTheListHasNotAnsweredYet_ThenTheViewAlreadyReadsAsLoading()
    {
        // The render before all three of those: the detail fetch cannot start until the list has
        // answered, because the list is what knows the kilde's name, and ComponentBase draws the
        // drilldown as soon as OnInitializedAsync yields on the list. For that render the view held
        // no name, no detail and no error, so it reported a finished, empty fetch that had not been
        // made — aria-busy "false", an empty status line, and a heading reading "Fant ingen
        // detaljer for denne datakilden." to a screen reader entering the landmark.
        //
        // No other test here reaches this render at all: FakeClient answers the list synchronously,
        // so its await never yields. An unresolved task is the shape a real HttpClient call has.
        var cut = RenderWith(
            new StallingListClient(), b => b.Add(c => c.SelectedKildeId, Guid.NewGuid()));

        var region = cut.Find(".munin-explorer-drilldown");
        var heading = cut.Find($"#{region.GetAttribute("aria-labelledby")}");
        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Equal("true", region.GetAttribute("aria-busy"));
        Assert.Equal("Henter datakilden …", heading.TextContent.Trim());
        Assert.Equal("Henter datakilden …", status.TextContent.Trim());
        Assert.DoesNotContain("Fant ingen detaljer", cut.Markup);
    }

    // ---------------------------------------------------------------------------------
    // Heading levels, language, and the host contract.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenTheHostSetsTheHeadingLevel_ThenTheOutlineFollowsIt()
    {
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client, b => b.Add(c => c.HeadingLevel, 3));

        Assert.Equal("Kildeutforsker", cut.Find("h3").TextContent.Trim());

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        // One step below the component's own title, so the kilde reads as part of it.
        Assert.Equal(4, cut.FindComponent<KildeView>().Instance.HeadingLevel);
    }

    [Fact]
    public void Render_WhenTheHostAsksForEnglish_ThenEveryStringThisComponentOwnsIsEnglish()
    {
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS")),
            b => b.Add(c => c.Language, "en"));

        Assert.Contains("Source explorer", cut.Markup);
        Assert.Contains("1 source", cut.Markup);
        Assert.Contains("Established", cut.Markup);
        Assert.DoesNotContain("Kildeutforsker", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheReaderIsNotNorwegian_ThenTheYearIsUnmarkedAndTheNameIsNot()
    {
        // Both halves of the one cell whose content is not the catalogue's words: a four-digit year
        // has no language to mark (WCAG 3.1.2) and "Not specified" is in the reader's own, so a
        // lang="no" over either only switches a screen reader's voice. The name is where it lives.
        var cut = RenderWith(
            new FakeClient(
                Kilde("Als registeret", "K_ALS", established: "1994"),
                Kilde("Dødsårsaksregisteret", "K_DAR")),
            b => b.Add(c => c.Language, "en"));

        var cells = cut.FindAll(".munin-explorer-kilder tbody td:not(.munin-explorer-kilder__expand)");

        Assert.Equal("1994", cells[4].TextContent.Trim());
        Assert.Null(cells[4].GetAttribute("lang"));

        Assert.Equal("Not specified", cells[9].TextContent.Trim());
        Assert.Null(cells[9].GetAttribute("lang"));

        // So the change above cannot become "stop marking anything": the kilde's name is the
        // catalogue's own words, and it is marked whatever the reader is reading in.
        Assert.Equal("no", cut.Find(".munin-explorer-kilder__name").GetAttribute("lang"));
    }

    [Fact]
    public void Select_WhenTheReaderIsNotNorwegian_ThenTheDrilldownHeadingIsMarkedAsTheCataloguesLanguage()
    {
        // The same pair one level down, on the element aria-labelledby points at: this heading is
        // the catalogue's Norwegian name for the kilde, so dropping the mark reads it out in an
        // English voice to the reader entering the landmark — WCAG 3.1.2 again.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS")) { StallDetail = true };

        var cut = RenderWith(client, b => b.Add(c => c.Language, "en"));

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var region = cut.Find(".munin-explorer-drilldown");
        var heading = cut.Find($"#{region.GetAttribute("aria-labelledby")}");

        Assert.Equal("Als registeret", heading.TextContent.Trim());
        Assert.Equal("no", heading.GetAttribute("lang"));
    }

    // ---------------------------------------------------------------------------------
    // The facets.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// A databehandler as the live catalogue really holds one: 212 characters of free text
    /// describing an arrangement rather than naming an organisation.
    /// </summary>
    /// <remarks>
    /// Measured on Kelda on 2026-08-20, where it is one of 39 values in that facet. It is here
    /// because a fixture of short tidy names passes against a component that puts the whole value
    /// on screen — and the panel it goes in is 384 pixels wide.
    /// </remarks>
    private const string LongDataProcessor =
        "Daglig drift av registeret, budsjett, ledelse og driftsrapportering gjennomføres av NKIR "
        + "ledergruppe, som består av registerleder, fagleder, kvalitetsrådgiver og controller, i nært "
        + "samarbeid med referansegruppen.";

    [Fact]
    public void Facets_WhenTheListHoldsThreeKildetyper_ThenTickingOneNarrowsTheListToThoseKilder()
    {
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister"),
            Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "sentraltHelseregister"),
            Kilde("Reseptregisteret", "K_NORPD", kildetype: "sentraltHelseregister"),
            Kilde("Den norske mor, far og barn-undersøkelsen", "K_MOBA", kildetype: "biobank")));

        var kildetype = Facet(cut, "Kildetype");

        // Ordered by the label in the catalogue's own collation, and counted over the whole list.
        Assert.Equal(
        [
            "Biobank (1)",
            "Nasjonalt medisinsk kvalitetsregister (1)",
            "Sentralt helseregister (2)"
        ], Choices(kildetype));

        Tick(cut, "Kildetype", "Sentralt helseregister");

        Assert.Equal(["Dødsårsaksregisteret", "Reseptregisteret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAFacetHasNoValuesAtAll_ThenItIsNotRenderedRatherThanRenderedEmpty()
    {
        // THE TRAP, and the whole reason this bead exists. Munin's own Kelda draws Kategori as a
        // heading with nothing under it, which reads as a broken panel rather than as a field
        // nobody filled in — and a fixture of well-populated kilder passes against exactly that
        // implementation, because every facet it has values for looks right.
        //
        // So this fixture has one facet where every kilde carries the SAME value, which must still
        // be drawn with its one choice, and one where no kilde carries any, which must not be drawn
        // at all. Asserted on the headings and on the group count rather than on the markup as a
        // string: an empty <div role="group"> with an empty heading in it is what a component that
        // renders every facet unconditionally produces, and it contains no text to search for.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", accessRights: "eu-access:NON_PUBLIC"),
            Kilde("Dødsårsaksregisteret", "K_DAR", accessRights: "eu-access:NON_PUBLIC")));

        Assert.Equal(["Kildetype", "Tilgangsnivå", "Databehandler"], FacetHeadings(cut));
        Assert.Equal(3, cut.FindAll(".munin-explorer-filters__facets [role=group]").Count);
        Assert.DoesNotContain("Kategori", cut.Markup);

        // The other half, so "drop the empty one" cannot become "drop the one with a single value":
        // one choice is a choice, and it is the only thing telling the reader what these kilder are.
        Assert.Equal(["Ikke-offentlig (2)"], Choices(Facet(cut, "Tilgangsnivå")));
    }

    [Fact]
    public void Facets_WhenNoKildeHasAnyValueForAnything_ThenThereIsNoPanelAtAll()
    {
        // The rule taken to its end: with every facet empty there is no panel, not an empty one
        // with a heading and a toggle over nothing.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "", dataProcessor: null)));

        Assert.Empty(cut.FindAll(".munin-explorer-filters"));
        Assert.DoesNotContain("Vis filtre", cut.Markup);
    }

    [Fact]
    public void Facets_WhenADataProcessorRunsToTwoHundredCharacters_ThenItIsCutShortWithTheWholeValueInTitle()
    {
        // A real value from the catalogue rather than a constructed extreme — see LongDataProcessor.
        // The component is not allowed to hide it, tidy it or let it out at full length into a
        // 384-pixel column, so what it does is cut the text and put the whole thing in the title.
        var cut = RenderWith(new FakeClient(
            Kilde("Norsk hjerteinfarktregister", "K_NKIR", dataProcessor: LongDataProcessor),
            Kilde("Dødsårsaksregisteret", "K_DAR", dataProcessor: "Folkehelseinstituttet")));

        var choice = Facet(cut, "Databehandler")
            .QuerySelectorAll("label")
            .Single(label => label.GetAttribute("title") is not null);

        Assert.Equal(LongDataProcessor, choice.GetAttribute("title"));
        Assert.StartsWith("Daglig drift av registeret", choice.TextContent.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain(LongDataProcessor, choice.TextContent, StringComparison.Ordinal);
        Assert.Contains("…", choice.TextContent, StringComparison.Ordinal);
        Assert.True(
            choice.TextContent.Trim().Length < 80,
            $"The choice is still {choice.TextContent.Trim().Length} characters long on screen.");

        // The value itself is untouched: the cut is cosmetic, and filtering on a truncated value
        // would match nothing.
        Tick(cut, "Databehandler", "Daglig drift av registeret");

        Assert.Equal(["Norsk hjerteinfarktregister"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAChoiceIsShortEnoughToDrawWhole_ThenItCarriesNoTitle()
    {
        // The other half of the rule above. A title repeating what is already on screen is read out
        // twice by some screen readers and hovers a tooltip over every option for nothing.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var choice = Facet(cut, "Databehandler").QuerySelector("label")!;

        Assert.Null(choice.GetAttribute("title"));
        Assert.Equal("Folkehelseinstituttet (1)", choice.TextContent.Trim());
    }

    [Fact]
    public void Facets_Always_ThenTheCountIsItsOwnElementInsideTheLabelAndStillInTheAccessibleName()
    {
        // Both halves of Fhi.Metadata-cgk85 pull in opposite directions: an element of its own
        // lets a host dim the number, inside the label keeps it in the announced name. A count
        // moved out to a sibling passes every on-screen text assertion, so both are asserted.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "biobank")));

        var label = Facet(cut, "Kildetype").QuerySelector("label")!;
        var count = label.QuerySelector(".munin-explorer-filters__count")!;

        // Its own element, and under the label rather than after it.
        Assert.Equal("SPAN", count.TagName);
        Assert.True(label.Contains(count));

        // Untrimmed on purpose: the separating space has to be a text node of the label, not the
        // span's first character, or the name announces as "Biobank(1)". Trim() would pass on both
        // shapes, and so would AccessibleName.Of, which flattens descendants. This one can fail.
        Assert.Equal("(1)", count.TextContent);

        // The words and the number are still one run on screen, with exactly one space between
        // them: a stray newline from the markup would read as "Biobank\n (1)" here.
        Assert.Equal("Biobank (1)", label.TextContent.Trim());

        // And the half a sibling element would have cost.
        Assert.Equal(
            "Biobank (1)", AccessibleName.Of(label.QuerySelector("input[type=checkbox]")!));
    }

    [Fact]
    public void Facets_WhenTwoValuesInOneFacetAreTicked_ThenTheListShowsKilderMatchingEither()
    {
        // OR within a facet. An implementation that ANDs them answers two ticked boxes with an empty
        // list, which on screen reads as "the catalogue has nothing like that" rather than as a bug
        // — the failure is invisible unless a test ticks two boxes in one facet, which is why this
        // one does.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister"),
            Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "sentraltHelseregister"),
            Kilde("Den norske mor, far og barn-undersøkelsen", "K_MOBA", kildetype: "biobank")));

        Tick(cut, "Kildetype", "Biobank");
        Tick(cut, "Kildetype", "Sentralt helseregister");

        Assert.Equal(["Dødsårsaksregisteret", "Den norske mor, far og barn-undersøkelsen"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenTwoFacetsAreTicked_ThenTheListShowsOnlyKilderMatchingBoth()
    {
        // AND across facets, which is the other half of the rule above: one facet narrowing and the
        // next widening again would make the panel unusable in the case it exists for.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS",
                kildetype: "nasjonaltMedisinskKvalitetsregister", dataProcessor: "St. Olavs hospital HF"),
            Kilde("Barnediabetes", "K_BDR",
                kildetype: "nasjonaltMedisinskKvalitetsregister", dataProcessor: "Oslo universitetssykehus HF"),
            Kilde("Dødsårsaksregisteret", "K_DAR",
                kildetype: "sentraltHelseregister", dataProcessor: "St. Olavs hospital HF")));

        Tick(cut, "Kildetype", "Nasjonalt medisinsk kvalitetsregister");
        Tick(cut, "Databehandler", "St. Olavs hospital HF");

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAValueIsTicked_ThenTheCountsStayWholeListAndNothingIsRefetched()
    {
        // Two claims that belong together, because they are the same decision seen from two sides:
        // the list is fetched once and the facets are counted over it, so a ticked box narrows the
        // rows and leaves every count where it was. Runa's counts cross-filter because its facets
        // come from an endpoint that recounts them per request; this list has no such endpoint
        // behind it, and a component that recounted anyway would have to ask the API again.
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister",
                dataProcessor: "St. Olavs hospital HF"),
            Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "sentraltHelseregister",
                dataProcessor: "Folkehelseinstituttet"));

        var cut = RenderWith(client);

        Tick(cut, "Kildetype", "Sentralt helseregister");

        Assert.Equal(["Dødsårsaksregisteret"], RowNames(cut));
        Assert.Equal(
            ["Folkehelseinstituttet (1)", "St. Olavs hospital HF (1)"],
            Choices(Facet(cut, "Databehandler")));
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Facets_WhenTheReaderUnticksAValue_ThenTheRowsComeBack()
    {
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister"),
            Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "sentraltHelseregister")));

        Tick(cut, "Kildetype", "Sentralt helseregister");

        Assert.Equal(["Dødsårsaksregisteret"], RowNames(cut));

        Facet(cut, "Kildetype")
            .QuerySelectorAll("label")
            .First(label => label.TextContent.Trim().StartsWith("Sentralt", StringComparison.Ordinal))
            .QuerySelector("input")!
            .Change(false);

        Assert.Equal(["Als registeret", "Dødsårsaksregisteret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAKildeCarriesSeveralKategorier_ThenEachOneIsItsOwnChoice()
    {
        // Kategori is the one facet a kilde can be in more than one of, and the catalogue writes it
        // as a JSON array inside a string. A component that took the value as one token would draw a
        // single choice named ["ehds-cat:biobanks","ehds-cat:health-registries"], which nothing
        // matches and nobody wants to read.
        var cut = RenderWith(new FakeClient(
            Kilde("Den norske mor, far og barn-undersøkelsen", "K_MOBA",
                category: """["ehds-cat:biobanks","ehds-cat:population-health-surveys"]"""),
            Kilde("Dødsårsaksregisteret", "K_DAR",
                category: """["ehds-cat:health-registries"]""")));

        // Words rather than CURIEs, and the catalogue's own words at that — the labels its
        // healthCategory vocabulary carries. A reader of a Norwegian health catalogue is not
        // expected to read EHDS, and the tilgangsnivå facet two headings away spells its tokens out
        // for exactly that reason. In label order, which is not token order.
        Assert.Equal(
        [
            "Befolkningsbaserte helseundersøkelser (1)",
            "Biobanker (1)",
            "Helseregistre (1)"
        ], Choices(Facet(cut, "Kategori")));

        Tick(cut, "Kategori", "Biobanker");

        Assert.Equal(["Den norske mor, far og barn-undersøkelsen"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAKategoriIsOutsideTheKnownVocabulary_ThenItsTokenIsTheChoice()
    {
        // A value stored on a kilde that the vocabulary lists no option for — a category retired
        // out of the definition, or one written straight into the bag. It has to keep its checkbox
        // and show what the catalogue sent: a facet that dropped it would filter over less than the
        // list holds, silently, and the kilder carrying it would be unreachable through the panel.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]"""),
            Kilde("Dødsårsaksregisteret", "K_DAR", category: """["ehds-cat:something-new"]""")));

        Assert.Equal(["Biobanker (1)", "ehds-cat:something-new (1)"], Choices(Facet(cut, "Kategori")));

        Tick(cut, "Kategori", "ehds-cat:something-new");

        Assert.Equal(["Dødsårsaksregisteret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAKategoriIsNotJsonAtAll_ThenItIsShownAsTheCatalogueWroteIt()
    {
        // Every value in the bag is a string, and this one is usually JSON. A parse failure must not
        // cost the facet its value: a kilde that silently left the panel would be the empty Kategori
        // this component exists not to draw, arrived at by a different route.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS", category: "Helseregistre")));

        Assert.Equal(["Helseregistre (1)"], Choices(Facet(cut, "Kategori")));
    }

    [Fact]
    public void Facets_WhenTheHostChangesLanguage_ThenTheHeadingsChangeWithIt()
    {
        // The four definitions are held between renders rather than rebuilt per read, because the
        // filtering reads them once per kilde — see Definitions. A heading is fixed at the moment
        // its definition is made, so the held list belongs to one reader and a host that switches
        // language has to be given a new one; a cache without that rule leaves Norwegian headings
        // over English choices, and nothing else in this suite would notice.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        Assert.Equal(["Kildetype", "Databehandler"], FacetHeadings(cut));

        cut.Render(b => b.Add(c => c.Language, "en"));

        Assert.Equal(["Source type", "Data processor"], FacetHeadings(cut));
    }

    [Fact]
    public void Facets_WhenAKategoriIsASingleJsonString_ThenItIsTheSameChoiceAsTheArrayWouldBe()
    {
        // The bag's values are strings that happen to hold JSON, so a kilde carrying one category
        // can plausibly arrive as a bare string where its neighbour arrives as an array of one.
        // Parsing succeeds either way, so nothing throws and the fall-through is silent: taken as
        // raw text the quoted form would be a *second* choice, six characters longer, whose label
        // lookup misses on the trailing quote and which counts a disjoint set of kilder. One
        // category, one checkbox, however the catalogue wrote it.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]"""),
            Kilde("Dødsårsaksregisteret", "K_DAR", category: """ "ehds-cat:biobanks" """)));

        Assert.Equal(["Biobanker (2)"], Choices(Facet(cut, "Kategori")));

        Tick(cut, "Kategori", "Biobanker");

        Assert.Equal(["Als registeret", "Dødsårsaksregisteret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAKategoriIsJsonNull_ThenTheKildeCarriesNoCategoryAtAll()
    {
        // A JSON null is the catalogue saying it has nothing here, and it says so in a field whose
        // other values are text. Taken as text it would draw a checkbox named "null" — this package
        // inventing a catalogue value — and with one kilde in the list it would be the whole facet.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS", category: "null")));

        Assert.DoesNotContain("Kategori", FacetHeadings(cut));
    }

    [Fact]
    public void Facets_WhenAKategoriIsJsonWithNoTokenInIt_ThenItIsShownAsTheCatalogueWroteIt()
    {
        // An object parses and holds no token to prefer, so there is nothing to unwrap and the rule
        // the not-JSON-at-all case follows applies: show what the catalogue holds rather than drop
        // the kilde out of a facet it belongs in.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", category: """{"id":"ehds-cat:biobanks"}""")));

        Assert.Equal(["""{"id":"ehds-cat:biobanks"} (1)"""], Choices(Facet(cut, "Kategori")));
    }

    [Fact]
    public void Facets_WhenAKildeHasNoPropertyBagAtAll_ThenTheOtherFacetsStillDraw()
    {
        // AdditionalProperties is declared non-nullable and initialised to an empty dictionary, and
        // that initialiser only survives a key the payload leaves *out*: System.Text.Json writes
        // null straight over it for an explicit "additionalProperties": null. Two of the four
        // facets read the bag for every kilde on every render, so one such entry would take the
        // whole panel down at render time — past the try/catch around the fetch, which finished
        // long before.
        var kilde = Kilde("Als registeret", "K_ALS", kildetype: "biobank");

        var cut = RenderWith(new FakeClient(kilde with { AdditionalProperties = null! }));

        Assert.Equal(["Als registeret"], RowNames(cut));
        Assert.Equal(["Kildetype", "Databehandler"], FacetHeadings(cut));
    }

    [Fact]
    public void Facets_WhenAChoiceIsDrawnInTheCataloguesOwnWords_ThenItIsMarkedWithTheCataloguesLanguage()
    {
        // The same marking the table's cells carry for the same strings: an unmarked Norwegian
        // organisation name inside an English page is read out with English phonetics (WCAG 3.1.2).
        // Which choices need it is not a property of the facet — three of the four look their values
        // up and every one of those falls back to the catalogue's token — so it follows the answer:
        // a label that came back as the value itself is the catalogue's text.
        var cut = RenderWith(
            new FakeClient(
                Kilde("Als registeret", "K_ALS",
                    kildetype: "biobank", dataProcessor: "Folkehelseinstituttet"),
                Kilde("Dødsårsaksregisteret", "K_DAR",
                    kildetype: "noeHeltNytt", dataProcessor: "Folkehelseinstituttet")),
            b => b.Add(c => c.Language, "en"));

        Assert.Equal(["no"], Languages(Facet(cut, "Data processor")));

        // Biobank is this package's own English word for the token; noeHeltNytt is a token it has
        // no word for, so what is on screen is the catalogue's.
        Assert.Equal([null, "no"], Languages(Facet(cut, "Source type")));
    }

    [Fact]
    public void Facets_WhenTheReaderIsNorwegian_ThenNoChoiceIsMarkedAtAll()
    {
        // A lang saying what the surrounding page already says is noise, and the package's rule is
        // that a value is marked only where it is foreign to the reader — CatalogueProperties.Foreign.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", dataProcessor: "Folkehelseinstituttet")));

        Assert.Equal([null], Languages(Facet(cut, "Databehandler")));
    }

    [Fact]
    public void Facets_WhenAVocabularyMissLeavesACurieOnScreen_ThenItIsNotMarkedAsTheCataloguesLanguage()
    {
        // The other half of the rule, and the half a plain label == value comparison gets wrong on
        // its own: these two facets fall back to the catalogue's *token*, and the token is a CURIE
        // into an EU or EHDS vocabulary — English-authored, and prose in no language at all. Marked
        // "no" it is handed to an English reader's screen reader in a Norwegian voice, which is the
        // WCAG 3.1.2 failure the marking exists to avoid, only inverted. Unmarked it is read in the
        // page's own language, which is as close as this component can get for an identifier.
        var cut = RenderWith(
            new FakeClient(
                Kilde("Als registeret", "K_ALS",
                    accessRights: "eu-access:OP_DATPRO",
                    category: """["ehds-cat:noeHeltNytt"]""")),
            b => b.Add(c => c.Language, "en"));

        Assert.Equal(["eu-access:OP_DATPRO (1)"], Choices(Facet(cut, "Access level")));
        Assert.Equal([null], Languages(Facet(cut, "Access level")));

        Assert.Equal(["ehds-cat:noeHeltNytt (1)"], Choices(Facet(cut, "Category")));
        Assert.Equal([null], Languages(Facet(cut, "Category")));
    }

    [Fact]
    public void Facets_WhenAVocabularyKnowsTheTokenInBothFacets_ThenTheWordsAreLeftUnmarkedToo()
    {
        // The unremarkable side of the same branch, pinned so that a change to the fallback rule
        // cannot start marking the words this package supplied: "Non-public" and "Biobanks" are
        // English on an English page, and a lang="no" over either is the failure the CURIE case
        // describes, this time on prose a reader can actually hear the difference in.
        var cut = RenderWith(
            new FakeClient(
                Kilde("Als registeret", "K_ALS",
                    accessRights: "eu-access:NON_PUBLIC",
                    category: """["ehds-cat:biobanks"]""")),
            b => b.Add(c => c.Language, "en"));

        Assert.Equal([null], Languages(Facet(cut, "Access level")));
        Assert.Equal([null], Languages(Facet(cut, "Category")));
    }

    [Fact]
    public void Facets_WhenTheVocabularyHasNoEnglishForAValue_ThenItsNorwegianWordIsMarkedAsNorwegian()
    {
        // Curation is uneven: the vocabulary carries a Norwegian label for every option and an
        // English one for most. The words now come from the catalogue rather than from a table in
        // this package, so an English page can legitimately end up with a Norwegian choice in it —
        // and it is marked, which is the difference between a screen reader saying "Prøvesamling"
        // and saying it with English phonetics (WCAG 3.1.2).
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS", category: """["ehds-cat:provesamling"]"""))
                .Serving(Vocabulary("healthCategory", ("ehds-cat:provesamling", "Prøvesamling", null))),
            b => b.Add(c => c.Language, "en"));

        Assert.Equal(["Prøvesamling (1)"], Choices(Facet(cut, "Category")));
        Assert.Equal(["no"], Languages(Facet(cut, "Category")));
    }

    [Fact]
    public void Facets_WhenTheCatalogueAddsAValueAfterThisPackageShipped_ThenTheFacetStillDrawsItsWord()
    {
        // The whole point of reading the vocabulary the API sends rather than one written down
        // here. Both tokens are ones no copy in this package ever knew — the eighth category and a
        // fourth access-right value — and they arrive with their words the same day the catalogue
        // adds them. A copied table would answer both of these with a raw CURIE, one click away
        // from a kilde view showing the Norwegian word for the very same token.
        var cut = RenderWith(new FakeClient(
                Kilde("Als registeret", "K_ALS",
                    accessRights: "eu-access:OP_DATPRO",
                    category: """["ehds-cat:noe-helt-nytt"]"""))
            .Serving(
                Vocabulary("healthCategory", [.. Categories, ("ehds-cat:noe-helt-nytt", "Noe helt nytt", "Something new")]),
                Vocabulary("accessRights", [.. AccessLevels, ("eu-access:OP_DATPRO", "Databehandleravtale", "Data processing agreement")])));

        Assert.Equal(["Noe helt nytt (1)"], Choices(Facet(cut, "Kategori")));
        Assert.Equal(["Databehandleravtale (1)"], Choices(Facet(cut, "Tilgangsnivå")));

        // And the token is still what it filters on, so a word arriving late changes nothing about
        // which kilder a checkbox reaches.
        Tick(cut, "Kategori", "Noe helt nytt");

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAValueRepeatsAnothersBareTokenUnderItsOwnPrefix_ThenItIsNotGivenThatWord()
    {
        // Matching is on the whole stored value, never on the part after the last colon. The copy
        // this package used to hold was keyed prefix-blind, so annet-vokabular:biobanks read as
        // "Biobanker" in the facet while the detail panel one click away showed it raw — one value,
        // two labels, depending on which screen the reader was on. It is a value the vocabulary
        // does not list, and it says so.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]"""),
            Kilde("Dødsårsaksregisteret", "K_DAR", category: """["annet-vokabular:biobanks"]""")));

        Assert.Equal(["annet-vokabular:biobanks (1)", "Biobanker (1)"], Choices(Facet(cut, "Kategori")));

        Tick(cut, "Kategori", "Biobanker");

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenAValueDiffersOnlyInCaseFromALabellessOption_ThenItIsStillLeftUnmarked()
    {
        // An option the vocabulary lists but has curated no label for is not a word, and the guard
        // that spots one asks whether the label came back as the token again. The lookup behind it
        // matches ordinal-insensitively and answers a label-less option with the *vocabulary's*
        // spelling of the code, so the guard has to ignore case as well: an ordinal check reads two
        // spellings of one code as a curated word and marks a bare CURIE lang="no" — the WCAG 3.1.2
        // failure the marking exists to avoid — while showing it in a casing the value never had.
        var cut = RenderWith(new FakeClient(
                Kilde("Als registeret", "K_ALS", category: """["ehds-cat:Biobanks"]"""))
            .Serving(Vocabulary("healthCategory", ("ehds-cat:biobanks", "", null))));

        var kategori = Facet(cut, "Kategori");

        Assert.Equal(["ehds-cat:Biobanks (1)"], Choices(kategori));
        Assert.Equal([null], Languages(kategori));
    }

    [Fact]
    public void Facets_WhenTheVocabularyCannotBeFetched_ThenTheChoicesKeepTheirTokensAndTheListIsUnharmed()
    {
        // Two calls that fail apart. The vocabulary only decides whether two facets read as words
        // or as CURIEs, so losing it costs those labels and nothing else: the list is on screen, no
        // error is claimed, and every checkbox still filters on the value the catalogue sent.
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]""",
                accessRights: "eu-access:NON_PUBLIC"))
        {
            FailVocabulary = true
        };

        var cut = RenderWith(client);

        Assert.Equal(["Als registeret"], RowNames(cut));
        Assert.DoesNotContain("Kunne ikke laste kilder", cut.Markup);

        Assert.Equal(["ehds-cat:biobanks (1)"], Choices(Facet(cut, "Kategori")));
        Assert.Equal(["eu-access:NON_PUBLIC (1)"], Choices(Facet(cut, "Tilgangsnivå")));

        Tick(cut, "Kategori", "ehds-cat:biobanks");

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public async Task Facets_WhenTheListAnswersBeforeTheVocabulary_ThenTheRowsAreOnScreenWithoutWaitingForIt()
    {
        // The point of two calls in flight together: the vocabulary's round trip is not one the
        // reader spends. Nothing using FakeClient can see whether that holds — it answers both from
        // Task.FromResult, so its awaits never yield and both are finished before the first render.
        // Here the list lands with the vocabulary still outstanding, which is what a host pointed
        // at a slow or undeployed egenskaper endpoint gets, and the finished list is on screen
        // rather than sitting behind "Laster kilder …" for the rest of that round trip.
        var client = new StagedClient(
            Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]"""));

        var cut = RenderWith(client);

        Assert.Contains("Laster kilder …", cut.Markup);

        await cut.InvokeAsync(client.AnswerList);

        Assert.Equal(["Als registeret"], RowNames(cut));
        Assert.DoesNotContain("Laster kilder …", cut.Markup);

        // And the panel beside them is usable meanwhile, showing the catalogue's token — the same
        // thing it shows for a vocabulary that never arrives at all.
        Assert.Equal(["ehds-cat:biobanks (1)"], Choices(Facet(cut, "Kategori")));

        await cut.InvokeAsync(client.AnswerVocabulary);

        Assert.Equal(["Biobanker (1)"], Choices(Facet(cut, "Kategori")));
    }

    [Fact]
    public async Task Render_WhenAKildeIsDeepLinkedAndTheVocabularyIsStillOutstanding_ThenItIsFetchedAndDrawnAnyway()
    {
        // The other half of the same rule, and the half that costs more: a host mounting with
        // SelectedKildeId set is a reader who came for the kilde, and the vocabulary decides
        // nothing the drilldown draws. The fetch used to be issued only after the vocabulary
        // landed — OnInitializedAsync could not reach it until LoadAsync returned — so the region
        // sat on "Henter datakilden …" for a request nobody had made, for up to HttpClient's
        // hundred-second default. Then, once it was issued, the answer used to sit undrawn behind
        // the same await.
        var als = Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]""");
        var client = new StagedClient(als);

        var cut = RenderWith(client, b => b.Add(c => c.SelectedKildeId, als.Id));

        // Nothing can be fetched before the list answers, because the list is what names the kilde.
        Assert.Equal(0, client.DetailCalls);

        await cut.InvokeAsync(client.AnswerList);

        // The list has landed and the vocabulary has not, and the kilde is already being fetched.
        Assert.Equal(1, client.DetailCalls);
        Assert.Equal("Als registeret", cut.Find(".munin-explorer-drilldown h3").TextContent.Trim());
        Assert.Equal("true", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));

        await cut.InvokeAsync(client.AnswerDetail);

        // And on screen as soon as it lands, with the vocabulary still outstanding.
        Assert.Equal(als.Id, cut.FindComponent<KildeView>().Instance.Kilde?.Id);
        Assert.Equal("false", cut.Find(".munin-explorer-drilldown").GetAttribute("aria-busy"));

        await cut.InvokeAsync(client.AnswerVocabulary);

        // The vocabulary was the last thing waited for, and the facets it labels are back on
        // screen the moment the reader closes the kilde.
        cut.Find(".munin-explorer-drilldown button").Click();

        Assert.Equal(["Biobanker (1)"], Choices(Facet(cut, "Kategori")));
    }

    [Fact]
    public void Facets_WhenTheHostsOwnClientPredatesTheVocabularyEndpoint_ThenTheChoicesKeepTheirTokens()
    {
        // The default body on IMuninExplorerClient.GetKildePropertyMetadataAsync, exercised by the
        // one kind of caller it exists for. The interface is on the feed and a version there cannot
        // be taken back, so a host that implements it rather than consuming MuninExplorerClient has
        // to keep compiling across the upgrade — and every other fake in this suite overrides the
        // member, which leaves that promise resting on nothing. UnupgradedHostClient is the guard:
        // it does not derive from EmptyMuninExplorerClient and does not implement this member, so a
        // member added here without a default stops the test build before it reaches a host.
        //
        // What the default costs is asserted rather than described: the coded facets show the
        // catalogue's own tokens, the same as a vocabulary that failed to arrive, and the list is
        // unharmed.
        var cut = RenderWith(new UnupgradedHostClient(
            Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]""",
                accessRights: "eu-access:NON_PUBLIC")));

        Assert.Equal(["Als registeret"], RowNames(cut));
        Assert.Equal(["ehds-cat:biobanks (1)"], Choices(Facet(cut, "Kategori")));
        Assert.Equal(["eu-access:NON_PUBLIC (1)"], Choices(Facet(cut, "Tilgangsnivå")));
    }

    [Fact]
    public void Facets_WhenTheVocabularyRepeatsAKeyOrCarriesBlankOnes_ThenTheChoicesStillReadAsWords()
    {
        // The repeated key is the guard that can only fail quietly, which is why it is worth a test
        // of its own: the fetch is wrapped in a catch, so a ToDictionary throwing on it would be
        // swallowed whole and *every* coded facet would fall back to raw CURIEs — the endpoint
        // being down and the grouping being dropped look identical on screen.
        //
        // First entry wins, so the repeat's label is the one that must not appear.
        //
        // The blank keys are not a second route to that throw, and this test does not claim they
        // are: the grouping would collapse them as readily as two real ones. They are here because
        // they are what a key-less entry does to the facets, which is nothing — no property is
        // named "" or "   ", so an entry filed under one is unreachable whether the filter that
        // drops it is there or not. Deleting that filter leaves this test green, and should.
        var cut = RenderWith(new FakeClient(
                Kilde("Als registeret", "K_ALS", category: """["ehds-cat:biobanks"]""",
                    accessRights: "eu-access:NON_PUBLIC"))
            .Serving(
                Vocabulary("", ("ehds-cat:biobanks", "Nøkkelløs", null)),
                Vocabulary("   ", ("ehds-cat:biobanks", "Nøkkelløs igjen", null)),
                Vocabulary("healthCategory", Categories),
                Vocabulary("healthCategory", ("ehds-cat:biobanks", "Andre gangs oppslag", null)),
                Vocabulary("accessRights", AccessLevels)));

        Assert.Equal(["Biobanker (1)"], Choices(Facet(cut, "Kategori")));
        Assert.Equal(["Ikke-offentlig (1)"], Choices(Facet(cut, "Tilgangsnivå")));
    }

    [Fact]
    public void Facets_WhenTheReaderSearchesAndOpensAKilde_ThenTheVocabularyIsFetchedOnceAndOnlyOnce()
    {
        // A sibling of the list and fetched like one: once, on initialisation. The vocabulary is
        // editable master data rather than anything the reader's typing changes, so a component
        // that refetched it per search or per open would spend a round trip to be told the same
        // thing — and the labels are drawn on every render, which is what makes that easy to miss.
        var client = new FakeClient(Kilde("Als registeret", "K_ALS")).Publishing();

        var cut = RenderWith(client);

        cut.Find(".searchbox__freetext").Change("als");
        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Equal(1, client.Calls);
        Assert.Equal(1, client.VocabularyCalls);
    }

    [Fact]
    public void Facets_WhenAnAccessRightsTokenIsKnown_ThenItIsDrawnAsAWordInTheReadersLanguage()
    {
        // The catalogue writes eu-access:NON_PUBLIC; Kelda says "Ikke-offentlig". The token is what
        // the facet filters on either way — the word is only what the reader sees.
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS", accessRights: "eu-access:NON_PUBLIC")),
            b => b.Add(c => c.Language, "en"));

        Assert.Equal(["Non-public (1)"], Choices(Facet(cut, "Access level")));
    }

    [Fact]
    public void Facets_WhenAnAccessRightsTokenIsUnknown_ThenItIsShownAsItArrived()
    {
        // A fallback rather than a blank or a throw, for the reason kildetype has one: a new token
        // in the vocabulary is a catalogue change, not a bug here, and a facet that dropped it would
        // hide kilder the reader can see in the list.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", accessRights: "eu-access:OP_DATPRO")));

        Assert.Equal(["eu-access:OP_DATPRO (1)"], Choices(Facet(cut, "Tilgangsnivå")));
    }

    [Fact]
    public void Facets_WhenNothingIsTicked_ThenThePanelIsFoldedAwayAndSaysSo()
    {
        // The panel is folded on a narrow screen, and a host with room for a sidebar unfolds it in
        // one CSS rule — see the sample stylesheet. What the markup owes is the pair: `hidden` for
        // the browser, aria-expanded for a screen reader, and one control moving both.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var toggle = cut.Find(".munin-explorer-filters__toggle");
        var facets = cut.Find(".munin-explorer-filters__facets");

        Assert.Equal("Vis filtre", toggle.TextContent.Trim());
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal(facets.Id, toggle.GetAttribute("aria-controls"));
        Assert.True(facets.HasAttribute("hidden"));

        toggle.Click();

        Assert.Equal("Skjul filtre", cut.Find(".munin-explorer-filters__toggle").TextContent.Trim());
        Assert.Equal("true", cut.Find(".munin-explorer-filters__toggle").GetAttribute("aria-expanded"));
        Assert.False(cut.Find(".munin-explorer-filters__facets").HasAttribute("hidden"));
    }

    [Fact]
    public void Facets_WhenValuesAreTicked_ThenTheHeadingSaysHowMany()
    {
        // With the panel folded on a phone, the heading is the only thing on screen saying the list
        // is narrowed at all — the same reason the variable explorer's collapsed facets carry their
        // count.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister"),
            Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "sentraltHelseregister")));

        Assert.Equal("Filtre", cut.Find(".munin-explorer-filters h3").TextContent.Trim());

        Tick(cut, "Kildetype", "Sentralt helseregister");
        Tick(cut, "Databehandler", "Folkehelseinstituttet");

        Assert.Equal("Filtre (2)", cut.Find(".munin-explorer-filters h3").TextContent.Trim());
    }

    [Fact]
    public void Facets_WhenNothingMatches_ThenTheEmptyStateNamesTheFiltersAndNotOnlyTheSearch()
    {
        // Two ways of narrowing the list and one sentence about the result: a reader who has ticked
        // a box and typed a word is told both, or they go and edit the wrong one.
        //
        // Two facets rather than one, because one facet can never empty the list — every choice it
        // offers came from a kilde that has it. It takes two facets that no kilde satisfies at once,
        // which is exactly the state a reader lands in and cannot explain.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS",
                kildetype: "nasjonaltMedisinskKvalitetsregister", dataProcessor: "St. Olavs hospital HF"),
            Kilde("Dødsårsaksregisteret", "K_DAR",
                kildetype: "sentraltHelseregister", dataProcessor: "Folkehelseinstituttet")));

        Tick(cut, "Kildetype", "Sentralt helseregister");
        Tick(cut, "Databehandler", "St. Olavs hospital HF");

        Assert.Empty(RowNames(cut));
        Assert.Contains("Ingen kilder samsvarer med filtrene som er valgt.", cut.Markup);

        cut.Find(".searchbox__freetext").Change("als");

        Assert.Contains(
            "Ingen kilder samsvarer med søket «als» og filtrene som er valgt.", cut.Markup);
    }

    [Fact]
    public void Facets_WhenTheSearchAndAFacetBothNarrow_ThenTheListAnswersBoth()
    {
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister"),
            Kilde("Als-biobanken", "K_ALSB", kildetype: "biobank"),
            Kilde("Dødsårsaksregisteret", "K_DAR", kildetype: "biobank")));

        cut.Find(".searchbox__freetext").Change("als");
        Tick(cut, "Kildetype", "Biobank");

        Assert.Equal(["Als-biobanken"], RowNames(cut));
    }

    [Fact]
    public void Facets_WhenTheReaderIsReadingEnglish_ThenTheHeadingsAndTheToggleAreEnglish()
    {
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS", accessRights: "eu-access:NON_PUBLIC")),
            b => b.Add(c => c.Language, "en"));

        Assert.Equal(["Source type", "Access level", "Data processor"], FacetHeadings(cut));
        Assert.Equal("Filters", cut.Find(".munin-explorer-filters h3").TextContent.Trim());
        Assert.Equal("Show filters", cut.Find(".munin-explorer-filters__toggle").TextContent.Trim());
    }

    [Fact]
    public void Facets_WhenTheHostMountsUsDeeper_ThenTheHeadingsFollowItsLevel()
    {
        // The panel's heading sits one below the component's title and a facet's one below that, so
        // the outline stays unbroken wherever the host mounted us. A panel hard-coded to h2/h3 would
        // claim a place in the host's document that it has not got.
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS")),
            b => b.Add(c => c.HeadingLevel, 3));

        Assert.Equal("Filtre", cut.Find(".munin-explorer-filters h4").TextContent.Trim());
        Assert.Equal("Kildetype", cut.Find(".munin-explorer-filters__facets h5").TextContent.Trim());
    }

    [Fact]
    public void Facets_Always_ThenEachGroupIsNamedByItsOwnHeading()
    {
        // role="group" with no accessible name is a group of nothing in particular. The id is what
        // ties the heading to it, and it carries this instance's discriminator so two explorers on
        // one page cannot point at each other's headings.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        foreach (var group in cut.FindAll(".munin-explorer-filters__facets [role=group]"))
        {
            var heading = group.QuerySelector("h4")!;

            Assert.Equal(heading.Id, group.GetAttribute("aria-labelledby"));
            Assert.False(string.IsNullOrWhiteSpace(heading.Id));
        }
    }

    // ---------------------------------------------------------------------------------
    // Class names.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenTheListIsOnScreen_ThenEveryClassNameIsOneSomeStylesheetDefines()
    {
        // The check no look at a sample host can stand in for: both samples style every name this
        // component writes, so a name that only they define renders at raw browser defaults on a
        // host that has Stiler and nothing else — which is the host the prefix exists for.
        //
        // Compared against an empty list rather than asserted empty, so a failure names the classes
        // instead of saying only that there were some.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_WhenAKildeIsOpen_ThenEveryClassNameIsOneSomeStylesheetDefines()
    {
        // The other state, which shares almost no markup with the list: the drill-in, the way back
        // and the whole of KildeView.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_WhenTheListIsOnScreen_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // The exact list on purpose: one more name is news that has to be answered in both sample
        // stylesheets before it ships. munin-explorer-kilder__select is absent because nothing here
        // wires ExploreVariablesRequested (KildeSelectionTest).
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        // Searched, not idle: the clear control is drawn only when there is something to clear, so
        // an idle render would leave its name out of this list and out of the orphan check with it
        // — coverage lost to a state nobody enters. (Fhi.Metadata-ag4n7)
        cut.Find(".searchbox__freetext").Change("als");

        var invented = HostClassNames.Of(cut.FindAll("[class]"))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            "munin-explorer",                    // shared with the variable explorer
            "munin-explorer-container",          // shared
            "munin-explorer-filters",            // shared
            "munin-explorer-filters__count",     // shared with the variable explorer's facets
            "munin-explorer-filters__facets",
            "munin-explorer-filters__toggle",
            // The column picker, shared with the variable explorer down to the markup (ColumnPicker).
            "munin-explorer-header",
            "munin-explorer-header__actions",
            "munin-explorer-header__actions-button",
            "munin-explorer-kilder",
            "munin-explorer-kilder__count",
            "munin-explorer-kilder__expand",
            "munin-explorer-kilder__expand-toggle",
            "munin-explorer-kilder__name",
            "munin-explorer-results",            // shared
            "munin-explorer-search__clear",      // shared
            "munin-explorer__dropdown",          // the picker, shared
        ], invented);
    }

    [Fact]
    public void Render_WhenThePanelIsOpenWithChoicesTicked_ThenEveryClassNameIsOneSomeStylesheetDefines()
    {
        // The panel's own state, which the two guards above cannot reach: folded away, the facets
        // are still in the DOM, but the toggle's second wording and a ticked choice are markup
        // nothing has rendered until something presses them.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", kildetype: "nasjonaltMedisinskKvalitetsregister",
                accessRights: "eu-access:NON_PUBLIC", category: """["ehds-cat:biobanks"]"""),
            Kilde("Dødsårsaksregisteret", "K_DAR", accessRights: "eu-access:NON_PUBLIC")));

        cut.Find(".munin-explorer-filters__toggle").Click();
        Tick(cut, "Kildetype", "Sentralt helseregister");

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Facets_WhenTheHostHasRoomForASidebar_ThenTheFoldIsUndoneByADeclaration()
    {
        // Same half of the bug the skip link had. The general guards ask whether a name has a rule
        // that declares something, which the fold's rules do - so they pass whether or not the rule
        // says the one thing that matters. What a host must actually supply is a PARTICULAR
        // DECLARATION: one that hides the toggle and undoes [hidden] once there is room for a
        // sidebar. Without it the panel stays folded behind "Vis filtre" on a desktop.
        var rules = HostClassNames.SampleDeclarationsFor("munin-explorer-filters__toggle")
            .Concat(HostClassNames.SampleDeclarationsFor("munin-explorer-filters__facets"))
            .ToList();

        static string Squeezed(string css) => new([.. css.Where(c => !char.IsWhiteSpace(c))]);

        IReadOnlyList<string> BlocksFor(string selector) =>
            [.. rules.Where(r => r.Selector == selector).Select(r => Squeezed(r.Declarations))];

        // Both are needed. Hiding the button while the facets stay folded leaves no way to open
        // them at all, which is worse than the fold.
        Assert.True(
            BlocksFor(".munin-explorer-filters__toggle")
                .Any(d => d.Contains("display:none", StringComparison.Ordinal)),
            "No rule takes the toggle off screen once the host has room for a sidebar.");

        Assert.True(
            BlocksFor(".munin-explorer-filters__facets[hidden]")
                .Any(d => d.Contains("display:block", StringComparison.Ordinal)),
            "No rule undoes [hidden] on the facets once the host has room for a sidebar.");
    }

    // ---------------------------------------------------------------------------------
    // The column picker. Kelda's rules, not the variable explorer's: ten optional columns,
    // three of them on to begin with, and no last-column lock — Navn, Status and Opprettet
    // are drawn whatever the picker says, so this control cannot empty a row. The choice is
    // not persisted and not in the host's URL, which is what Kelda does. (Fhi.Metadata-ay3zz)
    // ---------------------------------------------------------------------------------

    /// <summary>The picker's toggles, in the order it lists them.</summary>
    private static IReadOnlyList<IElement> ColumnToggles(IRenderedComponent<KildeExplorer> cut) =>
        cut.FindAll(".dropdown-choicepicker__item button");

    /// <summary>The toggle for one named column, refetched so it is never a stale node.</summary>
    private static void ToggleColumn(IRenderedComponent<KildeExplorer> cut, string label) =>
        ColumnToggles(cut).Single(b => b.TextContent.Trim() == label).Click();

    private static IReadOnlyList<string> Headers(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder thead th").Select(th => th.TextContent.Trim())];

    private static IReadOnlyList<string> FirstRowCells(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder tbody tr:first-child > *").Select(c => c.TextContent.Trim())];

    /// <summary>
    /// One kilde carrying every field the ten optional columns read, each value distinct.
    /// </summary>
    /// <remarks>
    /// The four date-shaped fields are the point. Two are Munin's own — Created and LastUpdated —
    /// and two are the catalogue's, written into the bag as text: Opprettet, a bare founding year,
    /// and SistOppdatert as <c>yyyyMMdd</c>. A column drawing the wrong one of the four still
    /// renders a plausible date, which is why every value here is a different year.
    /// </remarks>
    private static KildeSummary Furnished() =>
        Kilde("Als registeret", "K_ALS", established: "2011") with
        {
            DelkildeCount = 7,
            DataController = "St. Olavs hospital HF",
            DataProcessor = "Folkehelseinstituttet",
            PersonIdentificationLevel = "indirectlyIdentifiable",
            ValidFrom = new DateTimeOffset(2013, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ValidTo = null,
            Created = new DateTimeOffset(2015, 3, 4, 9, 0, 0, TimeSpan.Zero),
            LastUpdated = new DateTimeOffset(2017, 6, 7, 9, 0, 0, TimeSpan.Zero),
            AdditionalProperties = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Opprettet"] = "2011",
                ["SistOppdatert"] = "20190823",
            },
        };

    [Fact]
    public void Columns_WhenTheListLoads_ThenKeldasDefaultSetIsOnScreenAndTheOtherSevenAreNot()
    {
        // The set this component already shipped, restated as the picker's defaults so the change
        // is a control gained rather than a table rearranged. Kelda's DEFAULT_VISIBLE turns on
        // kildetype, datasamlinger and variabler and leaves the other seven off (kelda.tsx:86).
        var cut = RenderWith(new FakeClient(Furnished()));

        Assert.Equal(
        [
            "Vis datasamlinger",  // the expand control's screenreader-only heading
            "Navn",
            "Kildetype",
            "Status",
            "Datasamlinger",
            "Variabler",
            "Opprettet",
        ], Headers(cut));
    }

    [Fact]
    public void Picker_WhenTheListLoads_ThenItOffersKeldasTenColumnsInKeldasOrder()
    {
        // Kelda's OPTIONAL_COLUMNS, in its order (kelda.tsx:74). Navn, Status and Opprettet are
        // absent for the reason they are absent from Kelda's own list: the name is the row's
        // drill-in control, and the other two are what the list is read by.
        var cut = RenderWith(new FakeClient(Furnished()));

        Assert.Equal(
        [
            "Kildetype",
            "Datasamlinger",
            "Variabler",
            "Delkilder",
            "Dataansvarlig",
            "Databehandler",
            "Grad av personidentifikasjon",
            "Gyldighet",
            "Importert",
            "Sist endret",
        ], ColumnToggles(cut).Select(b => b.TextContent.Trim()));

        // aria-pressed is the whole truth about a toggle button, so the defaults have to be
        // readable off it rather than only off the table.
        Assert.Equal(
            ["true", "true", "true", "false", "false", "false", "false", "false", "false", "false"],
            ColumnToggles(cut).Select(b => b.GetAttribute("aria-pressed")));
    }

    [Theory]
    [InlineData("Delkilder", "7")]
    [InlineData("Dataansvarlig", "St. Olavs hospital HF")]
    [InlineData("Databehandler", "Folkehelseinstituttet")]
    [InlineData("Grad av personidentifikasjon", "Indirekte identifiserbar")]
    public void Picker_WhenAHiddenColumnIsTurnedOn_ThenItsHeaderAndItsOwnValueAppear(
        string label, string expected)
    {
        // One case per column, and the value rather than merely the header, because the seven were
        // wired to seven different fields and a header proves only that a cell exists. The three
        // that carry dates are next door: their spelling is not the same everywhere, so they are
        // asserted on the year rather than on a formatted string.
        var cut = RenderWith(new FakeClient(Furnished()));

        Assert.DoesNotContain(label, Headers(cut));

        ToggleColumn(cut, label);

        Assert.Contains(label, Headers(cut));
        Assert.Contains(expected, FirstRowCells(cut));
    }

    [Theory]
    [InlineData("Gyldighet", "2013")]
    [InlineData("Importert", "2015")]
    [InlineData("Sist endret", "2019")]
    public void Picker_WhenADateColumnIsTurnedOn_ThenItDrawsItsOwnFieldAndNotOneOfTheOtherThree(
        string label, string year)
    {
        // The trap this fixture exists for. Four date-shaped fields reach this row and any of them
        // renders a plausible date in any of these columns, so the assertion is on WHICH — each
        // field carries a different year, and the three it must not have read are named.
        //
        // The year and not the formatted date: the month's short form is the runtime's, and CI
        // spells Norwegian March "mars" where this box writes "mar." — which is how the first
        // version of this went red there and green here.
        var cut = RenderWith(new FakeClient(Furnished()));

        ToggleColumn(cut, label);

        var cell = Assert.Single(FirstRowCells(cut), c => c.Contains(year, StringComparison.Ordinal));

        foreach (var other in new[] { "2011", "2013", "2015", "2017", "2019" }.Except([year]))
        {
            Assert.DoesNotContain(other, cell);
        }
    }

    [Fact]
    public void Picker_WhenEveryColumnIsTurnedOn_ThenTheTableIsInKeldasHeaderOrderAndNotThePickers()
    {
        // The two orders differ in Kelda and so differ here: the picker lists counts before free
        // text (kelda.tsx:74), the header draws free text first (kelda.tsx:479-535). Nothing else
        // holds the header order once more than the default three are on, and the header and body
        // are two handwritten loops — so a column moved in one of them lands here.
        var cut = RenderWith(new FakeClient(Furnished()));

        foreach (var label in ColumnToggles(cut)
                     .Where(b => b.GetAttribute("aria-pressed") == "false")
                     .Select(b => b.TextContent.Trim())
                     .ToList())
        {
            ToggleColumn(cut, label);
        }

        Assert.Equal(
        [
            "Vis datasamlinger",
            "Navn",
            "Kildetype",
            "Status",
            "Dataansvarlig",
            "Databehandler",
            "Grad av personidentifikasjon",
            "Gyldighet",
            "Delkilder",
            "Datasamlinger",
            "Variabler",
            "Opprettet",
            "Importert",
            "Sist endret",
        ], Headers(cut));

        // The cells in order, not merely as many of them: `Headers` reads thead alone, so comparing
        // counts leaves the body free to draw the same columns in a different order — every cell
        // under the wrong `scope="col"`, for a screen reader as much as for a reader. Every field
        // in Furnished() has a different value so a swap cannot look like a match.
        Assert.Equal(
        [
            "+",
            NameCellText(cut),
            "Sentralt helseregister",
            "Aktiv",
            "St. Olavs hospital HF",
            "Folkehelseinstituttet",
            "Indirekte identifiserbar",
            ValidityText(cut),
            "7",
            "3",
            "42",
            "2011",
            ImportedText(cut),
            SourceUpdatedText(cut),
        ], FirstRowCells(cut));
    }

    /// <summary>The name cell as the row draws it, name and code together.</summary>
    /// <remarks>
    /// Read back rather than written down: the two are separate elements with the markup's own
    /// indentation between them, so a literal here pins the razor file's whitespace and breaks on a
    /// reindent with a diff nobody can read. What the caller asserts is its POSITION in the row.
    /// </remarks>
    private static string NameCellText(IRenderedComponent<KildeExplorer> cut) =>
        FirstRowCells(cut).Single(c => c.Contains("K_ALS", StringComparison.Ordinal));

    /// <summary>The three date cells as this runtime spells them.</summary>
    /// <remarks>
    /// Read back rather than written down, because the month's short form is the runtime's: CI
    /// spells Norwegian March "mars" where this box writes "mar.". Their year is what says the
    /// column read the right field, and that is asserted next door in
    /// <see cref="Picker_WhenADateColumnIsTurnedOn_ThenItDrawsItsOwnFieldAndNotOneOfTheOtherThree"/>.
    /// What these three hold up is the ORDER of the row, which no spelling affects.
    /// </remarks>
    private static string ValidityText(IRenderedComponent<KildeExplorer> cut) =>
        FirstRowCells(cut).Single(c => c.Contains("2013", StringComparison.Ordinal));

    private static string ImportedText(IRenderedComponent<KildeExplorer> cut) =>
        FirstRowCells(cut).Single(c => c.Contains("2015", StringComparison.Ordinal));

    private static string SourceUpdatedText(IRenderedComponent<KildeExplorer> cut) =>
        FirstRowCells(cut).Single(c => c.Contains("2019", StringComparison.Ordinal));

    [Fact]
    public void DataController_WhenTheCatalogueLeftItEmpty_ThenTheCellIsNotMarkedAsNorwegian()
    {
        // A lang the content is not in is worse than none — WCAG 3.1.2. The empty cell holds our
        // own "Not specified", in the reader's language, so marking it Norwegian would switch a
        // screen reader's voice for an English phrase. CatalogueLang is what answers null there;
        // the first version of these two columns reached past it to Foreign() and stamped both.
        var cut = RenderWith(
            new FakeClient(Furnished() with { DataController = null, DataProcessor = null }),
            b => b.Add(c => c.Language, "en"));

        ToggleColumn(cut, "Data controller");
        ToggleColumn(cut, "Data processor");

        var empty = cut.FindAll(".munin-explorer-kilder tbody td")
            .Where(td => td.TextContent.Trim() == "Not specified")
            .ToList();

        Assert.Equal(2, empty.Count);
        Assert.All(empty, td => Assert.Null(td.GetAttribute("lang")));
    }

    [Fact]
    public void DataController_WhenTheCatalogueFilledItIn_ThenTheCellIsMarkedAsNorwegian()
    {
        // The other half of the rule above, and the half that was missing: with both `lang`
        // attributes simply deleted the empty-cell test still passed, and an English reader heard
        // "St. Olavs hospital HF" read out in an English voice. The catalogue holds one language.
        var cut = RenderWith(new FakeClient(Furnished()), b => b.Add(c => c.Language, "en"));

        ToggleColumn(cut, "Data controller");
        ToggleColumn(cut, "Data processor");

        var cells = cut.FindAll(".munin-explorer-kilder tbody td")
            .Where(td => td.TextContent.Trim() is "St. Olavs hospital HF" or "Folkehelseinstituttet")
            .ToList();

        Assert.Equal(2, cells.Count);
        Assert.All(cells, td => Assert.Equal("no", td.GetAttribute("lang")));
    }

    [Fact]
    public void Validity_WhenTheCatalogueGaveNoEndDate_ThenTheCellSaysItIsOngoing()
    {
        // The wiring, not the formatting: CatalogueDate.Period is pinned elsewhere, but nothing
        // here noticed which ends were handed to it. Passing ValidFrom twice draws "2013 – 2013",
        // which every other assertion about this column accepts, since they all look for 2013.
        var cut = RenderWith(new FakeClient(Furnished()));

        ToggleColumn(cut, "Gyldighet");

        var cell = FirstRowCells(cut).Single(c => c.Contains("2013", StringComparison.Ordinal));

        Assert.EndsWith("Pågående", cell, StringComparison.Ordinal);
    }

    [Fact]
    public void Validity_WhenTheKildeHasClosed_ThenTheCellCarriesBothEnds()
    {
        // The other end, and the direction that ships a lie rather than a blank: with ValidTo
        // dropped on the way to the formatter, a register that stopped collecting in 2019 still
        // reads "Pågående". The test above cannot see that — every fixture here leaves ValidTo
        // null, so passing null explicitly changes nothing it looks at.
        var cut = RenderWith(new FakeClient(
            Furnished() with { ValidTo = new DateTimeOffset(2019, 12, 31, 0, 0, 0, TimeSpan.Zero) }));

        ToggleColumn(cut, "Gyldighet");

        var cell = FirstRowCells(cut).Single(c => c.Contains("2013", StringComparison.Ordinal));

        Assert.Contains("2019", cell, StringComparison.Ordinal);
        Assert.DoesNotContain("Pågående", cell, StringComparison.Ordinal);
    }

    [Fact]
    public void Imported_WhenThePayloadCarriesNoTimestamp_ThenTheCellSaysSoRatherThanDrawingYearOne()
    {
        // KildeSummary.Created is not nullable, so a payload that omits `opprettet` leaves it at
        // default and the column drew "1. januar 0001" — a date the catalogue never wrote, in a
        // column whose whole job is to say when Munin took the row.
        var cut = RenderWith(new FakeClient(Furnished() with { Created = default }));

        ToggleColumn(cut, "Importert");

        Assert.Contains("Ikke oppgitt", FirstRowCells(cut));
    }

    [Fact]
    public void SourceUpdated_WhenTheListIsTheCapturedPayload_ThenItShowsTheYearsTheApiSent()
    {
        // The one Sist endret test that does not write the key it reads. Every other one builds its
        // own bag and spells SistOppdatert the way the component looks it up, so all of them pass
        // just as well against a key the API never sends and a column of "Ikke oppgitt" ships —
        // which is exactly the hole the Opprettet column has a captured-payload test for.
        var kilder = JsonSerializer.Deserialize<IReadOnlyList<KildeSummary>>(
                TestData.Read("kilder.json"), MuninExplorerClient.Json)
            ?? throw new InvalidOperationException("kilder.json no longer reads as a kilde list.");

        var cut = RenderWith(new FakeClient([.. kilder]));

        ToggleColumn(cut, "Sist endret");

        // Years, not formatted dates: the month's short form is the runtime's. The payload holds
        // 20260423, 20260813 and 20230131.
        var years = cut.FindAll(".munin-explorer-kilder tbody tr")
            .Select(row => row.QuerySelectorAll("td")[^1].TextContent.Trim())
            .Select(text => text.Length >= 4 ? text[^4..] : text);

        Assert.Equal(["2026", "2026", "2023"], years);
    }

    [Fact]
    public void Picker_WhenAShownColumnIsTurnedOff_ThenItsHeaderAndItsCellsGoTogether()
    {
        // Header and body are two loops over the same choice, so a column can be taken out of one
        // and left in the other — which is not a cosmetic fault: it shifts every cell after it
        // under the wrong header for a screen reader as well as for a reader.
        var cut = RenderWith(new FakeClient(Furnished()));

        ToggleColumn(cut, "Kildetype");

        Assert.DoesNotContain("Kildetype", Headers(cut));
        Assert.Equal(Headers(cut).Count, FirstRowCells(cut).Count);
    }

    [Fact]
    public void Picker_WhenEveryOptionalColumnIsTurnedOff_ThenTheRowStillSaysWhatTheKildeIs()
    {
        // No last-column lock here, unlike the variable explorer's picker, because there is nothing
        // for one to prevent: Navn, Status and Opprettet are outside the picker's reach. The test
        // exists to keep that true — a lock added here would stop the tenth press, and a column
        // moved INTO the picker would let it empty a row.
        var cut = RenderWith(new FakeClient(Furnished()));

        // Only the ones that are on, since a press on a hidden column turns it back on.
        foreach (var label in ColumnToggles(cut)
                     .Where(b => b.GetAttribute("aria-pressed") == "true")
                     .Select(b => b.TextContent.Trim())
                     .ToList())
        {
            ToggleColumn(cut, label);
        }

        Assert.Equal(["Vis datasamlinger", "Navn", "Status", "Opprettet"], Headers(cut));
        Assert.Equal(Headers(cut).Count, FirstRowCells(cut).Count);
        Assert.All(ColumnToggles(cut), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public async Task Picker_WhenTheChoiceChanges_ThenTheDatasamlingerPanelStillSpansEveryColumn()
    {
        // The nested row carries a colspan, which is a count and not a layout: too small and the
        // panel leaves a column of dead cells beside it, too large and the table is malformed.
        // Nothing else here would notice, since both still render.
        var cut = RenderWith(new FakeClient(Furnished()));

        cut.Find(".munin-explorer-kilder__expand-toggle").Click();
        await cut.InvokeAsync(() => { });

        // Two on and none off, so the count moves. An earlier version of this turned one on and one
        // off, which left the table at exactly the seven columns the old constant named — so the
        // constant survived the mutation and this passed while proving nothing.
        ToggleColumn(cut, "Delkilder");
        ToggleColumn(cut, "Dataansvarlig");

        var panel = cut.Find(".munin-explorer-kilder__expanded");

        Assert.Equal(Headers(cut).Count.ToString(), panel.GetAttribute("colspan"));
    }

    [Fact]
    public void SourceUpdated_WhenTheCatalogueDateIsNotOne_ThenItIsShownAsTheCatalogueWroteIt()
    {
        // The catalogue writes this field as text and writes junk in it. A formatter asked to read
        // "20260231" answers 2 March, which hides a fault at source behind a plausible date; a
        // formatter asked to read "ukjent" answers nothing at all. Both are handed on unchanged.
        var cut = RenderWith(new FakeClient(
            Furnished() with
            {
                AdditionalProperties = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SistOppdatert"] = "20260231",
                },
            }));

        ToggleColumn(cut, "Sist endret");

        Assert.Contains("20260231", FirstRowCells(cut));
    }

    [Fact]
    public void Picker_WhenTheListIsEmpty_ThenThereIsNoPickerToOffer()
    {
        // The table is what the picker is about, and an empty list has none — so the control would
        // be offering columns for something that is not there. The variable explorer keeps its own
        // header on an empty result on purpose, because that header also holds the ordering the
        // reader just pressed; this one holds nothing else.
        var cut = RenderWith(new FakeClient());

        Assert.Empty(cut.FindAll(".dropdown-choicepicker__item button"));
    }
}
