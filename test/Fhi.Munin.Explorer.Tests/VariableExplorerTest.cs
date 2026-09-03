using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

using static Fhi.Munin.Explorer.Tests.SortHeader;

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
    /// Answers every search with the API's 429, the way a reader who asked too often meets it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="FailingClient"/> on purpose: the point of the tests using it is
    /// that the component tells the two apart. A fake that threw
    /// <see cref="HttpRequestException"/> for both would pass whatever the component said.
    /// </remarks>
    private sealed class RateLimitedClient : EmptyMuninExplorerClient
    {
        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
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

        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Contains("1. Tale", cut.Markup);
        Assert.Contains("V_ALS.F1.ALSFRSR1TALE", cut.Markup);
        Assert.Contains("2 variabler", cut.Markup);
    }

    [Fact]
    public void Render_WhenThereAreNoHits_ThenTheEmptyMessageIsShown()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Contains("Ingen variabler passet søket", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheApiFails_ThenAnErrorMessageIsShownRatherThanThrowing()
    {
        var cut = RenderWith(new FailingClient());

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list > li"));
    }

    [Fact]
    public void Render_WhenTheApiRateLimits_ThenTheReaderIsToldTheyAskedTooOftenRatherThanThatItFailed()
    {
        // "Prøv igjen om litt" is the wrong advice for a throttled reader — trying again is what
        // the limiter counts. The two failures have to read differently or the reader cannot act.
        var cut = RenderWith(new RateLimitedClient());

        var alert = cut.Find("[role='alert']");

        Assert.Contains("for mange forespørsler", alert.TextContent);
        Assert.DoesNotContain("Kunne ikke hente variabler", alert.TextContent);
    }

    [Fact]
    public void Render_WhenTheApiRateLimits_ThenItIsNotShownAsASearchWithNoHits()
    {
        // The trap, seen from the page: a throttled search must never read as "ingen variabler
        // passet søket". That sentence is a claim about the catalogue, and no search was run.
        var cut = RenderWith(new RateLimitedClient(), b => b.Add(c => c.Search, "tale"));

        Assert.DoesNotContain("Ingen variabler passet søket", cut.Markup);
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list > li"));
    }

    [Fact]
    public void Render_WhenTheApiRateLimitsAndTheLanguageIsEn_ThenTheEnglishRateLimitTextIsUsed()
    {
        var cut = RenderWith(new RateLimitedClient(), b => b.Add(c => c.Language, "en"));

        var alert = cut.Find("[role='alert']");

        Assert.Contains("too many requests", alert.TextContent);
        Assert.DoesNotContain("Could not load variables", alert.TextContent);
    }

    [Fact]
    public void Render_WhenTheApiRateLimits_ThenTheComponentDoesNotAskAgainByItself()
    {
        // No retry anywhere in the package, and the component is the other place one could hide.
        // helsedata's cluster shares one address bucket, so every reader's component retrying on
        // the same Retry-After would rebuild the burst that caused the 429.
        var client = new RateLimitedClient();

        RenderWith(client);

        Assert.Equal(1, client.Calls);
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

        var idA = a.Find(".searchbox__freetext").Id;
        var idB = b.Find(".searchbox__freetext").Id;

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

        var input = cut.Find(".searchbox__freetext");

        Assert.Throws<MissingEventHandlerException>(() => input.Input("svelging"));
        Assert.Equal(1, client.Calls); // only the initial load
    }

    [Fact]
    public void Search_WhenTheWholeTextIsTypedBeforeSubmitting_ThenItSearchesOnceWithAllOfIt()
    {
        var client = new FakeClient(OnePage());
        var cut = RenderWith(client);

        // onchange carries the finished value, however fast it was typed or pasted.
        cut.Find(".searchbox__freetext").Change("svelging");
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

        cut.Find(".searchbox__freetext").Change("svelging"); // blur caused by the click
        cut.Find("button[type=submit]").Click();

        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public void Render_Always_ThenTheSearchFieldHasAnAssociatedLabel()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        var input = cut.Find(".searchbox__freetext");
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

    // SortControl, SortButtons and ClickSort come from SortHeader, which ShareableStateTest
    // presses sorts through too. The selector and the "whatever direction suffix it carries"
    // matching live there, once, with the note on why both are scoped to the header.

    [Fact]
    public void Render_WhenNoSortIsChosen_ThenTheApisOwnOrderIsAskedForAscending()
    {
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));

        var cut = RenderWith(client);

        Assert.Equal(SortField.Default, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
        Assert.Equal("Navn ↑", SortButtons(cut)[0].TextContent);
    }

    [Fact]
    public void Render_Always_ThenTheDefaultOrderIsNotLabelledAsANameSort()
    {
        // This guard used to say the opposite, and the reason it flipped is worth keeping.
        //
        // The API's `name` sort leads with kilde, not the name — see the remarks on
        // SortField.Default. When the ordering was a row of standalone buttons, one reading "Navn"
        // claimed the list was ordered by name, which it is not. In a COLUMN HEADER the same word
        // names the column rather than the order: the column holds names, the arrow says it is the
        // active one, and what the list is actually ordered by is stated in full in the status
        // line below it.
        //
        // What must still not happen is the status line calling it a name sort. That is the
        // sentence a reader gets read out, and it is the one that would be wrong.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var labels = SortButtons(cut).Select(k => k.TextContent).ToList();

        Assert.Equal(["Navn ↑", "Kilde", "Datasamling", "Variabelgruppe"], labels);
        Assert.Contains("sortert på Standard", cut.Find("p.caption[role=status]").TextContent);
    }

    [Fact]
    public void Render_Always_ThenEverySortFieldTheContractOffersHasAButton()
    {
        // ResultHeader writes each header cell out by hand with a literal SortField, so the row
        // CAN fall behind the enum — this count is what catches it. A member added to SortField
        // with no cell to press it fails here, and the fix is a cell rather than a longer list.
        // Kode, datatype, status and dataperiode have no button because they are not in the enum
        // at all — the API does not sort on them, and a button would reorder nothing while
        // claiming to have.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Equal(Enum.GetValues<SortField>().Length, SortButtons(cut).Count);
    }

    [Theory]
    [InlineData("Kilde", SortField.Kilde)]
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

        ClickSort(cut, "Navn");

        Assert.Equal(SortField.Default, client.LastSort);
        Assert.Equal(SortDirection.Descending, client.LastDirection);
        Assert.Equal("Navn ↓", SortButtons(cut)[0].TextContent);

        ClickSort(cut, "Navn");

        Assert.Equal(SortDirection.Ascending, client.LastDirection);
    }

    [Fact]
    public void Sort_WhenANewFieldIsChosenAfterDescending_ThenItStartsAscendingAgain()
    {
        // Runa's rule: a new column always starts ascending rather than inheriting the direction
        // of the one before it.
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, "Navn");    // descending
        ClickSort(cut, "Kilde");   // a different field

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

        ClickSort(cut, "Kilde");

        Assert.Equal(1, client.Calls);
        Assert.Equal("Navn ↑", SortButtons(cut)[0].TextContent);
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

        ClickSort(cut, "Kilde");

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Equal("Navn ↑", SortButtons(cut)[0].TextContent);

        var marked = SortButtons(cut).Where(k => k.HasAttribute("aria-current")).ToList();
        Assert.Equal("Navn ↑", Assert.Single(marked).TextContent);

        ClickSort(cut, "Kilde"); // the same retry, not its reversal

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

        cut.Find(".searchbox__freetext").Change("noe helt annet");
        ClickSort(cut, "Kilde");

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

        cut.Find(".searchbox__freetext").Change("noe helt annet");
        ClickSort(cut, "Kilde");

        Assert.Empty(reported);
    }

    [Fact]
    public void Sort_WhenTheLanguageIsEn_ThenTheSortControlIsEnglishToo()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Language, "en"));

        var labels = SortButtons(cut).Select(k => k.TextContent).ToList();

        Assert.Equal(["Name ↑", "Source", "Data collection", "Variable group"], labels);
        Assert.NotNull(cut.Find($"{SortControl} .munin-explorer-data-list__item__row--header"));
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

        cut.Find(".searchbox__freetext").Change("svelging");
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

        cut.Find(".searchbox__freetext").Change("svelging");
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

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal("svelging", client.LastSearch);
        Assert.DoesNotContain("Kunne ikke hente variabler", cut.Markup);
        Assert.Single(cut.FindAll("ul.munin-explorer-data-list > li"));
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

        ClickSort(cut, "Kilde");

        Assert.Contains("sortert på Kilde, stigende", cut.Find("p[role='status']").TextContent);

        ClickSort(cut, "Kilde");

        Assert.Contains("sortert på Kilde, synkende", cut.Find("p[role='status']").TextContent);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenTheListNameNamesTheOrderingToo()
    {
        // The table's accessible name is the same sentence as the status line, so the two cannot
        // drift apart and say the result is ordered two different ways. It moved off the <ul> when
        // the list became the table's body rowgroup: one element names the whole thing, and a
        // named rowgroup inside a named table has the sentence read out twice.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Contains("sortert på Standard, stigende",
                        cut.Find(".munin-explorer-data-list__result[role='table']").GetAttribute("aria-label")!);
    }

    [Fact]
    public void Render_Always_ThenOnlyTheActiveSortFieldIsMarked()
    {
        // aria-current rather than aria-pressed: pressing the active button does not release it,
        // it reverses the direction, and a toggle that never toggles off misdescribes itself.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var marked = SortButtons(cut).Where(k => k.HasAttribute("aria-current")).ToList();

        Assert.Equal("Navn ↑", Assert.Single(marked).TextContent);
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
        // The grouping used to be a fieldset with a legend reading "Sorter etter". It is a column
        // header row now, which is what both helsedata and Runa use: each button names its own
        // column, so the group needs no separate name. What still has to hold is that they are
        // real buttons and that the header sits above the list rather than floating beside it.
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.NotNull(cut.Find($"{SortControl} .munin-explorer-data-list__item__row--header"));
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
    // The column picker. Runa's rules, not helsedata's: Navn is always on screen, the other
    // seven can be turned off one at a time, and the last one left refuses. The choice is not
    // persisted and is not in the host's URL — Runa loses it on a refresh too, and whether it
    // should be remembered is a decision of its own.
    // ---------------------------------------------------------------------------------

    /// <summary>The picker's toggles, in the order it lists them.</summary>
    private static IReadOnlyList<IElement> ColumnToggles(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".dropdown-choicepicker__item button");

    /// <summary>The toggle for one named column, refetched so it is never a stale node.</summary>
    private static IElement ColumnToggle(IRenderedComponent<VariableExplorer> cut, string label) =>
        ColumnToggles(cut).Single(b => b.TextContent.Trim() == label);

    private static void HideColumn(IRenderedComponent<VariableExplorer> cut, string label) =>
        ColumnToggle(cut, label).Click();

    [Fact]
    public void Render_Always_ThenThePickerOffersRunasSevenColumnsAndNotTheName()
    {
        // Runa's set, in Runa's order. It is deliberately not helsedata's five: the variable page
        // is the thing this component REPLACES, so it decides how a row looks and Runa decides
        // what a row says. Navn is missing on purpose — it is the row's disclosure button as well
        // as its first column, so turning it off would take the control that opens the panel away.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Equal(
            ["Kode", "Kilde", "Datasamling", "Variabelgruppe", "Datatype", "Status", "Dataperiode"],
            ColumnToggles(cut).Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public void Render_Always_ThenEveryColumnButStatusStartsOnScreen()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        // Status is the one exception, and it is the filter's doing rather than the picker's:
        // with historical variables excluded every row would say "Active", and a column that says
        // the same word on every row is furniture. See ShowStatusColumn.
        Assert.Equal(["true", "true", "true", "true", "true", "false", "true"],
                     ColumnToggles(cut).Select(b => b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Columns_WhenAColumnIsTurnedOff_ThenBothItsHeaderAndItsCellsGo()
    {
        // Both, or neither. helsedata's row is a flex container and the header row is a second
        // one beside it, so a header cell left behind by its values does not merely look odd —
        // every column after it in every row sits under the wrong name.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        HideColumn(cut, "Kode");

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-header__code"));
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__code"));
        Assert.Equal("false", ColumnToggle(cut, "Kode").GetAttribute("aria-pressed"));
    }

    /// <summary>The text of one named column's cell in the first result row.</summary>
    private static string CellText(IRenderedComponent<VariableExplorer> cut, string key) =>
        cut.Find($".munin-explorer-dataitem-main__{key} .munin-explorer-dataitem-main__column__text")
           .TextContent;

    [Fact]
    public void Columns_WhenOneIsTurnedOffAndOnAgain_ThenEveryCellStillHoldsItsOwnValue()
    {
        // The cells are built with explicit sequence numbers, which Blazor uses positionally to
        // diff one render against the next. A number that repeats or goes backwards patches one
        // column's node with another's — and only from the second render, never the first.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "V_ALS.F1.TALE") with
        {
            KildeShortName = "ALS",
            VariabelgruppeName = "Bakgrunn"
        })));

        HideColumn(cut, "Datasamling");
        ColumnToggle(cut, "Datasamling").Click();

        Assert.Equal("V_ALS.F1.TALE", CellText(cut, "code"));
        Assert.Equal("ALS", CellText(cut, "source"));
        Assert.Equal("Inklusjon", CellText(cut, "dataCollection"));
        Assert.Equal("Bakgrunn", CellText(cut, "theme"));

        // The hidden field name travels with the value, not with the position it was drawn in.
        Assert.Equal("Datasamling: ",
                     cut.Find(".munin-explorer-dataitem-main__dataCollection .screenreader-only").TextContent);
    }

    [Fact]
    public void Columns_WhenAColumnIsTurnedOffAndOnAgain_ThenItComesBack()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        HideColumn(cut, "Datasamling");
        ColumnToggle(cut, "Datasamling").Click();

        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__dataCollection"));
        Assert.Equal("true", ColumnToggle(cut, "Datasamling").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Columns_WhenOnlyOneIsLeft_ThenItRefusesToHideAndSaysWhy()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        // Status is already off, so five presses leave Dataperiode alone.
        foreach (var column in new[] { "Kode", "Kilde", "Datasamling", "Variabelgruppe", "Datatype" })
        {
            HideColumn(cut, column);
        }

        var last = ColumnToggle(cut, "Dataperiode");

        // Inert rather than disabled, the same treatment the pager's buttons get: `disabled` would
        // take it out of the tab order, so the one column a reader might ask about would be the
        // one they could not reach.
        Assert.Equal("true", last.GetAttribute("aria-disabled"));
        Assert.False(last.HasAttribute("disabled"));

        // And it says why, once, rather than on every button.
        var hint = last.GetAttribute("aria-describedby");
        Assert.Equal("Minst én kolonne må vises.", cut.Find($"#{hint}").TextContent);

        last.Click();

        Assert.Equal("true", ColumnToggle(cut, "Dataperiode").GetAttribute("aria-pressed"));
        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__period"));
    }

    [Fact]
    public void Columns_WhenEverythingElseIsOff_ThenTheNameColumnIsStillThere()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        foreach (var column in new[] { "Kode", "Kilde", "Datasamling", "Variabelgruppe", "Datatype" })
        {
            HideColumn(cut, column);
        }

        Assert.NotNull(cut.Find("button.munin-explorer-dataitem-main__name"));
        Assert.NotNull(cut.Find(".munin-explorer-dataitem-header__name"));
    }

    [Fact]
    public void Columns_WhenEveryOptionalOneIsOn_ThenEveryCellNameIsOneSomeStylesheetDefines()
    {
        // The column cells are the only class names the package composes at runtime -
        // $"munin-explorer-dataitem-main__{key}" and $"sortable-header
        // munin-explorer-dataitem-header__{key}" - and neither guard could see them.
        //
        // The shell guard reads literals out of src/, finds only the
        // munin-explorer-dataitem-main__ stem, and drops it, correctly: a stem is not a name. And
        // the Orphans call further down renders the DEFAULT column set, which leaves Status out
        // until a reader turns it on. So the composed names went unchecked from both directions.
        //
        // That was harmless while they were helsedata's names, listed in host-class-names.txt and
        // styled by their stylesheet. After the rename they are ours, the sample stylesheet is the
        // only thing that defines them, and an unstyled name is the failure this repo has now hit
        // twice. Rendering with every optional column on is what makes them exist to be checked.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        foreach (var column in new[] { "Status" })
        {
            ColumnToggle(cut, column).Click();
        }

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Columns_WhenStatusIsTurnedOnByHand_ThenItIsDrawnDespiteTheFilter()
    {
        // The filter decides where Status STARTS, not where it stays. A reader who wants the
        // column anyway can have it: otherwise the picker would show a choice that does nothing,
        // which is worse than not offering it.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__status"));

        ColumnToggle(cut, "Status").Click();

        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__status"));
        Assert.NotNull(cut.Find(".munin-explorer-dataitem-header__status"));
    }

    [Fact]
    public void Columns_WhenStatusIsTurnedOffByHand_ThenTheFilterDoesNotPutItBack()
    {
        // The other direction of the same press, and the one the flag exists for: with historical
        // variables in the list the filter is drawing Status, so turning it off has to record that
        // the reader has chosen as well as hide it. Without the record the press is a visible
        // no-op — aria-pressed goes to false over a column that is still on screen — and every
        // later trip through the filter puts it back.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Vis historiske");

        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__status"));

        ColumnToggle(cut, "Status").Click();

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__status"));
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-header__status"));
        Assert.Equal("false", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));

        // And it stays off through the filter that used to own it: their choice wins from here.
        ClickFacet(cut, "Vis historiske");
        ClickFacet(cut, "Vis historiske");

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__status"));
        Assert.Equal("false", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Columns_WhenHistoricalVariablesAreIncluded_ThenStatusStartsOnScreen()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                             b => b.Add(c => c.Filter, new VariableFilter { IncludeHistorical = true }));

        Assert.Equal("true", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));
        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__status"));
    }

    [Fact]
    public void Columns_WhenTheListIsSearchedAgain_ThenTheChoiceSurvives()
    {
        // The choice belongs to the reader, not to the result. Rebuilding it on every search would
        // put back the columns they had just cleared away, on the page where they cleared them.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        HideColumn(cut, "Kilde");
        cut.Find("form").Submit();

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__source"));
    }

    // ---------------------------------------------------------------------------------
    // Table semantics. Fhi.Metadata-3b1l4: the result list drew a grid of columns out of
    // <div>s and told assistive technology nothing about it — measured in Chrome's own
    // accessibility tree against the sample stylesheet, every row, column and header of it
    // resolved to `generic`, and the aria-sort on the sorted header sat on an element with no
    // role that may carry one. WCAG 1.3.1 and 4.1.2, both Level A.
    //
    // What these cases can and cannot prove is worth being precise about, because the bug they
    // guard hid behind exactly that gap. They assert the ATTRIBUTES are emitted and that the
    // structure they describe is well formed. They cannot assert that a browser resolves them,
    // since the CSS that lays this list out lives in Fhi.Helsedata.Stiler and bUnit applies no
    // stylesheet at all. That half was checked by hand, in Chrome, against both the sample
    // stylesheet and Stiler's own compiled main.css: `table`, `rowGroup`, `row`, `columnHeader`,
    // `rowHeader`, `cell` and `sortDirection=ascending` all appear in the internal accessibility
    // tree with `display: flex` applied. See the bead for the measurements.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Table_WhenTheSearchHasHits_ThenTheResultListExposesRowsAndColumns()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var table = cut.Find(".munin-explorer-data-list__result[role='table']");

        // A header rowgroup and a body rowgroup, which is what <thead> and <tbody> are.
        Assert.Equal("rowgroup", table.QuerySelector(".munin-explorer-data-list__header")!.GetAttribute("role"));
        Assert.Equal("rowgroup", table.QuerySelector("ul.munin-explorer-data-list")!.GetAttribute("role"));

        // One header row of column headers, and one data row per result.
        Assert.Equal("row", cut.Find(".munin-explorer-dataitem-header").GetAttribute("role"));
        Assert.Equal(7, cut.FindAll("[role='columnheader']").Count);

        var row = cut.Find("li.munin-explorer-data-list__item");

        Assert.Equal("row", row.GetAttribute("role"));

        // The name is the row's header, the way Kelda's <th scope="row"> is.
        Assert.Equal("rowheader",
                     row.QuerySelector(".munin-explorer-dataitem-main__name")!.GetAttribute("role"));
        Assert.Equal(6, row.QuerySelectorAll("[role='cell']").Length);

        // The two wrappers between the row and its cells are layout only. They have to say so, or
        // they sit in the tree as anonymous groups between a row and the columns it owns.
        Assert.Equal("none", row.QuerySelector(".munin-explorer-data-list__item__row")!.GetAttribute("role"));
        Assert.Equal("none", row.QuerySelector(".munin-explorer-dataitem-main")!.GetAttribute("role"));
    }

    [Fact]
    public void Table_WhenAColumnIsSorted_ThenAriaSortSitsOnAColumnHeader()
    {
        // The bug axe caught. aria-sort is allowed on columnheader and rowheader and nowhere else,
        // and the cell carrying it had no role at all — so the attribute was discarded and the
        // sort state was announced to nobody.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var sorted = cut.Find("[aria-sort]");

        Assert.Equal("columnheader", sorted.GetAttribute("role"));
        Assert.Equal("ascending", sorted.GetAttribute("aria-sort"));

        ClickSort(cut, "Navn");

        sorted = cut.Find("[aria-sort]");

        Assert.Equal("columnheader", sorted.GetAttribute("role"));
        Assert.Equal("descending", sorted.GetAttribute("aria-sort"));
    }

    [Fact]
    public void Table_Always_ThenNothingButCellsSitsInsideARow()
    {
        // The rule that shaped the markup: a row owns cells and nothing else. It is why the name
        // and the save button are wrapped, why the two layout boxes wear role="none", and why the
        // open panel is a cell rather than a region loose in the rowgroup. Get it wrong and axe
        // reports aria-required-children — which is how the first attempt at this was caught.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Toggles(cut)[0].Click();

        var cells = new[] { "cell", "columnheader", "rowheader" };
        var rows = cut.FindAll("[role='row']");

        // The header row and the one result row. Asserted, because a walk over no rows at all
        // passes without looking at anything.
        Assert.Equal(2, rows.Count);

        foreach (var row in rows)
        {
            Assert.NotEmpty(Owned(row));

            foreach (var owned in Owned(row))
            {
                Assert.Contains(owned.GetAttribute("role"), cells);
            }
        }

        // Every element a row owns, looking through the ones that stepped aside and past the ones
        // hidden from assistive technology — the same walk axe does.
        static IEnumerable<AngleSharp.Dom.IElement> Owned(AngleSharp.Dom.IElement element)
        {
            foreach (var child in element.Children)
            {
                if (child.GetAttribute("aria-hidden") == "true")
                {
                    continue;
                }

                if (child.GetAttribute("role") is "none" or "presentation")
                {
                    foreach (var deeper in Owned(child))
                    {
                        yield return deeper;
                    }

                    continue;
                }

                yield return child;
            }
        }
    }

    [Fact]
    public void Table_WhenARowIsOpen_ThenThePanelIsThatRowsLastCellAndStillARegion()
    {
        // The panel is the list item's second child, so the row has to start above both — and then
        // the panel is something a row owns, which may only be a cell. It is a bare wrapper rather
        // than a role on the panel itself so the panel goes on being the landmark that tells a
        // reader which variable they opened.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Toggles(cut)[0].Click();

        var row = cut.Find("li.munin-explorer-data-list__item");
        var last = row.Children[^1];

        Assert.Equal("cell", last.GetAttribute("role"));
        Assert.False(last.HasAttribute("class"));
        Assert.Equal("region", last.Children[0].GetAttribute("role"));
        Assert.Contains("munin-explorer-detail", last.Children[0].ClassName!);
    }

    [Fact]
    public void Table_WhenThereIsAPager_ThenTheSkipLinkIsOutsideTheTable()
    {
        // It used to sit between the header row and the rows, which is inside the table now — and
        // a link is not something a table may own. Still in the results column, still beside the
        // list it skips.
        var cut = RenderWith(new FakeClient(ResultPage(40)));

        var link = cut.Find("a.munin-explorer-skiplink-pagination");

        Assert.Null(link.Closest("[role='table']"));
        Assert.NotNull(link.Closest(".munin-explorer-results"));
    }

    [Fact]
    public void Columns_WhenTheSortedColumnIsHidden_ThenTheOrderingStaysAndIsStillAnnounced()
    {
        // A decision rather than an oversight, and this is where it is written down. Hiding the
        // column the list is ordered by takes its header cell away, and with it the aria-sort and
        // the arrow — leaving the cell behind would put every row out of line with its own column,
        // which is the rule the header already obeys. The ordering itself is left alone: sorting is
        // server-side, so resetting it here would make a press in the picker fire a query and
        // reorder the list underneath the reader. Excel keeps a sort on a hidden column too.
        var client = new FakeClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickSort(cut, "Kilde");
        ClickSort(cut, "Kilde");

        var calls = client.Calls;

        HideColumn(cut, "Kilde");

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-header__source"));
        Assert.Empty(cut.FindAll("[aria-sort]"));

        // The list is still in that order, nothing was re-fetched to get there, and the live
        // region still says so — which is what makes this survivable rather than silent.
        Assert.Equal(calls, client.Calls);
        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Descending, client.LastDirection);
        Assert.Contains("sortert på Kilde, synkende", cut.Find("p[role='status']").TextContent);

        // And the way back is the control that took it away.
        ColumnToggle(cut, "Kilde").Click();

        Assert.Equal("descending",
                     cut.Find(".munin-explorer-dataitem-header__source").GetAttribute("aria-sort"));
    }

    [Fact]
    public void Columns_WhenTheFilterWouldTakeTheLastColumnAway_ThenStatusStaysOnScreen()
    {
        // The one route around "the last column cannot be hidden" that does not go through the
        // picker. Status is normally the filter's to give and take — it says the same word on every
        // row unless historical variables are in the list — but a reader who has hidden the other
        // six has made it the last column, and a filter nobody associates with columns must not
        // then empty every row down to its name. Deleting this brings that state back, reachable in
        // seven presses and explained by nothing on screen.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Vis historiske");

        foreach (var column in new[] { "Kode", "Kilde", "Datasamling", "Variabelgruppe", "Datatype", "Dataperiode" })
        {
            HideColumn(cut, column);
        }

        Assert.Equal("true", ColumnToggle(cut, "Status").GetAttribute("aria-disabled"));

        ClickFacet(cut, "Vis historiske");

        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__status"));
        Assert.NotNull(cut.Find(".munin-explorer-dataitem-header__status"));
        Assert.Equal("true", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Columns_WhenAnotherColumnComesBack_ThenTheFilterTakesStatusAgain()
    {
        // The other half of the rule above: Status is held only for as long as it is all there is.
        // Give the reader a real column back and Status returns to being the filter's, otherwise
        // one trip through an empty picker would pin it on for the rest of the session.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Vis historiske");

        foreach (var column in new[] { "Kode", "Kilde", "Datasamling", "Variabelgruppe", "Datatype", "Dataperiode" })
        {
            HideColumn(cut, column);
        }

        ClickFacet(cut, "Vis historiske");
        ColumnToggle(cut, "Kode").Click();

        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__code"));
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__status"));
    }

    [Fact]
    public void Render_WhenThereAreNoHits_ThenThePickerStaysAnyway()
    {
        // The same rule the sort control has, for the same reason: a control that leaves the
        // document when a search comes back empty takes the reader's place in it with it, dropping
        // focus to <body>. Moving @ColumnPicker() inside the "there are hits" block would break
        // nothing else in this file, so the rule is asserted here rather than only in prose.
        // A count and no more: which columns the picker offers is
        // Render_Always_ThenThePickerOffersRunasSevenColumnsAndNotTheName's to say, and asserting
        // the list twice would make a change to Runa's set fail here too, reading as "the picker
        // vanished on an empty search" when it did nothing of the kind.
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Equal(7, ColumnToggles(cut).Count);
    }

    [Fact]
    public void Render_Always_ThenThePickerBorrowsItsClassNamesAndInventsNone()
    {
        // The companion to the munin-explorer guard further down, which only inspects names in
        // that prefix — the picker wears eight names outside it, and an invented ninth would slip
        // past that test unnoticed. Every name here was read back off helsedata's compiled
        // stylesheets; one that is not renders as a raw browser default inside a styled page.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var picker = cut.Find(".munin-explorer-header");

        var names = picker.QuerySelectorAll("[class]")
            .Prepend(picker)
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => !HostClassNames.IsOwnStructureName(k))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
        [
            "button-square--ghost",           // Stiler, the ghost colour the sort and facet
                                              //   buttons already wear
            "dropdown",                       // helsedata, variables.css — the trigger's width:
                                              //   `.munin-explorer-header__actions .dropdown
                                              //   { width: 100% }`, unconditional
            "dropdown-choicepicker",          // helsedata, variables.css — the open list
            "dropdown-choicepicker--right",
            "dropdown-choicepicker__item",
            "hd-button-reset",                // Stiler, "a button that draws nothing"
            "hd-button-square",               // Stiler, the square shape
            "screenreader-only",              // Stiler, and load-bearing: it hides the sentence
                                              //   saying why the last column will not turn off
        ], names);

        // The label is the button's own text, so it needs no name at all. An earlier draft wrapped
        // it in a span wearing `form-control__label`, which nothing else here uses and which could
        // not be found in Stiler's compiled stylesheet.
        Assert.Empty(picker.QuerySelectorAll("button span"));
    }

    [Fact]
    public void Render_Always_ThenThePickerCarriesHelsedatasOwnDropdownShape()
    {
        // Their names, read off the compiled variables.css and styles.css rather than guessed at,
        // and their nesting — dropdown-choicepicker is position:absolute and anchors to the
        // wrapper, which is why the inline position:relative below is emitted rather than left to
        // a stylesheet this package does not ship.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        // Both their names, and the exact pair: `dropdown` is the width their actions row gives a
        // trigger, `munin-explorer__dropdown` the z-index over the rows below. Asserted as a
        // set rather than with Contains, which `munin-explorer__dropdown` would satisfy on its
        // own and so could not tell the two apart.
        var dropdown = cut.Find(".munin-explorer-header__actions > details");
        Assert.Equal(["dropdown", "munin-explorer__dropdown"],
                     dropdown.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("relative", dropdown.GetAttribute("style")!);

        // A <details>, because their dropdown opens and closes from React state and this package
        // ships no script. Same reason the filter facets are disclosures.
        var summary = cut.Find(".munin-explorer-header__actions > details > summary");
        Assert.Contains("hd-button-square", summary.ClassName!);
        Assert.Equal("Kolonner", summary.TextContent);

        Assert.NotNull(cut.Find("ul.dropdown-choicepicker.dropdown-choicepicker--right"));
        Assert.Equal(7, cut.FindAll("li.dropdown-choicepicker__item").Count);

        // Not sortable-dropdown, which the bead named: that is their MOBILE sort control and it is
        // display:none above 1280px, so a picker wearing it would vanish on every desktop.
        Assert.Empty(cut.FindAll(".sortable-dropdown"));
    }

    [Fact]
    public void Render_Always_ThenTheDataperiodeColumnSaysWhatThePanelSays()
    {
        // The same two dates the open panel draws above its bar, from the same fields. The column
        // is text rather than helsedata's bar because a bar is drawn entirely by rules this
        // package does not ship: unstyled it is an empty cell, which reads as a variable with no
        // period recorded rather than as a host that has not styled it.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var cell = cut.Find(".munin-explorer-dataitem-main__period");

        Assert.Equal("Dataperiode: ", cell.QuerySelector(".screenreader-only")!.TextContent);
        Assert.Contains("2010", cell.TextContent);
        Assert.Contains("2025", cell.TextContent);

        // Unmarked, unlike every other column: see the English test below for why.
        Assert.Empty(cell.QuerySelectorAll("span[lang]"));
    }

    [Fact]
    public void Render_WhenTheLanguageIsEn_ThenTheDataperiodeColumnIsNotAnnouncedAsNorwegian()
    {
        // The one column whose value the component composes rather than repeats. Its month names
        // are formatted for the reader and the word for a period still running is a UI string, so
        // marking it lang="no" the way a variable name is marked would hand an English sentence to
        // a Norwegian speech synthesiser — the WCAG 3.1.2 argument the marker exists for, running
        // backwards. Left unmarked, it inherits the host page's language like every other string
        // this component writes itself.
        var stillRunning = new VariableSummary
        {
            Id = Guid.NewGuid(),
            Code = "KODE",
            PreferredTerm = "1. Tale",
            KildeName = "Als registeret",
            DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var cut = RenderWith(new FakeClient(OnePage(stillRunning)),
                             b => b.Add(c => c.Language, "en"));

        var cell = cut.Find(".munin-explorer-dataitem-main__period");

        Assert.Contains("Ongoing", cell.TextContent, StringComparison.Ordinal);
        Assert.Empty(cell.QuerySelectorAll("[lang]"));

        // The neighbouring columns still are Norwegian, so this is a distinction the markup draws
        // rather than a marker that went missing everywhere.
        Assert.Equal("no", cut.Find(".munin-explorer-dataitem-main__source span[lang]").GetAttribute("lang"));
    }

    [Fact]
    public void Render_WhenTheCatalogueRecordsNoPeriod_ThenTheCellSaysItIsNotGivenRatherThanOngoing()
    {
        // Neither date, which the two dates PeriodText is otherwise built from cannot express: an
        // open-ended period and an unknown one are different claims, and running them together
        // would print "? – Pågående" over a variable whose period nobody recorded — a statement
        // that data collection is ongoing, made about a variable we know nothing about. Null from
        // PeriodText instead, which the column writes out as "Ikke oppgitt" like every other
        // missing value.
        var noPeriod = new VariableSummary
        {
            Id = Guid.NewGuid(),
            Code = "KODE",
            PreferredTerm = "1. Tale",
            KildeName = "Als registeret"
        };

        var cut = RenderWith(new FakeClient(OnePage(noPeriod)));

        var cell = cut.Find(".munin-explorer-dataitem-main__period");

        Assert.Equal("Dataperiode: Ikke oppgitt", cell.TextContent);
        Assert.DoesNotContain("Pågående", cell.TextContent, StringComparison.Ordinal);

        // "Ikke oppgitt" is this component's word, not the catalogue's, so it is unmarked for the
        // reason the dates beside it are — see the English test above.
        Assert.Empty(cell.QuerySelectorAll("[lang]"));
    }

    [Fact]
    public void Render_WhenOnlyTheEndOfThePeriodIsKnown_ThenTheStartIsAQuestionMark()
    {
        // The substitute PeriodBar already writes above its bar, so the column and the open panel
        // never describe one variable's period two ways. A blank where the start should be would
        // read as a period that began at the left edge of the row.
        var endedOnly = new VariableSummary
        {
            Id = Guid.NewGuid(),
            Code = "KODE",
            PreferredTerm = "1. Tale",
            KildeName = "Als registeret",
            DataTo = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var cut = RenderWith(new FakeClient(OnePage(endedOnly)));

        var cell = cut.Find(".munin-explorer-dataitem-main__period");

        Assert.StartsWith("Dataperiode: ? – ", cell.TextContent, StringComparison.Ordinal);
        Assert.Contains("2025", cell.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Pågående", cell.TextContent, StringComparison.Ordinal);
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
        Assert.Equal("searchbox__freetext", cut.Find(".searchbox__freetext").ClassName);
        Assert.NotNull(cut.Find("div.searchbox__freetext-container"));

        // hd-button-square carries the shape, button-square--primary the colour, and
        // searchbox__freetext-submit-button places it inside the field's reserved padding.
        var submit = cut.Find("button[type=submit]").ClassName!;
        Assert.Contains("hd-button-square", submit);
        Assert.Contains("button-square--primary", submit);
        Assert.Contains("searchbox__freetext-submit-button", submit);
    }

    [Fact]
    public void Render_Always_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // The companion to the assertion below, and the wider of the two. That one pins the names in
        // the munin-explorer prefix exactly, which is the right shape for names we invent — a
        // ninth appearing is news. This one asks the different question, of every class in the DOM
        // and not just that prefix: does any stylesheet, ours or helsedata's, define it at all?
        //
        // Borrowed names needed their own check because nothing was watching them. Their `variable-meta__body`
        // came off this view: it wore the look of helsedata's `variable-meta` family, they define the
        // family but not that member, and so it promised a rule that has never existed anywhere.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // The munin-explorer prefix for a closed result list, pinned name by name so a new one is
        // news. Each is annotated below with who defines it; an invented name, or one no stylesheet
        // can point at, is the failure this package exists to avoid, and it has happened twice.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            "munin-explorer",            // ours, a handle
            // The search row's wrapper and the clear button inside it. Both are drawn on every
            // render now: the button is always present and greys out when there is nothing to
            // clear, rather than appearing and disappearing beside a field being typed in.
            "munin-explorer-search",
            "munin-explorer-search__clear",
            "munin-explorer-filters",    // ours, a handle
            // The toolbar row: a container of its own, because in inline flow the last button's
            // trailing margin counted against the line and the row broke apart under a scrollbar.
            "munin-explorer-filters__toolbar",
            "munin-explorer-container",  // ours, Stiler components/munin-explorer/
            "munin-explorer-results",    // ours, Stiler components/munin-explorer/
            // The column picker, all four theirs, all four read off the compiled variables.css
            // rather than guessed at. The one they do NOT include is `sortable-dropdown`, which
            // the bead pointed at: that is their mobile sort control, `display: none` above
            // 1280px, so a picker wearing it would be invisible on every desktop.
            "munin-explorer-header",                  // ours, Stiler components/munin-explorer/
            "munin-explorer-header__actions",         // ours, Stiler components/munin-explorer/
            "munin-explorer__dropdown",               // ours, Stiler (the z-index)
            "munin-explorer-header__actions-button",  // ours, Stiler components/munin-explorer/
        ], invented);
        Assert.Equal("munin-explorer", cut.Find("section").ClassName);

        // The filter panel wears Stiler's fieldset alongside the handle, so a host that styles
        // nothing still gets the fieldset the sort control gets.
        Assert.Contains("form-fieldset", cut.Find(".munin-explorer-filters").ClassName!);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenStilersCardLayoutIsUsedForTheResults()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.NotNull(cut.Find("ul.munin-explorer-data-list > li.munin-explorer-data-list__item > div.munin-explorer-data-list__item__row"));
        Assert.NotNull(cut.Find("button.munin-explorer-dataitem-main__name"));
        Assert.NotNull(cut.Find(".munin-explorer-dataitem-main__column > .munin-explorer-dataitem-main__column__text"));
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

        cut.Find(".searchbox__freetext").Change("noe helt annet");

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
        // markup that needs one is markup that shows its scaffolding on helsedata's page. On the
        // table rather than the <ul> under it, which is now the body rowgroup.
        var name = cut.Find(".munin-explorer-data-list__result[role='table']").GetAttribute("aria-label")!;

        Assert.Contains("1 variabel funnet", name);
        Assert.Contains("«tale»", name);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenTheNameIsTheDisclosureAndNotAHeading()
    {
        // This guarded the opposite until the row became a flex container. A heading per result
        // would let a screen-reader user move between them with the heading rotor, which is why it
        // was there — but helsedata sizes the name cell with `munin-explorer-dataitem-main__name`, and a
        // heading in between becomes the flex item, so the column stops lining up with its header.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.HeadingLevel, 3));

        // No heading per result. An earlier version wrapped the name in one so results could be
        // walked with a heading rotor, but their row is `display: flex` and the name cell is sized
        // by `.munin-explorer-dataitem-main__name` — a heading in between becomes the flex item and the
        // column falls out of line with its header. Neither reference wraps it: helsedata puts the
        // button straight in the row, and Runa's rows are table rows. The rows are a list of list
        // items, each with a named disclosure carrying aria-expanded.
        Assert.Empty(cut.FindAll("li h1, li h2, li h3, li h4, li h5, li h6"));

        var name = cut.Find("li button.munin-explorer-dataitem-main__name");

        Assert.Equal("1. Tale", name.TextContent);
        Assert.Equal("BUTTON", name.TagName);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenEveryFieldIsLabelledWithWhatItIs()
    {
        // A table had column headers doing this job. A card has nothing, and "Inklusjon" on
        // its own does not say which field it is.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "V_ALS.F1.TALE"))));

        var info = cut.Find(".munin-explorer-dataitem-main").TextContent;

        // The column header names the field on screen, so no cell shows its own label. The label
        // is still in the DOM for a screen reader moving down a column, inside Stiler's
        // screenreader-only span — visible to assistive technology, not to the eye.
        Assert.Contains("V_ALS.F1.TALE", info);
        Assert.Contains("Als registeret", info);
        Assert.Contains("Inklusjon", info);

        var cell = cut.Find(".munin-explorer-dataitem-main__code");

        Assert.Equal("Kode: ", cell.QuerySelector(".screenreader-only")!.TextContent);
        Assert.Equal("V_ALS.F1.TALE",
                     cell.QuerySelector(".munin-explorer-dataitem-main__column__text")!.TextContent);
        // Periode is not a Runa column, so it is not a row column here either — it is in the panel.
        Assert.DoesNotContain("Periode:", info);
    }

    [Fact]
    public void Render_WhenTheResultsAreShown_ThenTheListIsMarkedBusyWithoutAnExtraTabStop()
    {
        // The table version wrapped itself in a focusable scroll box, because a box that
        // scrolls sideways and cannot be focused cannot be scrolled from the keyboard. Cards
        // wrap instead of scrolling, so that tab stop is gone rather than merely moved.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var list = cut.Find("ul.munin-explorer-data-list");

        Assert.Equal("false", list.GetAttribute("aria-busy"));
        Assert.False(list.HasAttribute("tabindex"));
        Assert.Empty(cut.FindAll("[tabindex]"));
    }

    [Fact]
    public void Render_Always_ThenTheKildeColumnShowsTheShortNameWithTheFullOneOnHover()
    {
        // Runa's column reads "ALS" and carries "Als registeret" as its title. A kilde name is long
        // and repeats down every row of one register's variables, so the short form is what makes
        // the column readable — and the full name has to stay reachable, not be thrown away.
        var cut = RenderWith(new FakeClient(OnePage(new VariableSummary
        {
            Id = Guid.NewGuid(),
            Code = "V_ALS.F1.TALE",
            PreferredTerm = "1. Tale",
            KildeName = "Als registeret",
            KildeShortName = "ALS",
        })));

        var cell = cut.Find(".munin-explorer-dataitem-main__source");

        Assert.Equal("ALS", cell.QuerySelector(".munin-explorer-dataitem-main__column__text")!.TextContent);
        Assert.Equal("Als registeret", cell.GetAttribute("title"));
    }

    [Fact]
    public void Render_WhenAKildeHasNoShortName_ThenTheColumnFallsBackToTheFullName()
    {
        // Not every kilde has a short name, and a blank cell would be worse than a long one.
        var cut = RenderWith(new FakeClient(OnePage(new VariableSummary
        {
            Id = Guid.NewGuid(),
            Code = "V_X.1",
            PreferredTerm = "Uten kortnavn",
            KildeName = "Et register uten kortnavn",
        })));

        var cell = cut.Find(".munin-explorer-dataitem-main__source");

        Assert.Equal("Et register uten kortnavn",
                     cell.QuerySelector(".munin-explorer-dataitem-main__column__text")!.TextContent);
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

        var info = cut.Find(".munin-explorer-dataitem-main");

        Assert.Contains("Ikke oppgitt", info.TextContent);
        // The field name is present for assistive technology, hidden from the eye.
        Assert.Equal("Kilde: ",
                     cut.Find(".munin-explorer-dataitem-main__source .screenreader-only").TextContent);
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
        // The description is not in the row any more — see the panel.
        Assert.DoesNotContain("Hvordan er talen?", cut.Find(".munin-explorer-dataitem-main").TextContent);
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

        Assert.Equal("no", cut.Find("button.munin-explorer-dataitem-main__name .munin-explorer-dataitem-main__column__text").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".munin-explorer-dataitem-main__column__text span[lang]").GetAttribute("lang"));
        Assert.False(cut.Find("ul.munin-explorer-data-list").HasAttribute("lang"));
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

        Assert.Equal("false", cut.Find("ul.munin-explorer-data-list").GetAttribute("aria-busy"));

        cut.Find("form").Submit(); // second search, still in flight

        Assert.Equal("true", cut.Find("ul.munin-explorer-data-list").GetAttribute("aria-busy"));

        await cut.InvokeAsync(() => client.Answer(hits));

        cut.WaitForAssertion(() =>
            Assert.Equal("false", cut.Find("ul.munin-explorer-data-list").GetAttribute("aria-busy")));
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

    /// <summary>Forrige and Neste, which are not the only buttons in the pager.</summary>
    /// <remarks>
    /// A child selector, so neither the numbered pages nor the size control is caught by it: those
    /// sit one level down inside their own wrappers, and every caller here counts on this being
    /// exactly the pair.
    /// </remarks>
    private static IReadOnlyList<AngleSharp.Dom.IElement> PagerButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("div.munin-explorer-pagination .munin-explorer-pagination-content > button");

    private static AngleSharp.Dom.IElement Previous(IRenderedComponent<VariableExplorer> cut) =>
        PagerButtons(cut)[0];

    private static AngleSharp.Dom.IElement Next(IRenderedComponent<VariableExplorer> cut) =>
        PagerButtons(cut)[^1];

    /// <summary>The numbered buttons, in the order they are drawn.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> PageNumberButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("div.munin-explorer-pagination-pages > button");

    /// <summary>Where the pager says the reader is, as <c>"2/13"</c>.</summary>
    /// <remarks>
    /// Read off the run of numbers rather than off a caption, which is where the position moved to
    /// under Fhi.Metadata-ejcbi: the filled number is the page in force, and the run always ends on
    /// the last page. The separator is this helper's own and not the component's — nothing on
    /// screen says "2/13", so no caller can mistake this for an assertion about prose, and the one
    /// English test asserts the numbers' own labels instead. <c>Single</c> is load-bearing: two
    /// filled numbers, or none, is a pager that has stopped saying where the reader is.
    /// </remarks>
    private static string Position(IRenderedComponent<VariableExplorer> cut)
    {
        var numbers = PageNumberButtons(cut);
        var current = numbers.Single(number => number.GetAttribute("aria-current") == "page");

        return $"{current.TextContent}/{numbers[^1].TextContent}";
    }

    private static string StatusLine(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find("p[role='status']").TextContent;

    [Fact]
    public void Page_WhenNextIsPressed_ThenTheNextPageIsFetchedWithTheSearchAndOrderIntact()
    {
        // Turning a page must not quietly become a new search: the rows would change for two
        // reasons at once and the user would have no way to tell which.
        var client = new PagedClient(312);
        var cut = RenderWith(client, b => b.Add(c => c.Search, "tale"));

        ClickSort(cut, "Kilde"); // something for the page turn to preserve

        Next(cut).Click();

        Assert.Equal(2, client.LastPage);
        Assert.Equal("tale", client.LastSearch);
        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Ascending, client.LastDirection);
        Assert.Equal(3, client.Calls); // initial load, the sort, this page turn
        Assert.Equal("2/13", Position(cut));
    }

    [Fact]
    public void Page_WhenPreviousIsPressed_ThenThePageBeforeIsFetched()
    {
        var client = new PagedClient(312);
        var cut = RenderWith(client);

        Next(cut).Click();
        Next(cut).Click();
        Assert.Equal("3/13", Position(cut));

        Previous(cut).Click();

        Assert.Equal(2, client.LastPage);
        Assert.Equal("2/13", Position(cut));
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
        Assert.Equal("1/13", Position(cut));
    }

    [Fact]
    public void Page_WhenOnTheLastPage_ThenNextIsUnavailableAndAsksForNothing()
    {
        // An exact multiple of the page size: 50 rows at 25 a page is two full pages, which is
        // where an off-by-one in the page count offers a third page with nothing on it.
        var client = new PagedClient(50);
        var cut = RenderWith(client);

        Next(cut).Click();

        Assert.Equal("2/2", Position(cut));
        Assert.Equal("true", Next(cut).GetAttribute("aria-disabled"));
        Assert.Null(Previous(cut).GetAttribute("aria-disabled"));

        Next(cut).Click();

        Assert.Equal(2, client.Calls); // initial load and the one page turn
        Assert.Equal("2/2", Position(cut));
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

        Assert.Empty(cut.FindAll("div.munin-explorer-pagination"));
        Assert.Empty(cut.FindAll("a.munin-explorer-skiplink-pagination"));
    }

    [Fact]
    public void Page_WhenThereAreNoHitsAtAll_ThenThereIsNoPager()
    {
        var cut = RenderWith(new PagedClient(0));

        Assert.Empty(cut.FindAll("div.munin-explorer-pagination"));
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

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal(1, client.LastPage);
        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal("1/13", Position(cut));
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
        Assert.Equal("1/13", Position(cut));
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
        Assert.Equal("1/13", Position(cut));
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

        Assert.NotEmpty(cut.FindAll("div.munin-explorer-pagination"));
        Assert.Equal("1/13", Position(cut)); // rolled back, and still describing real rows
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

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Empty(cut.FindAll("div.munin-explorer-pagination"));
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
        Assert.Equal("8/8", Position(cut));
        Assert.Contains("Viser 176–200 av 200 variabler funnet", StatusLine(cut));
        Assert.Equal(25, cut.FindAll("ul.munin-explorer-data-list > li").Count);
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
        Assert.Equal("1/13", Position(cut));
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

        Assert.NotEmpty(cut.FindAll("div.munin-explorer-pagination"));
        Assert.Equal("1/13", Position(cut));
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
        Assert.Equal("1/13", Position(cut));
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

        Assert.Equal("1/13", Position(cut));
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

        Assert.NotEmpty(cut.FindAll("div.munin-explorer-pagination"));
        Assert.NotEmpty(cut.FindAll("a.munin-explorer-skiplink-pagination"));
        Assert.Equal("1/1", Position(cut));
        Assert.Contains("10 variabler funnet", StatusLine(cut)); // the whole result, so no range
        Assert.Equal(10, cut.FindAll("ul.munin-explorer-data-list > li").Count);

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

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Empty(cut.FindAll("div.munin-explorer-pagination"));
        Assert.Empty(cut.FindAll("a.munin-explorer-skiplink-pagination"));
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
        Assert.NotEmpty(cut.FindAll("div.munin-explorer-pagination"));
        Assert.Equal("1/13", Position(cut));
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

        Assert.Equal("13/13", Position(cut));
        Assert.Contains("Viser 301–312 av 312 variabler funnet", StatusLine(cut));
        Assert.Equal(12, cut.FindAll("ul.munin-explorer-data-list > li").Count);
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
    [InlineData(500, "2/4", "Viser 101–200 av 312")]
    [InlineData(0, "2/312", "Viser 2–2 av 312")]
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

        // The numbers themselves are digits in any language; what has to be translated is what a
        // screen reader hears instead of "one", and the two say different things.
        Assert.Equal("Showing page 1", AccessibleName.Of(PageNumberButtons(cut)[0]));
        Assert.Equal("Go to page 2", AccessibleName.Of(PageNumberButtons(cut)[1]));

        Assert.Equal(
            "Variables per page",
            AccessibleName.Of(cut.Find(".munin-explorer-pagination-size select")));
        Assert.Equal("Skip to pagination", cut.Find("a.munin-explorer-skiplink-pagination").TextContent);
        Assert.Contains("Showing 1–25 of 312 variables found", StatusLine(cut));
    }

    // ---------------------------------------------------------------------------------
    // The shape decided in Fhi.Metadata-ejcbi: Forrige, numbered pages, Neste, and a
    // page-size dropdown. What it replaced was "Side 1 av 907" between two buttons, which
    // says where the reader is and gives them nowhere to go — page 453 was 452 presses
    // away, and the last page was unreachable in any practical sense.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Page_WhenTheListIsLong_ThenTheLastPageIsOnePressAwayRatherThanTwelve()
    {
        // The defect itself. Forrige and Neste move by one, so before this the only route to the
        // end of a 13-page list was pressing Neste twelve times.
        var client = new PagedClient(312);
        var cut = RenderWith(client);

        var last = PageNumberButtons(cut)[^1];

        Assert.Equal("13", last.TextContent);

        last.Click();

        Assert.Equal(13, client.LastPage);
        Assert.Equal("13/13", Position(cut));
    }

    [Fact]
    public void Page_WhenTheRunIsDrawn_ThenExactlyOneNumberSaysItIsThePageInForce()
    {
        // Two would be a pager describing two places at once; none would be a row of numbers with
        // nothing saying which one the reader is standing on, which is what a screen reader gets
        // when the fill is the only marker.
        var cut = RenderWith(new PagedClient(312));

        Assert.Single(PageNumberButtons(cut), n => n.GetAttribute("aria-current") == "page");

        Next(cut).Click();

        var current = PageNumberButtons(cut).Single(n => n.GetAttribute("aria-current") == "page");

        Assert.Equal("2", current.TextContent);
    }

    [Fact]
    public void Page_WhenAScreenReaderMeetsTheRun_ThenEachNumberSaysWhetherItMovesOrMarks()
    {
        // "1" and "2" are the same word to a screen reader reading a row of digits. What tells them
        // apart is the sentence each carries, and the two sentences are deliberately different in
        // kind: where pressing would go, and where the reader already is.
        var cut = RenderWith(new PagedClient(312));

        var numbers = PageNumberButtons(cut);

        Assert.Equal("Viser side 1", AccessibleName.Of(numbers[0]));
        Assert.Equal("Gå til side 2", AccessibleName.Of(numbers[1]));

        // The visible digit is inside the sentence, or a speech-input user saying "2" reaches
        // nothing (WCAG 2.5.3).
        Assert.All(numbers, number => Assert.Contains(number.TextContent, AccessibleName.Of(number)));
    }

    [Fact]
    public void Page_WhenTheRunSkipsPages_ThenTheSkipIsNotSomethingToPressOrHear()
    {
        // The ellipsis stands for pages, not for a page. Left announced it is a control that does
        // nothing; left as a button it is a tab stop that does nothing.
        var cut = RenderWith(new PagedClient(312));

        var skip = cut.Find(".munin-explorer-pagination-pages > span");

        Assert.Equal("…", skip.TextContent);
        Assert.Equal("true", skip.GetAttribute("aria-hidden"));

        // 13 pages, the run showing 1 2 3 … 13: six numbers would mean the skip became one.
        Assert.Equal(["1", "2", "3", "13"], PageNumberButtons(cut).Select(n => n.TextContent));
    }

    [Fact]
    public void Page_WhenTheSizeIsChosen_ThenItIsADropdownRatherThanARowOfButtons()
    {
        // The other half of the decision, taken from Runa: helsedata.no has no page-size control at
        // all, and three buttons plus a label was three names wide in a row that already carries a
        // pager. A <select> is one control, and one an unstyled host still draws as a dropdown.
        var cut = RenderWith(new PagedClient(312));

        var size = cut.Find(".munin-explorer-pagination-size");

        Assert.Empty(size.QuerySelectorAll("button"));
        Assert.Equal(["10", "20", "50"], size.QuerySelectorAll("option").Select(o => o.TextContent));
        Assert.Equal("Variabler per side", AccessibleName.Of(size.QuerySelector("select")!));
    }

    // ---------------------------------------------------------------------------------
    // The pager's own accessibility and styling contract. The class names here are ours,
    // under the munin-explorer prefix, and were helsedata's `variables-pagination*` until
    // Fhi.Metadata-hyyxl: Stiler defines no pagination rule of its own, their variable
    // page's stylesheet does, and borrowing it left every host outside their estate with an
    // unstyled pager. Pinning the names is what keeps someone from "tidying" them into
    // names no stylesheet has ever heard of — back into helsedata's prefix included.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenThePagerWearsOurOwnPrefixedClassNames()
    {
        var cut = RenderWith(new PagedClient(312));

        var content = cut.Find("div.munin-explorer-pagination > div.munin-explorer-pagination-content");

        Assert.NotNull(content);
        Assert.Equal(2, PagerButtons(cut).Count);
        Assert.All(PagerButtons(cut), button => Assert.Contains("hd-button-square", button.ClassName!));
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenThePagerBorrowsNothingFromHelsedatasOwnStylesheet()
    {
        // The whole subtree, not just the two names above, because what this guards against is a
        // helsedata name creeping back in beside them. That is not hypothetical: the pager wore
        // `variables-pagination*` for as long as it did precisely because their variables.css had
        // the rule and Stiler had none, and the argument for borrowing reads as sound right up
        // until the component is mounted somewhere that stylesheet is not.
        //
        // An exact list rather than a prefix check: the buttons are deliberately Stiler's own, so
        // "everything here is ours" would be false, and "nothing here is helsedata's" is only
        // checkable by naming what is here.
        var cut = RenderWith(new PagedClient(312));

        var pager = cut.Find("div.munin-explorer-pagination");

        var names = HostClassNames.Of(pager.QuerySelectorAll("[class]").Prepend(pager))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
        [
            "button-square--ghost",               // Stiler, a page that is not the one in force
            "button-square--secondary",           // Stiler, the two page turns and the page in force
            "caption",                            // Stiler, the size label and the run's ellipsis
            "hd-button-square",                   // Stiler, the square shape
            "margin-right",                       // Stiler, what keeps the numbers apart
            "munin-explorer-pagination",          // ours, Stiler components/munin-explorer/
            "munin-explorer-pagination-content",  // ours, Stiler components/munin-explorer/
            "munin-explorer-pagination-pages",    // ours, and outstanding with Stiler
            "munin-explorer-pagination-size",     // ours, and outstanding with Stiler
        ], names);
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenEveryClassNameInThePagerIsOneSomeStylesheetDefines()
    {
        // The pager is drawn only when there is more than one page, so it slips past every other
        // Orphans check in this file — each of those renders a single page and the pager is not in
        // the DOM to be read. That mattered less while its names were helsedata's, listed in
        // host-class-names.txt: they were defined whatever this repository did. They are ours now,
        // the sample stylesheet is the only thing here that defines them, and a name with no rule
        // renders at raw browser defaults inside an otherwise styled page.
        //
        // The whole render rather than the pager subtree, because the pager is not the only thing
        // gated on more than one page: `munin-explorer-skiplink-pagination` is too, and it sits
        // outside div.munin-explorer-pagination, so a subtree check would leave the one other name
        // in this branch unread — as would anything a later >1-page branch adds.
        var cut = RenderWith(new PagedClient(312));

        // The pager has to actually be in this render, or the check below passes on an empty set.
        cut.Find("div.munin-explorer-pagination");

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenTheSkipLinkComesBeforeTheCardsAndTargetsThePager()
    {
        // Without it a keyboard user tabs through 25 cards to reach Neste. It has to sit ahead of
        // the list to save anything, and its target has to be focusable programmatically, or
        // following it moves the viewport while focus stays behind.
        var cut = RenderWith(new PagedClient(312));

        var skiplink = cut.Find("a.munin-explorer-skiplink-pagination");
        var pager = cut.Find("div.munin-explorer-pagination");

        Assert.Equal($"#{pager.Id}", skiplink.GetAttribute("href"));
        Assert.Equal("-1", pager.GetAttribute("tabindex"));
        Assert.Equal("Hopp til paginering", skiplink.TextContent);

        // Ahead of the results, otherwise it skips nothing.
        var markup = cut.Markup;
        // Ahead of the ROWS, not ahead of the header: the header holds four sort buttons and is
        // worth tabbing through. It is the twenty-five variables the link exists to skip.
        Assert.True(markup.IndexOf("munin-explorer-skiplink-pagination", StringComparison.Ordinal)
                    < markup.IndexOf("<ul class=\"munin-explorer-data-list\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenTheSkipLinkIsOursAndSitsOutsideTheColumnPickerHeader()
    {
        // The two facts Stiler's rule for this link stands on, neither of which any other check
        // here can see. Both guards in this repository ask whether a NAME has a rule, and both
        // look in the same two places: the capture of helsedata's live page and the sample
        // stylesheet. Neither reads Stiler. The borrowed `skiplink-pagination` was in both — it is
        // helsedata's own name and both samples style it themselves — while a host on Stiler alone
        // drew a permanently visible "Hopp til paginering" over every multi-page result list, and
        // nothing here could tell. The failure runs backwards from an ordinary missing rule: what
        // was missing was the rule that HIDES the link, so it showed up as an extra link rather
        // than as an unstyled one.
        //
        // So: the name is ours, whole, under our prefix — a borrowed name here means a host
        // outside helsedata has nowhere to get the rule from. And the anchor is no descendant of
        // munin-explorer-header, which opens and closes entirely inside ColumnPicker() well before
        // this link is rendered, which is why Stiler's rule for it is unscoped.
        var cut = RenderWith(new PagedClient(312));

        var skiplink = cut.Find("a.munin-explorer-skiplink-pagination");

        Assert.Equal("munin-explorer-skiplink-pagination", skiplink.GetAttribute("class"));

        // The header is in this render — so the check below is about where the anchor sits, not
        // about an element that happens to be absent.
        Assert.NotEmpty(cut.FindAll("div.munin-explorer-header"));
        Assert.Empty(cut.FindAll(".munin-explorer-header a.munin-explorer-skiplink-pagination"));
    }

    [Fact]
    public void SkipLink_WhenTheSampleStylesheetHidesIt_ThenItGoesOffScreenRatherThanOutOfTheTabOrder()
    {
        // The half of the bug no other check in this repository can see. The guards ask whether a
        // name has a rule that declares SOMETHING — HostClassNames.Orphans and
        // scripts/assert-sample-css-in-step.sh both read the block now, so an empty one no longer
        // reads as coverage — but neither can ask WHICH declarations, because they are asked of all
        // ~75 names at once and the answer differs for every one. The skip link satisfied them the
        // whole time it was broken and would satisfy them still: its resting rule was never empty.
        // What was missing on a Stiler-only host was one particular DECLARATION, the one that takes
        // the link off screen until it is focused, and only a test about this link can name it.
        //
        // `display: none` is the way the invariant gets broken, and it is the tempting way: it
        // looks like hiding, and it reads as tidier than a negative offset. It also takes the
        // anchor out of the tab order, which deletes the link for exactly the reader it exists for
        // — the keyboard user who would otherwise tab through twenty-five cards to reach the next
        // page. That is a WCAG 2.1 AA regression on a site where AA is a legal requirement, and it
        // is invisible to anyone using a mouse.
        //
        // Whitespace is squeezed out first, so `position:absolute` answers for `position: absolute`
        // and a reformat of the samples does not read as a regression.
        var rules = HostClassNames.SampleDeclarationsFor("munin-explorer-skiplink-pagination");
        static string Squeezed(string css) => new([.. css.Where(c => !char.IsWhiteSpace(c))]);

        // Each rule is picked by its exact selector and then read in file order, and the first is
        // the base one. Not `Assert.Single`: this file writes responsive overrides under the
        // identical selector inside an `@media`, and those come back as rules of their own — three
        // names already appear twice that way. Adding such an override here breaks nothing, and it
        // must not report as "the collection contained 2 matching elements" against an invariant
        // it leaves intact.
        IReadOnlyList<string> BlocksFor(string selector) =>
            [.. rules.Where(r => r.Selector == selector).Select(r => Squeezed(r.Declarations))];

        var resting = BlocksFor(".munin-explorer-skiplink-pagination");
        var focused = BlocksFor(".munin-explorer-skiplink-pagination:focus");

        Assert.NotEmpty(resting);
        Assert.Contains("position:absolute", resting[0], StringComparison.Ordinal);
        Assert.Contains("left:-10000px", resting[0], StringComparison.Ordinal);

        // The twin has to exist and has to undo the offset, which is the half an assertion about
        // the resting rule alone cannot see. Delete the `:focus` block and everything above still
        // passes, while the link stays at -10000px forever: the keyboard reader tabs onto a
        // focused link they can neither see nor read. That is the mirror image of the shipped bug
        // and the same WCAG 2.1 AA failure, 2.4.7 Focus Visible instead of a link nobody wanted.
        Assert.NotEmpty(focused);
        Assert.Contains("position:static", focused[0], StringComparison.Ordinal);

        // Across every rule: hiding it on focus would be the same regression arriving one keypress
        // later.
        Assert.All(rules, r => Assert.DoesNotContain("display:none", Squeezed(r.Declarations), StringComparison.Ordinal));
    }

    [Fact]
    public void Render_WhenThereIsMoreThanOnePage_ThenThePagerIsANamedLandmarkWithLabelledButtons()
    {
        // A second navigation landmark on the host's page has to say what it navigates, and
        // "Forrige" on its own does not say forrige what. Each accessible name starts with the
        // word on the button, so a speech-input user saying what they see still hits it (2.5.3).
        var cut = RenderWith(new PagedClient(312));

        var pager = cut.Find("div.munin-explorer-pagination");

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

        Assert.NotEqual(a.Find("div.munin-explorer-pagination").Id, b.Find("div.munin-explorer-pagination").Id);
        Assert.Equal($"#{a.Find("div.munin-explorer-pagination").Id}",
                     a.Find("a.munin-explorer-skiplink-pagination").GetAttribute("href"));
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
    /// <remarks>
    /// <paramref name="filteredAnswer"/> is what a narrowing request gets back, when a test supplies
    /// one: without it every filter answers with the same number of pages, and a narrowing that
    /// lands on a single page — the state the pager flag exists for — cannot be reached at all.
    /// </remarks>
    private sealed class FilteringClient(
        Page<VariableSummary> answer,
        FilterOptions? facets = null,
        Page<VariableSummary>? filteredAnswer = null,
        IReadOnlyList<PropertyMetadataEntry>? vocabulary = null)
        : EmptyMuninExplorerClient
    {
        private readonly FilterOptions _facets = facets ?? Facets();
        private readonly IReadOnlyList<PropertyMetadataEntry> _vocabulary = vocabulary ?? [];

        /// <summary>How many times the datakategori words were asked for.</summary>
        public int VocabularyCalls { get; private set; }

        /// <summary>Fail the vocabulary fetch — the degraded-labels path.</summary>
        public bool FailVocabulary { get; set; }

        public override Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(
            CancellationToken cancellationToken = default)
        {
            VocabularyCalls++;

            return FailVocabulary
                ? Task.FromException<IReadOnlyList<PropertyMetadataEntry>>(
                    new HttpRequestException("the API is down"))
                : Task.FromResult(_vocabulary);
        }

        // Never completed. A search asked for while this is set stays in flight for the rest of
        // the test, which is the only way to press a second facet while the first is still running.
        private readonly TaskCompletionSource<Page<VariableSummary>> _stalled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public VariableFilter? SearchFilter { get; private set; }
        public VariableFilter? FacetFilter { get; private set; }
        public string? FacetSearch { get; private set; }
        public string? LastSearch { get; private set; }
        public int SearchCalls { get; private set; }
        public int FacetCalls { get; private set; }
        public int LastPage { get; private set; }
        public SortField LastSort { get; private set; }
        public SortDirection LastDirection { get; private set; }

        /// <summary>Fail every search from the next one on — the rollback path.</summary>
        public bool FailSearch { get; set; }

        /// <summary>Answer every search from the next one on with the API's 429.</summary>
        /// <remarks>
        /// Its own switch rather than a mode of <see cref="FailSearch"/>, for the same reason
        /// <see cref="RateLimitFacets"/> is: the tests using it are about the two being told apart.
        /// </remarks>
        public bool RateLimitSearch { get; set; }

        /// <summary>Fail every facet refresh from the next one on.</summary>
        public bool FailFacets { get; set; }

        /// <summary>Answer every facet refresh from the next one on with the API's 429.</summary>
        /// <remarks>
        /// Its own switch rather than a mode of <see cref="FailFacets"/>, for the reason the
        /// separate <c>RateLimitedClient</c> exists: the point of the test using it is that the
        /// panel tells the two apart, which a single flag could not show.
        /// </remarks>
        public bool RateLimitFacets { get; set; }

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
            LastSearch = search;
            LastPage = page;
            LastSort = sort;
            LastDirection = direction;

            if (StallSearch)
            {
                return _stalled.Task;
            }

            if (RateLimitSearch)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            var body = filter is { ActiveCount: > 0 } && filteredAnswer is not null
                ? filteredAnswer
                : answer;

            // The page it was asked for, so the component's own paging state moves the way it does
            // against the real API rather than being reset to 1 by a fixture that always says 1.
            return FailSearch
                ? throw new HttpRequestException("nede")
                : Task.FromResult(body with { PageNumber = page });
        }

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null, CancellationToken cancellationToken = default)
        {
            FacetCalls++;
            FacetFilter = filter;
            FacetSearch = search;

            if (RateLimitFacets)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            return FailFacets
                ? throw new HttpRequestException("nede")
                : Task.FromResult(_facets);
        }
    }

    /// <summary>
    /// Every control in the panel a label can name: a facet value's own label, and the toolbar
    /// buttons, which are the panel's only buttons now the values are checkboxes.
    /// </summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> FacetControls(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".munin-explorer-filters button, .munin-explorer-filters li > label");

    /// <summary>
    /// The control in the panel whose visible text starts with <paramref name="label"/> — a facet
    /// value, or one of the toolbar buttons, which this selector also reaches. No label collides
    /// today, and <c>Single</c> is what says so if one ever starts to.
    /// </summary>
    private static AngleSharp.Dom.IElement Facet(IRenderedComponent<VariableExplorer> cut, string label) =>
        FacetControls(cut).Single(b => b.TextContent.StartsWith(label, StringComparison.Ordinal));

    /// <summary>The checkbox a facet value is chosen with.</summary>
    private static IElement FacetBox(IRenderedComponent<VariableExplorer> cut, string label) =>
        Facet(cut, label).QuerySelector("input[type=checkbox]")!;

    /// <summary>Whether a facet value is ticked — the fact <c>aria-pressed</c> used to carry.</summary>
    private static bool FacetChosen(IRenderedComponent<VariableExplorer> cut, string label) =>
        FacetBox(cut, label).HasAttribute("checked");

    /// <summary>
    /// Press a control: a toolbar button is clicked, a facet value is ticked or unticked.
    /// </summary>
    /// <remarks>
    /// A checkbox answers a change event and not a click, so a <c>Click()</c> here would leave the
    /// filter untouched and every test using it green over a control that does nothing.
    /// </remarks>
    private static void ClickFacet(IRenderedComponent<VariableExplorer> cut, string label)
    {
        var control = Facet(cut, label);

        if (control.QuerySelector("input[type=checkbox]") is { } box)
        {
            box.Change(!box.HasAttribute("checked"));

            return;
        }

        control.Click();
    }

    // ---- datakategori and dataperiode (Fhi.Metadata-uidue) ----

    private static readonly DataCategoryFacet[] TwoCategories =
    [
        new() { Value = "ehds-cat:population-health-surveys", Count = 12 },
        new() { Value = "ehds-cat:biobanks", Count = 3 }
    ];

    /// <summary>The vocabulary the API serves, holding the catalogue's word for one of the two.</summary>
    /// <remarks>
    /// Deliberately covering only one token. The other is what proves an unresolved code is shown
    /// rather than dropped — a facet that hid what it could not name would silently offer fewer
    /// choices than the catalogue has.
    /// </remarks>
    private static IReadOnlyList<PropertyMetadataEntry> CategoryWords() =>
    [
        new()
        {
            Key = "healthCategory",
            OptionsJson = """
                [{"value":"ehds-cat:population-health-surveys","label":"Befolkningsundersøkelser","labelEn":"Population health surveys"}]
                """
        }
    ];

    private static FilterOptions FacetsWith(
        IReadOnlyList<DataCategoryFacet>? categories = null, DateInterval? range = null) =>
        Facets() with { DataCategories = categories ?? [], DateRange = range };

    /// <summary>The facet disclosure headings, which is where a facet is present or absent.</summary>
    /// <remarks>
    /// Scoped to the panel rather than asserted over the whole markup: "Dataperiode" is also a
    /// results column, so a DoesNotContain over cut.Markup can never pass and would have to be
    /// weakened into meaninglessness to try.
    /// </remarks>
    private static IReadOnlyList<string> FacetHeadings(IRenderedComponent<VariableExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-filters summary").Select(s => s.TextContent)];

    private static IReadOnlyList<AngleSharp.Dom.IElement> DateInputs(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-filters input[type=date]")];

    /// <summary>The filter panel. <c>Panel</c> further down is the opened row's detail panel.</summary>
    private static AngleSharp.Dom.IElement FilterPanel(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-filters");

    /// <summary>The facet disclosures, and the ones of them drawn open.</summary>
    /// <remarks>
    /// Direct children only: the column picker is a <c>&lt;details&gt;</c> too, and the delkilde
    /// tree nests lists rather than disclosures, so a descendant query would count neither honestly.
    /// </remarks>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Disclosures(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-filters > details")];

    /// <inheritdoc cref="Disclosures"/>
    private static IReadOnlyList<AngleSharp.Dom.IElement> OpenDisclosures(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-filters > details[open]")];

    /// <summary>An answer with nothing in any facet, which is what a selection matching nothing gets.</summary>
    private static FilterOptions NothingLeft() => new() { TotalCount = 0 };

    /// <summary>Answers with the full facets first, then with nothing, as narrowing to zero does.</summary>
    private sealed class EmptyingClient(Page<VariableSummary> answer, FilterOptions? first = null)
        : EmptyMuninExplorerClient
    {
        private readonly FilterOptions _first = first ?? Facets();

        public bool Exhausted { get; set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Exhausted ? OnePage() : answer);

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Exhausted ? NothingLeft() : _first);
    }

    [Theory]
    [InlineData("Dødsårsaksregisteret")]
    [InlineData("Bakgrunn")]
    [InlineData("Streng")]
    public void Filter_WhenASelectionLeavesNothing_ThenTheFacetThatMadeItSurvivesAndCanBeUndone(string chosen)
    {
        // THE TRAP, and why a case per facet: the ten groups are built by ten separate methods.
        // At zero rows the API reports nothing for any of them, the chosen value included — so
        // there is nothing left to press. (Fhi.Metadata-v2bgr)
        VariableFilter? reported = null;
        var client = new EmptyingClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client, b => b.Add(c => c.FilterChanged, f => reported = f));

        ClickFacet(cut, chosen);
        client.Exhausted = true;
        ClickFacet(cut, "Vis historiske");

        Assert.Contains("0 variabler", cut.Markup, StringComparison.Ordinal);

        // The control is still there and still ticked.
        Assert.True(FacetChosen(cut, chosen));

        // And the counts are gone rather than stale: they described a selection no longer on screen.
        Assert.DoesNotContain("(", Facet(cut, chosen).TextContent, StringComparison.Ordinal);

        // Undone, which is the whole point — present but inert would be the same dead end with
        // more furniture. Unticking it takes the choice off the filter the host is given.
        ClickFacet(cut, chosen);

        Assert.False(FacetChosen(cut, chosen));
        Assert.True(reported!.ActiveCount < 2, $"the choice should be off the filter, got {reported}");
    }

    /// <summary>Answers nothing for the reader's filter, and the full facets for no filter.</summary>
    private sealed class ColdEmptyClient(Page<VariableSummary> answer) : EmptyMuninExplorerClient
    {
        public int UnfilteredAsks { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(answer);

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default)
        {
            if (filter is null || filter.IsEmpty)
            {
                UnfilteredAsks++;

                return Task.FromResult(Facets());
            }

            return Task.FromResult(NothingLeft());
        }
    }

    [Fact]
    public void Filter_WhenTheReaderArrivesOnALinkThatMatchesNothing_ThenTheControlsAreStillDrawn()
    {
        // Found in the browser, not by the tests above: they narrow into the empty state, so a
        // populated answer is already on screen to keep. A reader arriving on a shared link has no
        // such answer, and retention alone leaves them exactly as stranded. (Fhi.Metadata-v2bgr)
        var client = new ColdEmptyClient(OnePage());
        var cut = RenderWith(client,
            b => b.Add(c => c.Filter, new VariableFilter { KildeIds = [Dodsarsak] }));

        Assert.Equal(1, client.UnfilteredAsks);

        Assert.True(FacetChosen(cut, "Dødsårsaksregisteret"));
        Assert.DoesNotContain("(", Facet(cut, "Dødsårsaksregisteret").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_WhenTheFallbackAskFails_ThenTheReaderIsToldAndOfferedTheRetry()
    {
        // Swallowing it would report a failed request as a panel with nothing to offer, and cost
        // the reader the retry — the one thing that can bring the controls back. (Fhi.Metadata-v2bgr)
        var client = new FailingFallbackClient(OnePage());
        var cut = RenderWith(client,
            b => b.Add(c => c.Filter, new VariableFilter { KildeIds = [Dodsarsak] }));

        Assert.NotEmpty(cut.FindAll(".infobox"));
        Assert.NotNull(cut.FindAll("button").SingleOrDefault(b => b.TextContent.Contains("filtre", StringComparison.OrdinalIgnoreCase)
                                                                 && b.TextContent.Contains("Prøv", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Answers nothing for the reader's filter, and throws when asked without one.</summary>
    private sealed class FailingFallbackClient(Page<VariableSummary> answer) : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(answer);

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default) =>
            filter is null || filter.IsEmpty
                ? Task.FromException<FilterOptions>(new HttpRequestException("nede"))
                : Task.FromResult(NothingLeft());
    }

    [Fact]
    public void Filter_WhenNothingIsChosenAndTheApiOffersNothing_ThenNoFacetIsDrawn()
    {
        // THE SECOND TRAP: do not simply always keep the panel. With no selection there is nothing
        // to undo, so an answer with nothing in it must still drop the value facets rather than
        // leaving ten disclosures behind.
        var client = new EmptyingClient(OnePage(Variable("1. Tale", "KODE"))) { Exhausted = true };
        var cut = RenderWith(client);

        // Two survive for reasons of their own and always did: variabelgruppe says its emptiness
        // out loud, and andre filtre holds the historical toggle rather than API values.
        Assert.Equal(["Variabelgruppe", "Andre filtre"], FacetHeadings(cut));
    }

    [Fact]
    public void Filter_WhenExpandAllIsPressed_ThenEveryFacetIsOpen()
    {
        // The one thing a native <details> cannot do for itself, and the reason the open attribute
        // stopped being a constant. Only kildetype and kilde start open. (Fhi.Metadata-wcbxi)
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.NotEqual(Disclosures(cut).Count, OpenDisclosures(cut).Count);

        ClickFacet(cut, "Utvid alle");

        Assert.Equal(Disclosures(cut).Count, OpenDisclosures(cut).Count);
    }

    [Fact]
    public void Filter_WhenCollapseAllIsPressed_ThenNoFacetIsOpen()
    {
        // Both directions, because a press that only ever opens is half a control: the panel is
        // hundreds of buttons expanded, which is what there has to be a way back from.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Utvid alle");
        ClickFacet(cut, "Skjul alle");

        Assert.Empty(OpenDisclosures(cut));
    }

    [Fact]
    public void Filter_WhenAFacetIsFoldedOrUnfolded_ThenTheChangeIsAnnounced()
    {
        // A press rewrites every disclosure and moves no focus, so without this a screen-reader
        // reader gets no sign it did anything. Empty before the first press. (Fhi.Metadata-wcbxi)
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));
        var region = cut.Find(".munin-explorer-filters p[aria-live='polite']");

        Assert.Equal("", region.TextContent);

        ClickFacet(cut, "Utvid alle");

        Assert.Equal("Alle filtre er utvidet.", region.TextContent);

        ClickFacet(cut, "Skjul alle");

        Assert.Equal("Alle filtre er skjult.", region.TextContent);
    }

    [Fact]
    public void Filter_WhenAValueIsChosenAfterCollapseAll_ThenTheFacetsStayFolded()
    {
        // The fold is the reader's and a refresh is not allowed to undo it — the same invariant that
        // has always kept a filter change from collapsing a facet the reader opened.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Skjul alle");
        ClickFacet(cut, "Vis historiske");

        Assert.Empty(OpenDisclosures(cut));
    }

    [Fact]
    public void Filter_Always_ThenTheThreeToolbarButtonsShareOneContainerAndCarryNoMarginsOfTheirOwn()
    {
        // The container is what the host's `gap` hangs on. In inline flow each button carried a
        // `margin-right` and the last one's trailing 16px counted against the line, so the row
        // needed 369.05px of the 369.00 an expanded panel leaves. (Fhi.Metadata-l9l2n.37)
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        var toolbar = cut.Find(".munin-explorer-filters__toolbar");
        var buttons = toolbar.Children;

        Assert.Equal(
            ["Utvid alle", "Skjul alle", "Nivålinjer"],
            buttons.Select(b => b.TextContent.Trim()));
        Assert.All(buttons, b => Assert.DoesNotContain("margin-", b.ClassName!));
    }

    [Fact]
    public void Filter_WhenLevelLinesArePressed_ThenThePanelMarksThemAndTheHostIsTold()
    {
        // The package draws no lines and remembers no preference: it emits the marker a host styles
        // and raises the change, which is the whole storage mechanism it offers. (Fhi.Metadata-wcbxi)
        List<bool> reported = [];
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                             b => b.Add(c => c.LevelLinesChanged, reported.Add));

        Assert.Null(FilterPanel(cut).GetAttribute("data-level-lines"));

        ClickFacet(cut, "Nivålinjer");

        Assert.Equal("true", FilterPanel(cut).GetAttribute("data-level-lines"));
        Assert.Equal("true", Facet(cut, "Nivålinjer").GetAttribute("aria-pressed"));

        ClickFacet(cut, "Nivålinjer");

        // Off is the absence of the attribute rather than "false", so a host styles one selector.
        // aria-pressed is the opposite and is always spelled out: a stuck "true" would go on
        // announcing the lines as on after they went off.
        Assert.Null(FilterPanel(cut).GetAttribute("data-level-lines"));
        Assert.Equal("false", Facet(cut, "Nivålinjer").GetAttribute("aria-pressed"));
        Assert.Equal([true, false], reported);
    }

    [Fact]
    public void Filter_WhenTheHostSetsLevelLines_ThenTheyStartOn()
    {
        // The other half of the mechanism: a host that stored what the press raised passes it back.
        // Without this the parameter is write-only and remembering it is impossible.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                             b => b.Add(c => c.LevelLines, true));

        Assert.Equal("true", FilterPanel(cut).GetAttribute("data-level-lines"));
        Assert.Equal("true", Facet(cut, "Nivålinjer").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Filter_WhenTheSelectionYieldsRowsAgain_ThenTheCountsComeBack()
    {
        // The retained answer is for the stranded state only. Once the API has something to say,
        // it says it — otherwise the panel would keep showing countless values for good.
        var client = new EmptyingClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Dødsårsaksregisteret");
        client.Exhausted = true;
        ClickFacet(cut, "Vis historiske");

        Assert.DoesNotContain("(", Facet(cut, "Dødsårsaksregisteret").TextContent, StringComparison.Ordinal);

        client.Exhausted = false;
        ClickFacet(cut, "Vis historiske");

        Assert.Equal("Dødsårsaksregisteret (30)", Facet(cut, "Dødsårsaksregisteret").TextContent);
    }

    [Fact]
    public void Filter_WhenTheApiOffersDatakategorier_ThenTheyAreDrawnInTheCataloguesOwnWords()
    {
        // The words are the catalogue's, resolved through the same vocabulary Kelda reads. A table
        // of EHDS tokens transcribed into Texts is the other way to get words here, and it is the
        // one the note above Texts.FacetCategory forbids.
        var cut = RenderWith(new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")), FacetsWith(TwoCategories), vocabulary: CategoryWords()));

        Assert.Equal("Befolkningsundersøkelser (12)", Facet(cut, "Befolkningsundersøkelser").TextContent);

        // The token the vocabulary does not name is shown as itself rather than hidden. Ugly and
        // honest beats a facet quietly offering fewer choices than the catalogue has.
        Assert.Equal("ehds-cat:biobanks (3)", Facet(cut, "ehds-cat:biobanks").TextContent);
    }

    [Fact]
    public void Filter_WhenTheVocabularyCannotBeFetched_ThenTheFacetStillFiltersOnTokens()
    {
        // The vocabulary is a label lookup, not the facet. Losing it costs the choices their words
        // and nothing else — and it must not report a second failure beside the facet error, which
        // would have the panel announce a broken filter that is not broken.
        var client = new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")), FacetsWith(TwoCategories), vocabulary: CategoryWords())
        { FailVocabulary = true };
        var cut = RenderWith(client);

        Assert.Equal("ehds-cat:biobanks (3)", Facet(cut, "ehds-cat:biobanks").TextContent);
        Assert.Empty(cut.FindAll(".infobox"));
    }

    [Fact]
    public void Filter_WhenADatakategoriIsTicked_ThenTheFilterCarriesItsToken()
    {
        VariableFilter? reported = null;
        var client = new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")), FacetsWith(TwoCategories), vocabulary: CategoryWords());
        var cut = RenderWith(client, b => b.Add(c => c.FilterChanged, f => reported = f));

        ClickFacet(cut, "Befolkningsundersøkelser");

        // The token, not the word: the word is a label for a reader and the API filters on neither
        // it nor a prefix-stripped form of the code.
        Assert.Equal(["ehds-cat:population-health-surveys"], reported!.Categories);
        Assert.Equal(reported, client.SearchFilter);
    }

    [Fact]
    public void Filter_WhenTheFiltersCasingDiffersFromTheApis_ThenTickingStillInvertsTheFacet()
    {
        // How a facet marks itself chosen and how ToggleAsync removes have to be the SAME
        // comparison, and the case that shows it is a token whose casing differs from the API's —
        // which a shared link can carry, since VariableFilter.Parse takes the query string as
        // written. Marked case-insensitively over a case-sensitive removal, such a token draws as
        // chosen, and ticking it appends the canonical spelling instead of removing anything: the
        // box stays ticked and the filter has grown.
        //
        // The invariant asserted is the one that holds whichever comparison is chosen, as long as
        // both ends agree: ticking a facet inverts what it says about itself.
        var cut = RenderWith(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE")),
                                FacetsWith(TwoCategories), vocabulary: CategoryWords()),
            b => b.Add(c => c.Filter, new VariableFilter { Categories = ["EHDS-CAT:BIOBANKS"] }));

        var before = FacetChosen(cut, "ehds-cat:biobanks");

        ClickFacet(cut, "ehds-cat:biobanks");

        Assert.NotEqual(before, FacetChosen(cut, "ehds-cat:biobanks"));
    }

    [Fact]
    public void Filter_WhenAnExactlyMatchingDatakategoriIsPressedAgain_ThenItComesOffTheFilter()
    {
        // The ordinary path the invariant above protects: a token spelled as the API spells it
        // goes on and comes back off, leaving nothing behind.
        VariableFilter? reported = null;
        var cut = RenderWith(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE")),
                                FacetsWith(TwoCategories), vocabulary: CategoryWords()),
            b => b
                .Add(c => c.Filter, new VariableFilter { Categories = ["ehds-cat:biobanks"] })
                .Add(c => c.FilterChanged, f => reported = f));

        Assert.True(FacetChosen(cut, "ehds-cat:biobanks"));

        ClickFacet(cut, "ehds-cat:biobanks");

        Assert.Empty(reported!.Categories);
        Assert.False(FacetChosen(cut, "ehds-cat:biobanks"));
    }

    [Fact]
    public void Filter_WhenTheFacetsRefresh_ThenTheCategoryWordsAreFetchedOnceAndOnlyWhenNeeded()
    {
        // The vocabulary is a second request against a rate limit this component already shares
        // with the search beside it, so it is asked for lazily and at most once. Both halves are
        // easy to lose: a fetch on mount costs a call an API predating the facet can never use,
        // and a fetch per refresh costs one on every keystroke.
        var client = new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")), FacetsWith(TwoCategories), vocabulary: CategoryWords());
        var cut = RenderWith(client);

        Assert.Equal(1, client.VocabularyCalls);

        // Two more facet refreshes, which is what narrowing does.
        ClickFacet(cut, "Dødsårsaksregisteret");
        ClickFacet(cut, "Befolkningsundersøkelser");

        Assert.True(client.FacetCalls > 1, "the facets should have refreshed");
        Assert.Equal(1, client.VocabularyCalls);
    }

    [Fact]
    public void Filter_WhenThereAreNoDatakategorier_ThenTheCategoryWordsAreNotFetchedAtAll()
    {
        // The lazy half on its own: an API that predates the facet returns none, and a request
        // whose answer nothing on screen could use is one the panel should not make.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith());

        RenderWith(client);

        Assert.Equal(0, client.VocabularyCalls);
    }

    [Fact]
    public void Filter_WhenTheApiOffersNoDatakategorier_ThenTheFacetIsLeftOutRatherThanDrawnEmpty()
    {
        // THE TRAP, first direction. An API that predates the facet returns none at all, and the
        // line under the group list says such a facet is left out rather than drawn as an empty
        // disclosure. A fixture with only rich data passes against an implementation that always
        // draws the heading — the Kelda defect in Fhi.Metadata-2fomm.3.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith()));

        Assert.DoesNotContain("Datakategori", FacetHeadings(cut));
    }

    [Fact]
    public void Filter_WhenTheApiReportsADateRange_ThenBothFieldsAreDrawnBoundedByIt()
    {
        // Asserting the two inputs are in the DOM, not that a heading exists: a heading passes over
        // an empty accordion, which is exactly what the group-drop predicate and the FacetList /
        // EmptyText body would have produced before FacetGroup learned a third shape.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range)));

        var inputs = DateInputs(cut);

        Assert.Equal(2, inputs.Count);
        Assert.Equal(["2010-01-01", "2010-01-01"], inputs.Select(i => i.GetAttribute("min")));
        Assert.Equal(["2025-06-01", "2025-06-01"], inputs.Select(i => i.GetAttribute("max")));

        // Labelled, and the label points at the field rather than merely sitting above it.
        var labels = cut.FindAll(".munin-explorer-filters label").Select(l => l.GetAttribute("for"));

        Assert.Contains(inputs[0].Id, labels);
        Assert.Contains(inputs[1].Id, labels);
    }

    [Fact]
    public void Filter_WhenDatesAreSet_ThenTheFoldedDataperiodeSaysItIsNarrowing()
    {
        // Every other facet counts its chosen values into the summary, so a reader who folds one
        // can still see it is doing something. The dataperiode holds no values to count, so it has
        // to report the number itself — otherwise a folded facet over an active date filter reads
        // as inert, which is the one state the summary exists to rule out.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var cut = RenderWith(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range)),
            b => b.Add(c => c.Filter, new VariableFilter { DataFrom = new DateOnly(2015, 3, 4) }));

        Assert.Contains("Dataperiode (1)", FacetHeadings(cut));

        // Both ends set counts as two, the same way two ticked values in any other facet do.
        DateInputs(cut)[1].Change("2020-12-31");

        Assert.Contains("Dataperiode (2)", FacetHeadings(cut));
    }

    [Fact]
    public void Filter_WhenNoDatesAreSet_ThenTheDataperiodeSummaryCarriesNoCount()
    {
        // The other direction: a count of zero must not draw "(0)", which would report a filter
        // nobody has set.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var cut = RenderWith(new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range)));

        Assert.Contains("Dataperiode", FacetHeadings(cut));
        Assert.DoesNotContain("Dataperiode (", string.Join("|", FacetHeadings(cut)), StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_WhenTheApiReportsNoDateRange_ThenNoDataperiodeFacetIsDrawn()
    {
        // THE TRAP, second direction, and the one the third FacetGroup shape could most easily
        // break: a Body that is never null would draw an unbounded pair of fields over a selection
        // whose date span the API could not report.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith()));

        Assert.Empty(DateInputs(cut));
        Assert.DoesNotContain("Dataperiode", FacetHeadings(cut));
    }

    [Fact]
    public void Filter_WhenABoundCarriesAnOffset_ThenTheDateIsTheOneTheValueWrites()
    {
        // THE SEAM. DateInterval carries DateTimeOffset; the filter takes DateOnly. 2020-01-01
        // at +02:00 is 1 January — .UtcDateTime.Date would make it 31 December, and
        // .LocalDateTime.Date would hand the answer to whichever machine runs the test, so CI and
        // a Norwegian laptop would disagree and neither would be obviously wrong.
        //
        // Max null as well, which DateInterval documents as "unknown": one end bounded and the
        // other open.
        var range = new DateInterval { Min = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)) };
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range)));

        var inputs = DateInputs(cut);

        Assert.Equal(2, inputs.Count);
        Assert.Equal("2020-01-01", inputs[0].GetAttribute("min"));
        Assert.True(string.IsNullOrEmpty(inputs[1].GetAttribute("max")));
    }

    [Fact]
    public void Filter_WhenTheFromDateIsSet_ThenItReachesTheFilterAndTheApi()
    {
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        VariableFilter? reported = null;
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range));
        var cut = RenderWith(client, b => b.Add(c => c.FilterChanged, f => reported = f));

        DateInputs(cut)[0].Change("2015-03-04");

        Assert.Equal(new DateOnly(2015, 3, 4), reported!.DataFrom);
        Assert.Equal(new DateOnly(2015, 3, 4), client.SearchFilter!.DataFrom);
    }

    [Theory]
    [InlineData("0002-01-01")]
    [InlineData("2010-06-01")]
    public void Filter_WhenATypedDateFallsOutsideTheFieldsOwnBounds_ThenItIsNotApplied(string typed)
    {
        // A native date input reports a complete value as soon as all three segments hold digits,
        // so typing 01.01.2017 arrives as 0002-01-01 on the way. Applied, it empties the list and
        // reaches the host's URL. The to-field's lower bound is the from-date. (Fhi.Metadata-uidue)
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        VariableFilter? reported = null;
        var cut = RenderWith(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range)),
            b => b
                .Add(c => c.Filter, new VariableFilter { DataFrom = new DateOnly(2015, 1, 1) })
                .Add(c => c.FilterChanged, f => reported = f));

        DateInputs(cut)[1].Change(typed);

        Assert.Null(reported);
    }

    [Fact]
    public void Filter_WhenADateFilterEmptiesTheResults_ThenTheFacetStaysSoItCanBeUndone()
    {
        // THE TRAP. A date filter matching nothing is exactly when the API stops reporting a
        // range, and the facet was dropped on that — taking away the only control that could undo
        // it and leaving the address bar as the way out. (Fhi.Metadata-yxhv1)
        var cut = RenderWith(
            new FilteringClient(OnePage(), FacetsWith()),
            b => b.Add(c => c.Filter, new VariableFilter { DataTo = new DateOnly(2017, 1, 1) }));

        // The inputs themselves, not the heading: a heading passes over an empty accordion.
        var inputs = DateInputs(cut);

        Assert.Equal(2, inputs.Count);
        Assert.Equal("2017-01-01", inputs[1].GetAttribute("value"));
        Assert.Contains("Dataperiode (1)", FacetHeadings(cut));
    }

    [Fact]
    public void Filter_WhenNoDatesAreSetAndTheApiReportsNoRange_ThenTheFacetIsStillLeftOut()
    {
        // The other direction: without a date set there is nothing to undo, so the old rule holds.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith()));

        Assert.Empty(DateInputs(cut));
        Assert.DoesNotContain("Dataperiode", FacetHeadings(cut));
    }

    [Fact]
    public void Filter_WhenATypedDateIsInsideTheBounds_ThenItIsApplied()
    {
        // The guard must reject only what the field already says is out of range.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        VariableFilter? reported = null;
        var cut = RenderWith(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE")), FacetsWith(range: range)),
            b => b
                .Add(c => c.Filter, new VariableFilter { DataFrom = new DateOnly(2015, 1, 1) })
                .Add(c => c.FilterChanged, f => reported = f));

        DateInputs(cut)[1].Change("2017-01-01");

        Assert.Equal(new DateOnly(2017, 1, 1), reported!.DataTo);
    }

    [Fact]
    public void Filter_WhenBothNewFiltersAreShared_ThenTheyComeBackSetAndShownInTheControls()
    {
        // Not here to find a parsing bug — VariableFilterTest already covers the round trip. It is
        // here to catch controls drawn but bound to local state instead of to _filter, which looks
        // correct on screen and is lost on every shared link.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var set = new VariableFilter
        {
            Categories = ["ehds-cat:population-health-surveys"],
            DataFrom = new DateOnly(2015, 3, 4),
            DataTo = new DateOnly(2020, 12, 31)
        };

        var parsed = VariableFilter.Parse(set.ToQueryString());

        Assert.Equal(set, parsed);

        var cut = RenderWith(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE")),
                                FacetsWith(TwoCategories, range), vocabulary: CategoryWords()),
            b => b.Add(c => c.Filter, parsed));

        var inputs = DateInputs(cut);

        Assert.Equal("2015-03-04", inputs[0].GetAttribute("value"));
        Assert.Equal("2020-12-31", inputs[1].GetAttribute("value"));
        Assert.True(FacetChosen(cut, "Befolkningsundersøkelser"));
    }

    [Fact]
    public void Filter_WhenBothNewFacetsAreDrawn_ThenEveryClassNameTheyEmitIsStyled()
    {
        // The sample hosts style these names themselves, so opening one in a browser looks correct
        // whatever the name is. This guard is the only thing that sees an unstyled name on a
        // Stiler-only host such as helsedata.no.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var cut = RenderWith(new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")),
            FacetsWith(TwoCategories, range), vocabulary: CategoryWords()));

        Assert.Equal(2, DateInputs(cut).Count);
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Filter_WhenBothNewFacetsAreDrawn_ThenTheySitWhereTheOrderingCommentSaysTheyDo()
    {
        // Placement is a decision here, not an accident of where the list was appended to.
        var range = new DateInterval
        {
            Min = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Max = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var facets = FacetsWith(TwoCategories, range) with
        {
            // Present only so the slot AFTER dataperiode exists to be measured against.
            HelsefagligKodeverk = [new() { ShortName = "ICD-10", FullName = "ICD-10", Count = 5 }]
        };
        var cut = RenderWith(new FilteringClient(
            OnePage(Variable("1. Tale", "KODE")), facets, vocabulary: CategoryWords()));

        var headings = cut.FindAll(".munin-explorer-filters summary").Select(s => s.TextContent).ToList();

        // Datakategori third: the two above it are in helsedata's own order and were not moved.
        Assert.Equal(2, headings.FindIndex(h => h.StartsWith("Datakategori", StringComparison.Ordinal)));

        // Dataperiode in Runa's slot — after Datatype, before Helsefaglig kodeverk.
        var period = headings.FindIndex(h => h.StartsWith("Dataperiode", StringComparison.Ordinal));

        Assert.True(period > headings.FindIndex(h => h.StartsWith("Datatype", StringComparison.Ordinal)));
        Assert.True(period < headings.FindIndex(h => h.StartsWith("Helsefaglig", StringComparison.Ordinal)));
    }

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
        Assert.DoesNotContain("SentraltHelseregister", cut.Find(".munin-explorer-filters").TextContent);
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
    public void Filter_WhenAValueIsChosen_ThenItsCheckboxIsTicked()
    {
        // The checkbox's own `checked` is the whole state, which is why aria-pressed went with the
        // buttons: two mechanisms saying the same thing can disagree. (Fhi.Metadata-j0a2h)
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.False(FacetChosen(cut, "Dødsårsaksregisteret"));

        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.True(FacetChosen(cut, "Dødsårsaksregisteret"));
    }

    [Fact]
    public void Filter_WhenASecondKildetypeIsChosen_ThenItReplacesTheFirstRatherThanJoiningIt()
    {
        // The API takes one kildetype, not a list. Two ticked boxes would promise a filter it
        // cannot express.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Sentralt helseregister");
        ClickFacet(cut, "Biobank");

        Assert.Equal("biobank", client.SearchFilter?.KildeType);
        Assert.False(FacetChosen(cut, "Sentralt helseregister"));
    }

    [Fact]
    public void Filter_WhenTheChosenKildetypeIsChosenAgain_ThenItIsCleared()
    {
        // There is no "any kildetype" value to go back to, so unticking the chosen one has to be
        // the way out — which is also what its own checkbox promises.
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

        cut.FindAll("div.munin-explorer-pagination button")[1].Click(); // Neste
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Equal(1, client.LastPage);
    }

    [Fact]
    public void Filter_WhenTheFetchFails_ThenTheSelectionIsRolledBackToWhatTheRowsCameFrom()
    {
        // Same invariant the sort rollback protects: the rows on screen are still the old ones, so
        // the boxes have to keep saying so rather than claiming a filter that never arrived.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        client.FailSearch = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.False(FacetChosen(cut, "Dødsårsaksregisteret"));
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
        Assert.True(FacetChosen(cut, "Dødsårsaksregisteret"));
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
        Assert.Single(cut.FindAll("ul.munin-explorer-data-list > li"));
    }

    [Fact]
    public void Render_WhenTheFacetRefreshIsRateLimited_ThenThePanelSaysSoRatherThanThatTheCountsAreStale()
    {
        // The facet refresh goes out alongside every search, so a throttled reader meets this panel
        // and the result list in the same render. Left on the generic sentence, this region would
        // be advising the reader to try again while the alert above it says trying again is the
        // problem — two answers to one event, in one screenful.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        client.RateLimitFacets = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        var alert = cut.Find("[role='alert']");

        Assert.Contains("for mange forespørsler", alert.TextContent);
        Assert.DoesNotContain("Tallene kan være utdaterte", alert.TextContent);

        // The panel and the rows both stay, exactly as they do for the generic failure: it is the
        // numbers that may now be wrong, not the controls or the list.
        Assert.NotNull(Facet(cut, "Dødsårsaksregisteret"));
        Assert.Single(cut.FindAll("ul.munin-explorer-data-list > li"));
    }

    [Fact]
    public void Render_WhenTheFacetsHaveNeverArrived_ThenNoEmptyFilterPanelIsDrawn()
    {
        // The first search failed, so the facets were never asked for. A legend and a dead clear
        // button over nothing is furniture.
        var cut = RenderWith(new FailingClient());

        Assert.Empty(cut.FindAll(".munin-explorer-filters"));
    }

    [Fact]
    public void Render_WhenAFacetHasNoValues_ThenItIsLeftOutRatherThanDrawnEmpty()
    {
        // Except variabelgruppe, where the emptiness is the message: with no kilde chosen the API
        // answers with a curated shortlist, and an empty list would otherwise read as a broken one.
        var cut = RenderWith(new FilteringClient(OnePage(), new FilterOptions()));

        var panel = cut.Find(".munin-explorer-filters").TextContent;

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
    public void Render_WhenNothingMatchesForAnEnglishReader_ThenTheEmptyStateIsEnglishToo()
    {
        // NoResults is a delegate, so the parity test cannot compare it without arguments — and
        // unlike the other two delegates nothing asserted it where it is rendered either, which
        // left the English empty state as the one user-facing string in the package with no
        // guard at all. A completely routine path: an English reader whose search matches nothing.
        var cut = RenderWith(new FakeClient(OnePage()), b => b.Add(c => c.Language, "en"));

        Assert.Equal(
            "No variables matched your search. Historical variables are not included – "
            + "turn on “Show historical” to see expired ones.",
            StatusLine(cut));
    }

    [Fact]
    public void Filter_WhenNothingMatchesForAnEnglishReader_ThenBothClausesAreEnglishToo()
    {
        // The search and filter clauses are built separately from the sentence around them, so
        // either could stay Norwegian on its own.
        var cut = RenderWith(
            new FilteringClient(OnePage()),
            b => b.Add(c => c.Language, "en")
                  .Add(c => c.Search, "svelging")
                  .Add(c => c.Filter, new VariableFilter { KildeIds = [Dodsarsak] }));

        Assert.Equal(
            "No variables matched your search for \u201csvelging\u201d with the filters you have chosen. "
            + "Historical variables are not included \u2013 turn on \u201cShow historical\u201d to see expired ones.",
            StatusLine(cut));
    }

    [Fact]
    public void Render_WhenNothingMatchesAndHistoricalVariablesAreHidden_ThenTheEmptyStateOffersTheToggle()
    {
        // The toggle is off by default and sits inside a collapsed facet group, so a reader whose
        // variable exists only historically is told "no hits" by a filter they never set and
        // cannot see. Without this sentence they conclude the catalogue does not have it.
        var cut = RenderWith(new FakeClient(OnePage()), b => b.Add(c => c.Search, "svelging"));

        Assert.Contains("Vis historiske", StatusLine(cut));
    }

    [Fact]
    public void Render_WhenNothingMatchesWhileHistoricalVariablesAreShown_ThenTheEmptyStateIsSilentAboutThem()
    {
        // The trap. An empty state that always names the toggle passes the test above and tells a
        // reader who already turned it on to turn it on \u2014 a hint that is not merely useless but
        // points at the one explanation that has been ruled out.
        var cut = RenderWith(
            new FakeClient(OnePage()),
            b => b.Add(c => c.Search, "svelging")
                  .Add(c => c.Filter, new VariableFilter { IncludeHistorical = true }));

        Assert.DoesNotContain("Vis historiske", StatusLine(cut));
    }

    [Fact]
    public void Render_WhenTheReaderTurnsHistoricalOnWithNothingMatching_ThenTheHintGoesAway()
    {
        // Through the control rather than the parameter: the sentence reads off _filter, which the
        // toggle rewrites, and a hint computed once at render would survive the click that answers it.
        var cut = RenderWith(new FakeClient(OnePage()), b => b.Add(c => c.Search, "svelging"));
        Assert.Contains("Vis historiske", StatusLine(cut));

        ClickFacet(cut, "Vis historiske");

        Assert.DoesNotContain("Vis historiske", StatusLine(cut));
    }

    [Fact]
    public void Render_WhenHistoricalVariablesAreShownForAnEnglishReader_ThenTheEmptyStateIsSilentAboutThem()
    {
        // Both halves of the fix exist in two languages, and the English one is the half that is
        // routinely left behind \u2014 the toggle-on case doubly so, since it asserts an absence.
        var cut = RenderWith(
            new FakeClient(OnePage()),
            b => b.Add(c => c.Language, "en")
                  .Add(c => c.Filter, new VariableFilter { IncludeHistorical = true }));

        Assert.Equal(
            "No variables matched your search with the filters you have chosen.", StatusLine(cut));
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

        cut.FindAll("div.munin-explorer-pagination button")[1].Click(); // Neste
        ClickSort(cut, "Kilde");

        Assert.Equal(1, client.FacetCalls);
    }

    [Fact]
    public void Search_WhenANewSearchRuns_ThenTheCountsAreRefetchedForIt()
    {
        // The counts are cross-filtered against the search as well as the facets, so a new search
        // moves every number in the panel.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        cut.Find(".searchbox__freetext").Change("svelging");
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
    public void Filter_WhenHarKildekodeverkIsTickedTwice_ThenItStopsFilteringRatherThanAskingForNo()
    {
        // Two states, not three, and the difference is invisible on screen: the obvious-looking
        // negation ticks and unticks the box exactly the same way while sending
        // harKildekodeverk=false, which inverts the result set to the variables *without* one.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Har kildekodeverk");

        Assert.True(client.SearchFilter?.HasKildekodeverk);
        Assert.True(FacetChosen(cut, "Har kildekodeverk"));

        ClickFacet(cut, "Har kildekodeverk");

        Assert.Null(client.SearchFilter?.HasKildekodeverk);
        Assert.DoesNotContain("harKildekodeverk", client.SearchFilter!.ToQueryString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_WhenHistoricalIsChosen_ThenItGoesOnTheWireRatherThanOnlyOnTheBox()
    {
        // The one filter whose parameter is left out at its default, so a flip in the wrong
        // direction produces a URL that looks unfiltered rather than one that looks wrong.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Vis historiske");

        Assert.True(client.SearchFilter?.IncludeHistorical);
        Assert.Contains("includeHistorical=true", client.SearchFilter!.ToQueryString(), StringComparison.Ordinal);
        Assert.True(FacetChosen(cut, "Vis historiske"));
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

        // And still a filter, not just text: a root that cannot be ticked is the same loss.
        ClickFacet(cut, "Første besøk");
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

        Assert.Equal(1, Drawn(cut, "GruppeA"));
        Assert.Equal(1, Drawn(cut, "GruppeB"));

        // And a filter rather than a label: the nested one is the one a duplicate would double.
        ClickFacet(cut, "GruppeB");
        Assert.Equal([Levekaar], client.SearchFilter?.VariabelgruppeIds);

        static int Drawn(IRenderedComponent<VariableExplorer> cut, string label) =>
            FacetControls(cut).Count(b => b.TextContent.StartsWith(label, StringComparison.Ordinal));
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
        // Every shape here is one a host stylesheet already dresses, so none of them needs a class
        // name invented for it — and a name the host has never heard of renders as nothing.
        // (Fhi.Metadata-j0a2h)
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        var panel = cut.Find(".munin-explorer-filters");

        Assert.NotEmpty(panel.QuerySelectorAll("details > summary"));
        Assert.NotEmpty(panel.QuerySelectorAll("ul li > label > input[type=checkbox]"));
        Assert.All(panel.QuerySelectorAll("details"), d => Assert.False(d.HasAttribute("class")));
        Assert.All(panel.QuerySelectorAll("ul"), u => Assert.False(u.HasAttribute("class")));
        Assert.All(panel.QuerySelectorAll("li > label"), l => Assert.False(l.HasAttribute("class")));
        Assert.All(panel.QuerySelectorAll("li > label > input"), i => Assert.False(i.HasAttribute("class")));

        // The toolbar is still buttons, and still Stiler's own square one.
        Assert.All(panel.QuerySelectorAll("button"), b => Assert.Contains("hd-button-square", b.ClassName!));
    }

    [Fact]
    public void Render_Always_ThenNoFacetValueCarriesAriaPressed()
    {
        // A checkbox holds its state in `checked`, so an aria-pressed beside it would be a second
        // mechanism that can disagree with the first. (Fhi.Metadata-j0a2h)
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        ClickFacet(cut, "Dødsårsaksregisteret");

        var panel = cut.Find(".munin-explorer-filters");

        Assert.Empty(panel.QuerySelectorAll("li [aria-pressed]"));
        Assert.NotEmpty(panel.QuerySelectorAll("li > label > input[type=checkbox][checked]"));
    }

    [Fact]
    public void Render_Always_ThenAFacetValuesAccessibleNameHoldsItsCountAndItsLabelNamesTheBox()
    {
        // The label wraps the input, so the count is inside the accessible name rather than beside
        // it — "Dødsårsaksregisteret (30)" is announced whole. A count in a sibling element would be
        // read as a stray number or skipped, and an unwrapped input would have no name at all.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        var label = Facet(cut, "Dødsårsaksregisteret");

        Assert.Equal("label", label.LocalName);
        Assert.Equal("Dødsårsaksregisteret (30)", label.TextContent);
        Assert.NotNull(label.QuerySelector("input[type=checkbox]"));
    }

    [Fact]
    public void Filter_WhenTheFacetsRefreshUnderIt_ThenTheTickedValueIsStillTicked()
    {
        // The counts move on every refresh, so the values reorder and the ticked one is redrawn
        // somewhere else in the list. The tick lives on the filter rather than in the DOM, and the
        // keys are what stop the renderer patching it onto whichever value took its place.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderWith(client);

        ClickFacet(cut, "Dødsårsaksregisteret");
        Assert.True(FacetChosen(cut, "Dødsårsaksregisteret"));

        // A second facet refresh, from a change that leaves the first choice alone.
        ClickFacet(cut, "Vis historiske");

        Assert.True(FacetChosen(cut, "Dødsårsaksregisteret"));
        Assert.Equal([Dodsarsak], client.SearchFilter?.KildeIds);
    }

    // ---------------------------------------------------------------------------------
    // The hierarchy trail over the results. What has to hold is what the panel alone cannot
    // do: say WHERE in kilde → delkilde → datasamling → variabelgruppe the selection stands,
    // and let a reader step back up it. The panel holds the same selection as pressed buttons
    // inside collapsed disclosures, so without the trail a kilde chosen three levels down is
    // visible only as the result count changing.
    // ---------------------------------------------------------------------------------

    private static readonly Guid Inklusjon2 = new("cccccccc-0000-0000-0000-000000000003");

    private static IReadOnlyList<IElement> Crumbs(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".munin-explorer-breadcrumb ol li button");

    private static IElement Crumb(IRenderedComponent<VariableExplorer> cut, string label) =>
        Crumbs(cut).Single(b => b.TextContent.StartsWith(label, StringComparison.Ordinal));

    /// <summary>
    /// The lang a step's text is announced in, or null where the step carries no marking at all.
    /// </summary>
    /// <remarks>
    /// It reads the span inside the button rather than the button itself, which is the whole point:
    /// the button owns the aria-label, and that label is this component's prose in the UI's
    /// language, so a lang on the button would announce an English name in a Norwegian voice.
    /// Anything langed above the text would be the bug, so the button is checked for one too.
    /// </remarks>
    private static string? CrumbLang(IElement step)
    {
        Assert.False(step.HasAttribute("lang"));
        return step.QuerySelector("[lang]")?.GetAttribute("lang");
    }

    /// <summary>The component rendered with a hierarchy already chosen, as a shared link would.</summary>
    private IRenderedComponent<VariableExplorer> RenderFiltered(
        FilteringClient client, VariableFilter filter) =>
        RenderWith(client, b => b.Add(c => c.Filter, filter));

    [Fact]
    public void Render_WhenNothingInTheHierarchyIsChosen_ThenNoTrailIsDrawn()
    {
        // There is no trail to a selection nobody has made, and a permanent empty one would be the
        // same furniture as "Side 1 av 1" between two dead pager buttons.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Empty(cut.FindAll(".munin-explorer-breadcrumb"));
    }

    [Fact]
    public void Render_WhenOnlyADatatypeIsChosen_ThenStillNoTrailIsDrawn()
    {
        // The trail is the four hierarchy levels, not "any filter". A datatype is not a place in
        // the catalogue, so it has no step and cannot summon a trail on its own.
        var cut = RenderFiltered(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                                 new VariableFilter { DataTypes = ["1"] });

        Assert.Empty(cut.FindAll(".munin-explorer-breadcrumb"));
    }

    [Fact]
    public void Render_WhenTheHierarchyIsChosen_ThenThereIsAStepPerChosenLevelInOrder()
    {
        // Outermost first, and only the levels that have a selection: a kilde and a variabelgruppe
        // with no delkilde or datasamling between them is an ordinary thing to pick, and a step
        // saying nothing would be worse than a shorter path.
        var cut = RenderFiltered(
            new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
            new VariableFilter
            {
                KildeIds = [Tromso],
                DelkildeIds = [Tromso4],
                VariabelgruppeIds = [Bakgrunn]
            });

        Assert.Equal(["Tromsøundersøkelsen", "Tromsø 4", "Bakgrunn"],
                     Crumbs(cut).Select(b => b.TextContent));
    }

    [Fact]
    public void Render_WhenALevelHasSeveralValues_ThenTheStepNamesTheFirstAndCountsTheRest()
    {
        // Runa's rule. The alternative is a step whose width grows with the selection; the number
        // is the part a reader needs, and the names are all still in the panel one press away.
        var cut = RenderFiltered(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                                 new VariableFilter { KildeIds = [Dodsarsak, Tromso] });

        Assert.Equal("Dødsårsaksregisteret (+1)", Crumbs(cut).Single().TextContent);
    }

    [Fact]
    public void Render_WhenADatasamlingIsChosen_ThenItsNameComesFromTheRowsItLeft()
    {
        // Nothing in FilterOptions offers datasamlinger as a facet, so the rows are the only place
        // the name can come from — and every row a datasamling filter leaves belongs to it.
        var row = Variable("1. Tale", "KODE") with { DatasamlingId = Inklusjon2, DatasamlingName = "Inklusjon" };

        var cut = RenderFiltered(new FilteringClient(OnePage(row)),
                                 new VariableFilter { DatasamlingIds = [Inklusjon2] });

        Assert.Equal("Inklusjon", Crumbs(cut).Single().TextContent);
    }

    [Fact]
    public void Render_WhenTheFacetsDoNotCarryTheChosenKilde_ThenItsNameComesFromTheRowsItLeft()
    {
        // The facets are the first source but they can miss: they are cross-filtered, so a value
        // the reader selected can be absent from the payload describing what that selection
        // leaves, and a failed refresh leaves them missing altogether while the rows stay on
        // screen. Every row a kilde filter leaves belongs to that kilde and names it.
        var row = Variable("1. Tale", "KODE") with { KildeId = Tromso, KildeName = "Tromsøundersøkelsen" };
        var client = new FilteringClient(OnePage(row)) { FailFacets = true };

        var cut = RenderFiltered(client, new VariableFilter { KildeIds = [Tromso] });

        var crumb = Crumbs(cut).Single();

        Assert.Equal("Tromsøundersøkelsen", crumb.TextContent);
        // A name out of the catalogue whichever source supplied it, so it is still Norwegian.
        Assert.Equal("no", CrumbLang(crumb));
    }

    [Fact]
    public void Render_WhenTheFacetsDoNotCarryTheChosenVariabelgruppe_ThenItsNameComesFromTheRows()
    {
        // The variabelgruppe falls back the same way the kilde does, and off its own pair of row
        // fields — a step reading "Variabelgruppe" where the rows knew the name would be the
        // trail's most prominent levels degrading with the suite still green.
        var row = Variable("1. Tale", "KODE") with
        {
            VariabelgruppeId = Bakgrunn,
            VariabelgruppeName = "Bakgrunn"
        };
        var client = new FilteringClient(OnePage(row)) { FailFacets = true };

        var cut = RenderFiltered(client, new VariableFilter { VariabelgruppeIds = [Bakgrunn] });

        var crumb = Crumbs(cut).Single();

        Assert.Equal("Bakgrunn", crumb.TextContent);
        Assert.Equal("no", CrumbLang(crumb));
    }

    [Fact]
    public void Render_WhenNothingOnScreenKnowsAValuesName_ThenTheStepFallsBackToTheLevelsOwnWord()
    {
        // A guid in a trail is not information, and the step still has to be pressable to clear
        // what is under it — so an unknown value is drawn as its level rather than dropped.
        var cut = RenderFiltered(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                                 new VariableFilter { DatasamlingIds = [Inklusjon2] });

        var crumb = Crumbs(cut).Single();

        Assert.Equal("Datasamling", crumb.TextContent);
        // Our prose rather than a name out of the catalogue, so it must not be announced as
        // Norwegian — the same rule the variable panel's kildetype step follows. Nothing inside
        // the step is langed either: the marking lives on a span around catalogue words alone.
        Assert.Null(CrumbLang(crumb));
    }

    [Fact]
    public void Breadcrumb_WhenAStepIsPressed_ThenEveryLevelUnderItIsCleared()
    {
        // The acceptance criterion: a step removes what is below it and keeps itself.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderFiltered(client, new VariableFilter
        {
            KildeIds = [Tromso],
            DelkildeIds = [Tromso4],
            DatasamlingIds = [Inklusjon2],
            VariabelgruppeIds = [Bakgrunn]
        });

        Crumb(cut, "Tromsø 4").Click();

        Assert.Equal([Tromso], client.SearchFilter?.KildeIds);
        Assert.Equal([Tromso4], client.SearchFilter?.DelkildeIds);
        Assert.Empty(client.SearchFilter!.DatasamlingIds);
        Assert.Empty(client.SearchFilter!.VariabelgruppeIds);

        // And the trail itself shortens to the step that was pressed, which is the re-render the
        // press exists to produce: two steps go from the end, and the step left last takes on
        // being current — which is what drops its aria-label, since pressing it now does nothing.
        // Not a guard on SetKey(crumb.Level), which nothing here can see: a press only ever drops
        // steps off the end, which positional patching lands on identically. What the key is for
        // is a level appearing in the middle, from the panel rather than from here.
        Assert.Equal(["Tromsøundersøkelsen", "Tromsø 4"], Crumbs(cut).Select(b => b.TextContent));
        Assert.Equal("true", Crumbs(cut)[^1].GetAttribute("aria-current"));
        Assert.False(Crumbs(cut)[^1].HasAttribute("aria-label"));
    }

    [Fact]
    public void Breadcrumb_WhenAStepIsPressed_ThenTheFiltersThatAreNotPartOfThePathSurvive()
    {
        // A datatype is not a place in the catalogue. Taking it out because the reader stepped
        // back up the hierarchy would be a filter disappearing with no control having said so.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderFiltered(client, new VariableFilter
        {
            KildeIds = [Tromso],
            VariabelgruppeIds = [Bakgrunn],
            DataTypes = ["1"],
            IncludeHistorical = true
        });

        Crumb(cut, "Tromsøundersøkelsen").Click();

        Assert.Empty(client.SearchFilter!.VariabelgruppeIds);
        Assert.Equal(["1"], client.SearchFilter?.DataTypes);
        Assert.True(client.SearchFilter?.IncludeHistorical);
    }

    [Fact]
    public void Breadcrumb_WhenTheClearIsPressed_ThenTheWholeHierarchyGoesAndNothingElseDoes()
    {
        // The other half of the acceptance criterion, and the difference between this and
        // "Fjern alle filtre": the reader who narrowed deep into one kilde keeps the datatype,
        // the kodeverk and the dates they also chose.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderFiltered(client, new VariableFilter
        {
            KildeIds = [Tromso],
            DelkildeIds = [Tromso4],
            VariabelgruppeIds = [Bakgrunn],
            DataTypes = ["1"],
            KildeType = "biobank"
        });

        cut.Find(".munin-explorer-breadcrumb__clear").Click();

        Assert.Empty(client.SearchFilter!.KildeIds);
        Assert.Empty(client.SearchFilter!.DelkildeIds);
        Assert.Empty(client.SearchFilter!.VariabelgruppeIds);
        Assert.Equal(["1"], client.SearchFilter?.DataTypes);
        // Kildetype is a facet of its own rather than a step on the way to a kilde, so it is not
        // part of what the trail owns and does not go with it.
        Assert.Equal("biobank", client.SearchFilter?.KildeType);
    }

    [Fact]
    public void Breadcrumb_WhenTheClearIsPressed_ThenTheTrailItselfIsGone()
    {
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderFiltered(client, new VariableFilter { KildeIds = [Tromso] });

        cut.Find(".munin-explorer-breadcrumb__clear").Click();

        Assert.Empty(cut.FindAll(".munin-explorer-breadcrumb"));
    }

    [Fact]
    public void Breadcrumb_WhenTheDeepestStepIsPressed_ThenNothingIsFetched()
    {
        // It has nothing under it to clear, so ApplyFilterAsync is handed the filter already in
        // force and returns without a request. That is what makes it safe for it to stay a button.
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));
        var cut = RenderFiltered(client, new VariableFilter { KildeIds = [Tromso], DelkildeIds = [Tromso4] });

        Crumb(cut, "Tromsø 4").Click();

        Assert.Equal(1, client.SearchCalls); // the initial load, and nothing since
    }

    [Fact]
    public void Render_WhenTheTrailIsDrawn_ThenEveryStepIsAButtonAndTheDeepestSaysItIsWhereYouAre()
    {
        // Every step is a button, including the last. Pressing a step makes it the last step, so a
        // last step drawn as plain text would take the control the reader just pressed out of the
        // document and drop focus to <body> — the failure the pager's aria-disabled and the
        // ever-present clear button both exist to avoid.
        var cut = RenderFiltered(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                                 new VariableFilter { KildeIds = [Tromso], DelkildeIds = [Tromso4] });

        var steps = Crumbs(cut);

        Assert.All(steps, b => Assert.Equal("button", b.GetAttribute("type")));
        Assert.False(steps[0].HasAttribute("aria-current"));
        Assert.Equal("true", steps[1].GetAttribute("aria-current"));

        // What pressing it does, which the name on its own does not say — and starting with the
        // visible text, so speech input can still reach it (WCAG 2.5.3). The step you are already
        // on has none, because pressing it does nothing.
        Assert.Equal("Tromsøundersøkelsen – fjern nivåene under", steps[0].GetAttribute("aria-label"));
        Assert.False(steps[1].HasAttribute("aria-label"));
    }

    [Fact]
    public void Render_WhenTheTrailIsDrawn_ThenItIsBuiltFromShapesRatherThanFromNewClassNames()
    {
        // Stiler has no breadcrumb rule that can be read back off its compiled stylesheet, so the
        // trail is an <ol> of buttons and the chevrons are a host's to draw. The two names it does
        // wear are handles, and the steps reuse the panel's own crumb name rather than minting a
        // second one for the same affordance.
        var cut = RenderFiltered(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                                 new VariableFilter { KildeIds = [Tromso] });

        var trail = cut.Find(".munin-explorer-breadcrumb");

        // The span the Norwegian marking hangs on is invisible to a stylesheet too — it exists for
        // the synthesiser, not for a rule.
        Assert.All(trail.QuerySelectorAll("ol, li, span"), e => Assert.False(e.HasAttribute("class")));
        Assert.All(Crumbs(cut), b => Assert.Equal("hd-button-reset munin-explorer-crumb", b.ClassName));

        // The list carries the name rather than a <nav> landmark: two explorers on one page would
        // otherwise put two identically named navs in the landmark list with nothing to tell them
        // apart, which the search form avoids by naming itself after its own instance's title.
        Assert.Equal("Valgt hierarki", trail.QuerySelector("ol")!.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("nav"));

        // The × is decoration, so the control that empties the hierarchy says what it does in its
        // accessible name instead — and it sits outside the list, so "list, 1 item" is not counting
        // the button that empties it.
        var clear = cut.Find(".munin-explorer-breadcrumb__clear");

        Assert.Equal("Fjern hierarkifilteret", clear.GetAttribute("aria-label"));
        Assert.Null(clear.Closest("ol"));
    }

    [Fact]
    public void Render_WhenTheLanguageIsEn_ThenTheTrailIsEnglishAndTheCatalogueStaysNorwegian()
    {
        // The same split the panel's kilde trail makes: the names are Munin's whatever the UI is,
        // and only the prose around them follows Language.
        var cut = RenderWith(new FilteringClient(OnePage(Variable("1. Tale", "KODE"))),
                             b => b.Add(c => c.Language, "en")
                                   .Add(c => c.Filter, new VariableFilter
                                   {
                                       KildeIds = [Tromso],
                                       DatasamlingIds = [Inklusjon2]
                                   }));

        var steps = Crumbs(cut);

        Assert.Equal("Tromsøundersøkelsen", steps[0].TextContent);
        // The lang wraps the catalogue's words only. It cannot go on the button, because the
        // button carries the aria-label below — English prose, which a Norwegian voice would
        // mangle, and which is exactly the mistake the period column's remarks name.
        Assert.Equal("no", CrumbLang(steps[0]));
        Assert.Equal("Tromsøundersøkelsen – remove the levels below", steps[0].GetAttribute("aria-label"));

        // The unknown datasamling falls back to the level's own word, which is ours and therefore
        // English here — and carries no lang for exactly that reason.
        Assert.Equal("Data collection", steps[1].TextContent);
        Assert.Null(CrumbLang(steps[1]));

        Assert.Equal("Selected hierarchy",
                     cut.Find(".munin-explorer-breadcrumb ol").GetAttribute("aria-label"));
        Assert.Equal("Clear the hierarchy filter",
                     cut.Find(".munin-explorer-breadcrumb__clear").GetAttribute("aria-label"));
    }

    [Fact]
    public void Breadcrumb_WhenAStepIsPressed_ThenTheHostIsToldWhatIsNowInForce()
    {
        // The trail changes the filter, so it owes the host the same callback every facet button
        // does — otherwise a shared link keeps a hierarchy the page has already stepped out of.
        VariableFilter? reported = null;
        var client = new FilteringClient(OnePage(Variable("1. Tale", "KODE")));

        var cut = RenderWith(client, b => b
            .Add(c => c.Filter, new VariableFilter { KildeIds = [Tromso], DelkildeIds = [Tromso4] })
            .Add(c => c.FilterChanged, f => reported = f));

        Crumb(cut, "Tromsøundersøkelsen").Click();

        Assert.Equal([Tromso], reported?.KildeIds);
        Assert.Empty(reported!.DelkildeIds);
    }

    // ---------------------------------------------------------------------------------
    // The detail panel. The one thing that has to hold is the acceptance criterion itself:
    // selecting a variable shows its detail without a page navigation. Everything else here
    // follows from the panel living inside the row — the selection can only ever be a row on
    // screen, and the host is told which one so a reader's place can be linked to.
    // ---------------------------------------------------------------------------------

    private static readonly Guid TaleId = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid SpyttId = new("dddddddd-0000-0000-0000-000000000002");

    // The two owners every detail below names, so a variable panel always has both buttons under
    // it and the fetches they start can be told apart by id.
    private static readonly Guid AlsId = new("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid InklusjonId = new("eeeeeeee-0000-0000-0000-000000000002");

    /// <summary>A row with an id a test can hand back to the component as a selection.</summary>
    private static VariableSummary Row(Guid id, string name, string? description = null) => new()
    {
        Id = id,
        Code = $"V_ALS.F1.{name}",
        PreferredTerm = name,
        KildeName = "Als registeret",
        DatasamlingName = "Inklusjon",
        Description = description
    };

    // ---------------------------------------------------------------------------------
    // The catalogue's own properties. Nothing about them is known to this component: the
    // keys, their labels, their order and the vocabularies their coded values come from
    // all arrive with the payload. What has to hold is that a code is never shown where a
    // word exists, and that a key the catalogue no longer curates is not drawn under its
    // raw name.

    /// <summary>A detail carrying curated properties, as the API sends them.</summary>
    private static VariableDetail WithProperties(Guid id) => Detail(id) with
    {
        AdditionalProperties = new Dictionary<string, string?>
        {
            ["Opprinnelse"] = "5",
            ["Kommentar"] = "Variabelen er gyldig fra 2019 og fremover.",
            ["DatabaseReferanse"] = "ALSFRSR1Tale",
            ["FlerkodetFelt"] = "1",
        },
        PropertyMetadata =
        [
            new()
            {
                Key = "Kommentar",
                SortOrder = 50,
                Type = "Text",
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Kommentar", ["en"] = "Comment" },
            },
            new()
            {
                Key = "Opprinnelse",
                SortOrder = 30,
                Type = "SingleSelect",
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Opprinnelse", ["en"] = "Origin" },
                OptionsJson = """
                    [{"value":"1","label":"Beregnet","labelEn":"Calculated"},
                     {"value":"5","label":"Direkte fra skjema","labelEn":"Directly from the form"}]
                    """,
            },
            new()
            {
                Key = "DatabaseReferanse",
                SortOrder = 200,
                Type = "Text",
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Databasereferanse" },
            },
        ],
    };

    [Fact]
    public void Properties_WhenTheDetailCarriesThem_ThenTheyAreShownInTheCataloguesOrder()
    {
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)));

        Toggles(cut)[0].Click();

        var group = cut.Find(".munin-explorer-group ~ dl:last-of-type");

        // SortOrder, not the order the bag happens to enumerate in.
        Assert.Equal(["Opprinnelse", "Kommentar", "Databasereferanse"],
                     group.QuerySelectorAll("dt").Select(d => d.TextContent));
    }

    [Fact]
    public void Properties_WhenAValueIsCoded_ThenTheVocabularyIsUsedRatherThanTheCode()
    {
        // "Opprinnelse: 5" is the API's honest answer and a useless thing to read. The vocabulary
        // arrives in the same payload precisely so a consumer never has to keep its own copy.
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)));

        Toggles(cut)[0].Click();

        Assert.Contains("Direkte fra skjema", Panel(cut).TextContent);
        Assert.DoesNotContain("Opprinnelse: 5", Panel(cut).TextContent);
    }

    [Fact]
    public void Properties_WhenTheLanguageIsEn_ThenBothTheLabelAndTheCodedValueFollowIt()
    {
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)),
                             b => b.Add(c => c.Language, "en"));

        Toggles(cut)[0].Click();

        Assert.Contains("Origin", Panel(cut).TextContent);
        Assert.Contains("Directly from the form", Panel(cut).TextContent);
    }

    [Fact]
    public void Properties_WhenALabelHasNoEnglish_ThenTheNorwegianStandsIn()
    {
        // Curation is uneven. A field with no English label is better shown in Norwegian than
        // dropped from an English page, which would silently show a reader less than a colleague.
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)),
                             b => b.Add(c => c.Language, "en"));

        Toggles(cut)[0].Click();

        Assert.Contains("Databasereferanse", Panel(cut).TextContent);
    }

    [Fact]
    public void Properties_WhenTheCatalogueDoesNotCurateAKey_ThenItIsNotDrawn()
    {
        // The bag can carry keys the catalogue has stopped describing. "FlerkodetFelt: 1" under its
        // raw name tells a reader nothing and looks like a bug.
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)));

        Toggles(cut)[0].Click();

        Assert.DoesNotContain("FlerkodetFelt", Panel(cut).TextContent);
    }

    /// <summary>The properties group's rows, as label/value element pairs.</summary>
    private static (IElement Label, IElement Value)[] PropertyPairs(IRenderedComponent<VariableExplorer> cut)
    {
        var list = cut.Find(".munin-explorer-group ~ dl:last-of-type");

        return [.. list.QuerySelectorAll("div").Select(d => (d.QuerySelector("dt")!, d.QuerySelector("dd")!))];
    }

    [Fact]
    public void Properties_WhenAnEnglishPageFallsBackToNorwegian_ThenThatTextIsMarkedAsNorwegian()
    {
        // An English reader gets some Norwegian either way: labels nobody translated, and free text,
        // which the catalogue only ever stores once. Marking it is what lets a screen reader switch
        // voice instead of reading a Norwegian sentence with English phonetics.
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)),
                             b => b.Add(c => c.Language, "en"));

        Toggles(cut)[0].Click();

        var rows = PropertyPairs(cut);

        // Opprinnelse: label and coded value both exist in English, so both inherit the page.
        Assert.Equal("Origin", rows[0].Label.TextContent);
        Assert.Null(rows[0].Label.GetAttribute("lang"));
        Assert.Null(rows[0].Value.GetAttribute("lang"));

        // Kommentar: the label is translated, the free text underneath it never is.
        Assert.Equal("Comment", rows[1].Label.TextContent);
        Assert.Null(rows[1].Label.GetAttribute("lang"));
        Assert.Equal("no", rows[1].Value.GetAttribute("lang"));

        // Databasereferanse: no English label at all.
        Assert.Equal("no", rows[2].Label.GetAttribute("lang"));
    }

    [Fact]
    public void Properties_WhenThePageIsNorwegian_ThenNothingIsMarkedAtAll()
    {
        // Everything is already in the reader's language, so the attribute would say nothing. It
        // appears only where it carries information, which is what keeps it meaning something.
        var id = Guid.NewGuid();
        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(WithProperties(id)));

        Toggles(cut)[0].Click();

        Assert.All(PropertyPairs(cut), row =>
        {
            Assert.Null(row.Label.GetAttribute("lang"));
            Assert.Null(row.Value.GetAttribute("lang"));
        });
    }

    [Fact]
    public void Properties_WhenAVocabularyIsMalformed_ThenTheCodeIsShownAndThePanelSurvives()
    {
        // Curated data arriving over the wire. One bad definition should cost that field its label,
        // not take the panel down with it.
        var id = Guid.NewGuid();
        var detail = Detail(id) with
        {
            AdditionalProperties = new Dictionary<string, string?> { ["Opprinnelse"] = "5" },
            PropertyMetadata =
            [
                new()
                {
                    Key = "Opprinnelse",
                    SortOrder = 30,
                    Type = "SingleSelect",
                    DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Opprinnelse" },
                    OptionsJson = "{ not json at all",
                },
            ],
        };

        var cut = RenderWith(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(detail));

        Toggles(cut)[0].Click();

        Assert.Contains("Opprinnelse", Panel(cut).TextContent);
        Assert.Contains("5", Panel(cut).TextContent);
    }

    /// <summary>A detail payload shaped like the captured one, with every field the panel draws.</summary>
    private static VariableDetail Detail(Guid id, string name = "1. Tale") => new()
    {
        Id = id,
        Code = $"V_ALS.F1.{name}",
        PreferredTerm = name,
        Description = $"Angir pasientens grad av utfall på «{name}».",
        KildeId = AlsId,
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        KildeType = "nasjonaltMedisinskKvalitetsregister",
        DatasamlingId = InklusjonId,
        DatasamlingName = "Inklusjon",
        VariabelgruppeName = "Funksjonsscore",
        DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
        AllVariabelgrupper = [new() { Id = Bakgrunn, Name = "Funksjonsscore" }],
        KodeverkLinks =
        [
            new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true },
            new()
            {
                KodeverkType = "AdministrativtKodeverk",
                KodeverkReference = "2.16.578.1.12.4.1.1.7110",
                DisplayName = "ICD-10"
            }
        ]
    };

    /// <summary>Answers the search from one page and the detail endpoint from what it was given.</summary>
    private sealed class DetailClient(Page<VariableSummary> answer) : EmptyMuninExplorerClient
    {
        private readonly Dictionary<Guid, VariableDetail> _details = [];

        // One source per stalled detail fetch, none of them completed on their own, so a test can
        // keep a detail in flight while it opens another row — and can fault the first of two while
        // the second is still hanging, which is the only way to reach the reopened-panel guard.
        private readonly List<TaskCompletionSource<VariableDetail?>> _stalls = [];

        // What the searches after the current Answer are answered with, in order: a page to hand
        // back, or null to throw. Empty means every search is answered from Answer.
        private readonly Queue<Page<VariableSummary>?> _nextAnswers = new();

        /// <summary>The rows the next search answers with — settable, so a search can replace them.</summary>
        public Page<VariableSummary> Answer { get; set; } = answer;

        public int SearchCalls { get; private set; }

        /// <summary>How many detail fetches have been left hanging.</summary>
        public int Stalls => _stalls.Count;

        public int DetailCalls { get; private set; }
        public Guid LastDetailId { get; private set; }
        public bool LastIncludeHistorical { get; private set; }

        /// <summary>Fail every detail fetch from the next one on.</summary>
        public bool FailDetail { get; set; }

        /// <summary>Never answer a detail fetch from the next one on.</summary>
        public bool StallDetail { get; set; }

        public DetailClient Knows(VariableDetail detail)
        {
            _details[detail.Id] = detail;

            return this;
        }

        /// <summary>Answer one search after the current <see cref="Answer"/>; null fails it outright.</summary>
        public DetailClient Then(Page<VariableSummary>? next)
        {
            _nextAnswers.Enqueue(next);

            return this;
        }

        /// <summary>Answer the oldest detail fetch still hanging.</summary>
        public void AnswerStalled(VariableDetail detail) => Oldest().TrySetResult(detail);

        /// <summary>Fail the oldest detail fetch still hanging.</summary>
        public void FailStalled() => Oldest().TrySetException(new HttpRequestException("nede"));

        /// <summary>Refuse the oldest detail fetch still hanging with the API's 429.</summary>
        /// <remarks>
        /// The abandoned-fetch guard is written once per catch branch, so the 429 branch has its own
        /// copy of it — and a copy is exactly the thing that can be inverted or left out without any
        /// other test noticing.
        /// </remarks>
        public void RateLimitStalled() =>
            Oldest().TrySetException(new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)));

        private TaskCompletionSource<VariableDetail?> Oldest() =>
            _stalls.First(stall => !stall.Task.IsCompleted);

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;

            if (_nextAnswers.Count == 0)
            {
                return Task.FromResult(Answer);
            }

            return _nextAnswers.Dequeue() is { } next
                ? Task.FromResult(next)
                : throw new HttpRequestException("nede");
        }

        /// <summary>Refuse every detail fetch from the next one on with the API's 429.</summary>
        /// <remarks>
        /// Its own switch beside <see cref="FailDetail"/> rather than a mode of it, for the reason
        /// the separate <c>RateLimitedClient</c> exists: the point of the tests using it is that the
        /// panel tells the two failures apart, and one flag could not show that.
        /// </remarks>
        public bool RateLimitDetail { get; set; }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            LastDetailId = id;
            LastIncludeHistorical = includeHistorical;

            if (RateLimitDetail)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (FailDetail)
            {
                throw new HttpRequestException("nede");
            }

            if (StallDetail)
            {
                var stall = new TaskCompletionSource<VariableDetail?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _stalls.Add(stall);

                return stall.Task;
            }

            // Explicit, because Task.FromResult would infer a non-nullable result and the whole
            // point of the answer is that a variable the catalogue does not publish comes back null.
            return Task.FromResult<VariableDetail?>(_details.GetValueOrDefault(id));
        }

        // The two owner endpoints, kept on the same fake as the detail because that is how they are
        // reached: a kilde is only ever opened from a variable panel that is already on screen.
        private readonly Dictionary<Guid, KildeDetail> _kilder = [];
        private readonly Dictionary<Guid, DatasamlingDetail> _datasamlinger = [];

        // Only the kilde fetch can be stalled, and that is enough: the swap the generation guard
        // exists for needs one owner hanging and the other answering, which is exactly this.
        private readonly List<TaskCompletionSource<KildeDetail?>> _kildeStalls = [];

        public int KildeCalls { get; private set; }
        public int DatasamlingCalls { get; private set; }
        public Guid LastSourceId { get; private set; }

        /// <summary>Fail every owner fetch, of either kind, from the next one on.</summary>
        public bool FailSource { get; set; }

        /// <summary>Refuse every owner fetch, of either kind, with the API's 429.</summary>
        public bool RateLimitSource { get; set; }

        /// <summary>Never answer a kilde fetch from the next one on.</summary>
        public bool StallKilde { get; set; }

        public DetailClient Knows(KildeDetail kilde)
        {
            _kilder[kilde.Id] = kilde;

            return this;
        }

        public DetailClient Knows(DatasamlingDetail datasamling)
        {
            _datasamlinger[datasamling.Id] = datasamling;

            return this;
        }

        /// <summary>Answer the oldest kilde fetch still hanging.</summary>
        public void AnswerStalledKilde(KildeDetail kilde) =>
            _kildeStalls.First(stall => !stall.Task.IsCompleted).TrySetResult(kilde);

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            KildeCalls++;
            LastSourceId = id;

            if (RateLimitSource)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (FailSource)
            {
                throw new HttpRequestException("nede");
            }

            if (StallKilde)
            {
                var stall = new TaskCompletionSource<KildeDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _kildeStalls.Add(stall);

                return stall.Task;
            }

            // Explicit for the reason GetVariableAsync is: null is the answer for something the
            // catalogue does not publish, and it has to survive the inference.
            return Task.FromResult<KildeDetail?>(_kilder.GetValueOrDefault(id));
        }

        public override Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DatasamlingCalls++;
            LastSourceId = id;

            if (RateLimitSource)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (FailSource)
            {
                throw new HttpRequestException("nede");
            }

            return Task.FromResult<DatasamlingDetail?>(_datasamlinger.GetValueOrDefault(id));
        }

        // The codes endpoint, on the same fake for the reason the owners are: a code list is only
        // ever reached from a variable panel that is already open.
        private readonly Dictionary<(string Type, string Reference), KodeverkCodes> _codeLists = [];

        /// <summary>Every codes fetch made, in order — which is how "not until asked" is tested.</summary>
        public List<(Guid VariableId, string Type, string Reference)> CodeRequests { get; } = [];

        /// <summary>The fetches made for one reference.</summary>
        /// <remarks>
        /// A nameless link is fetched with the variable, so a test about a control's own fetching
        /// can no longer count the whole list (Fhi.Metadata-l9l2n.38).
        /// </remarks>
        public IReadOnlyList<(Guid VariableId, string Type, string Reference)> RequestsFor(string reference) =>
            [.. CodeRequests.Where(request => request.Reference == reference)];

        /// <summary>Fail every codes fetch from the next one on.</summary>
        public bool FailCodes { get; set; }

        /// <summary>Refuse every codes fetch from the next one on with the API's 429.</summary>
        public bool RateLimitCodes { get; set; }

        /// <summary>Never answer a codes fetch from the next one on.</summary>
        /// <remarks>
        /// The counterpart to <see cref="StallKilde"/>, and there for the same reason: the loading
        /// line, the de-duplication of a request already in flight and the generation guard all
        /// only exist between the ask and the answer, and a fetch that completes synchronously
        /// never spends any time there.
        /// </remarks>
        public bool StallCodes { get; set; }

        private readonly List<TaskCompletionSource<KodeverkCodes?>> _codeStalls = [];

        public DetailClient Knows(KodeverkCodes codes)
        {
            _codeLists[(codes.KodeverkType, codes.KodeverkReference)] = codes;

            return this;
        }

        /// <summary>Answer the oldest codes fetch still hanging.</summary>
        /// <remarks>
        /// Call it from inside <c>cut.InvokeAsync</c>, which every caller does, and the component's
        /// continuation has run by the time this returns: the stall completes its continuations
        /// inline, and the awaiting continuation captured the renderer's synchronisation context —
        /// which is the current one there — so the task machinery runs it on the spot rather than
        /// posting it.
        /// <para>
        /// That is the whole reason for not asking for <c>RunContinuationsAsynchronously</c> here.
        /// An answer the generation guard is <em>supposed</em> to drop leaves nothing behind to
        /// wait for, so a test about one has nothing to poll and no choice but to know it has
        /// landed. Nudging the dispatcher one turn is not that: with the continuation queued
        /// asynchronously the answer can still be a thread-pool hop away, and the test then races
        /// the very thing it is checking.
        /// </para>
        /// </remarks>
        public void AnswerStalledCodes(KodeverkCodes codes) =>
            _codeStalls.First(stall => !stall.Task.IsCompleted).TrySetResult(codes);

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference,
            CancellationToken cancellationToken = default)
        {
            CodeRequests.Add((variableId, kodeverkType, kodeverkReference));

            if (RateLimitCodes)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (FailCodes)
            {
                throw new HttpRequestException("nede");
            }

            if (StallCodes)
            {
                // Continuations inline deliberately - see AnswerStalledCodes.
                var stall = new TaskCompletionSource<KodeverkCodes?>();
                _codeStalls.Add(stall);

                return stall.Task;
            }

            // Explicit for the reason the other two are: a link the register does not know answers
            // null, and that has to survive the inference.
            return Task.FromResult<KodeverkCodes?>(
                _codeLists.GetValueOrDefault((kodeverkType, kodeverkReference)));
        }
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> Toggles(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name");
    // The variable's own name is the disclosure — helsedata's pattern. There is no longer a
    // separate "Vis detaljer" button under the metadata line.

    private static AngleSharp.Dom.IElement Panel(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-detail");

    /// <summary>The panel's values, in the order the definition list draws them.</summary>
    // ---------------------------------------------------------------------------------
    // The panel's tabs. Runa splits an open row into what the variable IS and what its
    // data holds; helsedata supplies the dress. What has to hold is that the split is
    // announced correctly and reachable from a keyboard, because a tablist that costs
    // one tab stop is unusable without arrow keys.

    private static IReadOnlyList<AngleSharp.Dom.IElement> TabButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".munin-explorer-meta__tabs [role=tab]");

    [Fact]
    public void Panel_WhenOpened_ThenItHasRunasTwoTabsWithDetailsSelected()
    {
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();

        Assert.Equal(["Detaljer", "Data"], TabButtons(cut).Select(b => b.TextContent));
        Assert.Equal(["true", "false"], TabButtons(cut).Select(b => b.GetAttribute("aria-selected")));

        // Only the selected tab is in the tab order, which is what keeps the tablist at one stop
        // rather than one per tab.
        Assert.Equal(["0", "-1"], TabButtons(cut).Select(b => b.GetAttribute("tabindex")));

        var panel = cut.Find("[role=tabpanel]");

        Assert.Equal(TabButtons(cut)[0].Id, panel.GetAttribute("aria-labelledby"));
        Assert.Equal(panel.Id, TabButtons(cut)[0].GetAttribute("aria-controls"));
    }

    [Fact]
    public void Panel_WhenTheDataTabIsChosen_ThenTheKodeverkShowsAndTheMetadataDoesNot()
    {
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();

        Assert.Contains("Beskrivelse", Panel(cut).TextContent);

        TabButtons(cut)[1].Click();

        Assert.Equal(["false", "true"], TabButtons(cut).Select(b => b.GetAttribute("aria-selected")));

        // The kind of kodeverk is the heading now, so it is the heading that says the tab arrived.
        Assert.Contains("Kildekodeverk", Panel(cut).TextContent);
        Assert.DoesNotContain("Beskrivelse", Panel(cut).TextContent);
    }

    /// <summary>The captured detail, plus the statistics Runa shows in the same tab.</summary>
    private static VariableDetail DetailWithStatistics(Guid id) => Detail(id) with
    {
        DatasamlingStatisticsType = "yearly",
        Statistics =
        [
            new()
            {
                AdditionalProperties = new Dictionary<string, string?>
                {
                    ["SisteOppdaterteAarssett"] = "2022",
                    ["MIN"] = "1",
                    ["MAX"] = "4",
                    ["AVG"] = "2.5"
                }
            }
        ]
    };

    [Fact]
    public void Panel_WhenTheDataTabIsChosen_ThenTheStatisticsShowBesideTheKodeverk()
    {
        // Runa's Data tab shows kodeverk and statistics together. This panel showed kodeverk alone
        // and left the numbers one click further in, inside the whole-variable view — so a reader
        // who opened a row to see what its values look like was told nothing about them.
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale")))
            .Knows(DetailWithStatistics(TaleId));
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();
        TabButtons(cut)[1].Click();

        var panel = Panel(cut);

        Assert.Contains("Kildekodeverk", panel.TextContent);
        Assert.Contains("Statistikk (Årsbasert)", panel.TextContent);

        // The table itself, not just its heading — and the dash for the column the payload holds
        // no number for, so a reader can tell "not measured" from a cell that failed to draw.
        var table = panel.QuerySelector("table.munin-explorer-statistics");

        Assert.NotNull(table);
        Assert.Equal(
            ["År", "Minimum", "Maksimum", "Gjennomsnitt", "Standardavvik"],
            table!.QuerySelectorAll("thead th").Select(cell => cell.TextContent));
        Assert.Equal(
            ["2022", "1", "4", "2.5", "—"],
            table.QuerySelectorAll("tbody tr")[0].Children.Select(cell => cell.TextContent));
    }

    [Fact]
    public void Panel_WhenTheVariableHasNoStatistics_ThenTheDataTabDrawsNeitherHeadingNorTable()
    {
        // THE TRAP this bead names. A test written only with rich data passes against an
        // implementation that draws a "Statistikk" heading over an empty table, which is exactly
        // the shape VariableView already avoided with its Count > 0 check. The captured Detail()
        // has no statistics, so this is the real payload for most variables rather than a
        // contrived one.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();
        TabButtons(cut)[1].Click();

        var panel = Panel(cut);

        // The tab did arrive — otherwise this passes for the wrong reason.
        Assert.Contains("Kildekodeverk", panel.TextContent);

        Assert.DoesNotContain("Statistikk", panel.TextContent, StringComparison.Ordinal);
        Assert.Empty(panel.QuerySelectorAll("table.munin-explorer-statistics"));
    }

    [Fact]
    public void Panel_WhenTheHostMovesTheComponentDown_ThenTheStatisticsHeadingFollowsTheRow()
    {
        // The component takes HeadingLevel because the host decides where in its document this
        // sits. A hardcoded level would be right only for the default, and the heading would jump
        // out of the row's outline everywhere else — with the rest of the tab still following it.
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale")))
            .Knows(DetailWithStatistics(TaleId));
        var cut = RenderWith(client, b => b.Add(c => c.HeadingLevel, 3));

        Toggles(cut)[0].Click();
        TabButtons(cut)[1].Click();

        var panel = Panel(cut);
        var statistics = panel.QuerySelectorAll("h4")
            .Single(h => h.TextContent.StartsWith("Statistikk", StringComparison.Ordinal));

        // HeadingLevel 3 → title h3 → RowLevel h4, which is where the kodeverk kinds beside it
        // sit. The two must land on the same level or the tab reads as two outlines.
        Assert.Equal("H4", statistics.TagName);
        Assert.Contains("Kildekodeverk", panel.QuerySelectorAll("h4").Select(h => h.TextContent));
    }

    [Fact]
    public void Panel_WhenAnArrowKeyIsPressed_ThenTheTabMoves()
    {
        // The APG tabs pattern. Without it a keyboard user reaches the tablist and cannot leave the
        // first tab: the others carry tabindex="-1", so Tab does not reach them.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();
        cut.Find(".munin-explorer-meta__tabs").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("true", TabButtons(cut)[1].GetAttribute("aria-selected"));

        cut.Find(".munin-explorer-meta__tabs").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Wraps rather than stopping, so the movement has no dead end.
        Assert.Equal("true", TabButtons(cut)[0].GetAttribute("aria-selected"));

        cut.Find(".munin-explorer-meta__tabs").KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Equal("true", TabButtons(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Panel_WhenAnotherRowIsOpened_ThenItStartsOnDetailsAgain()
    {
        // A reader who was on Data for one variable has not asked to be on Data for the next.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();
        TabButtons(cut)[1].Click();

        Assert.Equal("true", TabButtons(cut)[1].GetAttribute("aria-selected"));

        Toggles(cut)[1].Click();

        Assert.Equal("true", TabButtons(cut)[0].GetAttribute("aria-selected"));
    }


    // ---------------------------------------------------------------------------------
    // The Data tab's kodeverk. What has to hold is the acceptance criterion: the links
    // are grouped by kind with the reference visible, and "Vis koder" fetches and shows
    // Verdi / Navn / Gyldig fra / Gyldig til for the one that was pressed. No bare
    // reference is left standing as the only text on a line — which is what the old flat
    // list did whenever the API resolved no name, and it did so for the very variable
    // this panel was measured against.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// A detail carrying one kodeverk link of every kind the API sends.
    /// </summary>
    /// <remarks>
    /// Shaped like the live catalogue rather than tidied: the kildekodeverk has no resolved name,
    /// which is the case the bead was written for, and the two kildekodeverk are not adjacent in
    /// the payload — grouping that only worked on a sorted list would pass on a tidier fixture.
    /// The helsefaglig link carries <c>HasCodeValues = false</c>, which is what every one of them
    /// carries: the API serves no codes for that kind at all.
    /// </remarks>
    private static VariableDetail WithKodeverk(Guid id) => Detail(id) with
    {
        KodeverkLinks =
        [
            new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true },
            new()
            {
                KodeverkType = "AdministrativtKodeverk",
                KodeverkReference = "3402",
                DisplayName = "Kommunenummer",
                HasCodeValues = true
            },
            new()
            {
                KodeverkType = "Kildekodeverk",
                KodeverkReference = "2337",
                DisplayName = "Skjemastatus",
                HasCodeValues = true
            },
            new()
            {
                KodeverkType = "HelsefagligKodeverk",
                KodeverkReference = "ICD-10",
                DisplayName = "Den internasjonale statistiske klassifikasjonen av sykdommer",
                HasCodeValues = false
            }
        ]
    };

    /// <summary>The codes behind the nameless kildekodeverk, as the endpoint sends them.</summary>
    private static KodeverkCodes Codes2336() => new()
    {
        KodeverkType = "Kildekodeverk",
        KodeverkReference = "2336",
        Codes =
        [
            new()
            {
                Value = "0",
                Name = "Velg verdi",
                ValidFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new()
            {
                Value = "1",
                Name = "0: Tap av produktiv tale",
                ValidFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2020, 3, 31, 0, 0, 0, TimeSpan.Zero)
            }
        ]
    };

    /// <summary>Skjemastatus' codes: the same shape, behind a control because that link has a name.</summary>
    /// <remarks>
    /// Registered so the nameless link is not the only one in this fixture whose codes the fake
    /// knows. Since Fhi.Metadata-l9l2n.38 a nameless link's codes are drawn without a press, so a
    /// test about the control has to press a named link's.
    /// </remarks>
    private static KodeverkCodes Codes2337() => Codes2336() with { KodeverkReference = "2337" };

    /// <summary>Kommunenummer's shape: a kodeverk that records no start dates at all.</summary>
    private static KodeverkCodes Codes3402() => new()
    {
        KodeverkType = "AdministrativtKodeverk",
        KodeverkReference = "3402",
        Codes =
        [
            new()
            {
                Value = "0101",
                Name = "Halden",
                ValidTo = new DateTimeOffset(2023, 9, 6, 13, 13, 41, TimeSpan.Zero)
            }
        ]
    };

    private static DetailClient KodeverkRows() =>
        new DetailClient(OnePage(Row(TaleId, "1. Tale"), Row(SpyttId, "2. Spyttsekresjon")))
            .Knows(WithKodeverk(TaleId))
            .Knows(WithKodeverk(SpyttId))
            .Knows(Codes2336())
            .Knows(Codes2337())
            .Knows(Codes3402());

    /// <summary>Open the first row and move to the Data tab, where the kodeverk live.</summary>
    private IRenderedComponent<VariableExplorer> OpenData(
        DetailClient client, Action<ComponentParameterCollectionBuilder<VariableExplorer>>? p = null)
    {
        var cut = RenderWith(client, p);

        Toggles(cut)[0].Click();
        TabButtons(cut)[1].Click();

        return cut;
    }

    private static IReadOnlyList<string> KodeverkGroupHeadings(IRenderedComponent<VariableExplorer> cut) =>
        [.. Panel(cut).QuerySelectorAll(".munin-explorer-group").Select(h => h.TextContent)];

    private static IReadOnlyList<AngleSharp.Dom.IElement> KodeverkLines(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. Panel(cut).QuerySelectorAll("li.munin-explorer-kodeverk__item")];

    private static IReadOnlyList<AngleSharp.Dom.IElement> CodeToggles(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. Panel(cut).QuerySelectorAll("li.munin-explorer-kodeverk__item > button")];

    [Fact]
    public void Kodeverk_WhenTheDataTabIsOpen_ThenTheLinksAreGroupedByKindInPayloadOrder()
    {
        var cut = OpenData(KodeverkRows());

        // One heading per kind, in the order the payload first mentions each — not one heading per
        // link, and not an order of ours. The two kildekodeverk are apart in the payload, so a
        // grouping that only worked on adjacent links would show four headings here.
        Assert.Equal(["Kildekodeverk", "Administrativt kodeverk", "Helsefaglig kodeverk"],
                     KodeverkGroupHeadings(cut));

        var groups = Panel(cut).QuerySelectorAll("ul.munin-explorer-kodeverk");

        Assert.Equal([2, 1, 1], groups.Select(g => g.QuerySelectorAll("li").Length));
    }

    [Fact]
    public void Kodeverk_WhenTheApiResolvedNoName_ThenTheReferenceIsLabelledRatherThanStandingIn()
    {
        // Still the rule for every kind but Kildekodeverk. A V-AK's OId is an official number, so a
        // missing name is an ordinary lookup miss: it is said out loud and the reference is
        // labelled, rather than the reference standing in as the name.
        var id = Guid.NewGuid();
        var cut = OpenData(new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(Detail(id) with
        {
            KodeverkLinks =
            [
                new() { KodeverkType = "AdministrativtKodeverk", KodeverkReference = "3402", HasCodeValues = true }
            ]
        }));

        var nameless = KodeverkLines(cut)[0];

        Assert.Equal("Ukjent navn", nameless.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 3402",
                     nameless.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);
        Assert.DoesNotContain("Administrativt kodeverk: 3402", Panel(cut).TextContent);
    }

    [Fact]
    public void Kodeverk_WhenTheApiResolvedAName_ThenBothTheNameAndTheReferenceAreShown()
    {
        // The reference is on every line, named one and nameless alike: it is the thing a reader
        // can look the kodeverk up by, and a name alone cannot be typed into anything.
        var cut = OpenData(KodeverkRows());

        var named = KodeverkLines(cut)[1];

        Assert.Equal("Skjemastatus", named.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 2337",
                     named.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);

        // The catalogue's own name, so a screen reader on an English page still says it in
        // Norwegian rather than reading it with English phonetics.
        Assert.Equal("no", named.QuerySelector(".munin-explorer-kodeverk__name")!.GetAttribute("lang"));
    }

    [Fact]
    public void Kodeverk_WhenNoneAreRegistered_ThenTheTabSaysSoRatherThanBeingBlank()
    {
        var id = Guid.NewGuid();
        var cut = OpenData(new DetailClient(OnePage(Row(id, "1. Tale")))
            .Knows(Detail(id) with { KodeverkLinks = [] }));

        Assert.Equal("Ingen kodeverk registrert", Panel(cut).QuerySelector("[role=tabpanel] > p")!.TextContent);
    }

    [Fact]
    public void Codes_WhenTheTabIsOpened_ThenOnlyTheNamelessLinksCodesAreFetched()
    {
        // The reason the codes are their own endpoint at all. Kommunenummer is 885 codes and most
        // readers open none of them, so opening a row must not pay for a list nobody reads. The one
        // exception is a link with no name: its codes are the only thing that identifies it, so
        // they are fetched with the variable and nothing else is (Fhi.Metadata-l9l2n.38).
        var client = KodeverkRows();
        var cut = OpenData(client);

        Assert.Equal((TaleId, "Kildekodeverk", "2336"), Assert.Single(client.CodeRequests));

        // Both named links keep their collapsed control and have been asked for nothing.
        Assert.Equal(["Vis koder", "Vis koder"], CodeToggles(cut).Select(b => b.TextContent));
        Assert.All(CodeToggles(cut), b => Assert.Equal("false", b.GetAttribute("aria-expanded")));
    }

    [Fact]
    public void Codes_WhenTheLinkServesNone_ThenThereIsNoControlToPress()
    {
        // Every HelsefagligKodeverk link answers 404, which the payload says up front with
        // harKodeverdier. A button that could only ever report "no code values" is worse than none.
        var cut = OpenData(KodeverkRows());

        Assert.Equal(4, KodeverkLines(cut).Count);
        Assert.Empty(KodeverkLines(cut)[3].QuerySelectorAll("button"));

        // Two controls, not three: the nameless link's two codes are already on its line, so there
        // is nothing a press could reveal. The other reason for no button, and a different one.
        Assert.Equal(2, CodeToggles(cut).Count);
        Assert.Empty(KodeverkLines(cut)[0].QuerySelectorAll("button"));
    }

    [Fact]
    public void Codes_WhenVisKoderIsPressed_ThenOneRequestIsMadeAndTheFourColumnsAreShown()
    {
        // The rest of the acceptance criterion: the codes arrive from their own endpoint, for the
        // link that was pressed, and read as Verdi / Navn / Gyldig fra / Gyldig til.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        // 2337 rather than 2336: the nameless link's codes are on its line already, so the first
        // control on the page belongs to the named kildekodeverk beneath it.
        Assert.Equal((TaleId, "Kildekodeverk", "2337"), Assert.Single(client.RequestsFor("2337")));

        var table = Panel(cut).QuerySelector(".munin-explorer-codes table")!;

        Assert.Equal(["Verdi", "Navn", "Gyldig fra", "Gyldig til"],
                     table.QuerySelectorAll("thead th").Select(th => th.TextContent));

        var rows = table.QuerySelectorAll("tbody tr");
        var first = rows[0].QuerySelectorAll("td").Select(td => td.TextContent).ToArray();

        Assert.Equal("0", first[0]);
        Assert.Equal("Velg verdi", first[1]);

        // The day and not the instant: every one of these dates is midnight UTC or the moment a
        // bulk import ran, and neither is a fact about when the code applied. The separator is
        // Norwegian; the zero padding is ICU's business, not this test's.
        Assert.Matches(@"^\d{1,2}\.\d{1,2}\.2010$", first[2]);

        // Written out rather than shown as a dash: there is no visually-hidden helper in this
        // package to whisper the meaning of a dash into, so a missing value says so for everyone.
        Assert.Equal("Ikke oppgitt", first[3]);

        var second = rows[1].QuerySelectorAll("td").Select(td => td.TextContent).ToArray();

        Assert.Equal("0: Tap av produktiv tale", second[1]);
        Assert.Matches(@"^\d{1,2}\.\d{1,2}\.2020$", second[3]);
    }

    [Fact]
    public void Codes_WhenAKodeverkRecordsNoStartDates_ThenTheCellSaysSoRatherThanShowingYearOne()
    {
        // Kommunenummer's shape. A non-nullable date would render every one of its 885 codes as
        // starting on 01.01.0001, which reads as data rather than as an absence.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[1].Click();

        var cells = Panel(cut).QuerySelectorAll(".munin-explorer-codes tbody td")
            .Select(td => td.TextContent).ToArray();

        Assert.Equal(["0101", "Halden"], cells[..2]);
        Assert.Equal("Ikke oppgitt", cells[2]);

        // The end date carries a time of day on the wire — the import ran at 13:13:41 — and the
        // cell shows the day alone, because the import's clock is not when Halden stopped existing.
        Assert.Matches(@"^\d{1,2}\.\d{1,2}\.2023$", cells[3]);
    }

    [Fact]
    public void Codes_WhenTheListIsCollapsedAndOpenedAgain_ThenItIsNotFetchedTwice()
    {
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        Assert.Equal("Skjul koder", CodeToggles(cut)[0].TextContent);
        Assert.Equal("true", CodeToggles(cut)[0].GetAttribute("aria-expanded"));

        CodeToggles(cut)[0].Click();

        Assert.Empty(Panel(cut).QuerySelectorAll(".munin-explorer-codes"));
        Assert.Equal("Vis koder", CodeToggles(cut)[0].TextContent);

        CodeToggles(cut)[0].Click();

        // The answer was kept, so re-opening costs nothing. Without the cache a reader comparing
        // two kodeverk pays for the same list every time they look back at it.
        Assert.Single(client.RequestsFor("2337"));
        Assert.NotNull(Panel(cut).QuerySelector(".munin-explorer-codes table"));
    }

    [Fact]
    public void Codes_WhenTwoListsAreOpened_ThenBothStayOpenWithTheirOwnCodes()
    {
        // Unlike the kilde and datasamling panels, which are one at a time: those answer the same
        // question about the same variable twice, where two kodeverk are two different things.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();
        CodeToggles(cut)[1].Click();

        var tables = Panel(cut).QuerySelectorAll(".munin-explorer-codes table");

        Assert.Equal(2, tables.Length);
        Assert.Contains("Velg verdi", tables[0].TextContent);
        Assert.Contains("Halden", tables[1].TextContent);
        Assert.Single(client.RequestsFor("2337"));
        Assert.Single(client.RequestsFor("3402"));
    }

    [Fact]
    public void Codes_WhenAListIsOpen_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // The Orphans check reaches only as far as the markup a render produces, and the renders
        // that carry it elsewhere in this file all stop at a closed result list or an opened panel
        // with no code list in it. So the names under an open kodeverk — `munin-explorer-codes` and
        // its table — were the guard's blind spot rather than its coverage: emptying their rules in
        // both sample stylesheets left the whole suite green while the shell guard went red.
        //
        // This render is the deepest the component goes: a row open, the Data tab selected, and a
        // code list fetched and drawn under it.
        var cut = OpenData(KodeverkRows());

        CodeToggles(cut)[0].Click();

        Assert.NotNull(Panel(cut).QuerySelector(".munin-explorer-codes table"));
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Codes_WhenTheApiPublishesNoneForTheLink_ThenTheListSaysSoRatherThanFailing()
    {
        // A reference the upstream register does not know answers 404, which the client reports as
        // null. That is not a fault and must not be dressed as one — nor asked for again on every
        // expand, which is why the empty answer is cached like any other.
        //
        // Its own fixture rather than a link out of KodeverkRows: every named link there is one the
        // fake now knows, because a named link is the only kind left with a control to press.
        var id = Guid.NewGuid();
        var client = new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(Detail(id) with
        {
            KodeverkLinks =
            [
                new()
                {
                    KodeverkType = "Kildekodeverk",
                    KodeverkReference = "2337",
                    DisplayName = "Skjemastatus",
                    HasCodeValues = true
                }
            ]
        });

        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        Assert.Equal("Ingen kodeverdier tilgjengelig",
                     Panel(cut).QuerySelector(".munin-explorer-codes p")!.TextContent);

        CodeToggles(cut)[0].Click();
        CodeToggles(cut)[0].Click();

        Assert.Single(client.CodeRequests);
    }

    [Fact]
    public void Codes_WhenTheFetchFails_ThenOnlyThatListSaysSoAndPressingAgainRetries()
    {
        // What failed is one collapsed list inside one panel. The rows behind it and every other
        // line on the panel describe exactly what they described before, so the failure is reported
        // where it happened rather than in the component's alert region.
        var client = KodeverkRows();
        client.FailCodes = true;

        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        var message = Panel(cut).QuerySelector(".munin-explorer-codes p")!;

        Assert.Contains("Kunne ikke hente kodene", message.TextContent);
        Assert.Contains("infobox", message.ClassName!);

        // The panel around it is untouched: the other lines still offer their codes, and the rows
        // are still the rows the search returned. Three controls, because the nameless link's own
        // fetch failed too and a failed one keeps its control — that is the reader's retry.
        Assert.Equal(3, CodeToggles(cut).Count);
        Assert.Equal(2, Toggles(cut).Count);
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());

        // Pressing again is the only retry a reader has, and there is no answer being cached over.
        client.FailCodes = false;
        CodeToggles(cut)[0].Click();
        CodeToggles(cut)[0].Click();

        Assert.Equal(3, client.RequestsFor("2336").Count);

        // And the retry that landed puts the codes where they belonged all along — on the line, in
        // place of the name — so the control that offered the retry has nothing left to reveal.
        Assert.Equal("0 Velg verdi · 1 0: Tap av produktiv tale",
                     KodeverkLines(cut)[0].QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Empty(KodeverkLines(cut)[0].QuerySelectorAll("button"));
    }

    [Fact]
    public void Codes_WhenTheFetchIsRateLimited_ThenOnlyThatListSaysTheReaderAskedTooOften()
    {
        // Expanding one kodeverk after another is exactly the rhythm that meets the limiter, so a
        // throttled reader arrives here with one list already filled. The sentence is written into
        // the per-key dictionary, so the failure has to land on the key that was pressed and on no
        // other: a sibling that answered perfectly must not inherit it.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        Assert.NotNull(Panel(cut).QuerySelector(".munin-explorer-codes table"));

        client.RateLimitCodes = true;
        CodeToggles(cut)[1].Click();

        var lists = Panel(cut).QuerySelectorAll(".munin-explorer-codes");

        Assert.Equal(2, lists.Length);

        // The one that was refused says so, and says the throttled sentence rather than the
        // generic one or the not-published one.
        var refused = lists[1];

        Assert.Contains("for mange forespørsler", refused.TextContent);
        Assert.DoesNotContain("Kunne ikke hente kodene", refused.TextContent);
        Assert.DoesNotContain("Ingen kodeverdier tilgjengelig", refused.TextContent);
        Assert.Contains("infobox", refused.QuerySelector("p")!.ClassName!);

        // The one that arrived is untouched — still its table, and no borrowed sentence.
        Assert.NotNull(lists[0].QuerySelector("table"));
        Assert.DoesNotContain("for mange forespørsler", lists[0].TextContent);

        // And the panel around them, and the component's own alert region, are not the place this
        // was reported.
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());

        // Pressing again is the only retry a reader has, and nothing was cached over.
        client.RateLimitCodes = false;
        CodeToggles(cut)[1].Click();
        CodeToggles(cut)[1].Click();

        Assert.Equal(2, client.RequestsFor("3402").Count);
        Assert.Equal(2, Panel(cut).QuerySelectorAll(".munin-explorer-codes table").Length);
    }

    [Fact]
    public void Codes_WhenAnotherVariableIsOpened_ThenTheFirstOnesCodesAreNotInherited()
    {
        // Two variables can share a reference, so a cache left behind would look right and be
        // another variable's answer. The fetch is per variable as well as per reference.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        Assert.Equal(TaleId, Assert.Single(client.RequestsFor("2337")).VariableId);

        Toggles(cut)[1].Click();
        TabButtons(cut)[1].Click();

        // Every list starts collapsed again, and opening one asks for the new variable's codes.
        Assert.All(CodeToggles(cut), b => Assert.Equal("false", b.GetAttribute("aria-expanded")));

        CodeToggles(cut)[0].Click();

        Assert.Equal([TaleId, SpyttId], client.RequestsFor("2337").Select(r => r.VariableId));

        // The nameless link is per variable too, and it was asked once for each without a press.
        Assert.Equal([TaleId, SpyttId], client.RequestsFor("2336").Select(r => r.VariableId));
    }

    [Fact]
    public void Codes_WhileTheFetchIsStillOut_ThenTheListSaysItIsLoadingRatherThanEmpty()
    {
        // The window between the ask and the answer is a state of its own. Without it the open
        // list falls through to the empty case and reads "Ingen kodeverdier tilgjengelig" — an
        // answer, and the wrong one, about codes that are still on their way.
        var client = KodeverkRows();
        var cut = OpenData(client);

        client.StallCodes = true;
        CodeToggles(cut)[0].Click();

        var message = Panel(cut).QuerySelector(".munin-explorer-codes p")!;

        Assert.Equal("Henter koder \u2026", message.TextContent);
        Assert.Equal("caption", message.ClassName);
    }

    [Fact]
    public async Task Codes_WhenAStalledListIsOpenedAgain_ThenTheFetchAlreadyOutIsTheOneItWaitsFor()
    {
        // Collapsing and re-opening is what a reader does when a list is slow, and the request
        // already in flight is the one that will fill it. Without the in-flight check each press
        // starts another fetch of the same link — Kommunenummer's 885 codes, once per press.
        var client = KodeverkRows();
        var cut = OpenData(client);

        client.StallCodes = true;
        CodeToggles(cut)[0].Click();

        CodeToggles(cut)[0].Click();
        CodeToggles(cut)[0].Click();

        Assert.Single(client.RequestsFor("2337"));
        Assert.Equal("Henter koder \u2026",
                     Panel(cut).QuerySelector(".munin-explorer-codes p")!.TextContent);

        // And the one answer fills the list that was re-opened, rather than being orphaned by it.
        await cut.InvokeAsync(() => client.AnswerStalledCodes(Codes2337()));

        cut.WaitForAssertion(() =>
            Assert.Contains("Velg verdi", Panel(cut).QuerySelector(".munin-explorer-codes table")!.TextContent));

        Assert.Single(client.RequestsFor("2337"));
    }

    [Fact]
    public async Task Codes_WhenAnotherVariableIsOpenedWhileTheFetchIsOut_ThenTheAbandonedAnswerIsNotShown()
    {
        // The generation guard, and the reason the kilde panel has one. Two variables in this very
        // fixture link to reference 2337, so an answer that outlives the panel it was asked for
        // lands in the next variable's list looking entirely correct — and is another variable's
        // codes, never fetched for the one on screen.
        var client = KodeverkRows();
        var cut = OpenData(client);

        client.StallCodes = true;
        CodeToggles(cut)[0].Click();

        // The reader gave up on the hanging list and opened the other variable instead.
        Toggles(cut)[1].Click();
        TabButtons(cut)[1].Click();

        client.StallCodes = false;

        // The oldest hanging fetch is the one this answers: 2337 for the variable the reader left.
        await cut.InvokeAsync(() => client.AnswerStalledCodes(Codes2337() with
        {
            Codes = [new() { Value = "9", Name = "STALE" }]
        }));

        // The abandoned answer has landed by the line above, not merely been nudged towards
        // landing: the stall runs its continuations inline, so the component's has run before
        // AnswerStalledCodes returned. That ordering is what gives the rest of this test its bite.
        // Waiting for the fresh answer instead would not: both answers write the same _codes entry,
        // so with the guard broken the last writer wins, and a wait that ends once the fresh one is
        // in reads a table the abandoned write has already been erased from.
        CodeToggles(cut)[0].Click();

        // And the table this press produces is there by the time Click returns, so the read below
        // needs no wait of its own: StallCodes is off again, GetKodeverkCodesAsync hands back a
        // Task.FromResult, and LoadCodesAsync neither yields nor leaves the dispatcher — so its
        // await continues inline, inside the dispatch bUnit blocks on. Deterministic rather than
        // fast, which is why load does not change it, and why every other codes test in this file
        // reads the table straight after the press as well.
        var table = Panel(cut).QuerySelector(".munin-explorer-codes table")!;

        Assert.DoesNotContain("STALE", table.TextContent);
        Assert.Contains("Velg verdi", table.TextContent);

        // Nothing was cached under the new variable, so its list was fetched for it. Break the
        // guard and this fails too: the abandoned answer fills _codes for the key about to be
        // opened, so the toggle finds it there and never asks. Checked by removing the guard —
        // the STALE row shows up first, and this line behind it.
        Assert.Equal([TaleId, SpyttId], client.RequestsFor("2337").Select(r => r.VariableId));
    }

    [Fact]
    public void Codes_WhenTheListIsOpen_ThenTheControlAndTheTableSayWhatTheyBelongTo()
    {
        // aria-expanded and aria-controls on the control, and the table named from the line above
        // it. Set only while the list is open, the rule the owner toggles follow: an aria-controls
        // naming an element that is not in the document is worse than none.
        var client = KodeverkRows();
        var cut = OpenData(client);

        Assert.Null(CodeToggles(cut)[0].GetAttribute("aria-controls"));

        CodeToggles(cut)[0].Click();

        var region = Panel(cut).QuerySelector(".munin-explorer-codes")!;

        // Line 1, not line 0: the first control on the page belongs to the named kildekodeverk,
        // because the nameless one above it draws its codes without a control at all.
        Assert.Equal(region.Id, CodeToggles(cut)[0].GetAttribute("aria-controls"));
        Assert.Equal(KodeverkLines(cut)[1].QuerySelector(".munin-explorer-kodeverk__name")!.Id,
                     region.QuerySelector("table")!.GetAttribute("aria-labelledby"));

        // Column headers, so a screen reader can say which column a cell is in.
        Assert.All(region.QuerySelectorAll("thead th"), th => Assert.Equal("col", th.GetAttribute("scope")));
    }

    // ---------------------------------------------------------------------------------
    // A kildekodeverk with no name (Fhi.Metadata-l9l2n.38). The API stopped sending a name it could
    // not trust, for the majority of V-KK links on helsedata.no, so what identifies such a link is
    // its codes — and that is what its line shows.

    /// <summary>Nine codes behind the nameless link: one more than fits on the line.</summary>
    private static KodeverkCodes ManyCodes2336() => Codes2336() with
    {
        Codes = [.. Enumerable.Range(0, 9).Select(i => new KodeverkCode { Value = $"{i}", Name = $"Verdi {i}" })]
    };

    /// <summary>One nameless kildekodeverk and nothing else, so a line can be read on its own.</summary>
    private static DetailClient OneNamelessLink(Guid id, bool hasCodeValues = true) =>
        new DetailClient(OnePage(Row(id, "1. Tale"))).Knows(Detail(id) with
        {
            KodeverkLinks =
            [
                new()
                {
                    KodeverkType = "Kildekodeverk",
                    KodeverkReference = "2336",
                    HasCodeValues = hasCodeValues
                }
            ]
        });

    [Fact]
    public void Codes_WhenAKildekodeverkHasNoName_ThenItsCodesStandWhereTheNameWould()
    {
        // What a helsedata reader used to get instead: "Ukjent navn", an internal Munin id under
        // it, and the meaning behind a press. Runa has shown the codes inline since #5478.
        var cut = OpenData(KodeverkRows());
        var nameless = KodeverkLines(cut)[0];

        // Built from the stub rather than typed out here: a render that happened to print something
        // plausible would otherwise pass this without ever having drawn the codes it was given.
        Assert.Equal(string.Join(" · ", Codes2336().Codes.Select(code => $"{code.Value} {code.Name}")),
                     nameless.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);

        // None of the three things that stood here before.
        Assert.DoesNotContain("Ukjent navn", Panel(cut).TextContent);
        Assert.Null(nameless.QuerySelector(".munin-explorer-kodeverk__reference"));
        Assert.Empty(nameless.QuerySelectorAll("button"));

        // The catalogue's own words, so an English page still has them said in Norwegian — the rule
        // the table's own name column follows.
        Assert.Equal("no", nameless.QuerySelector(".munin-explorer-kodeverk__name span")!.GetAttribute("lang"));
    }

    [Fact]
    public void Codes_WhenANamelessLinkHasMoreCodesThanFitInline_ThenAControlOpensTheSameFullList()
    {
        var client = KodeverkRows().Knows(ManyCodes2336());
        var cut = OpenData(client);
        var all = ManyCodes2336().Codes;

        // Eight, which is Runa's INLINE_CODE_PREVIEW — matched so the two clients do not draw the
        // same list to different lengths.
        Assert.Equal(string.Join(" · ", all.Take(8).Select(code => $"{code.Value} {code.Name}")),
                     KodeverkLines(cut)[0].QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);

        // The total is on the control itself, so it is announced and not merely drawn beside it.
        Assert.Equal("Vis alle (9)", AccessibleName.Of(CodeToggles(cut)[0]));

        CodeToggles(cut)[0].Click();

        // The list the named links' control opens, drawn by the same fragment — not a second one
        // holding only what the line left out.
        var table = Panel(cut).QuerySelector(".munin-explorer-codes table")!;

        Assert.Single(Panel(cut).QuerySelectorAll(".munin-explorer-codes"));
        Assert.Equal(all.Count, table.QuerySelectorAll("tbody tr").Length);
        Assert.Equal(["Verdi", "Navn", "Gyldig fra", "Gyldig til"],
                     table.QuerySelectorAll("thead th").Select(th => th.TextContent));

        // And nothing was fetched twice: the line and the table are the same answer.
        Assert.Single(client.RequestsFor("2336"));
        Assert.Equal("Skjul koder", CodeToggles(cut)[0].TextContent);
    }

    [Fact]
    public void Codes_WhenAnAdministrativtKodeverkHasAName_ThenNothingAboutItsLineChanged()
    {
        // The control on the fix. A change applied to every kind rather than to nameless
        // kildekodeverk alone would pass the two tests above and break this one — a V-AK's OId is
        // an official number, so a resolved name is trustworthy and its codes stay behind a press.
        var client = KodeverkRows();
        var cut = OpenData(client);
        var named = KodeverkLines(cut)[2];

        Assert.Equal("Kommunenummer", named.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 3402", named.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);

        var control = Assert.Single(named.QuerySelectorAll("button"));

        Assert.Equal("Vis koder", control.TextContent);
        Assert.Equal("false", control.GetAttribute("aria-expanded"));
        Assert.Empty(client.RequestsFor("3402"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Codes_WhenTheNamelessLinksFetchFails_ThenItFallsBackAndTheComponentStaysUp(bool rateLimited)
    {
        // This fetch runs without anyone having pressed anything, and helsedata's legacy Blazor
        // Server host loses the whole CMS page on an unhandled exception — so a throw here takes
        // down a page the reader never touched. Both kinds: the limiter is the likelier of the two.
        var client = KodeverkRows();

        client.RateLimitCodes = rateLimited;
        client.FailCodes = !rateLimited;

        var cut = OpenData(client);
        var nameless = KodeverkLines(cut)[0];

        // The reference is back, because with no codes and no name it is the only thing telling two
        // such links apart — and the control with it, which is the only retry a reader has.
        Assert.Equal("Ukjent navn", nameless.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 2336", nameless.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);
        Assert.Equal("Vis koder", Assert.Single(nameless.QuerySelectorAll("button")).TextContent);

        // Still mounted, and the failure reported nowhere but on its own line: the rows, the rest of
        // the panel and the component's alert region all say what they said before.
        Assert.Equal(2, Toggles(cut).Count);
        Assert.Equal(4, KodeverkLines(cut).Count);
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Codes_WhenTheHostSuppliesTheSelectionAndTheFetchFails_ThenTheFirstRenderSurvivesIt()
    {
        // The other way in, and the one that runs from a lifecycle method rather than from a click:
        // a shared link lands with the panel already open, so the failing fetch is the component's
        // own first render and there is no event handler to carry the exception.
        var id = Guid.NewGuid();
        var client = OneNamelessLink(id);

        client.FailCodes = true;

        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableId, (Guid?)id));

        TabButtons(cut)[1].Click();

        Assert.Equal("Ukjent navn",
                     KodeverkLines(cut)[0].QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Single(client.RequestsFor("2336"));
    }

    [Fact]
    public void Codes_WhileTheNamelessLinksFetchIsOut_ThenItsLineSaysSoRatherThanStandingEmpty()
    {
        // The window between the ask and the answer. Without it the line falls through to the empty
        // case and says "Ingen kodeverdier tilgjengelig" about codes that are still on their way.
        var client = KodeverkRows();

        client.StallCodes = true;

        var cut = OpenData(client);
        var nameless = KodeverkLines(cut)[0];

        Assert.Equal("Henter koder …", nameless.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Null(nameless.QuerySelector(".munin-explorer-kodeverk__reference"));
    }

    [Fact]
    public void Codes_WhenTheNamelessLinkPublishesNone_ThenItsReferenceIsWhatIdentifiesIt()
    {
        // A reference the register does not know answers 404, which the client reports as null.
        // Two such links have nothing else to tell them apart, which is the reference's one job
        // left here — and it is not a failure, so it must not be dressed as one.
        var cut = OpenData(OneNamelessLink(Guid.NewGuid()));
        var nameless = KodeverkLines(cut)[0];

        Assert.Equal("Ingen kodeverdier tilgjengelig",
                     nameless.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 2336",
                     nameless.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);
        Assert.Empty(nameless.QuerySelectorAll("button"));
    }

    [Fact]
    public void Codes_WhenANamelessKildekodeverkServesNoCodes_ThenItIsLeftAsItWas()
    {
        // Runa's guard, and the reason it is there: without harKodeverdier the codes call is a
        // certain 404, so there is nothing to draw in place of the name and nothing to gain from
        // asking for it.
        var client = OneNamelessLink(Guid.NewGuid(), hasCodeValues: false);
        var cut = OpenData(client);
        var nameless = KodeverkLines(cut)[0];

        Assert.Equal("Ukjent navn", nameless.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 2336",
                     nameless.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);
        Assert.Empty(nameless.QuerySelectorAll("button"));
        Assert.Empty(client.CodeRequests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Codes_WhenAnAbandonedDetailFetchThrows_ThenItDoesNotAskAgainForTheOpenPanel(
        bool rateLimited)
    {
        // Copilot on #167. LoadDetailAsync's success path returns out of the try, but both catch
        // branches fall through to the tail — so a call that had lost the panel and then threw
        // would fetch nameless codes for whichever panel took its place.
        // Two nameless links, because the loop asks for them one at a time: while the first is out,
        // the second is in none of the three sets the de-duplication consults, and so is exactly
        // what a call that has lost the panel would ask for.
        var twoNameless = new[]
        {
            new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true },
            new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "9999", HasCodeValues = true }
        };

        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale"), Row(SpyttId, "2. Spyttsekresjon")))
            .Knows(Detail(TaleId) with { KodeverkLinks = twoNameless })
            .Knows(Detail(SpyttId, "2. Spyttsekresjon") with { KodeverkLinks = twoNameless });

        var cut = RenderWith(client);

        // The reader opens Tale, whose detail hangs, and gives up on it.
        client.StallDetail = true;
        Toggles(cut)[0].Click();

        // Spytt answers at once, and its first nameless fetch hangs — so its second link has not
        // been asked for yet, and nothing marks it as spoken for.
        client.StallDetail = false;
        client.StallCodes = true;
        Toggles(cut)[1].Click();

        Assert.Equal([SpyttId], client.RequestsFor("2336").Select(request => request.VariableId));
        Assert.Empty(client.RequestsFor("9999"));

        // Now Tale's abandoned fetch fails.
        await cut.InvokeAsync(rateLimited ? client.RateLimitStalled : client.FailStalled);

        // A flush rather than a WaitForAssertion, because a guard that works leaves nothing to wait
        // for. One turn is enough: that continuation was queued before this call, and the fetch it
        // would have made records itself before returning its stalled task.
        await cut.InvokeAsync(() => { });

        // The second link is still unasked. Without the guard the abandoned call reaches it, having
        // skipped the first only because that one happens to be in flight.
        Assert.Empty(client.RequestsFor("9999"));
        Assert.Equal([SpyttId], client.RequestsFor("2336").Select(request => request.VariableId));
    }

    [Fact]
    public async Task Kodeverk_WhenThePayloadNamesOneLinkTwice_ThenTheListSurvivesBeingDiffed()
    {
        // Two siblings carrying one @key throw inside Blazor's diff — in helsedata's legacy Server
        // host that is the whole CMS page. Latent on main, where nothing re-renders this list until
        // a press; reachable on open here, because the nameless fetch re-renders it.
        var id = Guid.NewGuid();
        var link = new KodeverkLink
        {
            KodeverkType = "Kildekodeverk",
            KodeverkReference = "2336",
            HasCodeValues = true
        };

        var client = new DetailClient(OnePage(Row(id, "1. Tale")))
            .Knows(Detail(id) with { KodeverkLinks = [link, link] });

        // Stalled on purpose. A fetch that answers at once never lets the list reach the DOM before
        // the codes do, so the whole open coalesces into one render and nothing is ever diffed —
        // which is a test of this that passes with the key broken.
        client.StallCodes = true;

        var cut = OpenData(client);

        Assert.Equal(2, KodeverkLines(cut).Count);
        Assert.All(KodeverkLines(cut), line => Assert.Equal(
            "Henter koder …", line.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent));

        // The answer re-renders the list that is already on screen, which is the diff.
        await cut.InvokeAsync(() => client.AnswerStalledCodes(Codes2336()));

        cut.WaitForAssertion(() => Assert.All(KodeverkLines(cut), line => Assert.Equal(
            "0 Velg verdi · 1 0: Tap av produktiv tale",
            line.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent)));

        // Both lines are the same kodeverk, so they share one cache entry and one answer.
        Assert.Equal(2, KodeverkLines(cut).Count);
        Assert.Single(client.RequestsFor("2336"));
    }

    // The de-duplication in LoadUnnamedCodesAsync has no test of its own: the payload that reaches
    // it is the one above, and there the first fetch succeeds, so the second mention is skipped on
    // the cache rather than on the error entry the guard was widened for (Fhi.Metadata-l9l2n.38).

    [Fact]
    public void Codes_WhenTheLanguageIsEnAndTheCodesDoNotFit_ThenTheControlIsTranslatedToo()
    {
        // The Texts parity test cannot compare a sentence-building delegate — it has no arguments
        // there — so each is asserted where it renders, the rule PageOf and ResultSummary follow.
        var cut = OpenData(KodeverkRows().Knows(ManyCodes2336()), b => b.Add(c => c.Language, "en"));

        Assert.Equal("Show all (9)", AccessibleName.Of(CodeToggles(cut)[0]));
    }

    [Fact]
    public void Codes_WhenTheLanguageIsEn_ThenTheHeadingsColumnsAndControlFollowIt()
    {
        var client = KodeverkRows();
        var cut = OpenData(client, b => b.Add(c => c.Language, "en"));

        // The two facet words are the filter panel's own, deliberately: a helsefaglig kodeverk is
        // the same thing whether it is being filtered on or read off a variable.
        Assert.Equal(["Source code system", "Administrative code system", "Clinical code system"],
                     KodeverkGroupHeadings(cut));
        // The nameless link's codes are the catalogue's own words, so they stay Norwegian on an
        // English page — and it says nothing about being unnamed, in either language.
        Assert.Equal("0 Velg verdi · 1 0: Tap av produktiv tale",
                     KodeverkLines(cut)[0].QuerySelector(".munin-explorer-kodeverk__name")!.TextContent);
        Assert.DoesNotContain("Unnamed", Panel(cut).TextContent);
        Assert.Equal("Reference: 2337",
                     KodeverkLines(cut)[1].QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent);

        CodeToggles(cut)[0].Click();

        Assert.Equal("Hide codes", CodeToggles(cut)[0].TextContent);
        Assert.Equal(["Value", "Name", "Valid from", "Valid to"],
                     Panel(cut).QuerySelectorAll(".munin-explorer-codes thead th").Select(th => th.TextContent));

        // The date follows the page too: an English reader gets slashes, not the dots a Norwegian
        // reader gets. Pinned by separator rather than by exact string, which is ICU's to change.
        Assert.Matches(@"^\d{1,2}/\d{1,2}/2010$",
                       Panel(cut).QuerySelectorAll(".munin-explorer-codes tbody td")[2].TextContent);
    }

    [Fact]
    public void Codes_WhenTheDataTabIsOpen_ThenItIsBuiltFromShapesRatherThanFromNewClassNames()
    {
        // The same rule the Details tab is held to. Four names of ours here, all handles that carry
        // no styling anywhere, plus the group heading the Details tab already introduced — and
        // Stiler's own ghost square button, infobox and caption for everything that has a name to
        // borrow. The table is an element rather than a class: Stiler styles no table, and an
        // unstyled table still aligns its columns where an invented class name renders as nothing.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        var invented = Panel(cut).QuerySelectorAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            "munin-explorer-group",                 // ours, already in use on the Details tab
            "munin-explorer-kodeverk",              // ours, a handle — the list of links
            "munin-explorer-kodeverk__item",
            "munin-explorer-kodeverk__name",
            "munin-explorer-kodeverk__reference",
            "munin-explorer-codes",                 // ours, a handle — the open code list
            "munin-explorer-codes__table",
        ], invented);

        var toggle = CodeToggles(cut)[0].ClassName!;

        Assert.Contains("hd-button-square", toggle);
        Assert.Contains("button-square--ghost", toggle);
        // Line 1: the nameless link above it draws no reference, because its codes are its identity.
        Assert.Contains("caption",
                        KodeverkLines(cut)[1].QuerySelector(".munin-explorer-kodeverk__reference")!.ClassName!);
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> Values(IRenderedComponent<VariableExplorer> cut) =>
        [.. Panel(cut).QuerySelectorAll("dl dd")];

    private static DetailClient TwoRows() =>
        new DetailClient(OnePage(Row(TaleId, "1. Tale"), Row(SpyttId, "2. Spyttsekresjon")))
            .Knows(Detail(TaleId))
            .Knows(Detail(SpyttId, "2. Spyttsekresjon"))
            // Both variables sit in the same datasamling in the same kilde, which is what the
            // catalogue actually looks like — and what lets a test open either row's owners.
            .Knows(Kilde())
            .Knows(Datasamling());

    [Fact]
    public void Detail_WhenAVariableIsSelected_ThenItsDetailIsShownWithoutAPageNavigation()
    {
        // The acceptance criterion. The component holds no NavigationManager and the card is not a
        // link, so the only way the detail can arrive is the way it does: into the same rendered
        // component, from a second call to the same client the search came from.
        var client = TwoRows();
        var cut = RenderWith(client);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;

        Toggles(cut)[0].Click();

        Assert.Equal(before, navigation.Uri);
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list a"));
        Assert.Equal(TaleId, client.LastDetailId);
        Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Detail_WhenTheDetailArrives_ThenItSaysWhatTheVariableIsAndWhereItSits()
    {
        // The five things the panel exists to show. The labels are the card's own words for the
        // same fields, so opening a row renames nothing that was already on screen.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();

        Assert.Equal(["Beskrivelse", "Kildesti", "Variabelgruppe", "Dataperiode"],
                     Panel(cut).QuerySelectorAll("dl dt").Select(t => t.TextContent));

        var values = Values(cut);

        Assert.Equal("Angir pasientens grad av utfall på «1. Tale».", values[0].TextContent);
        // The period reads as month and year now, and carries a bar beneath it. Runa's format.
        Assert.Contains("2010", values[3].TextContent);
        Assert.Contains("2025", values[3].TextContent);
        Assert.NotNull(values[3].QuerySelector(".munin-explorer-period__fill"));

        // Widest first, and the kilde's short name alongside its full one — the card has room for
        // neither the kildetype above it nor the abbreviation the register is known by.
        Assert.Equal(["Nasjonalt medisinsk kvalitetsregister", "Als registeret (ALS)", "Inklusjon"],
                     values[1].QuerySelectorAll("ol > li").Select(l => l.TextContent));

        Assert.Equal(["Funksjonsscore"], values[2].QuerySelectorAll("li").Select(l => l.TextContent));

        // Kodeverk moved to the Data tab — Runa splits the panel into what the variable IS and
        // what its data holds, and the kodeverk is the latter. Which kind a link is says what
        // "2336" alone cannot: a kildekodeverk the register defined is not a national
        // classification. It is a heading over the links of that kind rather than a prefix on each.
        cut.Find("[role=tab][aria-selected=false]").Click();

        Assert.Equal(["Kildekodeverk", "Administrativt kodeverk"], KodeverkGroupHeadings(cut));

        // This fake serves no codes for 2336, so the nameless link says the endpoint published none
        // rather than guessing — and keeps its reference, which is then all that identifies it.
        Assert.Equal(["Ingen kodeverdier tilgjengelig", "ICD-10"],
                     KodeverkLines(cut).Select(l => l.QuerySelector(".munin-explorer-kodeverk__name")!.TextContent));
        Assert.Equal(["Referanse: 2336", "Referanse: 2.16.578.1.12.4.1.1.7110"],
                     KodeverkLines(cut).Select(l => l.QuerySelector(".munin-explorer-kodeverk__reference")!.TextContent));
    }

    [Fact]
    public void Detail_WhenAValueIsMissing_ThenTheRowStillDrawsAndSaysSo()
    {
        // A variable with nothing but a name is a normal row in this catalogue, and a panel that
        // renders nothing for it would look like a panel that failed to load.
        var bare = new VariableDetail { Id = TaleId, Code = "K", PreferredTerm = "1. Tale" };
        var cut = RenderWith(new DetailClient(OnePage(Row(TaleId, "1. Tale"))).Knows(bare));

        Toggles(cut)[0].Click();

        Assert.All(Values(cut), v => Assert.Equal("Ikke oppgitt", v.TextContent));
    }

    [Fact]
    public void Detail_WhenTheOpenRowIsPressedAgain_ThenThePanelCloses()
    {
        // What aria-expanded promises: the same press that opened it closes it. The button itself
        // stays exactly where it was, so the focus does not go anywhere.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();
        Toggles(cut)[0].Click();

        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal("false", Toggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Equal("1. Tale", Toggles(cut)[0].TextContent);
    }

    [Fact]
    public void Detail_WhenASecondRowIsOpened_ThenOnlyOnePanelIsEverOpen()
    {
        // One selection, one fetched detail, one thing to tell the host — and a list of 25 rows
        // that cannot be turned into a page of expanded cards with no way back through it.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();
        Toggles(cut)[1].Click();

        Assert.Single(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal("false", Toggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Equal("true", Toggles(cut)[1].GetAttribute("aria-expanded"));
        Assert.Contains("2. Spyttsekresjon", Panel(cut).TextContent);
    }

    [Fact]
    public void Detail_WhenTheFetchFails_ThenThePanelSaysSoAndTheRowsStay()
    {
        // A panel that failed has not changed which rows are on screen, so the failure is reported
        // inside it rather than in the component's own alert region — and the panel stays open,
        // because closing it would take the button that was just pressed out of the document.
        var client = TwoRows();
        var cut = RenderWith(client);

        client.FailDetail = true;
        Toggles(cut)[0].Click();

        Assert.Contains("Kunne ikke hente detaljene nå", Panel(cut).TextContent);
        Assert.Contains("infobox", Panel(cut).QuerySelector("p")!.ClassName!);
        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));

        // The component's own alert region is for the list, and the list is fine.
        Assert.Equal(string.Empty, cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Detail_WhenTheFetchIsRateLimited_ThenThePanelSaysTheReaderAskedTooOften()
    {
        // Opening one row after another is what meets the per-address limiter, so this is the
        // branch a throttled reader is likeliest to reach. "Prøv igjen om litt" in the panel would
        // be advising the next press, which is what the limiter is counting.
        var client = TwoRows();
        var cut = RenderWith(client);

        client.RateLimitDetail = true;
        Toggles(cut)[0].Click();

        Assert.Contains("for mange forespørsler", Panel(cut).TextContent);
        Assert.DoesNotContain("Kunne ikke hente detaljene", Panel(cut).TextContent);

        // Not "Fant ingen detaljer" either: a detail that was refused is not a variable the
        // catalogue does not publish.
        Assert.DoesNotContain("Fant ingen detaljer", Panel(cut).TextContent);

        // Reported where it happened. The rows and the component's own alert region are untouched.
        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Equal(string.Empty, cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public async Task Detail_WhenAReopenedRowsAbandonedFetchIsRateLimited_ThenItIsNotReportedInTheNewPanel()
    {
        // The generation guard, on the 429 branch's own copy of it. The generic branch's copy is
        // pinned by the test below; an inverted or forgotten comparison on this one would paint an
        // abandoned fetch's sentence over a panel the reader is still waiting on, with nothing else
        // failing.
        var client = TwoRows();
        var cut = RenderWith(client);

        client.StallDetail = true;
        Toggles(cut)[0].Click();
        Toggles(cut)[0].Click();
        Toggles(cut)[0].Click();

        Assert.Equal(2, client.Stalls);

        await cut.InvokeAsync(client.RateLimitStalled);

        Assert.Equal("true", Panel(cut).GetAttribute("aria-busy"));
        Assert.Contains("Henter detaljer", Panel(cut).TextContent);
        Assert.DoesNotContain("for mange forespørsler", Panel(cut).TextContent);

        // And the fetch that does own the panel still gets to fill it.
        await cut.InvokeAsync(() => client.AnswerStalled(Detail(TaleId)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Panel(cut).GetAttribute("aria-busy"));
            Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);
        });
    }

    [Fact]
    public void Detail_WhenTheVariableIsNotPublished_ThenItSaysSoRatherThanOfferingARetry()
    {
        // The client answers null for something that is not published rather than throwing, and
        // "prøv igjen om litt" is advice that would never come good for it.
        var cut = RenderWith(new DetailClient(OnePage(Row(TaleId, "1. Tale"))));

        Toggles(cut)[0].Click();

        Assert.Contains("Fant ingen detaljer", Panel(cut).TextContent);
        Assert.DoesNotContain("Prøv igjen", Panel(cut).TextContent);
    }

    [Fact]
    public void Detail_WhenHistoricalVariablesAreShown_ThenTheDetailIsAskedForThemToo()
    {
        // The endpoint hides them by default. Without this a reader who turned "Vis historiske" on
        // would be told that a row they are looking at does not exist.
        var client = TwoRows();
        var cut = RenderWith(client, b => b.Add(c => c.Filter, new VariableFilter { IncludeHistorical = true }));

        Toggles(cut)[0].Click();

        Assert.True(client.LastIncludeHistorical);
    }

    [Fact]
    public void Detail_WhenTheReaderOpensAndClosesIt_ThenTheHostIsToldWhatIsOpen()
    {
        // The host writes this into its own URL, which is the whole of what "surfaced via
        // parameters" buys: a reader's place in the catalogue can be linked to.
        var reported = new List<Guid?>();
        var cut = RenderWith(TwoRows(), b => b.Add(c => c.SelectedVariableIdChanged, id => reported.Add(id)));

        Toggles(cut)[0].Click();
        Toggles(cut)[0].Click();

        Assert.Equal([TaleId, null], reported);
    }

    [Fact]
    public void Detail_WhenTheHostSuppliesASelection_ThenThatRowIsOpenOnTheFirstRender()
    {
        // The other half of the round trip: a shared link has to land with the panel open, not
        // merely with the row somewhere in the list.
        var client = TwoRows();

        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableId, (Guid?)SpyttId));

        Assert.Equal(SpyttId, client.LastDetailId);
        Assert.Equal("true", Toggles(cut)[1].GetAttribute("aria-expanded"));
        Assert.Contains("2. Spyttsekresjon", Panel(cut).TextContent);
    }

    [Fact]
    public void Detail_WhenTheHostSuppliesAVariableTheResultDoesNotHold_ThenItIsDroppedAndTheHostIsTold()
    {
        // The panel is drawn inside its own row, so an id the result does not contain is a
        // selection nothing can render. Fetching it would be a request for a panel with nowhere to
        // go, and leaving it set would let it spring open the moment the reader paged past that row.
        var client = TwoRows();
        Guid? reported = SpyttId;

        RenderWith(client,
                   b => b.Add(c => c.SelectedVariableId, (Guid?)Guid.NewGuid())
                         .Add(c => c.SelectedVariableIdChanged, id => reported = id));

        Assert.Equal(0, client.DetailCalls);
        Assert.Null(reported);
    }

    [Fact]
    public void Detail_WhenANewSearchLeavesTheOpenRowBehind_ThenThePanelClosesAndTheHostIsTold()
    {
        // Otherwise the selection is state the reader can neither see nor get rid of, and the
        // host's URL names a variable the page is not showing.
        var client = TwoRows();
        var reported = new List<Guid?>();
        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableIdChanged, id => reported.Add(id)));

        Toggles(cut)[0].Click();
        client.Answer = OnePage(Row(Guid.NewGuid(), "3. Svelging"));
        cut.Find("button[type=submit]").Click();

        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal([TaleId, null], reported);
    }

    [Fact]
    public async Task Detail_WhenASlowAnswerArrivesAfterAnotherRowWasOpened_ThenItIsDropped()
    {
        // Two rows opened in quick succession are two requests in flight, and nothing says the
        // first answers first. Without the guard the slower one paints itself under the other
        // row's heading — a panel describing a variable the reader is not looking at, which reads
        // as correct rather than as broken.
        var client = TwoRows();
        var cut = RenderWith(client);

        client.StallDetail = true;
        Toggles(cut)[0].Click();

        client.StallDetail = false;
        Toggles(cut)[1].Click();

        await cut.InvokeAsync(() => client.AnswerStalled(Detail(TaleId)));

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".munin-explorer-detail"));
            Assert.Contains("2. Spyttsekresjon", Panel(cut).TextContent);
            Assert.DoesNotContain("«1. Tale»", Panel(cut).TextContent);
        });
    }

    [Fact]
    public async Task Detail_WhenAReopenedRowsAbandonedFetchFails_ThenItIsNotReportedInTheNewPanel()
    {
        // Close-then-reopen is two fetches carrying one id, so a guard on the id alone would let the
        // first — already thrown away — answer for the second. Its failure would be written into a
        // panel that is still waiting and announced in the panel's own live region, then replaced
        // when the request that is actually running lands.
        var client = TwoRows();
        var cut = RenderWith(client);

        client.StallDetail = true;
        Toggles(cut)[0].Click();
        Toggles(cut)[0].Click();
        Toggles(cut)[0].Click();

        Assert.Equal(2, client.Stalls);

        await cut.InvokeAsync(client.FailStalled);

        Assert.Equal("true", Panel(cut).GetAttribute("aria-busy"));
        Assert.Contains("Henter detaljer", Panel(cut).TextContent);
        Assert.DoesNotContain("Kunne ikke hente detaljene", Panel(cut).TextContent);

        // And the fetch that does own the panel still gets to fill it.
        await cut.InvokeAsync(() => client.AnswerStalled(Detail(TaleId)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Panel(cut).GetAttribute("aria-busy"));
            Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);
        });
    }

    [Fact]
    public void Detail_WhenASearchFailsWithAPanelOpen_ThenTheSelectionGoesWithTheRows()
    {
        // A failed search clears the rows, so the panel leaves the document with them. The
        // selection has to go too: left set it is state the reader can neither see nor get rid of,
        // and the host's URL would keep naming a variable the page is no longer showing.
        var client = TwoRows();
        var reported = new List<Guid?>();
        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableIdChanged, id => reported.Add(id)));

        Toggles(cut)[0].Click();
        client.Then(null);
        cut.Find("button[type=submit]").Click();

        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal([TaleId, null], reported);
    }

    [Fact]
    public void Detail_WhenASearchRecoversFromAFailure_ThenTheClosedPanelDoesNotSpringBackOpen()
    {
        // The other half of the same rule. With the selection dropped there is nothing left to
        // reopen from, so the rows come back closed rather than the panel re-rendering the payload
        // fetched for the search before last — a detail nothing has confirmed is still current.
        var client = TwoRows();
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();
        client.Then(null);
        cut.Find("button[type=submit]").Click();
        cut.Find("button[type=submit]").Click();

        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal("false", Toggles(cut)[0].GetAttribute("aria-expanded"));

        // Opening it again is a fetch, not a redraw of what was kept.
        Toggles(cut)[0].Click();

        Assert.Equal(2, client.DetailCalls);
    }

    [Fact]
    public void Detail_WhenAFailedRetreatRollsThePageBack_ThenTheOpenPanelComesBackWithIt()
    {
        // The empty page 2 closes the panel on the way past, and then the retreat to page 1 fails.
        // _page and _result are put back, and the panel is part of the same undo: without it the
        // reader is left standing on the row they opened, shut, with their URL no longer naming it.
        var page1 = new Page<VariableSummary>
        {
            Items = [Row(TaleId, "1. Tale")],
            TotalCount = 30,
            PageNumber = 1,
            Size = 25,
            TotalPages = 2
        };
        var client = new DetailClient(page1).Knows(Detail(TaleId));
        var reported = new List<Guid?>();
        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableIdChanged, id => reported.Add(id)));

        Toggles(cut)[0].Click();

        // Page 2 comes back empty the way a 404 does, and the retreat to page 1 throws.
        client.Then(new Page<VariableSummary>()).Then(null);
        Next(cut).Click();

        Assert.Equal("1/2", Position(cut));
        Assert.Single(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);

        // Told null on the way through and told the id again on the way back, so the URL it ends up
        // with is the row that is on screen.
        Assert.Equal([TaleId, null, TaleId], reported);

        // Rolled back, not re-fetched: the payload put back is the one that described these rows.
        Assert.Equal(1, client.DetailCalls);

        // Three searches and no more: the first render, the page turn, and the retreat that failed.
        // A rollback that searched again would be the second failure the undo exists to avoid.
        Assert.Equal(3, client.SearchCalls);
    }

    [Fact]
    public async Task Detail_WhenAFailedRetreatRollsBackAPanelCaughtMidFetch_ThenItIsAskedForAgain()
    {
        // The other arm of the same rollback. The panel was captured with nothing in it — its own
        // detail fetch was still running when the page turn closed it — so there is no payload to
        // put back and the reopened panel has to ask again. The one place in the component that
        // starts a request from inside a rollback.
        var page1 = new Page<VariableSummary>
        {
            Items = [Row(TaleId, "1. Tale")],
            TotalCount = 30,
            PageNumber = 1,
            Size = 25,
            TotalPages = 2
        };
        var client = new DetailClient(page1).Knows(Detail(TaleId));
        var reported = new List<Guid?>();
        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableIdChanged, id => reported.Add(id)));

        client.StallDetail = true;
        Toggles(cut)[0].Click();

        // Page 2 comes back empty the way a 404 does, and the retreat to page 1 throws — with the
        // panel's first fetch still hanging, so what CapturePanel recorded is an empty panel.
        client.Then(new Page<VariableSummary>()).Then(null);
        Next(cut).Click();

        // Reopened and asking again, rather than put back blank and left that way for good.
        Assert.Equal("1/2", Position(cut));
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Equal("true", Panel(cut).GetAttribute("aria-busy"));
        Assert.Contains("Henter detaljer", Panel(cut).TextContent);
        Assert.Equal(2, client.DetailCalls);

        // The host has only been told the panel closed. On this arm the id waits on the re-fetch,
        // so a slow one leaves the URL naming nothing while the panel is open on screen.
        Assert.Equal([null], reported);

        // The fetch the page turn disowned now fails. Its failure belongs to a panel that is gone,
        // so the generation the rollback claimed has to keep it out of the one on screen.
        await cut.InvokeAsync(client.FailStalled);

        Assert.Equal("true", Panel(cut).GetAttribute("aria-busy"));
        Assert.DoesNotContain("Kunne ikke hente detaljene", Panel(cut).TextContent);

        // And the fetch the rollback started fills the panel it started for.
        await cut.InvokeAsync(() => client.AnswerStalled(Detail(TaleId)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Panel(cut).GetAttribute("aria-busy"));
            Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);
            Assert.Equal(TaleId, reported[^1]);
        });

        Assert.Equal(2, client.DetailCalls);
    }

    [Fact]
    public async Task Detail_WhenThePanelIsClosedWhileARollbackRefetchesIt_ThenTheHostIsToldWhatIsOpen()
    {
        // The rollback's re-fetch yields with the rows back on screen and clickable, so the reader
        // can shut the panel again before it answers. What the host is told has to be what is open
        // — the captured id would leave the URL naming a variable whose panel is closed.
        var page1 = new Page<VariableSummary>
        {
            Items = [Row(TaleId, "1. Tale")],
            TotalCount = 30,
            PageNumber = 1,
            Size = 25,
            TotalPages = 2
        };
        var client = new DetailClient(page1).Knows(Detail(TaleId));
        var reported = new List<Guid?>();
        var cut = RenderWith(client, b => b.Add(c => c.SelectedVariableIdChanged, id => reported.Add(id)));

        client.StallDetail = true;
        Toggles(cut)[0].Click();

        client.Then(new Page<VariableSummary>()).Then(null);
        Next(cut).Click();

        // Reopened by the rollback and shut again by the reader while its fetch hangs.
        Toggles(cut)[0].Click();

        Assert.Empty(cut.FindAll(".munin-explorer-detail"));

        // Oldest first: the page turn's abandoned fetch, then the rollback's, which is the one
        // whose continuation reports to the host.
        await cut.InvokeAsync(client.FailStalled);
        await cut.InvokeAsync(client.FailStalled);

        cut.WaitForAssertion(() => Assert.Equal(4, reported.Count));

        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.All(reported, Assert.Null);
    }

    [Fact]
    public void Detail_WhenTheVariableIsInSeveralVariabelgrupper_ThenEveryOneIsListed()
    {
        // The reason the panel reads the whole list rather than the primary name: a variable in
        // three groups written up under one is a half-truth the payload already has the answer to.
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale")))
            .Knows(Detail(TaleId) with
            {
                VariabelgruppeName = "Funksjonsscore",
                AllVariabelgrupper =
                [
                    new() { Id = Bakgrunn, Name = "Funksjonsscore" },
                    new() { Id = Levekaar, Name = "ALSFRS-R" },
                    new() { Id = Guid.NewGuid(), Name = "Pustefunksjon" }
                ]
            });
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();

        Assert.Equal(["Funksjonsscore", "ALSFRS-R", "Pustefunksjon"],
                     Values(cut)[2].QuerySelectorAll("li").Select(l => l.TextContent));
    }

    [Fact]
    public void Detail_WhenThePayloadCarriesNoVariabelgruppeList_ThenThePrimaryNameStandsIn()
    {
        // The documented fallback: a payload with the primary name and no list still says which
        // group the variable is in, rather than dropping to "Ikke oppgitt".
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale")))
            .Knows(Detail(TaleId) with
            {
                VariabelgruppeName = "Funksjonsscore",
                AllVariabelgrupper = []
            });
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();

        Assert.Equal(["Funksjonsscore"],
                     Values(cut)[2].QuerySelectorAll("li").Select(l => l.TextContent));
    }

    [Fact]
    public void Detail_WhenTheKildeHasNoShortName_ThenTheTrailDrawsNoEmptyParentheses()
    {
        // The arm most payloads take: KildeShortName defaults to empty, and a kilde without an
        // abbreviation must not be written up as "Als registeret ()".
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale")))
            .Knows(Detail(TaleId) with { KildeShortName = "" });
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();

        Assert.Equal(["Nasjonalt medisinsk kvalitetsregister", "Als registeret", "Inklusjon"],
                     Values(cut)[1].QuerySelectorAll("ol > li").Select(l => l.TextContent));
    }

    [Fact]
    public void Detail_WhenTheKildeShortNameOnlyRestatesTheFullName_ThenItIsWrittenOnce()
    {
        // Why the comparison ignores case: the catalogue writes the same name both ways, and
        // "Als registeret (ALS REGISTERET)" is the register's name said twice in one breath.
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale")))
            .Knows(Detail(TaleId) with { KildeShortName = "ALS REGISTERET" });
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();

        Assert.Equal(["Nasjonalt medisinsk kvalitetsregister", "Als registeret", "Inklusjon"],
                     Values(cut)[1].QuerySelectorAll("ol > li").Select(l => l.TextContent));
    }

    [Fact]
    public void Detail_WhileItIsLoading_ThenThePanelSaysSoAndIsMarkedBusy()
    {
        var client = TwoRows();
        var cut = RenderWith(client);

        client.StallDetail = true;
        Toggles(cut)[0].Click();

        Assert.Equal("true", Panel(cut).GetAttribute("aria-busy"));
        Assert.Contains("Henter detaljer", Panel(cut).TextContent);

        // Never disabled, including while its own fetch runs: disabling the element that has
        // focus drops focus to <body>, which is the rule the Søk button and the pager follow.
        Assert.False(Toggles(cut)[0].HasAttribute("disabled"));
    }

    [Fact]
    public void Detail_WhenThePanelShowsTheDescription_ThenTheCardDoesNotRepeatIt()
    {
        // The two come from different payloads but say the same thing, and printing both would
        // put one paragraph on screen twice inside one card.
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale", "Hvordan er talen?")))
            .Knows(Detail(TaleId) with { Description = "Hvordan er talen?" });
        var cut = RenderWith(client);
        // The description is not in the row any more — see the panel.

        Toggles(cut)[0].Click();
        // The description is not in the row any more — see the panel.
        Assert.Equal("Hvordan er talen?", Values(cut)[0].TextContent);
    }

    [Fact]
    public void Detail_Always_ThenTheToggleIsWiredToThePanelAndNamedAfterItsRow()
    {
        // Twenty-five buttons all called "Vis detaljer" say nothing about which row they open when
        // a screen reader lists them out of context. Pointing at the button's own words and then
        // at the heading names it "Vis detaljer 1. Tale" and keeps each half in its own language,
        // which an aria-label could not: the words follow Language, the variable's name is
        // Norwegian whatever the surrounding UI is.
        var cut = RenderWith(TwoRows());
        // The heading wraps the button; the panel points at the heading, which is what holds
        // the row's name in the document outline.
        // The name button is the row's name — there is no heading wrapping it any more.
        var heading = cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name")[0];

        // Closed: nothing to control yet, and aria-controls pointing at an element that is not in
        // the document is a dangling reference.
        Assert.False(Toggles(cut)[0].HasAttribute("aria-controls"));
        // The disclosure IS the heading's text now, so it names itself. The old wiring pointed
        // aria-labelledby at a separate heading because the button said only "Vis detaljer" and
        // would otherwise have read as forty identical buttons. That reason is gone, and an
        // aria-labelledby repeating the element's own content is noise.
        Assert.False(Toggles(cut)[0].HasAttribute("aria-labelledby"));
        Assert.Equal(heading.TextContent.Trim(), Toggles(cut)[0].TextContent.Trim());

        Toggles(cut)[0].Click();

        Assert.Equal(Panel(cut).Id, Toggles(cut)[0].GetAttribute("aria-controls"));
        Assert.Equal("region", Panel(cut).GetAttribute("role"));
        Assert.Equal(heading.Id, Panel(cut).GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Detail_WhenTwoInstancesShareAPage_ThenTheirPanelIdsDoNotCollide()
    {
        // Two explorers on one page can list the same variable, and a duplicated id is a WCAG
        // 4.1.1 failure as well as pointing both panels' wiring at whichever came first.
        Services.AddSingleton<IMuninExplorerClient>(TwoRows());

        var a = Render<VariableExplorer>();
        var b = Render<VariableExplorer>();

        Toggles(a)[0].Click();
        Toggles(b)[0].Click();

        Assert.NotEqual(Panel(a).Id, Panel(b).Id);
        Assert.NotEqual(Toggles(a)[0].Id, Toggles(b)[0].Id);
    }

    [Fact]
    public void Detail_WhenTheLanguageIsEn_ThenThePanelIsEnglishAndTheCatalogueStaysNorwegian()
    {
        // The UI turns English; Munin's metadata does not. The kildetype is the one step of the
        // trail that is our prose rather than a name out of the catalogue, so it is the one step
        // that follows Language — and the one that must not be announced as Norwegian.
        var cut = RenderWith(TwoRows(), b => b.Add(c => c.Language, "en"));

        // The disclosure is the variable's own name now, so it reads the same in either
        // language — the catalogue is Norwegian whatever the UI is.
        Assert.Equal("1. Tale", Toggles(cut)[0].TextContent);

        Toggles(cut)[0].Click();

        Assert.Equal(["Description", "Source path", "Variable group", "Data period"],
                     Panel(cut).QuerySelectorAll("dl dt").Select(t => t.TextContent));

        var trail = Values(cut)[1].QuerySelectorAll("ol > li");

        Assert.Equal("National medical quality registry", trail[0].TextContent);
        Assert.False(trail[0].HasAttribute("lang"));
        Assert.Equal("no", trail[1].GetAttribute("lang"));
        Assert.Equal("1. Tale", Toggles(cut)[0].TextContent);
    }

    [Fact]
    public void Render_WhenAPanelIsOpen_ThenItIsBuiltFromShapesRatherThanFromNewClassNames()
    {
        // The rule this guards has changed, and it is worth being precise about what it is now.
        // The component used to wear helsedata's variable-page vocabulary, so several names in
        // this prefix were THEIRS rather than ours - what are now munin-explorer-container
        // (10 rules), munin-explorer-results (6), and the column picker's header trio plus its
        // dropdown all sat in their variables.css, loaded on every page of their site. After the
        // rename they are ours, and their rules live in Stiler under components/munin-explorer/.
        //
        // That collapses the old two-way split, but not the thing it protected. What must never
        // grow is the list of names we INVENT and never style - the handles below, which carry
        // no rule anywhere by design and exist only so a host can find the component in the DOM.
        // A name in this prefix that is neither styled in Stiler nor one of those handles is a
        // name that renders as a raw browser default.
        //
        // Rendered with a hierarchy chosen as well as a panel open, so the trail over the results
        // is on screen: this list is the central registry, and names that only appear under a
        // filter would otherwise be invisible to it and pinned only by their own feature's tests.
        var cut = RenderWith(TwoRows(), b => b.Add(c => c.Filter, new VariableFilter { KildeIds = [Tromso] }));

        Toggles(cut)[0].Click();

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            "munin-explorer",            // ours, a handle
            // The search row's wrapper and the clear button inside it. Both are drawn on every
            // render now: the button is always present and greys out when there is nothing to
            // clear, rather than appearing and disappearing beside a field being typed in.
            "munin-explorer-search",
            "munin-explorer-search__clear",
            "munin-explorer-filters",    // ours, a handle
            // The toolbar row: a container of its own, because in inline flow the last button's
            // trailing margin counted against the line and the row broke apart under a scrollbar.
            "munin-explorer-filters__toolbar",
            "munin-explorer-breadcrumb", // ours — the trail over the results, which Stiler has
                                            // no breadcrumb rule of any kind to borrow
            "munin-explorer-crumb",      // ours — one step of a trail, and the same name in
                                            // both of them: the panel's kilde step, which Runa
                                            // makes a link and we make the control that discloses
                                            // the kilde, and each step of the hierarchy trail,
                                            // which reuses it rather than minting a second name
                                            // for the same affordance
            "munin-explorer-breadcrumb__clear",      // ours — the × that empties the hierarchy
            "munin-explorer-container",  // ours, Stiler components/munin-explorer/
            "munin-explorer-results",    // ours, Stiler components/munin-explorer/
            "munin-explorer-header",     // ours now; their own variable page hangs the
            "munin-explorer-header__actions",        // column picker in, and the ghost button
            "munin-explorer__dropdown",              // that opens it. All four came from variables.css.
            "munin-explorer-header__actions-button",
            "munin-explorer-detail",     // ours, a handle
            "munin-explorer-group",      // ours — helsedata's panel is flat, so it has no
                                            // group heading to borrow a name from
            "munin-explorer-period",     // ours — neither explorer's stylesheet has a period bar
            "munin-explorer-period__range",
            "munin-explorer-period__track",
            "munin-explorer-period__fill",
        ], invented);

        var panel = Panel(cut);

        // The definition list stays a definition list — it is labels and the values they name, and
        // helsedata's grid is class-based (`.variable-meta__grid { display: grid }`, with only a
        // `p { margin: 0 }` rule touching an element name), so their layout applies to dt/dd just
        // as it applies to their own spans. Semantics kept, styling borrowed.
        // Both of helsedata's grid variants are in use: -1 for a group with one field, -2 for the
        // ones with several. Runa's panel does the same.
        Assert.All(panel.QuerySelectorAll("dl"),
                   e => Assert.Contains("munin-explorer-meta__grid", e.ClassName!));
        // The multi-field group wears the plain grid, which is helsedata's two-lane one; a group
        // with a single field wears -1, their single-column variant. Both are theirs.
        Assert.All(panel.QuerySelectorAll("dl"),
                   e => Assert.Matches(@"^munin-explorer-meta__grid( munin-explorer-meta__grid-1)?$", e.ClassName!));
        Assert.All(panel.QuerySelectorAll("ol, ul"), e => Assert.False(e.HasAttribute("class")));
        Assert.All(panel.QuerySelectorAll("dl dt"),
                   e => Assert.Equal("headline headline-xxs margin--none", e.ClassName));
        Assert.Equal("munin-explorer-dataitem-main__name", Toggles(cut)[0].ClassName);
    }

    // ---------------------------------------------------------------------------------
    // The kilde and datasamling panel. The acceptance criterion is that both render from
    // the Explorer API, reached from a variable result row — so the tests that must hold
    // are the two payloads arriving from their own endpoints into the panel a row opened.
    // The rest follow from the panel hanging inside the variable's: it is one at a time,
    // it cannot outlive the variable it belongs to, and a failure at this depth leaves
    // both the variable above it and the rows around it alone.
    // ---------------------------------------------------------------------------------

    private static readonly Guid BiodataId = new("eeeeeeee-0000-0000-0000-000000000003");
    private static readonly Guid ProverId = new("eeeeeeee-0000-0000-0000-000000000004");
    private static readonly Guid Runde2Id = new("eeeeeeee-0000-0000-0000-000000000005");
    private static readonly Guid Runde2SkjemaId = new("eeeeeeee-0000-0000-0000-000000000006");

    /// <summary>
    /// A kilde payload shaped like the captured one, with a delkilde tree two levels deep.
    /// </summary>
    /// <remarks>
    /// The nesting is the point of the fixture rather than decoration: the panel reports how many
    /// datasamlinger the kilde holds, and a count that stopped at the top of the tree would report
    /// a fraction of a study series. There are three here — one on the kilde, one on the delkilde,
    /// one on the delkilde's own child.
    /// </remarks>
    private static KildeDetail Kilde() => new()
    {
        Id = AlsId,
        Code = "K_ALS",
        PreferredTerm = "Als registeret",
        ShortName = "ALS",
        Description = "Norsk register for ALS og andre motonevronsykdommer.",
        Kildetype = "nasjonaltMedisinskKvalitetsregister",
        LegalBasis = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        DataController = "St. Olavs hospital HF",
        DataProcessor = "St. Olavs hospital HF",
        PersonIdentificationLevel = "indirectlyIdentifiable",
        ValidFrom = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
        TotalVariables = 312,
        Datasamlinger = [new() { Id = InklusjonId, Name = "Inklusjon" }],
        Delkilder =
        [
            new()
            {
                Id = BiodataId,
                Code = "K_ALS.BIODATA",
                Name = "Biodata",
                Datasamlinger = [new() { Id = ProverId, Name = "Prøver" }],
                Children =
                [
                    new()
                    {
                        Id = Runde2Id,
                        Code = "K_ALS.BIODATA.R2",
                        Name = "Runde 2",
                        Datasamlinger = [new() { Id = Runde2SkjemaId, Name = "Runde 2, skjema" }]
                    }
                ]
            }
        ]
    };

    /// <summary>
    /// A datasamling payload shaped like the captured one.
    /// </summary>
    /// <remarks>
    /// Every own value is left null and only the <c>Effective…</c> ones are filled, which is what
    /// the real payload for this datasamling looks like: Munin curates dataansvarlig on the kilde
    /// and the datasamling inherits it. A panel drawing the own values would report "Ikke oppgitt"
    /// for all of them, which is why this fixture is shaped this way rather than fully populated.
    /// </remarks>
    private static DatasamlingDetail Datasamling() => new()
    {
        Id = InklusjonId,
        Code = "K_ALS.INKLUSJON",
        PreferredTerm = "Inklusjon",
        Description = "Skjemaet inneholder opplysninger om utredning og oppstart av behandling.",
        ParentKildeId = AlsId,
        ParentKildeName = "Als registeret",
        InclusionAndExclusionCriteria = "Alle pasienter som er 18 år eller eldre.",
        EffectiveDataController = "St. Olavs hospital HF",
        EffectiveDataProcessor = "St. Olavs hospital HF",
        EffectivePersonIdentificationLevel = "indirectlyIdentifiable",
        EffectiveLegalBasis = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        EffectiveValidFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        EffectiveKildetype = "nasjonaltMedisinskKvalitetsregister",
        LastUpdated = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
        // Every datasamling in the test catalogue carries a statistikktype, and it names the
        // statistics heading rather than filling a row of its own.
        StatisticsType = "yearly",
        Frequency = "Fortløpende",
        // Observed as an empty string in the captured payload rather than as null, which is a
        // different thing for the markup to get right than a missing field.
        CountingUnit = "",
        VariableCount = 99
    };

    /// <summary>The two owner toggles, in the order the panel draws them: kilde, then datasamling.</summary>
    /// <summary>Leaves the kilde view and returns to the list.</summary>
    private static void Back(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-drilldown button").Click();

    /// <summary>The buttons that open an owner — the kilde and the datasamling.</summary>
    /// <remarks>
    /// <c>button[id]</c>, not every button in the panel: the whole-variable button sits beside these
    /// and is not one of them. Selecting all of them made this helper mean "every button in the
    /// panel" the moment a third arrived, which is not what its name says or what its callers want.
    /// </remarks>
    private static IReadOnlyList<AngleSharp.Dom.IElement> SourceToggles(IRenderedComponent<VariableExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-detail > button[id]")];

    /// <summary>
    /// The kilde or datasamling view. It is no longer a panel inside the open row — it takes over
    /// the component's area, so the list is not on screen while it is.
    /// </summary>
    private static AngleSharp.Dom.IElement SourcePanel(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-drilldown");

    private static IReadOnlyList<string> SourceLabels(IRenderedComponent<VariableExplorer> cut) =>
        [.. SourcePanel(cut).QuerySelectorAll("dl dt").Select(t => t.TextContent)];

    private static IReadOnlyList<string> SourceValues(IRenderedComponent<VariableExplorer> cut) =>
        [.. SourcePanel(cut).QuerySelectorAll("dl dd").Select(d => d.TextContent)];

    /// <summary>Open the first row's detail panel and then one of its two owners.</summary>
    private IRenderedComponent<VariableExplorer> OpenOwner(DetailClient client, int index)
    {
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();
        SourceToggles(cut)[index].Click();

        return cut;
    }

    [Fact]
    public void Source_WhenTheKildeIsOpened_ThenItsRecordIsShownFromTheApi()
    {
        // Half the acceptance criterion. The kilde arrives from GetKildeAsync — its own endpoint,
        // not a field of the variable — into the panel the row already opened, with no navigation:
        // the component holds no NavigationManager and nothing in a card is a link.
        var client = TwoRows();
        var cut = RenderWith(client);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();

        Assert.Equal(1, client.KildeCalls);
        Assert.Equal(AlsId, client.LastSourceId);
        Assert.Equal(before, navigation.Uri);

        // The sidebar, in Runa's order. Beskrivelse is not in it any more: it moved up to the
        // ingress under the name, where a description belongs and where Runa puts it. The two
        // counts left too — the variable count is under Statistikk, and the datasamling count is
        // the table rather than a number.
        Assert.Equal(
            ["Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin",
             "Totalt antall variabler", "Dataperiode"],
            SourceLabels(cut));

        var values = SourceValues(cut);

        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", values[0]);
        Assert.Equal("St. Olavs hospital HF", values[2]);
        Assert.Equal("Indirekte identifiserbar", values[4]);
        Assert.Equal("312", values[7]);

        // The description reads as prose above the metadata, not as a row in a record.
        Assert.Equal(
            "Norsk register for ALS og andre motonevronsykdommer.",
            SourcePanel(cut).QuerySelector(".munin-explorer-kilde__description")!.TextContent.Trim());
    }

    [Theory]
    [InlineData(null, "1. januar 2023")]
    [InlineData("en", "1 January 2023")]
    public void Source_WhenADateIsShown_ThenItIsWrittenTheWayTheReadersLanguageWritesDates(
        string? language,
        string expected)
    {
        // The dot after the day is not punctuation, it is what makes the number an ordinal in
        // Norwegian. English does not use it, so handing an English reader the Norwegian skeleton
        // with English month names — "1. January 2023" — is neither language.
        var cut = RenderWith(TwoRows(), b => b.Add(c => c.Language, language));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();

        Assert.Contains(expected, SourceValues(cut)[5]);
    }

    [Fact]
    public void Source_WhenDatasamlingerShareASortOrder_ThenTheyFallBackToNorwegianAlphabetical()
    {
        // The names being sorted are the catalogue's, stored once in Norwegian, so å sorts last
        // whoever is reading. Sorting by the reader's culture would give an English reader a
        // different order from a Norwegian colleague looking at the same source, and sorting by the
        // thread's would make it depend on whatever the host happened to set.
        var client = TwoRows().Knows(Kilde() with
        {
            Datasamlinger = [Datasamling("Ålesund"), Datasamling("Bergen"), Datasamling("Oslo")],
            Delkilder = [],
        });

        var cut = RenderWith(client, b => b.Add(c => c.Language, "en"));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();

        var names = SourcePanel(cut).QuerySelectorAll("table tbody th").Select(e => e.TextContent);

        Assert.Equal(["Bergen", "Oslo", "Ålesund"], names);
    }

    private static KildeDatasamling Datasamling(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = name,
        VariableCount = 1,
    };

    [Fact]
    public void Source_WhenTheKildeHasADelkildeTree_ThenTheDatasamlingerAreCountedThroughIt()
    {
        // A study series keeps its datasamlinger one per wave, under nested delkilder. Counting
        // only the kilde's own would report 1 where the reader can reach 3, which reads as a small
        // register rather than as a miscount.
        var cut = OpenOwner(TwoRows(), 0);

        // Once the table replaced the flat count, this got stricter rather than weaker: all three
        // have to be reachable as rows, not merely add up to a number.
        Assert.Equal(3, SourcePanel(cut).QuerySelectorAll("table tbody tr").Length);
    }

    [Fact]
    public void Source_WhenTheDatasamlingIsOpened_ThenItsEffectiveValuesAreShownFromTheApi()
    {
        // The other half of the acceptance criterion, and the inheritance rule with it: this
        // datasamling sets none of dataansvarlig, databehandler, lovverk or identification level
        // itself. Drawing its own values would report "Ikke oppgitt" four times for a datasamling
        // whose controller is perfectly well known one level up.
        var client = TwoRows();

        var cut = OpenOwner(client, 1);

        Assert.Equal(1, client.DatasamlingCalls);
        Assert.Equal(InklusjonId, client.LastSourceId);

        // The sidebar, in the order the kilde view puts the same fields in. Beskrivelse and the
        // criteria are not in it: both are prose and both moved into the main column, where a
        // description and a page of inclusion criteria can be read (Fhi.Metadata-jgfum).
        Assert.Equal(
            ["Kilde", "Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin",
             "Frekvens", "Antall variabler"],
            SourceLabels(cut));

        var values = SourceValues(cut);

        Assert.Equal("Als registeret", values[0]);
        Assert.Equal("St. Olavs hospital HF", values[3]);
        Assert.Equal("Indirekte identifiserbar", values[5]);
        Assert.Equal("1. januar 2010 – Pågående", values[6]);
        Assert.Equal("Fortløpende", values[8]);
        Assert.Equal("99", values[9]);

        var panel = SourcePanel(cut);

        // Telleenhet arrives as an empty string, and an empty string draws no row at all rather
        // than a label over "Ikke oppgitt" — the rule the two sidebar boxes already follow.
        Assert.DoesNotContain("Telleenhet", panel.TextContent);

        Assert.Equal(
            "Skjemaet inneholder opplysninger om utredning og oppstart av behandling.",
            panel.QuerySelector(".munin-explorer-datasamling__description")!.TextContent.Trim());

        Assert.Equal(
            "Alle pasienter som er 18 år eller eldre.",
            panel.QuerySelector(".munin-explorer-datasamling__criteria")!.TextContent.Trim());
    }

    [Fact]
    public void Source_WhenTheOtherOwnerIsOpened_ThenItReplacesTheFirstRatherThanStacking()
    {
        // One owner at a time, the rule the variable panels themselves follow. Two open at once
        // inside one result card is a card nobody can find their way back out of.
        var cut = OpenOwner(TwoRows(), 0);

        // Both owners cannot be open at once because only one view is on screen at a time; the
        // reader returns to the row and opens the other from there.
        Back(cut);
        SourceToggles(cut)[1].Click();

        Assert.Single(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Equal("Inklusjon", SourcePanel(cut).QuerySelector(".headline-s")!.TextContent);

        // The datasamling view shows the datasamling, not a count that belongs to the kilde.
        Assert.DoesNotContain("Antall datasamlinger", SourcePanel(cut).TextContent);

        Back(cut);

        // Only one owner is ever marked open, because only one view is ever on screen.
        Assert.Equal("false", SourceToggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Equal("false", SourceToggles(cut)[1].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Source_WhenBackIsPressed_ThenTheListReturnsAndNothingIsRefetched()
    {
        // The toggle is how the panel is closed, which is why it is never disabled while its own
        // fetch runs — the same rule the disclosure above it follows.
        var client = TwoRows();
        var cut = OpenOwner(client, 0);

        // The kilde takes over the component's area, so the way out is the back control rather
        // than pressing a toggle that is no longer on screen.
        Back(cut);

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));

        // The list comes back as it was — the row is still open, because nothing was torn down.
        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Equal("false", SourceToggles(cut)[0].GetAttribute("aria-expanded"));

        // Closing asks the API for nothing: it is the panel that is being dropped, not the answer.
        Assert.Equal(1, client.KildeCalls);
    }

    /// <summary>
    /// The drill-in's second way out: back to the list, narrowed to the owner on screen.
    /// </summary>
    /// <remarks>
    /// Found by its own words among the region's direct children rather than by index, so it
    /// cannot silently start matching a button <see cref="KildeView"/> draws inside the same
    /// region — and so a rename fails this helper rather than moving the click onto "Tilbake".
    /// </remarks>
    private static AngleSharp.Dom.IElement NarrowButton(IRenderedComponent<VariableExplorer> cut) =>
        SourcePanel(cut).Children
            .Where(child => child.TagName == "BUTTON")
            .Single(button => button.TextContent.StartsWith("Vis bare variabler", StringComparison.Ordinal));

    [Fact]
    public void Source_WhenTheKildesVariablesAreAskedFor_ThenTheViewClosesAndTheFilterIsSet()
    {
        // The acceptance criterion, and the trap it warns about. A list that merely came back
        // shorter would satisfy a row count and still be wrong: the filter has to be SET, so the
        // facet panel shows it as active and the reader can take it off again. FilterChanged is
        // what the component reports as actually in force, and it is the same value the host
        // writes into its own URL — which is what makes the narrowed list a shareable link.
        VariableFilter? reported = null;
        var client = TwoRows();
        var cut = RenderWith(client, b => b.Add(c => c.FilterChanged, f => reported = f));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();
        NarrowButton(cut).Click();

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Equal(new VariableFilter { KildeIds = [AlsId] }, reported);
    }

    [Fact]
    public void Source_WhenTheDatasamlingsVariablesAreAskedFor_ThenTheFilterNamesTheDatasamling()
    {
        // The sibling the bead asks for by name. The datasamling view had the same gap and gets
        // the same button; what differs is only which facet it writes, so a shared handler that
        // wrote KildeIds for both would pass the kilde test and quietly mis-file this one.
        VariableFilter? reported = null;
        var client = TwoRows();
        var cut = RenderWith(client, b => b.Add(c => c.FilterChanged, f => reported = f));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[1].Click();
        NarrowButton(cut).Click();

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Equal(new VariableFilter { DatasamlingIds = [InklusjonId] }, reported);
    }

    [Fact]
    public void Source_WhenTheKildesVariablesAreAskedFor_ThenTheReadersOtherFacetsSurvive()
    {
        // The button narrows to one kilde; it does not start a new search. The facets the reader
        // already set are what the variable was found under, and dropping them silently would
        // undo work the button never mentions — and hand back a wider list than the one they
        // drilled in from, which is the opposite of what it says it does.
        VariableFilter? reported = null;
        var client = TwoRows();
        var cut = RenderWith(client, b => b
            .Add(c => c.Filter, new VariableFilter { DataTypes = ["Integer"] })
            .Add(c => c.FilterChanged, f => reported = f));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();
        NarrowButton(cut).Click();

        Assert.Equal(new VariableFilter { DataTypes = ["Integer"], KildeIds = [AlsId] }, reported);
    }

    [Fact]
    public void Source_WhenAnotherKildeWasAlreadyChosen_ThenNarrowingReplacesItRatherThanAdding()
    {
        // "Vis bare variabler fra denne datakilden" has to mean bare. Appending to KildeIds is
        // what the facet panel's own toggle does, and it would widen the list to two kilder while
        // the button claims to have narrowed it to one.
        var other = new Guid("eeeeeeee-0000-0000-0000-0000000000ff");
        VariableFilter? reported = null;
        var client = TwoRows();
        var cut = RenderWith(client, b => b
            .Add(c => c.Filter, new VariableFilter { KildeIds = [other] })
            .Add(c => c.FilterChanged, f => reported = f));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();
        NarrowButton(cut).Click();

        Assert.Equal(new VariableFilter { KildeIds = [AlsId] }, reported);
    }

    [Fact]
    public void Source_WhenTheListIsShowing_ThenThereIsNoNarrowingButton()
    {
        // It belongs to the drill-in, not to the list: on the list there is no owner to narrow to,
        // and a button offering to filter by "this kilde" with no kilde on screen has nothing to
        // name. This is what SourceVariablesText returning null buys.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();

        Assert.DoesNotContain("Vis bare variabler", cut.Markup);
    }

    [Fact]
    public void Source_WhenOpened_ThenTheToggleAndTheRegionAreWiredToEachOther()
    {
        // aria-controls only on the toggle that opened it: both buttons point at the same panel, so
        // the closed one carrying the reference too would be read as one region with two names.
        var cut = OpenOwner(TwoRows(), 0);

        var panel = SourcePanel(cut);

        Assert.Equal("region", panel.GetAttribute("role"));

        // Named after the thing it shows, and the catalogue keeps its own language.
        var heading = cut.Find($"#{panel.GetAttribute("aria-labelledby")}");

        Assert.Equal("Als registeret", heading.TextContent);

        // Unmarked here, and that is the point: the reader is already Norwegian, so lang="no" would
        // assert something the page already says. It appears on an English page — see
        // Source_WhenTheLanguageIsEn_ThenThePanelIsEnglishAndTheCatalogueStaysNorwegian.
        Assert.Null(heading.GetAttribute("lang"));

        Back(cut);

        // Back on the list, the toggle that opened it reads shut again, and the one never
        // pressed points at nothing.
        Assert.Equal("false", SourceToggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Null(SourceToggles(cut)[1].GetAttribute("aria-controls"));
    }

    [Fact]
    public void Source_WhenTheFetchFails_ThenTheOwnerPanelSaysSoAndLeavesEverythingElseAlone()
    {
        // What failed is one panel inside one panel inside one card. Reporting it in the
        // component's own alert region would say the whole list was stale, and reporting it in the
        // variable's would say the variable was.
        var client = TwoRows();
        client.FailSource = true;

        var cut = OpenOwner(client, 0);

        Assert.Contains("Kunne ikke hente datakilden", SourcePanel(cut).TextContent);
        Assert.Contains("infobox", SourcePanel(cut).QuerySelector("p")!.ClassName!);

        // The variable above it is untouched, and so are the rows.

        // Nothing behind the view was disturbed by the failure.
        Back(cut);

        Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);
        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Empty(cut.FindAll("div[role='alert'] p"));
    }

    [Fact]
    public void Source_WhenTheFetchIsRateLimited_ThenTheOwnerPanelSaysTheReaderAskedTooOften()
    {
        // One sentence for both owner kinds, unlike the generic branch: which endpoint the limiter
        // refused is not what the reader has to know, and it changes nothing about waiting.
        var client = TwoRows();
        client.RateLimitSource = true;

        var cut = OpenOwner(client, 0);

        Assert.Contains("for mange forespørsler", SourcePanel(cut).TextContent);
        Assert.DoesNotContain("Kunne ikke hente datakilden", SourcePanel(cut).TextContent);
        Assert.DoesNotContain("Fant ingen detaljer", SourcePanel(cut).TextContent);

        // Reported where it happened, like the generic failure: nothing behind the view moved.
        Back(cut);

        Assert.Contains("Angir pasientens grad av utfall", Panel(cut).TextContent);
        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Empty(cut.FindAll("div[role='alert'] p"));
    }

    [Fact]
    public void Source_WhenTheCatalogueHasNoSuchDatasamling_ThenItSaysSoRatherThanAskingForARetry()
    {
        // Null is not a failure — the client answers it for something that is not published — so
        // "try again in a moment" would be advice that never comes good.
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale"))).Knows(Detail(TaleId));

        var cut = OpenOwner(client, 1);

        Assert.Contains("Fant ingen detaljer for denne datasamlingen", SourcePanel(cut).TextContent);
        Assert.Empty(SourcePanel(cut).QuerySelectorAll("dl"));
    }

    [Fact]
    public void Source_WhenAnotherVariableIsOpened_ThenTheOwnerPanelDoesNotFollowIt()
    {
        // The owner is drawn from the variable's own detail, so it cannot survive that detail being
        // replaced: left behind, the first row's kilde would sit under the second row's name until
        // its own fetch landed.
        var cut = OpenOwner(TwoRows(), 0);

        // Back to the list, then open a different variable: its owner view does not come along.
        Back(cut);
        Toggles(cut)[1].Click();

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
        Assert.Contains("2. Spyttsekresjon", Panel(cut).TextContent);
        Assert.Equal("false", SourceToggles(cut)[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Source_WhenTheVariablePanelIsClosedAfterReturning_ThenTheOwnerIsNotReachable()
    {
        // State the reader cannot see and cannot get rid of is what closing the variable panel
        // exists to avoid one level up; an owner left set behind it would come back the moment the
        // same row was opened again.
        var cut = OpenOwner(TwoRows(), 0);

        // While the kilde view is showing, the row and its panel are not on screen at all.
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));

        Back(cut);
        Toggles(cut)[0].Click();

        // Closing the row takes its owner buttons with it, so there is no way back into the kilde
        // view from a row that is shut.
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));

        Toggles(cut)[0].Click();

        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void Source_WhenASearchReplacesTheRows_ThenTheOwnerPanelGoesWithTheSelection()
    {
        // The selection is always a row on screen, and the owner hangs inside the selection — so a
        // search that leaves the open row behind has to take both with it, not just the outer one.
        var client = TwoRows();
        var cut = OpenOwner(client, 0);

        client.Then(OnePage(Row(SpyttId, "2. Spyttsekresjon")));
        // The search box is on the list, so the reader is back there before searching.
        Back(cut);
        cut.Find("form").Submit();

        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Empty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void Source_WhenTheVariableNamesNoOwner_ThenNoButtonOffersOne()
    {
        // A button that could only ever report "not found" is worse than no button: the reader
        // presses it, waits, and is told the catalogue is missing something it never claimed.
        var bare = Detail(TaleId) with { KildeId = Guid.Empty, DatasamlingId = null };
        var client = new DetailClient(OnePage(Row(TaleId, "1. Tale"))).Knows(bare);
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();

        Assert.Empty(SourceToggles(cut));

        // The whole variable is still reachable: it is about this row, not about an owner the row
        // does not name, so it does not disappear with them.
        Assert.Contains(cut.FindAll(".munin-explorer-detail > button"),
                        b => b.TextContent.Contains("hele variabelen", StringComparison.Ordinal));
        Assert.Equal(0, client.KildeCalls);
        Assert.Equal(0, client.DatasamlingCalls);
    }

    [Fact]
    public void Drilldown_WhenAViewIsOpen_ThenBothWaysBackAreBlueRatherThanPlainGhosts()
    {
        // Both drilldowns say "Tilbake til variabler", so they cannot look like two different
        // controls. A plain ghost shows nothing until :hover; blue says link. (Fhi.Metadata-l9l2n.34)
        var cut = OpenOwner(TwoRows(), 0);

        Assert.Contains("button-square--ghost-blue", BackClasses(cut));
        Assert.DoesNotContain("button-square--ghost", BackClasses(cut));

        Back(cut);

        cut.FindAll(".munin-explorer-detail > button")
           .First(b => b.TextContent.Contains("hele variabelen", StringComparison.Ordinal))
           .Click();

        Assert.Contains("button-square--ghost-blue", BackClasses(cut));
        Assert.DoesNotContain("button-square--ghost", BackClasses(cut));
    }

    /// <summary>The class tokens on the way out of whichever drilldown is on screen.</summary>
    private static AngleSharp.Dom.ITokenList BackClasses(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-drilldown button").ClassList;

    [Fact]
    public async Task Source_WhenTheOtherOwnerIsOpenedFirst_ThenTheAbandonedFetchIsNotShown()
    {
        // Two owners opened in quick succession are two requests in flight against two endpoints,
        // and nothing says the first one answers first. Without the generation guard the slower
        // answer paints itself over the panel the reader is actually looking at — which reads as
        // correct rather than as broken.
        var client = TwoRows();
        var cut = RenderWith(client);

        Toggles(cut)[0].Click();

        client.StallKilde = true;
        SourceToggles(cut)[0].Click();

        // The reader gave up on the stalled kilde and went back for the datasamling instead.
        Back(cut);

        SourceToggles(cut)[1].Click();

        await cut.InvokeAsync(() => client.AnswerStalledKilde(Kilde()));

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".munin-explorer-drilldown"));

            // Each owner has a view of its own now, so which one is on screen is a question about
            // the markup rather than about which labels happen to differ between two field lists.
            Assert.NotNull(SourcePanel(cut).QuerySelector(".munin-explorer-datasamling"));
            Assert.Null(SourcePanel(cut).QuerySelector(".munin-explorer-kilde"));
        });
    }

    [Fact]
    public void Source_WhenTheLanguageIsEn_ThenThePanelIsEnglishAndTheCatalogueStaysNorwegian()
    {
        // The same split the variable panel makes, one level in: our labels and our prose for the
        // kildetype and the identification level follow Language, and Munin's own words do not.
        var cut = RenderWith(TwoRows(), b => b.Add(c => c.Language, "en"));

        Toggles(cut)[0].Click();

        Assert.Equal(["Show data source", "Show data collection"],
                     SourceToggles(cut).Select(t => t.TextContent));

        SourceToggles(cut)[0].Click();

        Assert.Equal(
            ["Type of data source", "Legal basis", "Data controller", "Data processor",
             "Level of personal identification", "Validity", "Last updated in Munin",
             "Total number of variables", "Data period"],
            SourceLabels(cut));

        var values = SourcePanel(cut).QuerySelectorAll("dl dd");

        // Our prose, so no lang of its own; the register's own name keeps one.
        Assert.Equal("National medical quality registry", values[0].TextContent);

        // The catalogue holds one name and one description, both Norwegian. On this page they are
        // the only Norwegian left, and both say so.
        var heading = SourcePanel(cut).QuerySelector(".headline-s")!;
        var description = SourcePanel(cut).QuerySelector(".munin-explorer-kilde__description")!;

        Assert.Equal("Als registeret", heading.TextContent);
        Assert.Equal("no", heading.GetAttribute("lang"));
        Assert.Equal("no", description.GetAttribute("lang"));
        Assert.Equal("Indirectly identifiable", values[4].TextContent);

        // The sidebar is mixed, and each cell says which it is: the kildetype and the identification
        // level are vocabularies this package translates, so they are English here and unmarked. The
        // legal basis and the controller are the catalogue's own words, stored once in Norwegian.
        Assert.Null(values[0].GetAttribute("lang"));
        Assert.Null(values[4].GetAttribute("lang"));
        Assert.Equal("no", values[1].GetAttribute("lang"));
        Assert.Equal("no", values[2].GetAttribute("lang"));

        Back(cut);

        // The toggle that opened it reads shut again, in English.
        Assert.Equal("Show data source", SourceToggles(cut)[0].TextContent);
    }

    [Fact]
    public void Source_WhenAPanelIsOpen_ThenItIsBuiltFromShapesRatherThanFromANewStyleName()
    {
        // Neither stylesheet has a key/value block that can be read back off it, so the owner is a
        // heading wearing Stiler's own headline classes and a <dl> that borrows helsedata's
        // munin-explorer-meta__grid, rather than a new style name of ours.
        var cut = OpenOwner(TwoRows(), 0);

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct()
            .ToList();

        // Every name this package invents, and the list is meant to stay short: each one is a class
        // helsedata's stylesheet has never heard of, so it is inert until Stiler is asked for it.
        // The kilde view adds a layout - a main column, a sidebar - that no existing helsedata class
        // describes, which is why these exist at all. Anything inside that layout uses helsedata's
        // own names.
        //
        // The three delkilde names are here because this fixture's kilde has a delkilde tree two
        // levels deep, which is deliberate: a source with no delkilder draws none of them, so a
        // list taken off a flat source would pin the ones that need Stiler least. `__identifiers`
        // is emitted twice over now - once by the kilde's own name block, once per delkilde - and
        // appears once, this being a set.
        Assert.Equal(
            [
                "munin-explorer",
                "munin-explorer-drilldown",
                "munin-explorer-kilde",
                "munin-explorer-kilde__header",
                "munin-explorer-kilde__identifiers",
                "munin-explorer-kilde__kildetype",
                "munin-explorer-kilde__description",
                "munin-explorer-kilde__body",
                "munin-explorer-kilde__main",
                "munin-explorer-kilde__datasamlinger",
                "munin-explorer-kilde__delkilder",
                "munin-explorer-kilde__delkilde",
                "munin-explorer-kilde__delkilde-name",
                "munin-explorer-kilde__aside",
            ],
            invented);

        var panel = SourcePanel(cut);

        // The prefix above is only half the guard: a borrowed name that no stylesheet defines is
        // just as inert as an invented one, and harder to notice, because it sits in a family that
        // does exist. headline-sm did exactly that on the four block headings until
        // Fhi.Metadata-e4bj2 — the family is real, that member of it is not. So the headline scale
        // is pinned to the three names the samples stand in for and the repository can point at.
        Assert.All(panel.QuerySelectorAll("[class]")
                        .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Where(k => k.StartsWith("headline-", StringComparison.Ordinal))
                        .Distinct(),
                   k => Assert.Contains(k, (string[])["headline-3", "headline-s", "headline-xxs"]));

        // The fact lists borrow the grid the detail panel already uses rather than a shape of their
        // own — the same pairs of label and value, so the same class.
        // GetAttribute, not ClassName: AngleSharp reports a missing class attribute as "" rather
        // than null, so the null branch of this list was unreachable and the assertion did not mean
        // what it said.
        Assert.All(panel.QuerySelectorAll("dl"),
                   e => Assert.Contains(e.GetAttribute("class"), (string?[])[null, "munin-explorer-meta__grid"]));

        Back(cut);

        Assert.Contains("hd-button-square", SourceToggles(cut)[0].ClassName!);
    }

    [Fact]
    public void Source_WhenTheDrillInIsOpen_ThenTheNameIsAnIdPrefixAndStaysExemptFromTheStylesheetGuards()
    {
        // The trap that comes with asking every name for a declaration rather than for a mention.
        //
        // `munin-explorer-source` is not a class. It is the prefix of the id that names the
        // drill-in region — `munin-explorer-source-{instance}`, so two mounts on one page cannot
        // collide — and the region itself wears `munin-explorer-drilldown`. So
        // `.munin-explorer-source` selects nothing, no stylesheet can have a rule for it, and no
        // amount of adding CSS would satisfy a guard that demanded one.
        //
        // scripts/assert-sample-css-in-step.sh reads names statically out of src/, prose and all,
        // so it does see the name — in the very paragraph explaining that it is an id — and IDS is
        // why it skips it. Under the old mention-only check that exemption looked like a rule
        // somebody had not got round to writing; under the stricter one it is the only thing
        // standing between the guard and a failure that cannot be fixed. The lazy repair is to
        // delete the entry, so this test asserts both halves: that the name really is an id prefix,
        // and that the entry survives.
        var cut = OpenOwner(TwoRows(), 0);

        // Nothing wears it. Rendered, not read off the source, because the question is what the
        // DOM contains — which is also why the C# guard is immune to this trap and the shell one
        // is not: HostClassNames only ever sees names in a rendered class attribute.
        Assert.Empty(cut.FindAll(".munin-explorer-source"));

        // What does carry the name is an id, on an element whose class is the drill-in handle.
        var panel = SourcePanel(cut);

        Assert.StartsWith("munin-explorer-source-", panel.Id, StringComparison.Ordinal);
        Assert.Equal("munin-explorer-drilldown", panel.GetAttribute("class"));

        // And the shell guard still lets it through. An `IDS=(` block that has lost this entry is
        // a guard that will go red on the next run against a name no rule can ever satisfy.
        //
        // The block is matched rather than sliced out on two unchecked indices. Reformatting the
        // array, renaming it, or closing it as `  )` is exactly the edit that would take the entry
        // with it, and slicing would meet that edit with an ArgumentOutOfRangeException pointing at
        // a string index — a reader would be told a number was out of range, in the one test
        // written to tell them why this entry has to survive.
        var guard = File.ReadAllText(Repo.In("scripts", "assert-sample-css-in-step.sh"));
        var ids = System.Text.RegularExpressions.Regex.Match(
            guard, @"^IDS=\((?<entries>.*?)^\)",
            System.Text.RegularExpressions.RegexOptions.Multiline
            | System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.True(ids.Success,
            "No `IDS=(` … `)` block in scripts/assert-sample-css-in-step.sh. The exemption list is " +
            "what keeps the guard off munin-explorer-source, which is an id prefix and not a class: " +
            ".munin-explorer-source selects nothing, so no stylesheet can ever have a rule for it " +
            "and a guard demanding one fails forever. If the list has moved, this test follows it; " +
            "if it has been deleted, the guard is about to go red on a name nothing can satisfy.");

        Assert.Contains("munin-explorer-source", ids.Groups["entries"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_WhenThePanelIsOpen_ThenItsHeadingSitsBelowTheCardsInTheOutline()
    {
        // A heading level the host cannot see is a broken outline, so it is derived rather than
        // hard-coded: title, then card, then owner. Mounted at h1 that is h1 › h2 › h3.
        var cut = RenderWith(TwoRows(), b => b.Add(c => c.HeadingLevel, 1));

        Toggles(cut)[0].Click();
        SourceToggles(cut)[0].Click();

        // The kilde view's heading sits one below the component's own title, so the outline holds
        // whichever view is showing. The rows are not on screen to compare against — that is the
        // point of the view — so the comparison is against the component title.
        // One below the component's own title. It used to be H3 because it hung under the row's
        // H2; as a view of its own it is no longer inside the row, so H2 is what keeps the outline
        // unbroken.
        Assert.Equal("H2", SourcePanel(cut).QuerySelector(".headline-s")!.TagName);

        Back(cut);

        Assert.Equal("H1", cut.Find(".munin-explorer > [class*='headline']").TagName);
        Assert.Equal("BUTTON", cut.Find(".munin-explorer-data-list__item__row button.munin-explorer-dataitem-main__name").TagName);
    }

    [Fact]
    public void Source_WhenTwoExplorersAreOnOnePage_ThenTheirPanelsDoNotShareIds()
    {
        // Two instances of the component on one CMS page. Duplicate ids are a WCAG 4.1.1 failure
        // and would point both toggles at whichever panel rendered first.
        Services.AddSingleton<IMuninExplorerClient>(TwoRows());

        var a = Render<VariableExplorer>();
        var b = Render<VariableExplorer>();

        Toggles(a)[0].Click();
        SourceToggles(a)[0].Click();
        Toggles(b)[0].Click();
        SourceToggles(b)[0].Click();

        Assert.NotEqual(SourcePanel(a).Id, SourcePanel(b).Id);

        // The toggles live in the row, which is not on screen while the kilde view is.
        Back(a);
        Back(b);

        Assert.NotEqual(SourceToggles(a)[0].Id, SourceToggles(b)[0].Id);
    }

    [Fact]
    public void ClearSearch_WhenPressed_ThenTheSearchIsRunAgainWithNothingInIt()
    {
        // The control that replaces the user-agent ✕, and here it has to be a real search rather
        // than a field assignment: these rows came from the API. Pressing it must therefore ask
        // again with no term - anything less leaves the reader looking at results for a search the
        // box no longer shows, and the host mirroring that search into a URL.
        var client = new FakeClient(new Page<VariableSummary>());

        var cut = RenderWith(client);

        cut.Find(".searchbox__freetext").Change("alder");
        cut.Find("form").Submit();

        Assert.Equal("alder", client.LastSearch);

        cut.Find(".munin-explorer-search__clear").Click();

        Assert.Null(client.LastSearch);
        Assert.Equal("true", cut.Find(".munin-explorer-search__clear").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void ClearSearch_WhenAFetchIsInFlight_ThenTheBoxDoesNotEmptyAheadOfTheRows()
    {
        // Found by review on PR #94, and it is the bug this button was added to fix, reached by a
        // different door. Clearing _search and then calling SearchAsync is safe only while nothing
        // is in flight: SearchAsync drops itself when _loading, so the box would empty, the fetch
        // would never run, and the reader would be left with an empty field over the old rows -
        // with the host still holding the previous search for its URL.
        //
        // The rule was already written down one method away, on SortAsync: the guard comes first,
        // because changing the state and then not fetching leaves a control describing a list
        // nobody is looking at.
        var client = new SlowClient(OnePage(Variable("1. Tale", "KODE")));

        var reported = new List<string?>();

        var cut = RenderWith(client, b => b.Add(
            c => c.SearchChanged, EventCallback.Factory.Create<string?>(this, reported.Add)));

        // A search that never answers, so the component is left mid-fetch.
        cut.Find(".searchbox__freetext").Change("alder");
        cut.Find("form").Submit();

        var callsWhileLoading = client.Calls;

        // Counted rather than inspected for null: SearchChanged carries null on the initial load
        // too, by design, so one is already in the list before the button is anywhere near it.
        var reportsWhileLoading = reported.Count;

        cut.Find(".munin-explorer-search__clear").Click();

        // The box still says what the rows on screen came from, and nothing was asked or reported.
        Assert.Equal("alder", cut.Find(".searchbox__freetext").GetAttribute("value"));
        Assert.Equal(callsWhileLoading, client.Calls);
        Assert.Equal(reportsWhileLoading, reported.Count);
    }

    [Fact]
    public void ClearSearch_WhenPressed_ThenTheHostIsToldTheSearchIsGone()
    {
        // The half with no visible symptom. A host mirrors SearchChanged into its URL, so a clear
        // that did not report itself would hand out a link reopening the search just cleared.
        var reported = new List<string?>();

        var cut = RenderWith(new FakeClient(new Page<VariableSummary>()), b => b.Add(
            c => c.SearchChanged, EventCallback.Factory.Create<string?>(this, reported.Add)));

        cut.Find(".searchbox__freetext").Change("alder");
        cut.Find("form").Submit();
        cut.Find(".munin-explorer-search__clear").Click();

        Assert.Null(reported[^1]);
    }

    [Fact]
    public void SearchField_WhenItIsRendered_ThenItIsNotASearchInputWithAClearButtonWeCannotHook()
    {
        // The same rule Kelda's field follows, and it binds both explorers because both bind on
        // change: a type="search" input carries a user-agent clear button this package cannot hook.
        // The ✕ fires the DOM `search` event, which Blazor does not know, so the box empties while
        // the search it ran stays in force - and here that is worse than in Kelda, because the
        // search reached the API and the host has been told to put it in a URL. The reader would be
        // looking at results for a query the box no longer shows, with a link to match.
        //
        // Found in Kelda first, on 2026-08-27. The two fields are one pattern and must not diverge.
        var cut = RenderWith(new FakeClient(new Page<VariableSummary>()));

        var field = cut.Find(".searchbox__freetext");

        Assert.Equal("text", field.GetAttribute("type"));
        Assert.Equal("search", field.GetAttribute("enterkeyhint"));
    }

    /// <summary>
    /// The client the retry tests fetch against: thirteen pages, and every switch those tests need
    /// already on <see cref="FilteringClient"/> rather than on a fifth near-identical fake.
    /// </summary>
    private static FilteringClient RetryClient(
        bool failSearch = false, Page<VariableSummary>? filtered = null) =>
        new(ResultPage(312), filteredAnswer: filtered) { FailSearch = failSearch };

    /// <summary>
    /// The retry controls, selected through the alert region rather than by their own class.
    /// </summary>
    /// <remarks>
    /// The selector is the assertion in every test below that uses it: a button outside
    /// <c>role="alert" aria-live="assertive"</c> is a failure announced with no way forward
    /// mentioned, and a plain search for a button would find one there and call it done.
    /// </remarks>
    private static IReadOnlyList<IElement> RetryButtons(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("div[role='alert'][aria-live='assertive'] button");

    private static IElement Retry(IRenderedComponent<VariableExplorer> cut, string label) =>
        RetryButtons(cut).Single(b => b.TextContent == label);

    private const string RetryRows = "Prøv søket på nytt";
    private const string RetryFacets = "Prøv filtrene på nytt";

    /// <summary>
    /// The sentences in the alert region, which is where both failures report.
    /// </summary>
    /// <remarks>
    /// Scoped to the <c>div</c>. Every saveable row carries a <c>role="alert"</c> span of its own
    /// (<c>VariableExplorer.SaveButton.cs</c>), and an unscoped selector would start counting those.
    /// </remarks>
    private static IReadOnlyList<IElement> AlertMessages(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("div[role='alert'] p");

    [Fact]
    public void Retry_WhenTheSearchFails_ThenTheButtonIsInsideTheRegionTheFailureIsAnnouncedFrom()
    {
        // The whole point of the change. A message with no control is a dead end for everyone, and
        // a control outside the assertive region is a dead end for a screen-reader user only: the
        // interruption says something went wrong and stops there, while the way out sits silently
        // somewhere further down the page.
        var cut = RenderWith(new FailingClient());

        Assert.Contains("Kunne ikke hente variabler", cut.Find("[role='alert']").TextContent);
        Assert.Equal(RetryRows, Assert.Single(RetryButtons(cut)).TextContent);
    }

    [Fact]
    public void Retry_WhenTheSearchFails_ThenTheButtonStandsBesideItsOwnSentence()
    {
        // A message and a control that answers it belong on one row. Stacked, and drawn as a
        // ghost on a coloured infobox, the offer read as stray text under a box rather than as
        // something to press. (Fhi.Metadata-31ogu)
        var cut = RenderWith(new FailingClient());

        var row = cut.Find("div[role='alert'] > div.munin-explorer-alert");

        Assert.NotNull(row.QuerySelector("p.infobox"));
        Assert.Equal(RetryRows, row.QuerySelector("button")!.TextContent);

        // Filled rather than a ghost, the pair Tøm søket already uses for a control that acts.
        Assert.Contains("button-square--secondary", Retry(cut, RetryRows).ClassName!);
        Assert.DoesNotContain("button-square--ghost", Retry(cut, RetryRows).ClassName!);
    }

    [Fact]
    public void Retry_WhenNothingHasFailed_ThenTheRegionHoldsNoRowAtAll()
    {
        // The region itself is always in the DOM and deliberately classless — an announcement is
        // unreliable when the element arrives with its text. The row inside it is what must not
        // be there when there is nothing to say, or every quiet page draws an empty box.
        var cut = RenderWith(new PagedClient(312));

        Assert.NotNull(cut.Find("div[role='alert']"));
        Assert.Empty(cut.FindAll("div.munin-explorer-alert"));
    }

    [Fact]
    public void Retry_WhenTheApiAnswersOnTheSecondTry_ThenTheRowsArriveAndTheMessageIsGone()
    {
        // The trap: a retry that fetches without clearing the failure shows the rows and the
        // sentence saying they could not be fetched, side by side, and passes any test that only
        // asks whether the results came back.
        var client = RetryClient(failSearch: true);
        var cut = RenderWith(client);

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.NotEmpty(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenTheApiRateLimits_ThenNoRetryIsOfferedBesideTheSentenceTellingThemToWait()
    {
        // The one failure a retry cannot answer: the catalogue is up and the reader has asked too
        // often, so the button would invite the exact action the sentence beside it advises against
        // — and would feed the burst that caused the 429. A test throwing a generic exception never
        // reaches this branch and stays green whatever this branch does.
        var cut = RenderWith(new RateLimitedClient());

        Assert.Contains("for mange forespørsler", cut.Find("[role='alert']").TextContent);
        Assert.Empty(RetryButtons(cut));
    }

    [Fact]
    public void Retry_WhenARateLimitFollowsAFailureThatOfferedOne_ThenTheButtonIsInertRatherThanGone()
    {
        // The 429 arriving over a button already on screen. It cannot leave — that is the focus
        // rule below — so it goes inert instead, and pressing it sends nothing.
        var client = RetryClient(failSearch: true);
        var cut = RenderWith(client);

        client.FailSearch = false;
        client.RateLimitSearch = true;
        Retry(cut, RetryRows).Click();

        var button = Retry(cut, RetryRows);
        var calls = client.SearchCalls;

        Assert.Contains("for mange forespørsler", cut.Find("[role='alert']").TextContent);
        Assert.Equal("true", button.GetAttribute("aria-disabled"));

        button.Click();

        Assert.Equal(calls, client.SearchCalls); // inert: no request went out
    }

    [Fact]
    public void Retry_WhenAPageTurnFailed_ThenItAsksForThatPageAgainAndNotForWhatIsInTheBox()
    {
        // The trap the whole handler exists for. Wiring the button to SearchAsync passes a test
        // that only ever fails the first search, and throws away both the reader's position and the
        // query the rows came from: the box holds text that was never submitted, and every caller
        // that fails has already rolled the page back to the one still on screen.
        var client = RetryClient();
        var cut = RenderWith(client, b => b.Add(c => c.Search, "tale"));

        Next(cut).Click(); // page 2, which arrives

        client.FailSearch = true;
        Next(cut).Click(); // page 3, which does not

        cut.Find(".searchbox__freetext").Change("noe helt annet");

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal(3, client.LastPage);
        Assert.Equal("tale", client.LastSearch);
        Assert.Equal("3/13", Position(cut));
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenASortFailed_ThenItAsksForTheOrderThatFailedRatherThanTheOneOnScreen()
    {
        // Same trap one control along, and the reason the request is captured rather than read back
        // off the fields: a failed sort rolls the order back, so a retry built from _sort would
        // fetch the ordering the reader was already looking at and report success for it.
        var client = RetryClient();
        var cut = RenderWith(client);

        client.FailSearch = true;
        ClickSort(cut, "Kilde");

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenADirectionReversalFailed_ThenItAsksForThatDirectionAndTellsTheHostSo()
    {
        // The other half of the captured request, and the one a retry built off the fields would
        // get wrong without any test noticing: a second click on the active column reverses to
        // descending, and the failure rolls the direction back to ascending — so _direction names
        // the order still on screen, not the order the reader asked for. The host was told the
        // rolled-back value too, so it is holding ascending until this retry says otherwise.
        var raised = new List<string>();
        SortDirection? direction = null;
        var client = RetryClient();

        var cut = RenderWith(client, b => b
            .Add(c => c.SortChanged, _ => raised.Add("sort"))
            .Add(c => c.DirectionChanged, d => { direction = d; raised.Add("direction"); }));

        ClickSort(cut, "Kilde");   // ascending, which arrives

        raised.Clear();

        client.FailSearch = true;
        ClickSort(cut, "Kilde");   // its reversal, which does not

        Assert.Empty(raised); // the rollback said nothing, so the host still has ascending

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Descending, client.LastDirection);

        // The field never moved — the host was told it two clicks ago and this request kept it.
        Assert.Equal(["direction"], raised);
        Assert.Equal(SortDirection.Descending, direction);
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenAFilterChangeFailed_ThenTheCountsAndThePagerFollowTheFilterThatWentOut()
    {
        // The third caller that can leave a failed request behind, and the only one where the
        // filter half of it matters. Two things a retried narrowing has to bring with it: the
        // counts, which are cross-filtered and would otherwise go on describing the selection the
        // reader left; and the pager flag, which the narrowing cleared on its way to page one and
        // its rollback put back, leaving "Side 1 av 1" between two inert buttons over a result the
        // same narrowing would have hidden it for on the first attempt.
        var client = RetryClient(filtered: ResultPage(4));
        var cut = RenderWith(client);

        Next(cut).Click(); // page 2, so the pager is on screen by the reader's own doing

        var facetCalls = client.FacetCalls;

        client.FailSearch = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal(1, client.SearchFilter!.ActiveCount);
        Assert.Equal(1, client.LastPage);
        Assert.Equal(facetCalls + 1, client.FacetCalls);
        Assert.Equal(client.SearchFilter, client.FacetFilter);
        Assert.Empty(AlertMessages(cut));
        Assert.Empty(cut.FindAll("div.munin-explorer-pagination"));
    }

    [Fact]
    public void Retry_WhenASubmittedSearchFailed_ThenTheCountsFollowTheTermThatFinallyWentOut()
    {
        // The scenario the change is named for, and the second of the three things that move the
        // counts: they are cross-filtered against the query as well as the filter, so a retried
        // search that leaves them alone leaves every number describing the term the reader typed
        // over — with a panel that says nothing about being stale, because that message belongs to
        // the facets' own failure and this was the rows'.
        var client = RetryClient();
        var cut = RenderWith(client);

        client.FailSearch = true;
        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        var facetCalls = client.FacetCalls;

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal(facetCalls + 1, client.FacetCalls);
        Assert.Equal("svelging", client.FacetSearch);
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenTheFirstLoadFailed_ThenTheFilterPanelArrivesWithTheRows()
    {
        // The worst case of not refreshing the counts: OnInitializedAsync never asked for them, so
        // _facets is null and the whole fieldset is off the page. Rows without filters, silently,
        // with nothing to say so and no way back short of submitting a fresh search — which is the
        // page reload this change exists to remove.
        var client = RetryClient(failSearch: true);
        var cut = RenderWith(client);

        Assert.Empty(cut.FindAll("fieldset.munin-explorer-filters"));

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.NotEmpty(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Single(cut.FindAll("fieldset.munin-explorer-filters"));
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenASortFailed_ThenTheHostIsToldTheFieldThatFinallyTookEffect()
    {
        // The failure rolled the order back and told the host nothing, so the host is holding the
        // order that is still on screen. Without these callbacks a host mirroring sort into its URL
        // keeps a URL that reopens the list in the wrong order — and every assertion about the
        // request that went out stays green, because none of them looks at what was raised.
        var raised = new List<string>();
        SortField? sort = null;
        var client = RetryClient();

        var cut = RenderWith(client, b => b
            .Add(c => c.SortChanged, f => { sort = f; raised.Add("sort"); })
            .Add(c => c.DirectionChanged, _ => raised.Add("direction")));

        client.FailSearch = true;
        ClickSort(cut, "Kilde");

        Assert.Empty(raised); // the failure rolled it back and said nothing

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        // The field, and only the field: a first sort starts ascending, which is what the host was
        // handed at mount and has been holding ever since.
        Assert.Equal(["sort"], raised);
        Assert.Equal(SortField.Kilde, sort);
    }

    [Fact]
    public void Retry_WhenAPageTurnFailed_ThenTheHostIsToldThePageMovedAndNothingElse()
    {
        // The opposite regression, and the one a blanket four-callback ending causes: GoToPageAsync
        // deliberately raises the page alone, so a retried page turn that also raises sort,
        // direction and filter hands a host that navigates or pushes history on them three entries
        // for values it was already holding and this request never touched.
        var raised = new List<string>();
        var client = RetryClient();

        var cut = RenderWith(client, b => b
            .Add(c => c.SortChanged, _ => raised.Add("sort"))
            .Add(c => c.DirectionChanged, _ => raised.Add("direction"))
            .Add(c => c.FilterChanged, _ => raised.Add("filter"))
            .Add(c => c.PageChanged, _ => raised.Add("page")));

        raised.Clear(); // the mount echoes the page it was given

        client.FailSearch = true;
        Next(cut).Click();

        raised.Clear();

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal(["page"], raised);
    }

    [Fact]
    public void Retry_WhenItSucceeds_ThenTheButtonStaysInTheDocumentInsteadOfLeavingWithTheMessage()
    {
        // A successful retry clears the message, and the button belongs to the message. Removing it
        // would take the element the reader has just pressed — and is therefore focused — out of the
        // document, dropping focus to <body> and sending a keyboard user back to the top of
        // helsedata's page. The same failure the pager's buttons and Fjern alle filtre refuse the same way.
        //
        // bUnit reparses the markup on every render, so document.activeElement cannot be inspected
        // across one here. What is pinned instead is the condition focus survives on: the pressed
        // element is still in the document, and inert rather than disabled — the attribute would
        // drop focus without removing anything.
        var client = RetryClient(failSearch: true);
        var cut = RenderWith(client);

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        var button = Retry(cut, RetryRows);

        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void Retry_WhenTheButtonIsShown_ThenItsClassNamesAreOnesSomeStylesheetActuallyDefines()
    {
        // Every other sweep in this file renders a page with nothing wrong, so the one control that
        // exists only after a failure is past all of them.
        var cut = RenderWith(RetryClient(failSearch: true));

        Assert.Equal(RetryRows, Assert.Single(RetryButtons(cut)).TextContent);
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Retry_WhenALaterFetchSucceedsOnItsOwn_ThenTheDeadOfferLeavesTheRegion()
    {
        // The region is aria-atomic, so everything in it is announced together. A button kept for
        // good is read out beside every failure after it — "Tallene kan være utdaterte … Prøv søket
        // på nytt Prøv filtrene på nytt", one live offer and one dead one with nothing but
        // aria-disabled telling them apart. So it stays only until the next fetch someone started
        // elsewhere comes back, which is long enough for the press that kept it to hold its focus.
        var client = RetryClient();
        var cut = RenderWith(client);

        client.FailSearch = true;
        Next(cut).Click();

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        Assert.Equal(RetryRows, Assert.Single(RetryButtons(cut)).TextContent);

        client.FailFacets = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Contains("Tallene kan være utdaterte", cut.Find("[role='alert']").TextContent);
        Assert.Equal(RetryFacets, Assert.Single(RetryButtons(cut)).TextContent);
    }

    [Fact]
    public void Retry_WhenALaterFetchIsRateLimited_ThenTheDeadOfferLeavesRatherThanSittingBesideVentLitt()
    {
        // A 429 is neither a success nor a failure that can be retried, so an offer already
        // answered survives it unless this branch clears it too — and the atomic region then reads
        // the wait instruction and a dead button out as one utterance, which is the exact
        // re-announcement clearing it on success exists to stop. No focus argument keeps it here:
        // the reader's finger is on whatever control started the fetch that got throttled.
        var client = RetryClient();
        var cut = RenderWith(client);

        client.FailSearch = true;
        Next(cut).Click();

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        client.RateLimitSearch = true;
        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Contains("for mange forespørsler", cut.Find("[role='alert']").TextContent);
        Assert.Empty(RetryButtons(cut));
    }

    [Fact]
    public void Retry_WhenTheButtonIsInert_ThenItCarriesTheNameThatDrawsItThatWay()
    {
        // aria-disabled says it to a screen reader and to nobody else. The pager and the filter
        // panel are drawn inert by rules scoped to their containers; this button's container is the
        // alert region, which carries no class, so neither rule reaches it and it needs its own
        // name. Without one a sighted reader presses a normal-looking ghost button that does
        // nothing — the exact failure the aria-disabled styling exists to prevent.
        var client = RetryClient(failSearch: true);
        var cut = RenderWith(client);

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        var button = Retry(cut, RetryRows);

        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Contains("munin-explorer-retry", button.ClassName!.Split(' '));
    }

    [Fact]
    public void Retry_WhenNothingHasFailed_ThenThereIsNoRetryButtonAndTheRegionIsStillEmpty()
    {
        // The container is deliberately always present and empty. A retry button parked in it would
        // be furniture on every page that has nothing to report, and would make the region announce
        // something the first time anything else changed inside it.
        var cut = RenderWith(RetryClient());

        Assert.Empty(RetryButtons(cut));
        Assert.Equal(string.Empty, cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Retry_WhenTheFacetRefreshFails_ThenItsOwnButtonRefreshesTheCountsAndNothingElse()
    {
        // The facets fail on their own request and clear their own message, so a handler shared
        // with the rows would answer this sentence by re-fetching a list nobody said was wrong.
        var client = RetryClient();
        var cut = RenderWith(client);

        client.FailFacets = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        Assert.Contains("Tallene kan være utdaterte", cut.Find("[role='alert']").TextContent);

        var searchCalls = client.SearchCalls;
        var facetCalls = client.FacetCalls;

        client.FailFacets = false;
        Retry(cut, RetryFacets).Click();

        Assert.Equal(facetCalls + 1, client.FacetCalls);
        Assert.Equal(searchCalls, client.SearchCalls); // the rows were never in doubt
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenBothFailuresAreReported_ThenEachSentenceHasItsOwnButtonAndItsOwnRequest()
    {
        // Both messages share one assertive region, so both buttons land in it together. Two
        // reading "Prøv igjen" would be two identical offers with nothing to choose between them,
        // and one button for both would clear one sentence with the answer to the other question.
        var client = RetryClient();
        var cut = RenderWith(client);

        client.FailFacets = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        client.FailSearch = true;
        Next(cut).Click();

        var buttons = RetryButtons(cut);

        Assert.Equal(2, buttons.Count);
        Assert.Equal(RetryRows, buttons[0].TextContent);
        Assert.Equal(RetryFacets, buttons[1].TextContent);
        Assert.Equal(2, AlertMessages(cut).Count);

        var facetCalls = client.FacetCalls;

        client.FailSearch = false;
        Retry(cut, RetryRows).Click();

        // The rows came back; the counts are still stale and still say so, in their own sentence.
        Assert.Equal(facetCalls, client.FacetCalls);
        Assert.DoesNotContain("Kunne ikke hente variabler", cut.Find("[role='alert']").TextContent);
        Assert.Contains("Tallene kan være utdaterte", cut.Find("[role='alert']").TextContent);

        var searchCalls = client.SearchCalls;

        client.FailFacets = false;
        Retry(cut, RetryFacets).Click();

        Assert.Equal(searchCalls, client.SearchCalls);
        Assert.Empty(AlertMessages(cut));
    }

    [Fact]
    public void Retry_WhenTheLanguageIsEn_ThenTheButtonsAreInEnglishToo()
    {
        // The reader parameter decides, not the machine's culture — and a button left in Norwegian
        // beside an English sentence is the half-translated release the parity guard cannot see,
        // because both languages would have a string and only one of them would be rendered.
        var client = RetryClient();
        var cut = RenderWith(client, b => b.Add(c => c.Language, "en"));

        client.FailFacets = true;
        ClickFacet(cut, "Dødsårsaksregisteret");

        client.FailSearch = true;
        Next(cut).Click();

        var buttons = RetryButtons(cut);

        Assert.Equal(2, buttons.Count);
        Assert.Equal("Try the search again", buttons[0].TextContent);
        Assert.Equal("Try the filters again", buttons[1].TextContent);
    }
}
