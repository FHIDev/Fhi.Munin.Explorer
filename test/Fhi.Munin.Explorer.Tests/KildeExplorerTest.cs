using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Kelda's shell: the search field, the count, the eight-column result table and what happens when
/// a kilde is opened.
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
        string? dataController = "Folkehelseinstituttet",
        string? dataProcessor = "Folkehelseinstituttet",
        int delkilder = 0,
        int datasamlinger = 3,
        int variables = 42,
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
            DataController = dataController,
            DataProcessor = dataProcessor,
            DelkildeCount = delkilder,
            DatasamlingCount = datasamlinger,
            TotalVariables = variables,
            AdditionalProperties = Properties(category, accessRights),
        };

    /// <summary>
    /// The curated bag two of the facets read from, holding only the keys a test asked for.
    /// </summary>
    /// <remarks>
    /// A key is left out entirely rather than set to null or to an empty string, because that is
    /// what the API does for a kilde nobody filled the field in on — and "the key is absent" is the
    /// state the empty-facet rule has to survive. <paramref name="category"/> is passed as the API
    /// writes it, a JSON array inside a string, so a test can hand over a malformed one as easily
    /// as a good one.
    /// </remarks>
    private static IReadOnlyDictionary<string, string?> Properties(string? category, string? accessRights)
    {
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);

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

        /// <summary>How many detail fetches have been left hanging.</summary>
        public int Stalls => _stalls.Count;

        /// <summary>Fail every detail fetch from the next one on — the API being down, not an id it does not know.</summary>
        public bool FailDetail { get; set; }

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

        /// <summary>Answer the oldest detail fetch still hanging.</summary>
        public void AnswerStalled(KildeDetail detail) => Oldest().TrySetResult(detail);

        /// <summary>Fail the oldest detail fetch still hanging.</summary>
        public void FailStalled() => Oldest().TrySetException(new HttpRequestException("the API is down"));

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

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DetailCalls++;

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

    /// <summary>Fails the list call, which is the API being down rather than the catalogue being empty.</summary>
    private sealed class FailingClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("the API is down");
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
    public void Render_WhenTwoInstancesShareAPage_ThenTheirDomIdsDoNotCollide()
    {
        // Duplicate ids break label association and fail WCAG 4.1.1. helsedata can legitimately put
        // more than one explorer on a page.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var a = Render<KildeExplorer>();
        var b = Render<KildeExplorer>();

        var idA = a.Find("input[type=search]").Id;
        var idB = b.Find("input[type=search]").Id;

        Assert.False(string.IsNullOrWhiteSpace(idA));
        Assert.NotEqual(idA, idB);
    }

    // ---------------------------------------------------------------------------------
    // The columns.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Always_ThenTheTableHasTheEightColumnsInKeldasOwnOrder()
    {
        // Measured off Munin's own Kelda. Its table puts two control columns in front of these —
        // an expand toggle and a row checkbox — and hides four more behind a column picker; the
        // checkbox belongs to Fhi.Metadata-5ghur and the picker to Fhi.Metadata-ay3zz, so neither
        // is here. The order of the eight that are is Kelda's, unchanged.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var headers = cut.FindAll(".munin-explorer-kilder thead th").Select(th => th.TextContent.Trim());

        Assert.Equal(
        [
            "Navn",
            "Kildetype",
            "Status",
            "Dataansvarlig",
            "Databehandler",
            "Delkilder",
            "Datasamlinger",
            "Variabler",
        ], headers);
    }

    [Fact]
    public void Render_Always_ThenARowCarriesAValueForEveryColumnItHasOne()
    {
        var cut = RenderWith(new FakeClient(Kilde(
            "Dødsårsaksregisteret", "K_DAR",
            shortName: "DÅR",
            kildetype: "sentraltHelseregister",
            dataController: "Folkehelseinstituttet",
            dataProcessor: "Norsk helsenett SF",
            delkilder: 2,
            datasamlinger: 7,
            variables: 312)));

        var row = cut.Find(".munin-explorer-kilder tbody tr");
        var cells = row.QuerySelectorAll("th, td").Select(c => c.TextContent.Trim()).ToList();

        // The name cell carries the code under the name, the way Kelda does: it is how a reader who
        // knows K_DAR finds the row whose name they do not know.
        Assert.StartsWith("Dødsårsaksregisteret", cells[0]);
        Assert.Contains("K_DAR", cells[0]);

        Assert.Equal("Sentralt helseregister", cells[1]);
        Assert.Equal("Aktiv", cells[2]);
        Assert.Equal("Folkehelseinstituttet", cells[3]);
        Assert.Equal("Norsk helsenett SF", cells[4]);
        Assert.Equal("2", cells[5]);
        Assert.Equal("7", cells[6]);
        Assert.Equal("312", cells[7]);
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
    public void Render_WhenTheCatalogueLeftAFieldEmpty_ThenTheCellSaysSoRatherThanGoingBlank()
    {
        // "Ikke oppgitt" for everyone, rather than an em dash whispered to assistive technology —
        // the rule the rest of this package follows.
        var cut = RenderWith(new FakeClient(
            Kilde("Als registeret", "K_ALS", dataController: null, dataProcessor: "")));

        var cells = cut.FindAll(".munin-explorer-kilder tbody td").Select(c => c.TextContent.Trim()).ToList();

        Assert.Equal("Ikke oppgitt", cells[2]);
        Assert.Equal("Ikke oppgitt", cells[3]);
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

        var input = cut.Find("input[type=search]");

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
        cut.Find("input[type=search]").Change("registeret");
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

        cut.Find("input[type=search]").Change("norpd");

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

        cut.Find("input[type=search]").Change("dår");

        Assert.Equal(["Dødsårsaksregisteret"], RowNames(cut));
    }

    [Fact]
    public void Search_WhenTheTermIsInAnotherCase_ThenItStillMatches()
    {
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        cut.Find("input[type=search]").Change("ALS REGISTERET");

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Search_WhenNothingMatches_ThenTheEmptyStateNamesWhatWasSearchedFor()
    {
        // A different sentence from the one an empty catalogue gets: this one tells the reader the
        // catalogue has kilder and that their words were the problem, which is the difference
        // between trying again and giving up.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        cut.Find("input[type=search]").Change("hjortedyr");

        Assert.Contains("Ingen kilder samsvarer med søket «hjortedyr»", cut.Markup);
        Assert.Empty(cut.FindAll(".munin-explorer-kilder"));
    }

    [Fact]
    public void Search_WhenTheTermIsCleared_ThenTheWholeListComesBackWithoutARefetch()
    {
        var client = new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Reseptregisteret", "K_NORPD"));

        var cut = RenderWith(client);

        cut.Find("input[type=search]").Change("als");
        cut.Find("input[type=search]").Change("");

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

        // Kelda's own, in the same slot. The datasamling heading is the fourth section and arrives
        // the other way, as DataCollectionsHeading over the core's own table.
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
    public void Select_WhenAKildeIsChosen_ThenKildeViewIsGivenKeldasOwnHeadingForTheDatasamlinger()
    {
        // The other half of the same seam: Runa says "Datasamlinger" over those rows and Kelda says
        // "Delkilder og datasamlinger". One word is not worth a second table, so it arrives as a
        // parameter — and a component that hard-coded either word would be renaming the other
        // explorer's heading.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        var cut = RenderWith(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        Assert.Equal(
            "Delkilder og datasamlinger",
            cut.FindComponent<KildeView>().Instance.DataCollectionsHeading);
    }

    [Fact]
    public void Select_WhenTheReaderGoesBack_ThenTheListIsThereAsTheyLeftIt()
    {
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als, Kilde("Reseptregisteret", "K_NORPD")).Publishing(als);

        var cut = RenderWith(client);

        cut.Find("input[type=search]").Change("als");
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
        Assert.Contains("Sub-sources", cut.Markup);
        Assert.DoesNotContain("Kildeutforsker", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheCatalogueLeftAFieldEmpty_ThenTheCellIsNotMarkedAsTheCataloguesLanguage()
    {
        // The cell holds the package's own "Not specified" then, in the reader's own language, so
        // a lang="no" left on it switches a screen reader to a Norwegian voice for an English
        // sentence — WCAG 3.1.2, Language of Parts. KildeView never hits this because it drops
        // blank facts before rendering them; a table has to keep the cell, so it drops the
        // attribute instead.
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS", dataController: null, dataProcessor: null)),
            b => b.Add(c => c.Language, "en"));

        var cells = cut.FindAll(".munin-explorer-kilder tbody td");

        Assert.Equal("Not specified", cells[2].TextContent.Trim());
        Assert.Null(cells[2].GetAttribute("lang"));
        Assert.Equal("Not specified", cells[3].TextContent.Trim());
        Assert.Null(cells[3].GetAttribute("lang"));
    }

    [Fact]
    public void Render_WhenTheCatalogueSuppliedTheField_ThenTheCellIsMarkedAsTheCataloguesLanguage()
    {
        // The other half, so the fix above cannot be "stop marking anything": the catalogue holds
        // these two in Norwegian whatever the reader is reading in.
        var cut = RenderWith(
            new FakeClient(Kilde("Als registeret", "K_ALS")),
            b => b.Add(c => c.Language, "en"));

        var cells = cut.FindAll(".munin-explorer-kilder tbody td");

        Assert.Equal("Folkehelseinstituttet", cells[2].TextContent.Trim());
        Assert.Equal("no", cells[2].GetAttribute("lang"));
        Assert.Equal("no", cells[3].GetAttribute("lang"));
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

        // Raw tokens, deliberately: they are EHDS CURIEs from a controlled vocabulary this package
        // is not the author of, and the list endpoint sends no labels for them. Inventing words here
        // would put a translation on screen that nothing in the catalogue backs.
        Assert.Equal(
        [
            "ehds-cat:biobanks (1)",
            "ehds-cat:health-registries (1)",
            "ehds-cat:population-health-surveys (1)"
        ], Choices(Facet(cut, "Kategori")));

        Tick(cut, "Kategori", "ehds-cat:biobanks");

        Assert.Equal(["Den norske mor, far og barn-undersøkelsen"], RowNames(cut));
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

        cut.Find("input[type=search]").Change("als");

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

        cut.Find("input[type=search]").Change("als");
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
        // The exact list, for the reason the other two of these are exact: a tenth name appearing
        // here is news, and news that has to be answered in both sample stylesheets before it
        // ships. Four of these nine are the explorer's existing structure, reused rather than
        // reinvented; the three under `munin-explorer-kilder` and the two under
        // `munin-explorer-filters__` are this view's own.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var invented = HostClassNames.Of(cut.FindAll("[class]"))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            "munin-explorer",                    // shared with the variable explorer
            "munin-explorer-container",          // shared
            "munin-explorer-filters",            // shared
            "munin-explorer-filters__facets",
            "munin-explorer-filters__toggle",
            "munin-explorer-kilder",
            "munin-explorer-kilder__count",
            "munin-explorer-kilder__name",
            "munin-explorer-results",            // shared
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
}
