using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class VariableExplorerTest : BunitContext
{
    private static Page<VariableSummary> OnePage(params VariableSummary[] rows) =>
        new() { Items = rows, TotalCount = rows.Length, PageNumber = 1, Size = 25, TotalPages = 1 };

    private static VariableSummary Variable(string name, string code, string? kilde = "Als registeret") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            PreferredTerm = name,
            KildeName = kilde,
            DatasamlingName = "Inklusjon",
            DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DataTo = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };

    private sealed class FakeClient(Page<VariableSummary> answer) : EmptyMuninExplorerClient
    {
        public string? LastSearch { get; private set; }
        public int LastPage { get; private set; }
        public SortField LastSort { get; private set; }
        public SortDirection LastDirection { get; private set; }
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastSearch = search;
            LastPage = page;
            LastSort = sort;
            LastDirection = direction;
            Calls++;
            return Task.FromResult(answer);
        }
    }

    /// <summary>
    /// Fails every call. Given a <paramref name="firstAnswer"/> it answers the first one and fails
    /// only from the second — the case where a sort fails over rows already on screen.
    /// </summary>
    private sealed class FailingClient(Page<VariableSummary>? firstAnswer = null) : EmptyMuninExplorerClient
    {
        public SortField LastSort { get; private set; }
        public SortDirection LastDirection { get; private set; }
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastSort = sort;
            LastDirection = direction;
            Calls++;

            return Calls == 1 && firstAnswer is not null
                ? Task.FromResult(firstAnswer)
                : throw new HttpRequestException("nede");
        }
    }

    /// <summary>
    /// A client that never answers until the test lets it, so the loading state can be inspected.
    /// Given a <paramref name="firstAnswer"/> it answers the first call at once and stalls only on
    /// the next one — the case where a second search is in flight over rows already on screen.
    /// </summary>
    private sealed class SlowClient(Page<VariableSummary>? firstAnswer = null) : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<Page<VariableSummary>> _answer =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Calls == 1 && firstAnswer is not null ? Task.FromResult(firstAnswer) : _answer.Task;
        }

        public void Answer(Page<VariableSummary> page) => _answer.TrySetResult(page);
    }

    private IRenderedComponent<VariableExplorer> RenderWith(
        IMuninExplorerClient client, Action<ComponentParameterCollectionBuilder<VariableExplorer>>? p = null)
    {
        Services.AddSingleton(client);
        return Render<VariableExplorer>(b => p?.Invoke(b));
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenACardIsShownPerVariable()
    {
        var client = new FakeClient(OnePage(Variable("1. Tale", "V_ALS.F1.ALSFRSR1TALE"),
                                           Variable("2. Spyttsekresjon", "V_ALS.F1.ALSFRSR2SPYTT")));

        var cut = RenderWith(client);

        Assert.Equal(2, cut.FindAll("ul.datasourcecard-list > li").Count);
        Assert.Contains("1. Tale", cut.Markup);
        Assert.Contains("V_ALS.F1.ALSFRSR1TALE", cut.Markup);
        Assert.Contains("2 variabler", cut.Markup);
    }

    [Fact]
    public void Render_WhenThereAreNoHits_ThenTheEmptyMessageIsShown()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Empty(cut.FindAll("ul.datasourcecard-list > li"));
        Assert.Contains("Ingen variabler passet søket", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheApiFails_ThenAnErrorMessageIsShownRatherThanThrowing()
    {
        var cut = RenderWith(new FailingClient());

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("ul.datasourcecard-list > li"));
    }

    [Fact]
    public void Render_WhenTheLanguageIsEn_ThenTheEnglishTextsAreUsed()
    {
        // helsedata's culture token is "en"/"no", not "nb" — worth pinning.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                         b => b.Add(c => c.Language, "en"));

        Assert.Contains("Variable explorer", cut.Markup);
        Assert.Contains("1 variable", cut.Markup);
        Assert.DoesNotContain("Variabelutforsker", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheHostSetsTheSearch_ThenItIsSentToTheApi()
    {
        var client = new FakeClient(OnePage());

        RenderWith(client, b => b.Add(c => c.Search, "tale"));

        Assert.Equal("tale", client.LastSearch);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Render_WhenTwoInstancesShareAPage_ThenTheirDomIdsDoNotCollide()
    {
        // Duplicate ids break label association and fail WCAG 4.1.1. helsedata can
        // legitimately put more than one explorer on a page.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(OnePage()));

        var a = Render<VariableExplorer>();
        var b = Render<VariableExplorer>();

        var idA = a.Find("input[type=search]").Id;
        var idB = b.Find("input[type=search]").Id;

        Assert.False(string.IsNullOrWhiteSpace(idA));
        Assert.NotEqual(idA, idB);
    }

    [Fact]
    public void Search_WhenTheUserTypesInTheField_ThenNoServerRoundTripIsMade()
    {
        // Regression guard. The field used to be value="@_sok" + @oninput, which on
        // helsedata's Blazor Server circuit is one round-trip per keystroke — and the
        // re-render each round-trip triggers rewrote the element while more input was
        // still arriving, so a fast fill lost characters ("svelging" arrived as "sng").
        // No registered oninput handler means the browser event never reaches the circuit,
        // and bUnit says so by refusing to dispatch it.
        var client = new FakeClient(OnePage());
        var cut = RenderWith(client);

        var input = cut.Find("input[type=search]");

        Assert.Throws<MissingEventHandlerException>(() => input.Input("svelging"));
        Assert.Equal(1, client.Calls); // only the initial load
    }

    [Fact]
    public void Search_WhenTheWholeTextIsTypedBeforeSubmitting_ThenItSearchesOnceWithAllOfIt()
    {
        var client = new FakeClient(OnePage());
        var cut = RenderWith(client);

        // onchange carries the finished value, however fast it was typed or pasted.
        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal(2, client.Calls); // initial load + this one search
    }

    [Fact]
    public void Search_WhenTheUserClicksSearchWithoutLeavingTheFieldFirst_ThenTheWholeTextIsSearchedFor()
    {
        // The case onchange has to survive: type, then go straight for the Søk button
        // without tabbing away. The browser blurs the field as the button takes focus, so
        // change reaches the circuit before the click turns into a submit — this test
        // pins that order, and would fail if the value only ever arrived on blur-by-tab.
        var client = new FakeClient(OnePage());
        var cut = RenderWith(client);

        cut.Find("input[type=search]").Change("svelging"); // blur caused by the click
        cut.Find("button[type=submit]").Click();

        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public void Render_Always_ThenTheSearchFieldHasAnAssociatedLabel()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        var input = cut.Find("input[type=search]");
        var label = cut.Find("label");

        Assert.Equal(input.Id, label.GetAttribute("for"));
    }

    // ---------------------------------------------------------------------------------
    // Sorting. Runa sorts by clicking a column header; there are no headers here, so the
    // ordering gets a control of its own above the list. The rules it keeps from Runa are
    // the ones about the ORDER, not about the headers: four sortable orders, the API's own
    // default ascending to start with, the active field reverses, and any change goes back
    // to page one.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The sort control's own fieldset.
    /// </summary>
    /// <remarks>
    /// Both the sort control and the filter panel are <c>form-fieldset</c> — Stiler's name for a
    /// fieldset with its border off — so a selector for one has to say it is not the other. Without
    /// the exclusion these tests would silently start asserting about the filters.
    /// </remarks>
    private const string SortControl = "fieldset.form-fieldset:not(.variable-explorer-filters)";

    /// <summary>The sort buttons, in the order they are rendered.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> SortButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll($"{SortControl} button");

    /// <summary>Clicks the sort button with the given label, whatever direction suffix it carries.</summary>
    private static void ClickSort(IRenderedComponent<VariableExplorer> cut, string label) =>
        SortButtons(cut).Single(k => k.TextContent.StartsWith(label, StringComparison.Ordinal)).Click();

    [Fact]
    public void Render_WhenNoSortIsChosen_ThenTheApisOwnOrderIsAskedForAscending()
    {
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));

        var cut = RenderWith(client);

        Assert.Equal(SortField.Default, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
        Assert.Equal("Standard (stigende)", SortButtons(cut)[0].TextContent);
    }

    [Fact]
    public void Render_Always_ThenTheDefaultOrderIsNotLabelledAsANameSort()
    {
        // The API's `name` sort leads with kilde, not the name — see the remarks on
        // SortField.Default. A button reading "Navn" would describe an order the list is not in,
        // and would make this button and Datakilde look like they differ in primary key when they
        // only differ in what happens inside a kilde.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var labels = SortButtons(cut).Select(k => k.TextContent).ToList();

        Assert.Equal(["Standard (stigende)", "Datakilde", "Datasamling", "Variabelgruppe"], labels);
        Assert.DoesNotContain("Navn", cut.Find(SortControl).TextContent);
    }

    [Fact]
    public void Render_Always_ThenEverySortFieldTheContractOffersHasAButton()
    {
        // The button row is Enum.GetValues rather than a list of its own, so it cannot fall behind
        // the enum. Kode, datatype, status and dataperiode are absent because they are absent
        // there — the API does not sort on them, and a fifth button would reorder nothing while
        // claiming to have.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Equal(Enum.GetValues<SortField>().Length, SortButtons(cut).Count);
    }

    [Theory]
    [InlineData("Datakilde", SortField.Kilde)]
    [InlineData("Datasamling", SortField.Datasamling)]
    [InlineData("Variabelgruppe", SortField.Variabelgruppe)]
    public void Sort_WhenAnotherFieldIsChosen_ThenThatFieldIsFetchedAscending(string label, SortField expected)
    {
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, label);

        Assert.Equal(expected, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
        Assert.Equal(2, client.Calls); // initial load + this one
    }

    [Fact]
    public void Sort_WhenTheActiveFieldIsChosenAgain_ThenTheDirectionReverses()
    {
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, "Standard");

        Assert.Equal(SortField.Default, client.LastSort);
        Assert.Equal(SortDirection.Descending, client.LastDirection);
        Assert.Equal("Standard (synkende)", SortButtons(cut)[0].TextContent);

        ClickSort(cut, "Standard");

        Assert.Equal(SortDirection.Ascending, client.LastDirection);
    }

    [Fact]
    public void Sort_WhenANewFieldIsChosenAfterDescending_ThenItStartsAscendingAgain()
    {
        // Runa's rule: a new column always starts ascending rather than inheriting the direction
        // of the one before it.
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, "Standard");    // descending
        ClickSort(cut, "Datakilde");   // a different field

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
    }

    [Fact]
    public void Sort_WhenTheOrderChanges_ThenTheFirstPageIsFetchedAgain()
    {
        // Reordering renumbers every page, so page 7 of the old order is not page 7 of the new one.
        // There is no pager yet (bead Fhi.Metadata-l9l2n.12), which is exactly why this is pinned
        // now: the reset has to already be in place when one arrives.
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, "Datasamling");

        Assert.Equal(1, client.LastPage);
    }

    [Fact]
    public void Sort_WhenAFetchIsAlreadyRunning_ThenTheClickIsIgnored()
    {
        // Same reasoning as a second submit: the buttons are never disabled, because disabling the
        // element that has focus drops focus to <body>. Changing the state and skipping the fetch
        // would leave a button claiming an order the list is not in, so the guard comes first.
        var client = new SlowClient();
        var cut = RenderWith(client);

        ClickSort(cut, "Datakilde");

        Assert.Equal(1, client.Calls);
        Assert.Equal("Standard (stigende)", SortButtons(cut)[0].TextContent);
    }

    [Fact]
    public void Sort_WhenTheFetchFails_ThenTheOrderRollsBackToTheOneTheListIsActuallyIn()
    {
        // The buttons and the status line describe the order the list is in, and a failed fetch
        // delivered no order at all. Left moved, the state would have them claim one the API never
        // returned — and pressing the same button again would take the reversal branch and ask for
        // descending, leaving no way back to the ascending fetch that just failed short of cycling
        // twice. Same invariant as the _laster guard, on the path that guard cannot see.
        var client = new FailingClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, "Datakilde");

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Equal("Standard (stigende)", SortButtons(cut)[0].TextContent);

        var marked = SortButtons(cut).Where(k => k.HasAttribute("aria-current")).ToList();
        Assert.Equal("Standard (stigende)", Assert.Single(marked).TextContent);

        ClickSort(cut, "Datakilde"); // the same retry, not its reversal

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
        Assert.Equal(3, client.Calls);
    }

    [Fact]
    public void Sort_WhenTheBoxHoldsTextNobodySubmitted_ThenSortingDoesNotSearchForIt()
    {
        // A sort click blurs the field first — the same ordering the Søk button relies on — so the
        // change event has already written the box's contents to _sok by the time the click is
        // handled. Fetching with that would run a search nobody asked for, and the status line
        // would then describe the accidental search instead of saying anything had moved.
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client, b => b.Add(c => c.Search, "tale"));

        cut.Find("input[type=search]").Change("noe helt annet");
        ClickSort(cut, "Datakilde");

        Assert.Equal("tale", client.LastSearch);
        Assert.Contains("«tale»", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Sort_Always_ThenTheHostIsNotToldTheSearchChanged()
    {
        // SearchChanged is the host's URL contract, and sorting is not searching. Raising it here
        // would put text the user never submitted into the host's URL.
        var reported = new List<string?>();
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale")
                                  .Add(c => c.SearchChanged, (string? s) => reported.Add(s)));

        reported.Clear(); // the initial load's own notification

        cut.Find("input[type=search]").Change("noe helt annet");
        ClickSort(cut, "Datakilde");

        Assert.Empty(reported);
    }

    [Fact]
    public void Sort_WhenTheLanguageIsEn_ThenTheSortControlIsEnglishToo()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Language, "en"));

        var labels = SortButtons(cut).Select(k => k.TextContent).ToList();

        Assert.Equal(["Default (ascending)", "Data source", "Data collection", "Variable group"], labels);
        Assert.Equal("Sort by", cut.Find($"{SortControl} legend").TextContent);
    }

    // ---------------------------------------------------------------------------------
    // SearchChanged — the host's URL contract. The Search/SearchChanged pair gives a host
    // @bind-Search, and helsedata's CMS host is what turns that into a shareable link, so when
    // it fires is part of the contract rather than an implementation detail.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Search_WhenTheUserSearches_ThenTheHostIsToldWhatWasSearchedFor()
    {
        var reported = new List<string?>();
        var cut = RenderWith(new FakeClient(OnePage()),
                            b => b.Add(c => c.SearchChanged, (string? s) => reported.Add(s)));

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        // The initial load reports the parameter it was given, then the search reports itself.
        Assert.Equal([null, "svelging"], reported);
    }

    [Fact]
    public void Search_WhenTheFetchFailsOnASearch_ThenTheHostIsStillToldWhatWasSearchedFor()
    {
        // Unconditional, as the parameter's own doc says. A host whose URL kept the previous query
        // after a failed search would hand out a link that reloads into a different search than
        // the box on screen is showing.
        var reported = new List<string?>();
        var cut = RenderWith(new FailingClient(),
                            b => b.Add(c => c.SearchChanged, (string? s) => reported.Add(s)));

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Equal([null, "svelging"], reported);
    }

    [Fact]
    public void Search_WhenTheHostsOwnHandlerThrows_ThenItDoesNotEscapeIntoTheHost()
    {
        // The handler this exists for is a NavigationManager call or a CMS URL rewrite, which is
        // exactly the kind that throws. Unhandled it would propagate out of Blazor's event
        // dispatch — and out of the initial render, since OnInitializedAsync runs this path too —
        // which in helsedata's legacy Blazor Server host tears down the circuit for the whole CMS
        // page. Nothing is shown to the reader either: the search itself worked, and reporting a
        // host bug as "Kunne ikke hente variabler" would blame the API for it.
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));

        var cut = RenderWith(client,
                            b => b.Add<string?>(c => c.SearchChanged,
                                                _ => throw new InvalidOperationException("vertsfeil")));

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal("svelging", client.LastSearch);
        Assert.DoesNotContain("Kunne ikke hente variabler", cut.Markup);
        Assert.Single(cut.FindAll("ul.datasourcecard-list > li"));
    }

    // ---------------------------------------------------------------------------------
    // Accessibility. helsedata.no is a public-sector site, so WCAG 2.1 AA is a legal
    // requirement there — and this is our markup on their page. Each test below pins one
    // property a screen-reader or keyboard user depends on, so it cannot quietly go away.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Sort_WhenTheOrderChanges_ThenTheStatusLineSaysWhatTheListIsOrderedBy()
    {
        // There are no column headers, so there is no aria-sort to carry this. The chosen order
        // rides on the status line instead — already a polite, atomic live region, so changing the
        // sentence is what announces the new ordering.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Contains("sortert på Standard, stigende", cut.Find("p[role='status']").TextContent);

        ClickSort(cut, "Datakilde");

        Assert.Contains("sortert på Datakilde, stigende", cut.Find("p[role='status']").TextContent);

        ClickSort(cut, "Datakilde");

        Assert.Contains("sortert på Datakilde, synkende", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenTheListNameNamesTheOrderingToo()
    {
        // The list's accessible name is the same sentence as the status line, so the two cannot
        // drift apart and say the result is ordered two different ways.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Contains("sortert på Standard, stigende",
                        cut.Find("ul.datasourcecard-list").GetAttribute("aria-label")!);
    }

    [Fact]
    public void Render_Always_ThenOnlyTheActiveSortFieldIsMarked()
    {
        // aria-current rather than aria-pressed: pressing the active button does not release it,
        // it reverses the direction, and a toggle that never toggles off misdescribes itself.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var marked = SortButtons(cut).Where(k => k.HasAttribute("aria-current")).ToList();

        Assert.Equal("Standard (stigende)", Assert.Single(marked).TextContent);
        Assert.Equal("true", marked[0].GetAttribute("aria-current"));
    }

    [Fact]
    public void Render_WhenThereAreNoHits_ThenTheSortControlStaysAnyway()
    {
        // Removing the control after a search that found nothing would take the button the user
        // just pressed out of the document, dropping focus to <body> — the same failure the Søk
        // button is never disabled to avoid.
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Equal(Enum.GetValues<SortField>().Length, SortButtons(cut).Count);
    }

    [Fact]
    public void Render_Always_ThenTheSortFieldsAreGroupedAndNamed()
    {
        // fieldset + legend names the group of buttons for a screen reader without inventing ARIA.
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Equal("Sorter etter", cut.Find($"{SortControl} legend").TextContent);
        Assert.All(SortButtons(cut), k => Assert.Equal("button", k.GetAttribute("type")));
    }

    [Fact]
    public void Render_Always_ThenTheStatusLineIsAPoliteAtomicStatusRegion()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        var status = cut.Find("p[role='status']");

        // role + aria-live together, because older screen readers honour one or the other.
        // aria-atomic so the whole sentence is read: hearing "12" on its own is not news.
        Assert.Equal("status", status.GetAttribute("role"));
        Assert.Equal("polite", status.GetAttribute("aria-live"));
        Assert.Equal("true", status.GetAttribute("aria-atomic"));
    }

    // ---------------------------------------------------------------------------------
    // Styling contract. The package ships no CSS, so every class name it emits has to be
    // one Fhi.Helsedata.Stiler already defines — otherwise the host stylesheet has never
    // heard of it and the element renders as a raw browser default inside a styled page.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Always_ThenStilersOwnClassNamesAreUsedOnTheSearchField()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Equal("form-element__label", cut.Find("label").ClassName);
        Assert.Equal("searchbox__freetext", cut.Find("input[type=search]").ClassName);
        Assert.NotNull(cut.Find("div.searchbox__freetext-container"));

        // hd-button-square carries the shape, button-square--primary the colour, and
        // searchbox__freetext-submit-button places it inside the field's reserved padding.
        var submit = cut.Find("button[type=submit]").ClassName!;
        Assert.Contains("hd-button-square", submit);
        Assert.Contains("button-square--primary", submit);
        Assert.Contains("searchbox__freetext-submit-button", submit);
    }

    [Fact]
    public void Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // Two names of our own, and both are DOM handles rather than style hooks — nothing in this
        // package or in Stiler defines a rule for either. Everything else has to come from Stiler,
        // and this is the guard that says so out loud. The list is exact on purpose: a third name
        // appearing here is the failure this package exists to avoid, and it has happened twice.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.Equal(["variable-explorer", "variable-explorer-filters"], invented);
        Assert.Equal("variable-explorer", cut.Find("section").ClassName);

        // The filter panel wears Stiler's fieldset alongside the handle, so a host that styles
        // nothing still gets the fieldset the sort control gets.
        Assert.Contains("form-fieldset", cut.Find(".variable-explorer-filters").ClassName!);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenStilersCardLayoutIsUsedForTheResults()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.NotNull(cut.Find("ul.datasourcecard-list > li.datasourcecard-list__item > div.datasourcecard"));
        Assert.NotNull(cut.Find(".datasourcecard__heading"));
        Assert.NotNull(cut.Find(".datasourcecard__info > .datasourcecard__info--text"));
    }

    [Fact]
    public void Render_WhenTheApiFails_ThenTheErrorMessageGetsStilersInfobox()
    {
        var cut = RenderWith(new FailingClient());

        Assert.Contains("infobox", cut.Find("[role='alert'] p").ClassName!);
    }

    [Fact]
    public void Render_WhenNothingIsWrong_ThenNoEmptyInfoboxIsDrawn()
    {
        // The alert container is always in the document (see below), so it must carry no
        // class of its own — an `infobox` there would paint an empty coloured box on every
        // page that has nothing to report.
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.False(cut.Find("[role='alert']").HasAttribute("class"));
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenTheStatusLineNamesBothTheCountAndTheSearchTerm()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        var status = cut.Find("p[role='status']").TextContent;

        Assert.Contains("1 variabel funnet", status);
        Assert.Contains("«tale»", status);
    }

    [Fact]
    public void Render_WhenThereAreNoHits_ThenTheMessageSaysWhichSearchFoundNothing()
    {
        var cut = RenderWith(new FakeClient(OnePage()), b => b.Add(c => c.Search, "svelging"));

        Assert.Contains("Ingen variabler passet søket «svelging»",
                        cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Render_WhenOnlyTheFirstPageIsShown_ThenTheSummarySaysHowManyOfTheTotal()
    {
        // 25 rows captioned "312 variabler" would be a lie to whoever cannot see the table — and
        // once there is a pager, so would "25 av 312" on page 2. The sentence says which rows.
        var page = new Page<VariableSummary>
        {
            Items = [Variable("1. Tale", "K1"), Variable("2. Spytt", "K2")],
            TotalCount = 312,
            PageNumber = 1,
            Size = 25,
            TotalPages = 156
        };

        var cut = RenderWith(new FakeClient(page));

        Assert.Contains("Viser 1–2 av 312 variabler funnet",
                        cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Search_WhenTheFieldChangesWithoutBeingSubmitted_ThenTheStatusStillDescribesTheSearchTheRowsCameFrom()
    {
        // @bind writes the field on blur, so the box can hold an unsubmitted query while the
        // table still shows the previous result. The announcement follows the table.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        cut.Find("input[type=search]").Change("noe helt annet");

        Assert.Contains("«tale»", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Render_WhenTheApiFails_ThenTheErrorIsAnnouncedAssertivelyAndSaysWhatCanBeDone()
    {
        var cut = RenderWith(new FailingClient());

        var alert = cut.Find("[role='alert']");

        Assert.Equal("assertive", alert.GetAttribute("aria-live"));
        Assert.Equal("true", alert.GetAttribute("aria-atomic"));
        Assert.Contains("Kunne ikke hente variabler", alert.TextContent);
        Assert.Contains("Prøv igjen", alert.TextContent); // a way out, not just bad news
    }

    [Fact]
    public void Render_WhenNothingIsWrong_ThenTheAlertRegionIsStillPresentAndEmpty()
    {
        // A role="alert" element inserted and filled in the same DOM update is announced
        // unreliably; one already sitting in the document is not. So it is always rendered.
        var cut = RenderWith(new FakeClient(OnePage()));

        var alert = cut.Find("[role='alert']");

        Assert.Equal(string.Empty, alert.TextContent.Trim());
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenTheResultListHasAnAccessibleName()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        // aria-label rather than a clipped <caption>: Stiler has no visually-hidden rule, so
        // markup that needs one is markup that shows its scaffolding on helsedata's page.
        var name = cut.Find("ul.datasourcecard-list").GetAttribute("aria-label")!;

        Assert.Contains("1 variabel funnet", name);
        Assert.Contains("«tale»", name);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenEachResultIsAHeadingOneLevelBelowTheTitle()
    {
        // Real headings per result are what let a screen-reader user move between them with
        // the heading rotor. One level below the component's own title keeps the outline whole.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.HeadingLevel, 3));

        var cardHeading = cut.Find("li h4");

        Assert.Equal("1. Tale", cardHeading.TextContent);
        Assert.Equal("datasourcecard__heading", cardHeading.ClassName);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenEveryFieldIsLabelledWithWhatItIs()
    {
        // A table had column headers doing this job. A card has nothing, and "Inklusjon" on
        // its own does not say which field it is.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "V_ALS.F1.TALE"))));

        var info = cut.Find(".datasourcecard__info").TextContent;

        Assert.Contains("Kode: V_ALS.F1.TALE", info);
        Assert.Contains("Datakilde: Als registeret", info);
        Assert.Contains("Datasamling: Inklusjon", info);
        Assert.Contains("Periode: 2010–2025", info);
    }

    [Fact]
    public void Render_WhenTheResultsAreShown_ThenTheListIsMarkedBusyWithoutAnExtraTabStop()
    {
        // The table version wrapped itself in a focusable scroll box, because a box that
        // scrolls sideways and cannot be focused cannot be scrolled from the keyboard. Cards
        // wrap instead of scrolling, so that tab stop is gone rather than merely moved.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var list = cut.Find("ul.datasourcecard-list");

        Assert.Equal("false", list.GetAttribute("aria-busy"));
        Assert.False(list.HasAttribute("tabindex"));
        Assert.Empty(cut.FindAll("[tabindex]"));
    }

    [Fact]
    public void Render_WhenAValueIsMissing_ThenNotSpecifiedIsWrittenVisiblyForEveryone()
    {
        // "—" is either read as "em dash" or skipped in silence, depending on the reader's
        // punctuation setting. Neither says "we do not know". The words used to be there but
        // clipped out of sight for everyone except a screen reader; now they are simply there,
        // which needs no visually-hidden rule from the host — and Stiler has none to give.
        var withoutKilde = new VariableSummary { Id = Guid.NewGuid(), Code = "K", PreferredTerm = "Uten kilde" };

        var cut = RenderWith(new FakeClient(OnePage(withoutKilde)));

        var info = cut.Find(".datasourcecard__info");

        Assert.Contains("Datakilde: Ikke oppgitt", info.TextContent);
        Assert.Contains("Periode: Ikke oppgitt", info.TextContent);
        Assert.DoesNotContain("—", info.TextContent);
    }

    [Fact]
    public void Render_WhenTheVariableHasADescription_ThenItStandsOnItsOwnBelowTheKeyFacts()
    {
        // The code and the description used to be two adjacent spans in one table cell, and
        // Razor eats the whitespace between them: "…ALSFRSR1TALEHvordan er talen?". They are
        // now different parts of the card, so nothing can run them together.
        var withDescription = new VariableSummary
        {
            Id = Guid.NewGuid(),
            Code = "V_ALS.F1.TALE",
            PreferredTerm = "1. Tale",
            Description = "Hvordan er talen?"
        };

        var cut = RenderWith(new FakeClient(OnePage(withDescription)));

        Assert.Equal("Hvordan er talen?", cut.Find(".datasourcecard__intro p").TextContent);
        Assert.DoesNotContain("Hvordan er talen?", cut.Find(".datasourcecard__info").TextContent);
    }

    [Fact]
    public void Render_WhenTheLanguageIsEn_ThenTheMetadataItselfIsStillMarkedAsNorwegian()
    {
        // The UI turns English; Munin's variable names do not. An English synthesiser
        // reading Norwegian terms is unintelligible (WCAG 3.1.2). The mark sits on the data
        // rather than on the whole list, so the English field labels around it are not
        // announced as Norwegian too.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Language, "en"));

        Assert.Equal("no", cut.Find(".datasourcecard__heading").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".datasourcecard__info--text span[lang]").GetAttribute("lang"));
        Assert.False(cut.Find("ul.datasourcecard-list").HasAttribute("lang"));
    }

    [Fact]
    public void Render_WhenTheHostSaysNothing_ThenTheTitleIsAnH2()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Equal("Variabelutforsker", cut.Find("h2").TextContent);
    }

    [Fact]
    public void Render_WhenTheHostSetsTheHeadingLevel_ThenThatLevelIsUsedAndTheSectionPointsAtIt()
    {
        // The level that keeps a page outline unbroken is only knowable at the mount site.
        var cut = RenderWith(new FakeClient(OnePage()), b => b.Add(c => c.HeadingLevel, 3));

        var heading = cut.Find("h3");

        Assert.Equal("Variabelutforsker", heading.TextContent);
        Assert.Empty(cut.FindAll("h2"));
        Assert.Equal(heading.Id, cut.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Render_WhenTheHeadingLevelIsOutOfRange_ThenItIsClampedTo1Through6()
    {
        // An <h9> is not a heading at all, which would be a worse failure than an
        // approximately-right level.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(OnePage()));

        var low = Render<VariableExplorer>(b => b.Add(c => c.HeadingLevel, 0));
        var high = Render<VariableExplorer>(b => b.Add(c => c.HeadingLevel, 9));

        Assert.NotEmpty(low.FindAll("h1"));
        Assert.NotEmpty(high.FindAll("h6"));
    }

    [Fact]
    public void Render_Always_ThenTheSearchLandmarkIsNamedAfterTheInstance()
    {
        // Two explorers on one page otherwise leave two identical, unnamed "search"
        // entries in a screen reader's landmark list.
        var cut = RenderWith(new FakeClient(OnePage()));

        var form = cut.Find("form[role='search']");

        Assert.Equal(cut.Find("h2").Id, form.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Render_WhenTwoInstancesShareAPage_ThenTheHeadingsAreUniqueToo()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var a = Render<VariableExplorer>();
        var b = Render<VariableExplorer>();

        // The title id is what both the section and the search landmark are named by, so a
        // collision would leave a screen reader with two identically named landmarks.
        Assert.NotEqual(a.Find("h2").Id, b.Find("h2").Id);
    }

    [Fact]
    public void Search_WhileASearchIsRunning_ThenTheSearchButtonIsNotDisabled()
    {
        // Disabling the element that has focus drops focus to <body>: press Enter on Søk
        // and a keyboard user starts tabbing from the top of the page again.
        var cut = RenderWith(new SlowClient());

        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Contains("Henter variabler", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Search_WhenASearchIsAlreadyRunning_ThenANewSubmitIsIgnored()
    {
        // What the disabled attribute used to do, without taking focus away to do it.
        var client = new SlowClient();
        var cut = RenderWith(client);

        cut.Find("form").Submit();
        cut.Find("form").Submit();

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Search_WhenTheResultsAreStaleFromANewSearch_ThenTheListIsMarkedBusy()
    {
        // The previous cards stay on screen while the next search runs, so they are stale
        // rather than current — aria-busy is what says so to a screen reader.
        var hits = OnePage(Variable("1. Tale", "KODE"));
        var client = new SlowClient(hits);
        var cut = RenderWith(client);

        Assert.Equal("false", cut.Find("ul.datasourcecard-list").GetAttribute("aria-busy"));

        cut.Find("form").Submit(); // second search, still in flight

        Assert.Equal("true", cut.Find("ul.datasourcecard-list").GetAttribute("aria-busy"));

        await cut.InvokeAsync(() => client.Answer(hits));

        cut.WaitForAssertion(() =>
            Assert.Equal("false", cut.Find("ul.datasourcecard-list").GetAttribute("aria-busy")));
    }

    [Fact]
    public async Task Search_WhenTheAnswerArrives_ThenTheResultReplacesTheLoadingMessage()
    {
        // One shared status region, so the messages replace each other instead of stacking.
        var client = new SlowClient();
        var cut = RenderWith(client);

        await cut.InvokeAsync(() => client.Answer(OnePage(Variable("1. Tale", "KODE"))));

        cut.WaitForAssertion(() =>
        {
            var status = cut.Find("p[role='status']").TextContent;
            Assert.Contains("1 variabel funnet", status);
            Assert.DoesNotContain("Henter variabler", status);
        });
    }

    // ---------------------------------------------------------------------------------
    // Paging. The result set is 18 000 variables and a page is 25 of them, so without a
    // pager the other 17 975 are unreachable. Runa's rules are kept: Forrige/Neste with
    // the position between them, no infinite scrolling, and any change of search or of
    // ordering starts again at page one. The two things that are ours rather than Runa's
    // are pinned here too — the buttons are never `disabled`, and the pager is left out
    // when the whole result already fits on one page.
    // ---------------------------------------------------------------------------------

    /// <summary>One page of a <paramref name="totalCount"/>-row result, as the API would return it.</summary>
    private static Page<VariableSummary> ResultPage(int totalCount, int page = 1, int pageSize = 25)
    {
        var first = (page - 1) * pageSize;
        var count = Math.Clamp(totalCount - first, 0, pageSize);

        return new Page<VariableSummary>
        {
            Items = [.. Enumerable.Range(1, count).Select(i => Variable($"Variabel {first + i}", $"K{first + i}"))],
            TotalCount = totalCount,
            PageNumber = page,
            Size = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    /// <summary>
    /// A client with more rows than fit on one page, answering with whichever slice it is asked for.
    /// </summary>
    /// <remarks>
    /// It has to answer per page rather than return one fixed <see cref="Page{T}"/>, because what
    /// these tests are about is the row range and the position moving as the pages turn — a fake
    /// that answered page 1 forever would agree with a component that never sent the page number.
    /// </remarks>
    private sealed class PagedClient(int totalCount, int rowsPerPage = 25) : EmptyMuninExplorerClient
    {
        public string? LastSearch { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public SortField LastSort { get; private set; }
        public SortDirection LastDirection { get; private set; }
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastSearch = search;
            LastPage = page;
            LastPageSize = pageSize;
            LastSort = sort;
            LastDirection = direction;
            Calls++;

            return Task.FromResult(ResultPage(totalCount, page, rowsPerPage));
        }
    }

    /// <summary>
    /// A client whose index shrinks under the reader: the first <paramref name="calmCalls"/> calls
    /// are answered out of <paramref name="totalCount"/> rows and every one after them out of
    /// <paramref name="afterwards"/>, so the page the pager offered is past the end by the time it
    /// is asked for. A moment in time rather than a property of a page number, which is what makes
    /// the request that straddles it come back empty.
    /// </summary>
    private sealed class ShrinkingPagedClient(int totalCount, int calmCalls, int afterwards)
        : EmptyMuninExplorerClient
    {
        public int LastPage { get; private set; }
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastPage = page;
            Calls++;

            return Task.FromResult(ResultPage(Calls <= calmCalls ? totalCount : afterwards, page));
        }
    }

    /// <summary>
    /// Answers page 1 and hands back an entirely empty <see cref="Page{T}"/> for anything after it —
    /// which is what <c>MuninExplorerClient</c> produces from a 404 on an out-of-range page: no
    /// exception, no rows, and a count and page total of zero.
    /// </summary>
    private sealed class NotFoundPagedClient(int totalCount) : EmptyMuninExplorerClient
    {
        public int LastPage { get; private set; }
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastPage = page;
            Calls++;

            return Task.FromResult(page == 1 ? ResultPage(totalCount) : new Page<VariableSummary>());
        }
    }

    /// <summary>
    /// Answers page 1, 404s anything after it into an empty <see cref="Page{T}"/> the same way
    /// <see cref="NotFoundPagedClient"/> does — and then fails outright, so the retreat's own fetch
    /// is the call that throws. The transient blip the retreat has to survive, arriving in the one
    /// window where the result on screen is the empty page the retreat is trying to escape.
    /// </summary>
    private sealed class RetreatFailingClient(int totalCount) : EmptyMuninExplorerClient
    {
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            return Calls switch
            {
                1 => Task.FromResult(ResultPage(totalCount)),
                2 => Task.FromResult(new Page<VariableSummary>()),
                _ => throw new HttpRequestException("nede")
            };
        }
    }

    /// <summary>
    /// A client that hands back rows but describes nothing about the paging it did them by:
    /// <see cref="Page{T}.Size"/>, <see cref="Page{T}.PageNumber"/> and
    /// <see cref="Page{T}.TotalPages"/> all stay at zero.
    /// </summary>
    /// <remarks>
    /// Which is what a substituted <see cref="IMuninExplorerClient"/> leaves them at — a host's mock,
    /// or a stand-in over a different backend — and is the case the arithmetic fallbacks in
    /// <c>TotalPages</c>, <c>ResultPageSize</c> and <c>ResultPage</c> exist for. Every other fake here
    /// echoes <c>Size = pageSize</c> back, which makes the fallback and the server's own answer
    /// indistinguishable and so pins neither.
    /// </remarks>
    private sealed class SizelessPagedClient(int totalCount) : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                ResultPage(totalCount, page, pageSize) with { PageNumber = 0, Size = 0, TotalPages = 0 });
        }
    }

    /// <summary>
    /// A server that clamps an out-of-range page rather than 404ing it: asked for page 12 of 8 it
    /// answers page 8, and says so in the page it echoes back.
    /// </summary>
    private sealed class ClampingPagedClient(int totalCount, int maxPage) : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ResultPage(totalCount, Math.Min(page, maxPage)));
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> PagerButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("div.variables-pagination .variables-pagination-content button");

    private static AngleSharp.Dom.IElement Previous(IRenderedComponent<VariableExplorer> cut) =>
        PagerButtons(cut)[0];

    private static AngleSharp.Dom.IElement Next(IRenderedComponent<VariableExplorer> cut) =>
        PagerButtons(cut)[1];

    /// <summary>The "Side 2 av 13" between the two buttons.</summary>
    private static string Position(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".variables-pagination-content span.caption").TextContent;

    private static string StatusLine(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find("p[role='status']").TextContent;

    [Fact]
    public void Page_WhenNextIsPressed_ThenTheNextPageIsFetchedWithTheSearchAndOrderIntact()
    {
        // Turning a page must not quietly become a new search: the rows would change for two
        // reasons at once and the user would have no way to tell which.
        var client = new PagedClient(312);
        var cut = RenderWith(client, b => b.Add(c => c.Search, "tale"));

        ClickSort(cut, "Datakilde"); // something for the page turn to preserve

        Next(cut).Click();

        Assert.Equal(2, client.LastPage);
        Assert.Equal("tale", client.LastSearch);
        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
        Assert.Equal(3, client.Calls); // initial load, the sort, this page turn
        Assert.Equal("Side 2 av 13", Position(cut));
    }

    [Fact]
    public void Page_WhenPreviousIsPressed_ThenThePageBeforeIsFetched()
    {
        var client = new PagedClient(312);
        var cut = RenderWith(client);

        Next(cut).Click();
        Next(cut).Click();
        Assert.Equal("Side 3 av 13", Position(cut));

        Previous(cut).Click();

        Assert.Equal(2, client.LastPage);
        Assert.Equal("Side 2 av 13", Position(cut));
        Assert.Contains("Variabel 26", cut.Markup);
    }

    [Fact]
    public void Page_WhenOnTheFirstPage_ThenPreviousIsUnavailableAndAsksForNothing()
    {
        var client = new PagedClient(312);
        var cut = RenderWith(client);

        Assert.Equal("true", Previous(cut).GetAttribute("aria-disabled"));
        Assert.Null(Next(cut).GetAttribute("aria-disabled"));

        Previous(cut).Click();

        Assert.Equal(1, client.Calls); // the initial load, and nothing since
        Assert.Equal("Side 1 av 13", Position(cut));
    }

    [Fact]
    public void Page_WhenOnTheLastPage_ThenNextIsUnavailableAndAsksForNothing()
    {
        // An exact multiple of the page size: 50 rows at 25 a page is two full pages, which is
        // where an off-by-one in the page count offers a third page with nothing on it.
        var client = new PagedClient(50);
        var cut = RenderWith(client);

        Next(cut).Click();

        Assert.Equal("Side 2 av 2", Position(cut));
        Assert.Equal("true", Next(cut).GetAttribute("aria-disabled"));
        Assert.Null(Previous(cut).GetAttribute("aria-disabled"));

        Next(cut).Click();

        Assert.Equal(2, client.Calls); // initial load and the one page turn
        Assert.Equal("Side 2 av 2", Position(cut));
    }

    [Fact]
    public void Page_AtEitherEndOfTheList_ThenTheButtonsAreNotDisabledAndKeepTheirFocus()
    {
        // The reason aria-disabled is used instead of the disabled attribute. Pressing Neste until
        // the last page is the ordinary way to reach it, and disabling the element that currently
        // has focus drops focus to <body> — so the reward for finishing the list would be tabbing
        // from the top of the host's page again. Same decision as the never-disabled Søk button.
        var client = new PagedClient(50);
        var cut = RenderWith(client);

        Assert.All(PagerButtons(cut), button => Assert.False(button.HasAttribute("disabled")));

        Next(cut).Click();

        Assert.All(PagerButtons(cut), button => Assert.False(button.HasAttribute("disabled")));
        Assert.Equal("true", Next(cut).GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Page_WhenTheWholeResultFitsOnOnePage_ThenThereIsNoPagerAtAll()
    {
        // "Side 1 av 1" between two buttons that can never do anything is furniture, and the skip
        // link would be a tab stop leading nowhere.
        var cut = RenderWith(new PagedClient(3));

        Assert.Empty(cut.FindAll("div.variables-pagination"));
        Assert.Empty(cut.FindAll("a.skiplink-pagination"));
    }

    [Fact]
    public void Page_WhenThereAreNoHitsAtAll_ThenThereIsNoPager()
    {
        var cut = RenderWith(new PagedClient(0));

        Assert.Empty(cut.FindAll("div.variables-pagination"));
        Assert.DoesNotContain("Viser", StatusLine(cut));
        Assert.Contains("Ingen variabler passet søket", StatusLine(cut));
    }

    [Fact]
    public void Page_WhenANewSearchIsMade_ThenItStartsAtPageOneAgain()
    {
        // A different search is a different result set, and page 3 of the old one means nothing
        // in it. Without this the user searches and lands in the middle of the answer.
        var client = new PagedClient(312);
        var cut = RenderWith(client);

        Next(cut).Click();
        Next(cut).Click();

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal(1, client.LastPage);
        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal("Side 1 av 13", Position(cut));
    }

    [Fact]
    public void Page_WhenTheOrderChanges_ThenItStartsAtPageOneAgain()
    {
        // Reordering renumbers every page, so page 3 of the old order holds rows from the middle
        // of a sequence the reader never saw the start of.
        var client = new PagedClient(312);
        var cut = RenderWith(client);

        Next(cut).Click();
        Next(cut).Click();

        ClickSort(cut, "Datasamling");

        Assert.Equal(1, client.LastPage);
        Assert.Equal("Side 1 av 13", Position(cut));
    }

    [Fact]
    public void Page_WhenAFetchIsAlreadyRunning_ThenTheClickIsIgnored()
    {
        // Same rule as a second submit and a second sort click: dropped rather than queued, so two
        // impatient clicks cannot leave the position saying one page while the rows are another.
        var client = new SlowClient(ResultPage(312));
        var cut = RenderWith(client);

        cut.Find("form").Submit(); // second search, still in flight
        Next(cut).Click();

        Assert.Equal(2, client.Calls); // the initial load and the stalled search, not the page turn
        Assert.Equal("Side 1 av 13", Position(cut));
    }

    [Fact]
    public void Page_WhenTheFetchFails_ThenTheFailureIsReportedInsteadOfEscaping()
    {
        var client = new FailingClient(ResultPage(312));
        var cut = RenderWith(client);

        Next(cut).Click();

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
    }

    [Fact]
    public void Page_WhenTheFetchFails_ThenTheRowsAndThePagerStayOnScreen()
    {
        // The component's standing rule: a control the user just pressed is never taken out of the
        // document, because that drops focus to <body> and a keyboard user restarts from the top of
        // helsedata's CMS page. It is why Søk is never disabled and why the pager uses
        // aria-disabled — and the pager is the only pressable control here that is rendered
        // conditionally, so a page turn that cleared the rows is the one way left to break it.
        var client = new FailingClient(ResultPage(312));
        var cut = RenderWith(client);

        Next(cut).Click();

        Assert.NotEmpty(cut.FindAll("div.variables-pagination"));
        Assert.Equal("Side 1 av 13", Position(cut)); // rolled back, and still describing real rows
        Assert.Contains("Variabel 1", cut.Markup);
        Assert.Contains("Viser 1–25 av 312 variabler funnet", StatusLine(cut));
    }

    [Fact]
    public void Search_WhenTheFetchFails_ThenThePreviousSearchesRowsAreNotLeftBehind()
    {
        // The other half of the same rule. A page turn keeps its rows because they came from the
        // search still on screen; a failed *search* has none of its own, and leaving the previous
        // one's rows under the error would say they answered the new query.
        var client = new FailingClient(ResultPage(312));
        var cut = RenderWith(client);

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("ul.datasourcecard-list > li"));
        Assert.Empty(cut.FindAll("div.variables-pagination"));
    }

    [Fact]
    public void Page_WhenTheResultShrankBetweenTheTwoRequests_ThenItLandsOnTheLastPageThatExists()
    {
        // The clamp in GoToPageAsync measures against the count the *previous* answer carried, so
        // it can offer a page that has since stopped existing. Left alone the reader would be told
        // "Ingen variabler passet søket" over a search that matched 200 rows, with no pager left
        // to press.
        // Eleven answers out of 312 rows carry the reader to page 11; the twelfth request — for
        // page 12 of the 13 the pager is still offering — arrives after the index dropped to 200.
        var client = new ShrinkingPagedClient(312, calmCalls: 11, afterwards: 200);
        var cut = RenderWith(client);

        for (var i = 0; i < 11; i++)
        {
            Next(cut).Click();
        }

        Assert.Equal(8, client.LastPage); // asked for 12, told it was gone, went to the last real one
        Assert.Equal("Side 8 av 8", Position(cut));
        Assert.Contains("Viser 176–200 av 200 variabler funnet", StatusLine(cut));
        Assert.Equal(25, cut.FindAll("ul.datasourcecard-list > li").Count);
    }

    [Fact]
    public void Page_WhenTheApiReportsAnOutOfRangePageAsNotFound_ThenItFallsBackToTheFirstPage()
    {
        // MuninExplorerClient maps 404 to an empty Page rather than throwing, so nothing rolls the
        // page number back: the count and the page total arrive as zero, which describes no page at
        // all. Page 1 is the one page that can never be out of range.
        var client = new NotFoundPagedClient(312);
        var cut = RenderWith(client);

        Next(cut).Click();

        Assert.Equal(1, client.LastPage);
        Assert.Equal(3, client.Calls); // the initial load, the missing page 2, and the way back
        Assert.Equal("Side 1 av 13", Position(cut));
        Assert.Contains("Viser 1–25 av 312 variabler funnet", StatusLine(cut));
    }

    [Fact]
    public void Page_WhenAPageArrivesWithNoRowsOnIt_ThenThePagerIsStillThereToGetBackFrom()
    {
        // Belt to the retreat's braces: the pager is rendered from the page count and not from the
        // rows, so an answer carrying a count but no rows leaves something to press instead of a
        // dead end. Past page one RetreatFromEmptyPageAsync steps out of that state; on page one
        // there is nowhere to step to, so the pager staying is the whole of the recovery.
        var cut = RenderWith(new FakeClient(new Page<VariableSummary>
        {
            Items = [],
            TotalCount = 312,
            PageNumber = 1,
            Size = 25,
            TotalPages = 13
        }));

        Assert.NotEmpty(cut.FindAll("div.variables-pagination"));
        Assert.Equal("Side 1 av 13", Position(cut));
        Assert.Null(Next(cut).GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Page_WhenTheServerAnswersADifferentPageThanItWasAsked_ThenTheRangeFollowsTheAnswer()
    {
        // The same treatment the page size already gets: an API that clamps page 12 to page 8 and
        // says so has described itself truthfully, and the row range has to be counted from what
        // arrived rather than from what was asked for — otherwise the status line offers rows the
        // reader is not looking at.
        var cut = RenderWith(new ClampingPagedClient(312, maxPage: 1));

        Next(cut).Click();

        Assert.Contains("Viser 1–25 av 312 variabler funnet", StatusLine(cut));

        // And the caption between the buttons counts from the same answer. Pinning only the range
        // would leave the pager free to say "Side 2 av 13" over page 1's rows — the row range and
        // the position describing two different pages of one result.
        Assert.Equal("Side 1 av 13", Position(cut));
    }

    [Fact]
    public void Page_WhenTheServerKeepsAnsweringTheSamePage_ThenThePositionDoesNotWalkAwayFromTheRows()
    {
        // The half that only shows up on the second press. With the position taken from the number
        // that was asked for, Neste stays enabled against a page the server disowned, so every
        // further press bumps the caption — "Side 3 av 13", "Side 4 av 13" — while the same 25 rows
        // sit underneath it.
        var cut = RenderWith(new ClampingPagedClient(312, maxPage: 1));

        Next(cut).Click();
        Next(cut).Click();
        Next(cut).Click();

        Assert.Equal("Side 1 av 13", Position(cut));
        Assert.Contains("Viser 1–25 av 312 variabler funnet", StatusLine(cut));
        Assert.Contains("Variabel 1", cut.Markup);
    }

    [Fact]
    public void Page_WhenTheRetreatLandsOnASinglePageResult_ThenThePagerIsStillUnderTheFinger()
    {
        // The pager is normally left out of a single-page result, on the grounds that one is only
        // ever reached by a new search or a new ordering — neither started from a pager button. The
        // retreat is the exception: an index that shrank to one page's worth between two requests
        // answers Neste with an empty page and puts the reader back on page 1 of 1. Dropping the
        // pager in that render would take Neste out of the document under the finger that pressed
        // it, which is the failure the retreat exists to avoid rather than a new one to introduce.
        var cut = RenderWith(new ShrinkingPagedClient(312, calmCalls: 1, afterwards: 10));

        Next(cut).Click();

        Assert.NotEmpty(cut.FindAll("div.variables-pagination"));
        Assert.NotEmpty(cut.FindAll("a.skiplink-pagination"));
        Assert.Equal("Side 1 av 1", Position(cut));
        Assert.Contains("10 variabler funnet", StatusLine(cut)); // the whole result, so no range
        Assert.Equal(10, cut.FindAll("ul.datasourcecard-list > li").Count);

        // Both ends of a one-page result: neither button can go anywhere, and both say so without
        // being taken away.
        Assert.Equal("true", Previous(cut).GetAttribute("aria-disabled"));
        Assert.Equal("true", Next(cut).GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Page_WhenANewSearchFollowsARetreatToOnePage_ThenTheOneButtonPagerIsGoneAgain()
    {
        // The other half of the same rule: the pager is kept because a button was pressed, not
        // forever. A search is not started from one, so a single-page answer to it costs no
        // furniture — and the reader's focus is in the search box, not on a pager button.
        var cut = RenderWith(new ShrinkingPagedClient(312, calmCalls: 1, afterwards: 10));

        Next(cut).Click();

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Empty(cut.FindAll("div.variables-pagination"));
        Assert.Empty(cut.FindAll("a.skiplink-pagination"));
    }

    [Fact]
    public void Page_WhenTheRetreatsOwnFetchFails_ThenTheRowsItWasEscapingBackToAreRestored()
    {
        // The retreat turns a second page, so it can fail the same way the first one can — and its
        // failure is the worse of the two, because the result it would otherwise keep is the empty
        // page it was called to escape from. Left unchecked the reader gets "Ingen variabler passet
        // søket" over a search that matched 312, and no pager either: a zero count makes TotalPages
        // 1 and takes the guard's other branch with it. So the whole page turn is undone, back to
        // the page that had rows on it.
        var client = new RetreatFailingClient(312);
        var cut = RenderWith(client);

        Next(cut).Click();

        Assert.Equal(3, client.Calls); // the initial load, the missing page 2, and the failed way back
        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.NotEmpty(cut.FindAll("div.variables-pagination"));
        Assert.Equal("Side 1 av 13", Position(cut));
        Assert.Contains("Viser 1–25 av 312 variabler funnet", StatusLine(cut));
        Assert.Contains("Variabel 1", cut.Markup);
        Assert.DoesNotContain("Ingen variabler passet", StatusLine(cut));
    }

    [Fact]
    public void Page_Always_ThenTheHostIsNotToldTheSearchChanged()
    {
        // SearchChanged is the host's URL contract and turning a page did not change what was
        // searched for. The page number belongs in that URL too, but through its own contract.
        var reported = new List<string?>();
        var cut = RenderWith(new PagedClient(312),
                            b => b.Add(c => c.Search, "tale")
                                  .Add(c => c.SearchChanged, (string? s) => reported.Add(s)));

        reported.Clear(); // the initial load's own notification

        Next(cut).Click();

        Assert.Empty(reported);
    }

    [Fact]
    public void Page_WhenThePageTurns_ThenTheStatusLineSaysWhichRowsAreOnScreen()
    {
        // "Viser 25 av 312" was true only of the first page. The live region is what announces a
        // page change, so this sentence is also the announcement — hence one sentence, not two.
        var cut = RenderWith(new PagedClient(312));

        Assert.Contains("Viser 1–25 av 312 variabler funnet", StatusLine(cut));

        Next(cut).Click();

        Assert.Contains("Viser 26–50 av 312 variabler funnet", StatusLine(cut));
        Assert.Contains("sortert på Standard, stigende", StatusLine(cut));
    }

    [Fact]
    public void Page_WhenTheLastPageIsPartlyFull_ThenTheRangeStopsAtTheRowsThatExist()
    {
        // 13 pages of 25 over 312 rows leaves 12 on the last one. Counting the range as
        // page × size would caption it "301–325 av 312".
        var cut = RenderWith(new PagedClient(312));

        for (var i = 0; i < 12; i++)
        {
            Next(cut).Click();
        }

        Assert.Equal("Side 13 av 13", Position(cut));
        Assert.Contains("Viser 301–312 av 312 variabler funnet", StatusLine(cut));
        Assert.Equal(12, cut.FindAll("ul.datasourcecard-list > li").Count);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Page_WhenTheHostAsksForAnImpossiblePageSize_ThenItIsClampedToWhatTheApiAccepts(
        int asked, int sent)
    {
        // Both ends of the documented 1–100 range. A zero page size would make every page count a
        // division by zero, and the server clamps regardless, so asking for something outside that
        // only desynchronises the two: it would answer with 100 rows while the component counted
        // the pages — and wrote the row range — as if it had asked for half a billion.
        var client = new PagedClient(312, rowsPerPage: 1);

        RenderWith(client, b => b.Add(c => c.PageSize, asked));

        Assert.Equal(sent, client.LastPageSize);
    }

    [Theory]
    [InlineData(500, "Side 2 av 4", "Viser 101–200 av 312")]
    [InlineData(0, "Side 2 av 312", "Viser 2–2 av 312")]
    public void Page_WhenTheServerDescribesNoPageSize_ThenTheArithmeticUsesTheClampedOneNotTheAsked(
        int asked, string position, string range)
    {
        // The row range and the page count are counted client-side whenever the server leaves
        // `size` at zero, and they have to be counted against the size the rows were actually
        // requested with — the clamped one, which is what went out on the wire. Counting against
        // the raw PageSize parameter instead is a one-keystroke slip that no other test would
        // catch, because every other fake echoes the size back and hides the fallback: 500 would
        // make this one page of 312 and take the pager off screen entirely, and 0 would divide by
        // zero. Both ends of the clamp, so neither direction of the slip survives.
        var cut = RenderWith(new SizelessPagedClient(312), b => b.Add(c => c.PageSize, asked));

        Next(cut).Click();

        Assert.Equal(position, Position(cut));
        Assert.Contains(range, StatusLine(cut));
    }

    [Fact]
    public void Page_WhenTheLanguageIsEn_ThenThePagerIsEnglishToo()
    {
        var cut = RenderWith(new PagedClient(312), b => b.Add(c => c.Language, "en"));

        Assert.Equal("Previous", Previous(cut).TextContent);
        Assert.Equal("Next", Next(cut).TextContent);
        Assert.Equal("Page 1 of 13", Position(cut));
        Assert.Equal("Skip to pagination", cut.Find("a.skiplink-pagination").TextContent);
        Assert.Contains("Showing 1–25 of 312 variables found", StatusLine(cut));
    }

    // ---------------------------------------------------------------------------------
    // The pager's own accessibility and styling contract. The class names here are NOT
    // Stiler's: Stiler defines no pagination rule of any kind, so these are helsedata's
    // own, from the stylesheet their variable page carries. Pinning them is what keeps
    // someone from "tidying" them into names no host has ever heard of.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenThePagerUsesHelsedatasOwnClassNames()
    {
        var cut = RenderWith(new PagedClient(312));

        var pager = cut.Find("div.variables-pagination > div.variables-pagination-content");

        Assert.NotNull(pager);
        Assert.Equal(2, PagerButtons(cut).Count);
        Assert.All(PagerButtons(cut), button => Assert.Contains("hd-button-square", button.ClassName!));
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenTheSkipLinkComesBeforeTheCardsAndTargetsThePager()
    {
        // Without it a keyboard user tabs through 25 cards to reach Neste. It has to sit ahead of
        // the list to save anything, and its target has to be focusable programmatically, or
        // following it moves the viewport while focus stays behind.
        var cut = RenderWith(new PagedClient(312));

        var skiplink = cut.Find("a.skiplink-pagination");
        var pager = cut.Find("div.variables-pagination");

        Assert.Equal($"#{pager.Id}", skiplink.GetAttribute("href"));
        Assert.Equal("-1", pager.GetAttribute("tabindex"));
        Assert.Equal("Hopp til paginering", skiplink.TextContent);

        // Ahead of the results, otherwise it skips nothing.
        var markup = cut.Markup;
        Assert.True(markup.IndexOf("skiplink-pagination", StringComparison.Ordinal)
                    < markup.IndexOf("datasourcecard-list", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenThePagerIsANamedLandmarkWithLabelledButtons()
    {
        // A second navigation landmark on the host's page has to say what it navigates, and
        // "Forrige" on its own does not say forrige what. Each accessible name starts with the
        // word on the button, so a speech-input user saying what they see still hits it (2.5.3).
        var cut = RenderWith(new PagedClient(312));

        var pager = cut.Find("div.variables-pagination");

        Assert.Equal("navigation", pager.GetAttribute("role"));
        Assert.Equal("Paginering", pager.GetAttribute("aria-label"));
        Assert.Equal("Forrige side", Previous(cut).GetAttribute("aria-label"));
        Assert.Equal("Neste side", Next(cut).GetAttribute("aria-label"));
        Assert.All(PagerButtons(cut), button => Assert.Equal("button", button.GetAttribute("type")));
    }

    [Fact]
    public void Render_WhenTwoInstancesShareAPage_ThenEachSkipLinkTargetsItsOwnPager()
    {
        // Duplicate DOM ids would send both skip links to the same pager — and fail WCAG 4.1.1.
        Services.AddSingleton<IMuninExplorerClient>(new PagedClient(312));

        var a = Render<VariableExplorer>();
        var b = Render<VariableExplorer>();

        Assert.NotEqual(a.Find("div.variables-pagination").Id, b.Find("div.variables-pagination").Id);
        Assert.Equal($"#{a.Find("div.variables-pagination").Id}",
                     a.Find("a.skiplink-pagination").GetAttribute("href"));
    }

    // ---------------------------------------------------------------------------------
    // The filter panel. Two things have to hold for it to be worth anything: choosing a
    // facet narrows the list, and the counts beside the facets describe that same narrowed
    // list rather than the catalogue. Both come from the API — the component's part is to
    // ask both endpoints with the same selection, and to hand that selection to the host so
    // a filtered search can be linked to.
    // ---------------------------------------------------------------------------------

    private static readonly Guid Dodsarsak = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Tromso = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Tromso4 = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Tromso4Visit = new("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Bakgrunn = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Levekaar = new("cccccccc-0000-0000-0000-000000000002");

    /// <summary>Facets shaped like the real ones: two kildetyper, a kilde each, a nested delkilde.</summary>
    private static FilterOptions Facets() => new()
    {
        KildeTyper =
        [
            new() { Value = "sentraltHelseregister", DisplayName = "SentraltHelseregister", Count = 30 },
            new() { Value = "biobank", DisplayName = "Biobank", Count = 12 }
        ],
        Kilder =
        [
            new() { Id = Dodsarsak, Name = "Dødsårsaksregisteret", KildeType = "sentraltHelseregister", Count = 30 },
            new() { Id = Tromso, Name = "Tromsøundersøkelsen", KildeType = "biobank", Count = 12 }
        ],
        Delkilder =
        [
            new() { Id = Tromso4, Name = "Tromsø 4", KildeId = Tromso, Count = 8 },
            new() { Id = Tromso4Visit, Name = "Første besøk", KildeId = Tromso, ParentDelkildeId = Tromso4, Count = 3 }
        ],
        Variabelgrupper = [new() { Id = Bakgrunn, Name = "Bakgrunn", Count = 7 }],
        DataTypes = [new() { Value = "1", Count = 9 }],
        KildeKodeverkCount = 4,
        TotalCount = 42
    };

    /// <summary>Answers both endpoints and remembers what each was asked with.</summary>
    private sealed class FilteringClient(Page<VariableSummary> answer, FilterOptions? facets = null)
        : EmptyMuninExplorerClient
    {
        private readonly FilterOptions _facets = facets ?? Facets();

        // Never completed. A search asked for while this is set stays in flight for the rest of
        // the test, which is the only way to press a second facet while the first is still running.
        private readonly TaskCompletionSource<Page<VariableSummary>> _stalled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public VariableFilter? SearchFilter { get; private set; }
        public VariableFilter? FacetFilter { get; private set; }
        public string? FacetSearch { get; private set; }
        public int SearchCalls { get; private set; }
        public int FacetCalls { get; private set; }
        public int LastPage { get; private set; }

        /// <summary>Fail every search from the next one on — the rollback path.</summary>
        public bool FailSearch { get; set; }

        /// <summary>Fail every facet refresh from the next one on.</summary>
        public bool FailFacets { get; set; }

        /// <summary>Never answer a search from the next one on — the in-flight path.</summary>
        public bool StallSearch { get; set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;

            // Recorded before the failure, so a test can see what was asked for as well as what
            // the component was left holding afterwards.
            SearchFilter = filter;
            LastPage = page;

            if (StallSearch)
            {
                return _stalled.Task;
            }

            // The page it was asked for, so the component's own paging state moves the way it does
            // against the real API rather than being reset to 1 by a fixture that always says 1.
            return FailSearch
                ? throw new HttpRequestException("nede")
                : Task.FromResult(answer with { PageNumber = page });
        }

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, CancellationToken cancellationToken = default)
        {
            FacetCalls++;
            FacetFilter = filter;
            FacetSearch = search;

            return FailFacets
                ? throw new HttpRequestException("nede")
                : Task.FromResult(_facets);
        }
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> FacetButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".variable-explorer-filters button");

    /// <summary>The facet button whose visible text starts with <paramref name="label"/>.</summary>
    private static AngleSharp.Dom.IElement Facet(IRenderedComponent<VariableExplorer> cut, string label) =>
        FacetButtons(cut).Single(b => b.TextContent.StartsWith(label, StringComparison.Ordinal));

    private static void ClickFacet(IRenderedComponent<VariableExplorer> cut, string label) =>
        Facet(cut, label).Click();

    [Fact]
    public void Render_WhenTheApiOffersFacets_ThenEachValueIsDrawnWithTheCountItWouldLeave()
    {
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        // The count is inside the button's own text rather than beside it, so it is part of the
        // accessible name — a number in a sibling element is read as a stray one or skipped.
        Assert.Equal("Dødsårsaksregisteret (30)", Facet(cut, "Dødsårsaksregisteret").TextContent);
        Assert.Equal("Sentralt helseregister (30)", Facet(cut, "Sentralt helseregister").TextContent);
    }

    [Fact]
    public void Render_WhenTheApiNamesAKildetypeByItsEnumName_ThenTheButtonSaysItInProse()
    {
        // The facet's own displayName is the raw enum name. Munin's explorer carries the prose,
        // and this carries the same words so the two UIs name one value the same way.
        var cut = RenderWith(new FilteringClient(OnePage()));

        Assert.NotNull(Facet(cut, "Sentralt helseregister"));
        Assert.DoesNotContain("SentraltHelseregister", cut.Find(".variable-explorer-filters").TextContent);
    }

    [Fact]
    public void Render_WhenADatatypeArrivesAsABareCode_ThenTheButtonSaysWhatTheCodeMeans()
    {
        // The API returns "1" with no label at all, so a UI has to carry its own mapping or put a
        // button reading "1" on the page.
        var cut = RenderWith(new FilteringClient(OnePage()));

        Assert.Equal("Streng (9)", Facet(cut, "Streng").TextContent);
    }

    [Fact]
    public void Render_WhenAKildeHasDelkilder_ThenTheyAreNestedUnderIt()
    {
        // The whole tree comes out of the facet payload — DelkildeFacet carries both its kilde and
        // its parent delkilde precisely so no second request is needed to draw it.
        var cut = RenderWith(new FilteringClient(OnePage()));

        var kilde = Facet(cut, "Tromsøundersøkelsen").ParentElement!;
        var delkilde = Facet(cut, "Tromsø 4").ParentElement!;
        var nested = Facet(cut, "Første besøk").ParentElement!;

        Assert.Contains(delkilde, kilde.QuerySelectorAll("li"));
        Assert.Contains(nested, delkilde.QuerySelectorAll("li"));
    }

    [Fact]
    public void Filter_WhenAFacetValueIsChosen_ThenTheSearchIsFetchedWithIt()
    {
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal([Dodsarsak], client.SearchFilter?.KildeIds);
    }

    [Fact]
    public void Filter_WhenAFacetValueIsChosen_ThenTheCountsAreRefetchedWithTheSameNarrowing()
    {
        // The counts are cross-filtered. Asking the two endpoints with different narrowing is the
        // one way to put a list and a set of numbers on screen that describe different selections.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal(client.SearchFilter, client.FacetFilter);
        Assert.Equal(2, client.FacetCalls); // the initial load, then the refresh
    }

    [Fact]
    public void Filter_WhenAChosenValueIsChosenAgain_ThenItIsRemovedRatherThanAddedTwice()
    {
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Dødsårsaksregisteret");
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Empty(client.SearchFilter!.KildeIds);
        Assert.True(client.SearchFilter.IsEmpty);
    }

    [Fact]
    public void Filter_WhenAValueIsChosen_ThenItsButtonSaysItIsPressed()
    {
        // aria-pressed rather than aria-current, and spelled out as "false" on the rest: the
        // attribute is what says these are two-state controls at all.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Equal("false", Facet(cut, "Dødsårsaksregisteret").GetAttribute("aria-pressed"));

        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal("true", Facet(cut, "Dødsårsaksregisteret").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Filter_WhenASecondKildetypeIsChosen_ThenItReplacesTheFirstRatherThanJoiningIt()
    {
        // The API takes one kildetype, not a list. Two pressed buttons would promise a filter it
        // cannot express.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Sentralt helseregister");
        ClickFacet(cut, "Biobank");

        Assert.Equal("biobank", client.SearchFilter?.KildeType);
        Assert.Equal("false", Facet(cut, "Sentralt helseregister").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Filter_WhenTheChosenKildetypeIsChosenAgain_ThenItIsCleared()
    {
        // There is no "any kildetype" value to go back to, so pressing the chosen one has to be
        // the way out — which is also what its own aria-pressed promises.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Biobank");
        ClickFacet(cut, "Biobank");

        Assert.Null(client.SearchFilter?.KildeType);
    }

    [Fact]
    public void Filter_WhenAValueIsChosenFromAPageOtherThanTheFirst_ThenItGoesBackToPageOne()
    {
        // Narrowing renumbers every page, so page 7 of the old result is not the same rows.
        var client = new FilteringClient(new Page<VariableSummary>
        {
            Items = [Variable("1. Tale", "KODE")],
            TotalCount = 312,
            PageNumber = 1,
            Size = 25,
            TotalPages = 13
        });
        var cut = RenderWith(client);

        cut.FindAll("div.variables-pagination button")[1].Click(); // Neste
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal(1, client.LastPage);
    }

    [Fact]
    public void Filter_WhenTheFetchFails_ThenTheSelectionIsRolledBackToWhatTheRowsCameFrom()
    {
        // Same invariant the sort rollback protects: the rows on screen are still the old ones, so
        // the buttons have to keep saying so rather than claiming a filter that never arrived.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        client.FailSearch = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal("false", Facet(cut, "Dødsårsaksregisteret").GetAttribute("aria-pressed"));
        Assert.Equal(1, client.FacetCalls); // not refreshed: the counts still describe what is shown
    }

    [Fact]
    public void Filter_WhenTheHostSuppliesOne_ThenTheFirstFetchIsAlreadyNarrowedByIt()
    {
        // The deep-link half of the round trip: a shared URL has to land on the filtered result,
        // not on the whole catalogue with the filters merely drawn as chosen.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var filter = new VariableFilter { KildeIds = [Dodsarsak] };

        var cut = RenderWith(client, b => b.Add(c => c.Filter, filter));

        Assert.Equal(filter, client.SearchFilter);
        Assert.Equal(filter, client.FacetFilter);
        Assert.Equal("true", Facet(cut, "Dødsårsaksregisteret").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Filter_WhenTheReaderChangesIt_ThenTheHostIsToldWhatIsNowInForce()
    {
        // The other half: the host writes this into its own URL, so a filtered search can be
        // linked to at all.
        VariableFilter? reported = null;
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                             b => b.Add(c => c.FilterChanged, f => reported = f));

        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal(new VariableFilter { KildeIds = [Dodsarsak] }, reported);
    }

    [Fact]
    public void Filter_WhenTheFetchFails_ThenTheHostIsToldTheFilterTheRowsActuallyCameFrom()
    {
        // A host that wrote the attempted filter to its URL would hand out a link that reloads
        // into a different selection than the page is showing.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        VariableFilter? reported = null;
        var cut = RenderWith(client, b => b.Add(c => c.FilterChanged, f => reported = f));

        client.FailSearch = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal(VariableFilter.None, reported);
    }

    [Fact]
    public void Filter_WhenNothingHasChangedYet_ThenTheHostIsNotToldOnTheInitialLoad()
    {
        // Nothing has moved, and the value would be the one the host just passed in.
        var reported = 0;

        RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                   b => b.Add(c => c.FilterChanged, _ => reported++));

        Assert.Equal(0, reported);
    }

    [Fact]
    public void Filter_WhenClearIsPressed_ThenEveryFacetIsDropped()
    {
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Dødsårsaksregisteret");
        ClickFacet(cut, "Streng");
        Facet(cut, "Fjern alle filtre").Click();

        Assert.True(client.SearchFilter?.IsEmpty);
    }

    [Fact]
    public void Render_WhenThereIsNothingToClear_ThenTheClearButtonIsInertRatherThanAbsent()
    {
        // Taking the control the reader just pressed out of the document drops focus to <body> —
        // the same reason the pager's buttons carry aria-disabled instead of disabled.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        var clear = Facet(cut, "Fjern alle filtre");
        Assert.Equal("true", clear.GetAttribute("aria-disabled"));
        Assert.False(clear.HasAttribute("disabled"));

        clear.Click();
        Assert.Equal(1, client.SearchCalls); // inert: no request went out

        ClickFacet(cut, "Dødsårsaksregisteret");
        Assert.False(Facet(cut, "Fjern alle filtre").HasAttribute("aria-disabled"));
    }

    [Fact]
    public void Render_WhenTheFacetRefreshFails_ThenTheFiltersStayOnScreenAndSayTheCountsMayBeStale()
    {
        // The rows are the right rows; it is the numbers beside the filters that may now be wrong.
        // Emptying the panel would take the controls the reader is using off the page.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        client.FailFacets = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.NotNull(Facet(cut, "Dødsårsaksregisteret"));
        Assert.Contains("Tallene kan være utdaterte", cut.Find("[role='alert']").TextContent);
        Assert.Single(cut.FindAll("ul.datasourcecard-list > li"));
    }

    [Fact]
    public void Render_WhenTheFacetsHaveNeverArrived_ThenNoEmptyFilterPanelIsDrawn()
    {
        // The first search failed, so the facets were never asked for. A legend and a dead clear
        // button over nothing is furniture.
        var cut = RenderWith(new FailingClient());

        Assert.Empty(cut.FindAll(".variable-explorer-filters"));
    }

    [Fact]
    public void Render_WhenAFacetHasNoValues_ThenItIsLeftOutRatherThanDrawnEmpty()
    {
        // Except variabelgruppe, where the emptiness is the message: with no kilde chosen the API
        // answers with a curated shortlist, and an empty list would otherwise read as a broken one.
        var cut = RenderWith(new FilteringClient(OnePage(), new FilterOptions()));

        var panel = cut.Find(".variable-explorer-filters").TextContent;

        Assert.DoesNotContain("Instrument", panel, StringComparison.Ordinal);
        Assert.Contains("Velg en datakilde for å se variabelgrupper", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_WhenValuesAreChosen_ThenTheStatusLineSaysTheListIsNarrowed()
    {
        // With the facets collapsed, this sentence is the only place that says the list is
        // narrowed at all — and it is the one a screen reader reads back after every change.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Dødsårsaksregisteret");
        Assert.Contains("avgrenset av 1 filter", cut.Find("p[role='status']").TextContent);

        ClickFacet(cut, "Streng");
        Assert.Contains("avgrenset av 2 filtre", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Filter_WhenNothingMatches_ThenTheEmptyStateNamesTheFiltersRatherThanBlamingTheCatalogue()
    {
        var client = new FilteringClient(OnePage());
        var cut = RenderWith(client, b => b.Add(c => c.Filter, new VariableFilter { KildeIds = [Dodsarsak] }));

        Assert.Contains("med filtrene som er valgt", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Filter_WhenAPageIsTurned_ThenTheCountsAreNotRefetched()
    {
        // Paging does not change what the counts describe, and the facet endpoint is the expensive
        // one — it aggregates the whole read model once per facet.
        var client = new FilteringClient(new Page<VariableSummary>
        {
            Items = [Variable("1. Tale", "KODE")],
            TotalCount = 312,
            PageNumber = 1,
            Size = 25,
            TotalPages = 13
        });
        var cut = RenderWith(client);

        cut.FindAll("div.variables-pagination button")[1].Click(); // Neste
        ClickSort(cut, "Datakilde");

        Assert.Equal(1, client.FacetCalls);
    }

    [Fact]
    public void Search_WhenANewSearchRuns_ThenTheCountsAreRefetchedForIt()
    {
        // The counts are cross-filtered against the search as well as the facets, so a new search
        // moves every number in the panel.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        cut.Find("input[type=search]").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal(2, client.FacetCalls);
        Assert.Equal("svelging", client.FacetSearch);
    }

    [Fact]
    public void Render_WhenOnlyOneKildetypeIsLeft_ThenItsHeadingIsNotRepeatedOverTheKilder()
    {
        // One heading over the whole list says nothing the kildetype facet above does not — and
        // there is exactly one whenever a kildetype has been chosen, which is when the panel is
        // most crowded.
        var facets = Facets() with
        {
            KildeTyper = [new() { Value = "biobank", DisplayName = "Biobank", Count = 12 }],
            Kilder = [new() { Id = Tromso, Name = "Tromsøundersøkelsen", KildeType = "biobank", Count = 12 }]
        };

        var cut = RenderWith(new FilteringClient(OnePage(), facets));

        // The kilde sits at the top of its facet's list rather than one level in, under a heading.
        var kilde = Facet(cut, "Tromsøundersøkelsen").ParentElement!;
        Assert.Null(kilde.ParentElement?.ParentElement?.Closest("li"));
    }

    [Fact]
    public void Filter_WhenHarKildekodeverkIsPressedTwice_ThenItStopsFilteringRatherThanAskingForNo()
    {
        // Two states, not three, and the difference is invisible on screen: the obvious-looking
        // negation cycles aria-pressed exactly the same way while sending harKildekodeverk=false,
        // which inverts the result set to only the variables *without* a kildekodeverk.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Har kildekodeverk");

        Assert.True(client.SearchFilter?.HasKildekodeverk);
        Assert.Equal("true", Facet(cut, "Har kildekodeverk").GetAttribute("aria-pressed"));

        ClickFacet(cut, "Har kildekodeverk");

        Assert.Null(client.SearchFilter?.HasKildekodeverk);
        Assert.DoesNotContain("harKildekodeverk", client.SearchFilter!.ToQueryString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_WhenHistoricalIsChosen_ThenItGoesOnTheWireRatherThanOnlyOnTheButton()
    {
        // The one filter whose parameter is left out at its default, so a flip in the wrong
        // direction produces a URL that looks unfiltered rather than one that looks wrong.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Vis historiske");

        Assert.True(client.SearchFilter?.IncludeHistorical);
        Assert.Contains("includeHistorical=true", client.SearchFilter!.ToQueryString(), StringComparison.Ordinal);
        Assert.Equal("true", Facet(cut, "Vis historiske").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Render_WhenADelkildesParentIsMissingFromTheFacets_ThenItBecomesARootRatherThanDisappearing()
    {
        // Routine rather than defensive: the API cross-filters every facet, so a parent delkilde
        // with no matching variables of its own is genuinely absent from a payload its children are
        // in. Dropping the child would be a filter the reader can neither see nor clear — and
        // narrowing the root rule to "no parent at all" is a plausible simplification that nothing
        // else in the suite would catch.
        var absent = new Guid("dddddddd-0000-0000-0000-000000000009");
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")), Facets() with
        {
            Delkilder =
            [
                new()
                {
                    Id = Tromso4Visit, Name = "Første besøk", KildeId = Tromso,
                    ParentDelkildeId = absent, Count = 3
                }
            ]
        });

        var cut = RenderWith(client);

        var orphan = Facet(cut, "Første besøk");
        Assert.Contains(orphan.ParentElement!, Facet(cut, "Tromsøundersøkelsen").ParentElement!.QuerySelectorAll("li"));

        // And still a filter, not just a label: a root that cannot be pressed is the same loss.
        orphan.Click();
        Assert.Equal([Tromso4Visit], client.SearchFilter?.DelkildeIds);
    }

    [Fact]
    public void Render_WhenAParentChainLoopsBackOnItself_ThenTheNodesAreStillDrawn()
    {
        // A self-parented row is one bad record, and every member of a loop has its parent present
        // — so none of them is a root, and without a second pass over what the walk did not reach
        // the whole subtree leaves the panel with no error anywhere. Same loss as a dropped orphan.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")), Facets() with
        {
            Variabelgrupper = [new() { Id = Bakgrunn, Name = "Selvforelder", ParentId = Bakgrunn, Count = 7 }]
        });

        var cut = RenderWith(client);

        Assert.NotNull(Facet(cut, "Selvforelder"));

        ClickFacet(cut, "Selvforelder");
        Assert.Equal([Bakgrunn], client.SearchFilter?.VariabelgruppeIds);
    }

    [Fact]
    public void Render_WhenTwoNodesNameEachOtherAsParent_ThenEachIsDrawnExactlyOnce()
    {
        // The case the second pass actually exists for, and the one the self-parented row above
        // cannot reach: a self-parent is placed whole by a single Build, whereas here building the
        // first node places the second, so the pass has to re-read what it has placed as it goes.
        // Get that wrong and the second node is drawn again as a root — two <li> siblings carrying
        // the same key, which the renderer throws on rather than drawing a stray row.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")), Facets() with
        {
            Variabelgrupper =
            [
                new() { Id = Bakgrunn, Name = "GruppeA", ParentId = Levekaar, Count = 7 },
                new() { Id = Levekaar, Name = "GruppeB", ParentId = Bakgrunn, Count = 4 }
            ]
        });

        var cut = RenderWith(client);

        Assert.Equal(1, Buttons(cut, "GruppeA"));
        Assert.Equal(1, Buttons(cut, "GruppeB"));

        // And a filter rather than a label: the nested one is the one a duplicate would double.
        ClickFacet(cut, "GruppeB");
        Assert.Equal([Levekaar], client.SearchFilter?.VariabelgruppeIds);

        static int Buttons(IRenderedComponent<VariableExplorer> cut, string label) =>
            FacetButtons(cut).Count(b => b.TextContent.StartsWith(label, StringComparison.Ordinal));
    }

    [Fact]
    public void Filter_WhenAFetchIsAlreadyRunning_ThenTheClickIsIgnored()
    {
        // The fourth entry point to the loading state, and the one that does the most on the far
        // side of the guard. Two overlapping applications interleave their rollback captures, so a
        // second fetch that fails can restore a filter that is neither what is on screen nor what
        // was asked for — and then report that filter to the host, which is exactly the "the URL
        // claims something the page is not showing" failure the rollback tests exist to prevent.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        client.StallSearch = true;
        ClickFacet(cut, "Dødsårsaksregisteret");
        ClickFacet(cut, "Streng");

        Assert.Equal(2, client.SearchCalls); // the initial load and the first press, not the second
        Assert.Equal([Dodsarsak], client.SearchFilter?.KildeIds);
        Assert.Empty(client.SearchFilter!.DataTypes);
    }

    [Fact]
    public void Render_Always_ThenTheFilterPanelIsBuiltFromShapesRatherThanFromNewClassNames()
    {
        // Stiler has no accordion, no tree and no checkbox this package can verify, so the panel is
        // <details> for the disclosure, a bare <ul> for the hierarchy and Stiler's own square button
        // in its two states for the values. A class name for any of those would be one the host
        // stylesheet has never heard of.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        var panel = cut.Find(".variable-explorer-filters");

        Assert.NotEmpty(panel.QuerySelectorAll("details > summary"));
        Assert.NotEmpty(panel.QuerySelectorAll("ul li button"));
        Assert.All(panel.QuerySelectorAll("details"), d => Assert.False(d.HasAttribute("class")));
        Assert.All(panel.QuerySelectorAll("ul"), u => Assert.False(u.HasAttribute("class")));
        Assert.All(FacetButtons(cut), b => Assert.Contains("hd-button-square", b.ClassName!));
    }
}
