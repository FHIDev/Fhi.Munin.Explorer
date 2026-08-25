using System.Text.RegularExpressions;
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
/// assertion that only looks for the text on screen. The assertion here is on the parameter.
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
        int variables = 42) =>
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
        };

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

        public string? LastSearch { get; private set; }
        public string? LastKildeType { get; private set; }
        public int Calls { get; private set; }
        public int DetailCalls { get; private set; }

        /// <summary>Publish a detail for a kilde; anything not published answers null, as the API does.</summary>
        public FakeClient Publishing(params KildeSummary[] summaries)
        {
            foreach (var summary in summaries)
            {
                _details[summary.Id] = Detail(summary);
            }

            return this;
        }

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

            return Task.FromResult(_details.TryGetValue(id, out var detail) ? detail : null);
        }
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
        // again — and the facets in the bead that follows count over this list.
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
        // hold up. So the assertion is on the parameter, not on the output.
        var als = Kilde("Als registeret", "K_ALS");
        var client = new FakeClient(als).Publishing(als);

        RenderFragment sections = builder => builder.AddMarkupContent(0, "<p>Kriterier for tilgang</p>");

        var cut = RenderWith(client, b => b.Add(c => c.Sections, sections));

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var view = cut.FindComponent<KildeView>();

        Assert.Same(sections, view.Instance.Sections);
        Assert.Contains("Kriterier for tilgang", cut.Markup);
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
    public void Component_WhenItsSourceIsRead_ThenItHasNoPageNoRenderModeAndNoRouter()
    {
        // A one-off check that costs nothing and catches the one edit that makes this package
        // unmountable in helsedata's Optimizely host, where there is no router at all and the host
        // decides the render mode at the mount site. Neither shows up as a failing render here:
        // bUnit supplies both.
        //
        // Razor comments are stripped first, because this file explains in prose why it has no
        // @page and no @rendermode — a check that a comment can break is one that gets deleted the
        // first time somebody documents the rule it enforces.
        var markup = Regex.Replace(ComponentSource(), @"@\*.*?\*@", " ", RegexOptions.Singleline);

        Assert.DoesNotContain("@page", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@rendermode", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@attribute [Route", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<Router", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("HeadOutlet", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Component_WhenItsSourceIsRead_ThenItShipsNoStylesheetOfItsOwn()
    {
        // The package ships no CSS, and a scoped `.razor.css` beside a component is the one way to
        // add some without touching the project file — scripts/assert-package-contents.sh catches
        // it in the packed artefact, and this catches it in the checkout.
        Assert.False(File.Exists(ComponentPath() + ".css"));
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
        // The exact list, for the reason the other two of these are exact: a seventh name appearing
        // here is news, and news that has to be answered in both sample stylesheets before it
        // ships. Three of these six are the explorer's existing structure, reused rather than
        // reinvented; the three under `munin-explorer-kilder` are this view's own.
        var cut = RenderWith(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var invented = HostClassNames.Of(cut.FindAll("[class]"))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            "munin-explorer",                    // shared with the variable explorer
            "munin-explorer-container",          // shared
            "munin-explorer-kilder",
            "munin-explorer-kilder__count",
            "munin-explorer-kilder__name",
            "munin-explorer-results",            // shared
        ], invented);
    }

    private static string ComponentPath() =>
        Path.Combine(RepoRoot(), "src", "Fhi.Munin.Explorer", "Blazor", "KildeExplorer.razor");

    private static string ComponentSource() => File.ReadAllText(ComponentPath());

    /// <summary>
    /// The checkout root, walked up to from the test binary rather than taken from the working
    /// directory, which differs between <c>dotnet test</c>, the IDE runner and CI.
    /// </summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Fhi.Munin.Explorer.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                $"No Fhi.Munin.Explorer.slnx above '{AppContext.BaseDirectory}', so the component source "
                + "this check reads cannot be found. Running the tests from outside the checkout?");
    }
}
