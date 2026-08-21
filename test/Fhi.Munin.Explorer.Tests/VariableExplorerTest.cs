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

        Assert.Equal(2, cut.FindAll("ul.variable-data-list > li").Count);
        Assert.Contains("1. Tale", cut.Markup);
        Assert.Contains("V_ALS.F1.ALSFRSR1TALE", cut.Markup);
        Assert.Contains("2 variabler", cut.Markup);
    }

    [Fact]
    public void Render_WhenThereAreNoHits_ThenTheEmptyMessageIsShown()
    {
        var cut = RenderWith(new FakeClient(OnePage()));

        Assert.Empty(cut.FindAll("ul.variable-data-list > li"));
        Assert.Contains("Ingen variabler passet søket", cut.Markup);
    }

    [Fact]
    public void Render_WhenTheApiFails_ThenAnErrorMessageIsShownRatherThanThrowing()
    {
        var cut = RenderWith(new FailingClient());

        Assert.Contains("Kunne ikke hente variabler", cut.Markup);
        Assert.Empty(cut.FindAll("ul.variable-data-list > li"));
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

        cut.Find("input[type=search]").Change("noe helt annet");
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

        cut.Find("input[type=search]").Change("noe helt annet");
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
        Assert.NotNull(cut.Find($"{SortControl} .variable-data-list__item__row--header"));
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
        Assert.Single(cut.FindAll("ul.variable-data-list > li"));
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
        // The list's accessible name is the same sentence as the status line, so the two cannot
        // drift apart and say the result is ordered two different ways.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Contains("sortert på Standard, stigende",
                        cut.Find("ul.variable-data-list").GetAttribute("aria-label")!);
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

        Assert.NotNull(cut.Find($"{SortControl} .variable-data-list__item__row--header"));
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

        Assert.Empty(cut.FindAll(".variable-dataitem-header__code"));
        Assert.Empty(cut.FindAll(".variable-dataitem-main__code"));
        Assert.Equal("false", ColumnToggle(cut, "Kode").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Columns_WhenAColumnIsTurnedOffAndOnAgain_ThenItComesBack()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        HideColumn(cut, "Datasamling");
        ColumnToggle(cut, "Datasamling").Click();

        Assert.NotNull(cut.Find(".variable-dataitem-main__dataCollection"));
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
        Assert.NotNull(cut.Find(".variable-dataitem-main__period"));
    }

    [Fact]
    public void Columns_WhenEverythingElseIsOff_ThenTheNameColumnIsStillThere()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        foreach (var column in new[] { "Kode", "Kilde", "Datasamling", "Variabelgruppe", "Datatype" })
        {
            HideColumn(cut, column);
        }

        Assert.NotNull(cut.Find(".variable-dataitem-main__name"));
        Assert.NotNull(cut.Find(".variable-dataitem-header__name"));
    }

    [Fact]
    public void Columns_WhenStatusIsTurnedOnByHand_ThenItIsDrawnDespiteTheFilter()
    {
        // The filter decides where Status STARTS, not where it stays. A reader who wants the
        // column anyway can have it: otherwise the picker would show a choice that does nothing,
        // which is worse than not offering it.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.Empty(cut.FindAll(".variable-dataitem-main__status"));

        ColumnToggle(cut, "Status").Click();

        Assert.NotNull(cut.Find(".variable-dataitem-main__status"));
        Assert.NotNull(cut.Find(".variable-dataitem-header__status"));
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

        Assert.NotNull(cut.Find(".variable-dataitem-main__status"));

        ColumnToggle(cut, "Status").Click();

        Assert.Empty(cut.FindAll(".variable-dataitem-main__status"));
        Assert.Empty(cut.FindAll(".variable-dataitem-header__status"));
        Assert.Equal("false", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));

        // And it stays off through the filter that used to own it: their choice wins from here.
        ClickFacet(cut, "Vis historiske");
        ClickFacet(cut, "Vis historiske");

        Assert.Empty(cut.FindAll(".variable-dataitem-main__status"));
        Assert.Equal("false", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Columns_WhenHistoricalVariablesAreIncluded_ThenStatusStartsOnScreen()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                             b => b.Add(c => c.Filter, new VariableFilter { IncludeHistorical = true }));

        Assert.Equal("true", ColumnToggle(cut, "Status").GetAttribute("aria-pressed"));
        Assert.NotNull(cut.Find(".variable-dataitem-main__status"));
    }

    [Fact]
    public void Columns_WhenTheListIsSearchedAgain_ThenTheChoiceSurvives()
    {
        // The choice belongs to the reader, not to the result. Rebuilding it on every search would
        // put back the columns they had just cleared away, on the page where they cleared them.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        HideColumn(cut, "Kilde");
        cut.Find("form").Submit();

        Assert.Empty(cut.FindAll(".variable-dataitem-main__source"));
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

        Assert.Empty(cut.FindAll(".variable-dataitem-header__source"));
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
                     cut.Find(".variable-dataitem-header__source").GetAttribute("aria-sort"));
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

        Assert.NotNull(cut.Find(".variable-dataitem-main__status"));
        Assert.NotNull(cut.Find(".variable-dataitem-header__status"));
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

        Assert.NotNull(cut.Find(".variable-dataitem-main__code"));
        Assert.Empty(cut.FindAll(".variable-dataitem-main__status"));
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
        // The companion to the variable-explorer guard further down, which only inspects names in
        // that prefix — the picker wears eight names outside it, and an invented ninth would slip
        // past that test unnoticed. Every name here was read back off helsedata's compiled
        // stylesheets; one that is not renders as a raw browser default inside a styled page.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        var picker = cut.Find(".variable-explorer-header");

        var names = picker.QuerySelectorAll("[class]")
            .Prepend(picker)
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => !k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
        [
            "button-square--ghost",           // Stiler, the ghost colour the sort and facet
                                              //   buttons already wear
            "dropdown",                       // helsedata, variables.css — the trigger's width:
                                              //   `.variable-explorer-header__actions .dropdown
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
        // trigger, `variable-explorer__dropdown` the z-index over the rows below. Asserted as a
        // set rather than with Contains, which `variable-explorer__dropdown` would satisfy on its
        // own and so could not tell the two apart.
        var dropdown = cut.Find(".variable-explorer-header__actions > details");
        Assert.Equal(["dropdown", "variable-explorer__dropdown"],
                     dropdown.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("relative", dropdown.GetAttribute("style")!);

        // A <details>, because their dropdown opens and closes from React state and this package
        // ships no script. Same reason the filter facets are disclosures.
        var summary = cut.Find(".variable-explorer-header__actions > details > summary");
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

        var cell = cut.Find(".variable-dataitem-main__period");

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

        var cell = cut.Find(".variable-dataitem-main__period");

        Assert.Contains("Ongoing", cell.TextContent, StringComparison.Ordinal);
        Assert.Empty(cell.QuerySelectorAll("[lang]"));

        // The neighbouring columns still are Norwegian, so this is a distinction the markup draws
        // rather than a marker that went missing everywhere.
        Assert.Equal("no", cut.Find(".variable-dataitem-main__source span[lang]").GetAttribute("lang"));
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

        var cell = cut.Find(".variable-dataitem-main__period");

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

        var cell = cut.Find(".variable-dataitem-main__period");

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
    public void Render_Always_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // The companion to the assertion below, and the wider of the two. That one pins the names in
        // the variable-explorer prefix exactly, which is the right shape for names we invent — a
        // ninth appearing is news. This one asks the different question, of every class in the DOM
        // and not just that prefix: does any stylesheet, ours or helsedata's, define it at all?
        //
        // Borrowed names needed their own check because nothing was watching them. `variable-meta__body`
        // came off this view: it wears the look of helsedata's `variable-meta` family, they define the
        // family but not that member, and so it promised a rule that has never existed anywhere.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // Eight names in the variable-explorer prefix: two of our own, and both DOM handles rather
        // than style hooks — nothing in this package or in Stiler defines a rule for either — and
        // six of helsedata's, every one read back off their compiled variables.css. This is the
        // guard that says so out loud. The list is exact on purpose: a ninth name appearing here,
        // or a name in it that cannot be pointed at in a stylesheet, is the failure this package
        // exists to avoid, and it has happened twice.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.Search, "tale"));

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            "variable-explorer",            // ours, a handle
            "variable-explorer-filters",    // ours, a handle
            "variable-explorer-container",  // theirs, variables.css (10 rules)
            "variable-explorer-results",    // theirs, variables.css (6 rules)
            // The column picker, all four theirs, all four read off the compiled variables.css
            // rather than guessed at. The one they do NOT include is `sortable-dropdown`, which
            // the bead pointed at: that is their mobile sort control, `display: none` above
            // 1280px, so a picker wearing it would be invisible on every desktop.
            "variable-explorer-header",                  // theirs, variables.css
            "variable-explorer-header__actions",         // theirs, variables.css
            "variable-explorer__dropdown",               // theirs, variables.css (the z-index)
            "variable-explorer-header__actions-button",  // theirs, variables.css
        ], invented);
        Assert.Equal("variable-explorer", cut.Find("section").ClassName);

        // The filter panel wears Stiler's fieldset alongside the handle, so a host that styles
        // nothing still gets the fieldset the sort control gets.
        Assert.Contains("form-fieldset", cut.Find(".variable-explorer-filters").ClassName!);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenStilersCardLayoutIsUsedForTheResults()
    {
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))));

        Assert.NotNull(cut.Find("ul.variable-data-list > li.variable-data-list__item > div.variable-data-list__item__row"));
        Assert.NotNull(cut.Find(".variable-dataitem-main__name"));
        Assert.NotNull(cut.Find(".variable-dataitem-main__column > .variable-dataitem-main__column__text"));
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
        var name = cut.Find("ul.variable-data-list").GetAttribute("aria-label")!;

        Assert.Contains("1 variabel funnet", name);
        Assert.Contains("«tale»", name);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenTheNameIsTheDisclosureAndNotAHeading()
    {
        // This guarded the opposite until the row became a flex container. A heading per result
        // would let a screen-reader user move between them with the heading rotor, which is why it
        // was there — but helsedata sizes the name cell with `variable-dataitem-main__name`, and a
        // heading in between becomes the flex item, so the column stops lining up with its header.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "KODE"))),
                            b => b.Add(c => c.HeadingLevel, 3));

        // No heading per result. An earlier version wrapped the name in one so results could be
        // walked with a heading rotor, but their row is `display: flex` and the name cell is sized
        // by `.variable-dataitem-main__name` — a heading in between becomes the flex item and the
        // column falls out of line with its header. Neither reference wraps it: helsedata puts the
        // button straight in the row, and Runa's rows are table rows. The rows are a list of list
        // items, each with a named disclosure carrying aria-expanded.
        Assert.Empty(cut.FindAll("li h1, li h2, li h3, li h4, li h5, li h6"));

        var name = cut.Find("li .variable-dataitem-main__name");

        Assert.Equal("1. Tale", name.TextContent);
        Assert.Equal("BUTTON", name.TagName);
    }

    [Fact]
    public void Render_WhenTheSearchHasHits_ThenEveryFieldIsLabelledWithWhatItIs()
    {
        // A table had column headers doing this job. A card has nothing, and "Inklusjon" on
        // its own does not say which field it is.
        var cut = RenderWith(new FakeClient(OnePage(Variable("1. Tale", "V_ALS.F1.TALE"))));

        var info = cut.Find(".variable-dataitem-main").TextContent;

        // The column header names the field on screen, so no cell shows its own label. The label
        // is still in the DOM for a screen reader moving down a column, inside Stiler's
        // screenreader-only span — visible to assistive technology, not to the eye.
        Assert.Contains("V_ALS.F1.TALE", info);
        Assert.Contains("Als registeret", info);
        Assert.Contains("Inklusjon", info);

        var cell = cut.Find(".variable-dataitem-main__code");

        Assert.Equal("Kode: ", cell.QuerySelector(".screenreader-only")!.TextContent);
        Assert.Equal("V_ALS.F1.TALE",
                     cell.QuerySelector(".variable-dataitem-main__column__text")!.TextContent);
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

        var list = cut.Find("ul.variable-data-list");

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

        var cell = cut.Find(".variable-dataitem-main__source");

        Assert.Equal("ALS", cell.QuerySelector(".variable-dataitem-main__column__text")!.TextContent);
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

        var cell = cut.Find(".variable-dataitem-main__source");

        Assert.Equal("Et register uten kortnavn",
                     cell.QuerySelector(".variable-dataitem-main__column__text")!.TextContent);
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

        var info = cut.Find(".variable-dataitem-main");

        Assert.Contains("Ikke oppgitt", info.TextContent);
        // The field name is present for assistive technology, hidden from the eye.
        Assert.Equal("Kilde: ",
                     cut.Find(".variable-dataitem-main__source .screenreader-only").TextContent);
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
        Assert.DoesNotContain("Hvordan er talen?", cut.Find(".variable-dataitem-main").TextContent);
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

        Assert.Equal("no", cut.Find(".variable-dataitem-main__name .variable-dataitem-main__column__text").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".variable-dataitem-main__column__text span[lang]").GetAttribute("lang"));
        Assert.False(cut.Find("ul.variable-data-list").HasAttribute("lang"));
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

        Assert.Equal("false", cut.Find("ul.variable-data-list").GetAttribute("aria-busy"));

        cut.Find("form").Submit(); // second search, still in flight

        Assert.Equal("true", cut.Find("ul.variable-data-list").GetAttribute("aria-busy"));

        await cut.InvokeAsync(() => client.Answer(hits));

        cut.WaitForAssertion(() =>
            Assert.Equal("false", cut.Find("ul.variable-data-list").GetAttribute("aria-busy")));
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

        ClickSort(cut, "Kilde"); // something for the page turn to preserve

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
        Assert.Empty(cut.FindAll("ul.variable-data-list > li"));
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
        Assert.Equal(25, cut.FindAll("ul.variable-data-list > li").Count);
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
        Assert.Equal(10, cut.FindAll("ul.variable-data-list > li").Count);

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
        Assert.Equal(12, cut.FindAll("ul.variable-data-list > li").Count);
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
        // Ahead of the ROWS, not ahead of the header: the header holds four sort buttons and is
        // worth tabbing through. It is the twenty-five variables the link exists to skip.
        Assert.True(markup.IndexOf("skiplink-pagination", StringComparison.Ordinal)
                    < markup.IndexOf("<ul class=\"variable-data-list\"", StringComparison.Ordinal));
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
            string? search = null, VariableFilter? filter = null, string? language = null, CancellationToken cancellationToken = default)
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
        Assert.Single(cut.FindAll("ul.variable-data-list > li"));
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
    public void Render_WhenNothingMatchesForAnEnglishReader_ThenTheEmptyStateIsEnglishToo()
    {
        // NoResults is a delegate, so the parity test cannot compare it without arguments — and
        // unlike the other two delegates nothing asserted it where it is rendered either, which
        // left the English empty state as the one user-facing string in the package with no
        // guard at all. A completely routine path: an English reader whose search matches nothing.
        var cut = RenderWith(new FakeClient(OnePage()), b => b.Add(c => c.Language, "en"));

        Assert.Equal("No variables matched your search.", StatusLine(cut));
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
            "No variables matched your search for \u201csvelging\u201d with the filters you have chosen.",
            StatusLine(cut));
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

        var group = cut.Find(".variable-explorer-group ~ dl:last-of-type");

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
        var list = cut.Find(".variable-explorer-group ~ dl:last-of-type");

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

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            LastDetailId = id;
            LastIncludeHistorical = includeHistorical;

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

        /// <summary>Fail every codes fetch from the next one on.</summary>
        public bool FailCodes { get; set; }

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
        public void AnswerStalledCodes(KodeverkCodes codes) =>
            _codeStalls.First(stall => !stall.Task.IsCompleted).TrySetResult(codes);

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference,
            CancellationToken cancellationToken = default)
        {
            CodeRequests.Add((variableId, kodeverkType, kodeverkReference));

            if (FailCodes)
            {
                throw new HttpRequestException("nede");
            }

            if (StallCodes)
            {
                var stall = new TaskCompletionSource<KodeverkCodes?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
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
        cut.FindAll("ul.variable-data-list .variable-dataitem-main__name");
    // The variable's own name is the disclosure — helsedata's pattern. There is no longer a
    // separate "Vis detaljer" button under the metadata line.

    private static AngleSharp.Dom.IElement Panel(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".variable-explorer-detail");

    /// <summary>The panel's values, in the order the definition list draws them.</summary>
    // ---------------------------------------------------------------------------------
    // The panel's tabs. Runa splits an open row into what the variable IS and what its
    // data holds; helsedata supplies the dress. What has to hold is that the split is
    // announced correctly and reachable from a keyboard, because a tablist that costs
    // one tab stop is unusable without arrow keys.

    private static IReadOnlyList<AngleSharp.Dom.IElement> TabButtons(
        IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".variable-meta__tabs [role=tab]");

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

    [Fact]
    public void Panel_WhenAnArrowKeyIsPressed_ThenTheTabMoves()
    {
        // The APG tabs pattern. Without it a keyboard user reaches the tablist and cannot leave the
        // first tab: the others carry tabindex="-1", so Tab does not reach them.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();
        cut.Find(".variable-meta__tabs").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("true", TabButtons(cut)[1].GetAttribute("aria-selected"));

        cut.Find(".variable-meta__tabs").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Wraps rather than stopping, so the movement has no dead end.
        Assert.Equal("true", TabButtons(cut)[0].GetAttribute("aria-selected"));

        cut.Find(".variable-meta__tabs").KeyDown(new KeyboardEventArgs { Key = "End" });

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
        [.. Panel(cut).QuerySelectorAll(".variable-explorer-group").Select(h => h.TextContent)];

    private static IReadOnlyList<AngleSharp.Dom.IElement> KodeverkLines(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. Panel(cut).QuerySelectorAll("li.variable-explorer-kodeverk__item")];

    private static IReadOnlyList<AngleSharp.Dom.IElement> CodeToggles(
        IRenderedComponent<VariableExplorer> cut) =>
        [.. Panel(cut).QuerySelectorAll("li.variable-explorer-kodeverk__item > button")];

    [Fact]
    public void Kodeverk_WhenTheDataTabIsOpen_ThenTheLinksAreGroupedByKindInPayloadOrder()
    {
        var cut = OpenData(KodeverkRows());

        // One heading per kind, in the order the payload first mentions each — not one heading per
        // link, and not an order of ours. The two kildekodeverk are apart in the payload, so a
        // grouping that only worked on adjacent links would show four headings here.
        Assert.Equal(["Kildekodeverk", "Administrativt kodeverk", "Helsefaglig kodeverk"],
                     KodeverkGroupHeadings(cut));

        var groups = Panel(cut).QuerySelectorAll("ul.variable-explorer-kodeverk");

        Assert.Equal([2, 1, 1], groups.Select(g => g.QuerySelectorAll("li").Length));
    }

    [Fact]
    public void Kodeverk_WhenTheApiResolvedNoName_ThenTheReferenceIsLabelledRatherThanStandingIn()
    {
        // The bead's own case, measured against variable bc8a6515: displayName is null for its one
        // link, and the flat list this replaces rendered that as "Kildekodeverk: 2336" — which
        // reads as the kodeverk being called 2336. The name being unknown is said out loud, and the
        // reference is labelled, so no bare code is the only text on a line.
        var cut = OpenData(KodeverkRows());

        var nameless = KodeverkLines(cut)[0];

        Assert.Equal("Ukjent navn", nameless.QuerySelector(".variable-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 2336",
                     nameless.QuerySelector(".variable-explorer-kodeverk__reference")!.TextContent);
        Assert.DoesNotContain("Kildekodeverk: 2336", Panel(cut).TextContent);
    }

    [Fact]
    public void Kodeverk_WhenTheApiResolvedAName_ThenBothTheNameAndTheReferenceAreShown()
    {
        // The reference is on every line, named one and nameless alike: it is the thing a reader
        // can look the kodeverk up by, and a name alone cannot be typed into anything.
        var cut = OpenData(KodeverkRows());

        var named = KodeverkLines(cut)[1];

        Assert.Equal("Skjemastatus", named.QuerySelector(".variable-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Referanse: 2337",
                     named.QuerySelector(".variable-explorer-kodeverk__reference")!.TextContent);

        // The catalogue's own name, so a screen reader on an English page still says it in
        // Norwegian rather than reading it with English phonetics.
        Assert.Equal("no", named.QuerySelector(".variable-explorer-kodeverk__name")!.GetAttribute("lang"));
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
    public void Codes_WhenTheTabIsOpened_ThenNothingIsFetchedUntilAReaderAsks()
    {
        // The reason the codes are their own endpoint at all. Kommunenummer is 885 codes and most
        // readers open none of them, so opening a row must not pay for a list nobody reads.
        var client = KodeverkRows();
        var cut = OpenData(client);

        Assert.Empty(client.CodeRequests);
        Assert.Equal(["Vis koder", "Vis koder", "Vis koder"], CodeToggles(cut).Select(b => b.TextContent));
        Assert.All(CodeToggles(cut), b => Assert.Equal("false", b.GetAttribute("aria-expanded")));
    }

    [Fact]
    public void Codes_WhenTheLinkServesNone_ThenThereIsNoControlToPress()
    {
        // Every HelsefagligKodeverk link answers 404, which the payload says up front with
        // harKodeverdier. A button that could only ever report "no code values" is worse than none.
        var cut = OpenData(KodeverkRows());

        Assert.Equal(4, KodeverkLines(cut).Count);
        Assert.Equal(3, CodeToggles(cut).Count);
        Assert.Empty(KodeverkLines(cut)[3].QuerySelectorAll("button"));
    }

    [Fact]
    public void Codes_WhenVisKoderIsPressed_ThenOneRequestIsMadeAndTheFourColumnsAreShown()
    {
        // The rest of the acceptance criterion: the codes arrive from their own endpoint, for the
        // link that was pressed, and read as Verdi / Navn / Gyldig fra / Gyldig til.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        Assert.Equal((TaleId, "Kildekodeverk", "2336"), Assert.Single(client.CodeRequests));

        var table = Panel(cut).QuerySelector(".variable-explorer-codes table")!;

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

        CodeToggles(cut)[2].Click();

        var cells = Panel(cut).QuerySelectorAll(".variable-explorer-codes tbody td")
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

        Assert.Empty(Panel(cut).QuerySelectorAll(".variable-explorer-codes"));
        Assert.Equal("Vis koder", CodeToggles(cut)[0].TextContent);

        CodeToggles(cut)[0].Click();

        // The answer was kept, so re-opening costs nothing. Without the cache a reader comparing
        // two kodeverk pays for the same list every time they look back at it.
        Assert.Single(client.CodeRequests);
        Assert.NotNull(Panel(cut).QuerySelector(".variable-explorer-codes table"));
    }

    [Fact]
    public void Codes_WhenTwoListsAreOpened_ThenBothStayOpenWithTheirOwnCodes()
    {
        // Unlike the kilde and datasamling panels, which are one at a time: those answer the same
        // question about the same variable twice, where two kodeverk are two different things.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();
        CodeToggles(cut)[2].Click();

        var tables = Panel(cut).QuerySelectorAll(".variable-explorer-codes table");

        Assert.Equal(2, tables.Length);
        Assert.Contains("Velg verdi", tables[0].TextContent);
        Assert.Contains("Halden", tables[1].TextContent);
        Assert.Equal(2, client.CodeRequests.Count);
    }

    [Fact]
    public void Codes_WhenTheApiPublishesNoneForTheLink_ThenTheListSaysSoRatherThanFailing()
    {
        // A reference the upstream register does not know answers 404, which the client reports as
        // null. That is not a fault and must not be dressed as one — nor asked for again on every
        // expand, which is why the empty answer is cached like any other.
        var client = KodeverkRows();
        var cut = OpenData(client);

        // 2337 is a link the fake knows nothing about, the same as an unpublished reference.
        CodeToggles(cut)[1].Click();

        Assert.Equal("Ingen kodeverdier tilgjengelig",
                     Panel(cut).QuerySelector(".variable-explorer-codes p")!.TextContent);

        CodeToggles(cut)[1].Click();
        CodeToggles(cut)[1].Click();

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

        var message = Panel(cut).QuerySelector(".variable-explorer-codes p")!;

        Assert.Contains("Kunne ikke hente kodene", message.TextContent);
        Assert.Contains("infobox", message.ClassName!);

        // The panel around it is untouched: the other lines still offer their codes, and the rows
        // are still the rows the search returned.
        Assert.Equal(3, CodeToggles(cut).Count);
        Assert.Equal(2, Toggles(cut).Count);
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());

        // Pressing again is the only retry a reader has, and there is no answer being cached over.
        client.FailCodes = false;
        CodeToggles(cut)[0].Click();
        CodeToggles(cut)[0].Click();

        Assert.Equal(2, client.CodeRequests.Count);
        Assert.NotNull(Panel(cut).QuerySelector(".variable-explorer-codes table"));
    }

    [Fact]
    public void Codes_WhenAnotherVariableIsOpened_ThenTheFirstOnesCodesAreNotInherited()
    {
        // Two variables can share a reference, so a cache left behind would look right and be
        // another variable's answer. The fetch is per variable as well as per reference.
        var client = KodeverkRows();
        var cut = OpenData(client);

        CodeToggles(cut)[0].Click();

        Assert.Single(client.CodeRequests);

        Toggles(cut)[1].Click();
        TabButtons(cut)[1].Click();

        // Every list starts collapsed again, and opening one asks for the new variable's codes.
        Assert.All(CodeToggles(cut), b => Assert.Equal("false", b.GetAttribute("aria-expanded")));

        CodeToggles(cut)[0].Click();

        Assert.Equal(2, client.CodeRequests.Count);
        Assert.Equal(SpyttId, client.CodeRequests[1].VariableId);
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

        var message = Panel(cut).QuerySelector(".variable-explorer-codes p")!;

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

        Assert.Single(client.CodeRequests);
        Assert.Equal("Henter koder \u2026",
                     Panel(cut).QuerySelector(".variable-explorer-codes p")!.TextContent);

        // And the one answer fills the list that was re-opened, rather than being orphaned by it.
        await cut.InvokeAsync(() => client.AnswerStalledCodes(Codes2336()));

        cut.WaitForAssertion(() =>
            Assert.Contains("Velg verdi", Panel(cut).QuerySelector(".variable-explorer-codes table")!.TextContent));

        Assert.Single(client.CodeRequests);
    }

    [Fact]
    public async Task Codes_WhenAnotherVariableIsOpenedWhileTheFetchIsOut_ThenTheAbandonedAnswerIsNotShown()
    {
        // The generation guard, and the reason the kilde panel has one. Two variables in this very
        // fixture link to reference 2336, so an answer that outlives the panel it was asked for
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

        await cut.InvokeAsync(() => client.AnswerStalledCodes(Codes2336() with
        {
            Codes = [new() { Value = "9", Name = "STALE" }]
        }));

        // One turn of the dispatcher, so the abandoned answer has landed if it is going to.
        await cut.InvokeAsync(() => { });

        CodeToggles(cut)[0].Click();

        var table = Panel(cut).QuerySelector(".variable-explorer-codes table")!;

        Assert.DoesNotContain("STALE", table.TextContent);
        Assert.Contains("Velg verdi", table.TextContent);

        // Nothing was cached under the new variable, so its list was fetched for it.
        Assert.Equal(2, client.CodeRequests.Count);
        Assert.Equal(SpyttId, client.CodeRequests[1].VariableId);
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

        var region = Panel(cut).QuerySelector(".variable-explorer-codes")!;

        Assert.Equal(region.Id, CodeToggles(cut)[0].GetAttribute("aria-controls"));
        Assert.Equal(KodeverkLines(cut)[0].QuerySelector(".variable-explorer-kodeverk__name")!.Id,
                     region.QuerySelector("table")!.GetAttribute("aria-labelledby"));

        // Column headers, so a screen reader can say which column a cell is in.
        Assert.All(region.QuerySelectorAll("thead th"), th => Assert.Equal("col", th.GetAttribute("scope")));
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
        Assert.Equal("Unnamed",
                     KodeverkLines(cut)[0].QuerySelector(".variable-explorer-kodeverk__name")!.TextContent);
        Assert.Equal("Reference: 2336",
                     KodeverkLines(cut)[0].QuerySelector(".variable-explorer-kodeverk__reference")!.TextContent);

        CodeToggles(cut)[0].Click();

        Assert.Equal("Hide codes", CodeToggles(cut)[0].TextContent);
        Assert.Equal(["Value", "Name", "Valid from", "Valid to"],
                     Panel(cut).QuerySelectorAll(".variable-explorer-codes thead th").Select(th => th.TextContent));

        // The date follows the page too: an English reader gets slashes, not the dots a Norwegian
        // reader gets. Pinned by separator rather than by exact string, which is ICU's to change.
        Assert.Matches(@"^\d{1,2}/\d{1,2}/2010$",
                       Panel(cut).QuerySelectorAll(".variable-explorer-codes tbody td")[2].TextContent);
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
            .Where(k => k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            "variable-explorer-group",                 // ours, already in use on the Details tab
            "variable-explorer-kodeverk",              // ours, a handle — the list of links
            "variable-explorer-kodeverk__item",
            "variable-explorer-kodeverk__name",
            "variable-explorer-kodeverk__reference",
            "variable-explorer-codes",                 // ours, a handle — the open code list
            "variable-explorer-codes__table",
        ], invented);

        var toggle = CodeToggles(cut)[0].ClassName!;

        Assert.Contains("hd-button-square", toggle);
        Assert.Contains("button-square--ghost", toggle);
        Assert.Contains("caption",
                        KodeverkLines(cut)[0].QuerySelector(".variable-explorer-kodeverk__reference")!.ClassName!);
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
        Assert.Empty(cut.FindAll("ul.variable-data-list a"));
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
        Assert.NotNull(values[3].QuerySelector(".variable-explorer-period__fill"));

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
        Assert.Equal(["Ukjent navn", "ICD-10"],
                     KodeverkLines(cut).Select(l => l.QuerySelector(".variable-explorer-kodeverk__name")!.TextContent));
        Assert.Equal(["Referanse: 2336", "Referanse: 2.16.578.1.12.4.1.1.7110"],
                     KodeverkLines(cut).Select(l => l.QuerySelector(".variable-explorer-kodeverk__reference")!.TextContent));
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

        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
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

        Assert.Single(cut.FindAll(".variable-explorer-detail"));
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
        Assert.Equal(2, cut.FindAll("ul.variable-data-list > li").Count);
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));

        // The component's own alert region is for the list, and the list is fine.
        Assert.Equal(string.Empty, cut.Find("[role='alert']").TextContent.Trim());
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

        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
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
            Assert.Single(cut.FindAll(".variable-explorer-detail"));
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

        Assert.Empty(cut.FindAll("ul.variable-data-list > li"));
        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
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

        Assert.Equal(2, cut.FindAll("ul.variable-data-list > li").Count);
        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
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

        Assert.Equal("Side 1 av 2", Position(cut));
        Assert.Single(cut.FindAll("ul.variable-data-list > li"));
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
        Assert.Equal("Side 1 av 2", Position(cut));
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

        Assert.Empty(cut.FindAll(".variable-explorer-detail"));

        // Oldest first: the page turn's abandoned fetch, then the rollback's, which is the one
        // whose continuation reports to the host.
        await cut.InvokeAsync(client.FailStalled);
        await cut.InvokeAsync(client.FailStalled);

        cut.WaitForAssertion(() => Assert.Equal(4, reported.Count));

        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
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
        var heading = cut.FindAll("ul.variable-data-list .variable-dataitem-main__name")[0];

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
        // The component wears helsedata's own variable-page vocabulary, which includes several
        // names in this prefix that are THEIRS, not ours — variable-explorer-container (10 rules),
        // variable-explorer-results (6), and the column picker's header trio plus its dropdown,
        // all in variables.css and loaded on every page of their site. What must never grow is
        // the list of names we INVENT — the handles below, which carry no styling anywhere and
        // exist only so a host can find the component in the DOM. A name in this prefix that is
        // neither theirs nor one of those is a name that renders as a raw browser default.
        var cut = RenderWith(TwoRows());

        Toggles(cut)[0].Click();

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            "variable-explorer",            // ours, a handle
            "variable-explorer-filters",    // ours, a handle
            "variable-explorer-container",  // theirs, variables.css (10 rules)
            "variable-explorer-results",    // theirs, variables.css (6 rules)
            "variable-explorer-header",     // theirs — the row their own variable page hangs the
            "variable-explorer-header__actions",        // column picker in, and the ghost button
            "variable-explorer__dropdown",              // that opens it. All four are variables.css.
            "variable-explorer-header__actions-button",
            "variable-explorer-detail",     // ours, a handle
            "variable-explorer-group",      // ours — helsedata's panel is flat, so it has no
                                            // group heading to borrow a name from
            "variable-explorer-crumb",      // ours — the kilde step, which Runa makes a link and
                                            // we make the control that discloses the kilde
            "variable-explorer-period",     // ours — neither explorer's stylesheet has a period bar
            "variable-explorer-period__range",
            "variable-explorer-period__track",
            "variable-explorer-period__fill",
        ], invented);

        var panel = Panel(cut);

        // The definition list stays a definition list — it is labels and the values they name, and
        // helsedata's grid is class-based (`.variable-meta__grid { display: grid }`, with only a
        // `p { margin: 0 }` rule touching an element name), so their layout applies to dt/dd just
        // as it applies to their own spans. Semantics kept, styling borrowed.
        // Both of helsedata's grid variants are in use: -1 for a group with one field, -2 for the
        // ones with several. Runa's panel does the same.
        Assert.All(panel.QuerySelectorAll("dl"),
                   e => Assert.Contains("variable-meta__grid", e.ClassName!));
        // The multi-field group wears the plain grid, which is helsedata's two-lane one; a group
        // with a single field wears -1, their single-column variant. Both are theirs.
        Assert.All(panel.QuerySelectorAll("dl"),
                   e => Assert.Matches(@"^variable-meta__grid( variable-meta__grid-1)?$", e.ClassName!));
        Assert.All(panel.QuerySelectorAll("ol, ul"), e => Assert.False(e.HasAttribute("class")));
        Assert.All(panel.QuerySelectorAll("dl dt"),
                   e => Assert.Equal("headline headline-xxs margin--none", e.ClassName));
        Assert.Equal("variable-dataitem-main__name", Toggles(cut)[0].ClassName);
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
        Frequency = "Fortløpende",
        // Observed as an empty string in the captured payload rather than as null, which is a
        // different thing for the markup to get right than a missing field.
        CountingUnit = "",
        VariableCount = 99
    };

    /// <summary>The two owner toggles, in the order the panel draws them: kilde, then datasamling.</summary>
    /// <summary>Leaves the kilde view and returns to the list.</summary>
    private static void Back(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".variable-explorer-drilldown button").Click();

    /// <summary>The buttons that open an owner — the kilde and the datasamling.</summary>
    /// <remarks>
    /// <c>button[id]</c>, not every button in the panel: the whole-variable button sits beside these
    /// and is not one of them. Selecting all of them made this helper mean "every button in the
    /// panel" the moment a third arrived, which is not what its name says or what its callers want.
    /// </remarks>
    private static IReadOnlyList<AngleSharp.Dom.IElement> SourceToggles(IRenderedComponent<VariableExplorer> cut) =>
        [.. cut.FindAll(".variable-explorer-detail > button[id]")];

    /// <summary>
    /// The kilde or datasamling view. It is no longer a panel inside the open row — it takes over
    /// the component's area, so the list is not on screen while it is.
    /// </summary>
    private static AngleSharp.Dom.IElement SourcePanel(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".variable-explorer-drilldown");

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
            SourcePanel(cut).QuerySelector(".variable-explorer-kilde__description")!.TextContent.Trim());
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

        Assert.Equal(
            ["Beskrivelse", "Kilde", "Inklusjons- og eksklusjonskriterier", "Dataansvarlig",
             "Databehandler", "Grad av personidentifikasjon", "Lovverk", "Gyldighet", "Frekvens",
             "Telleenhet", "Antall variabler"],
            SourceLabels(cut));

        var values = SourceValues(cut);

        Assert.Equal("Als registeret", values[1]);
        Assert.Equal("Alle pasienter som er 18 år eller eldre.", values[2]);
        Assert.Equal("St. Olavs hospital HF", values[3]);
        Assert.Equal("Indirekte identifiserbar", values[5]);
        Assert.Equal("2010–", values[7]);
        Assert.Equal("Fortløpende", values[8]);

        // Empty rather than missing, and written out for everyone rather than drawn as a dash.
        Assert.Equal("Ikke oppgitt", values[9]);
        Assert.Equal("99", values[10]);
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

        Assert.Single(cut.FindAll(".variable-explorer-drilldown"));
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

        Assert.Empty(cut.FindAll(".variable-explorer-drilldown"));

        // The list comes back as it was — the row is still open, because nothing was torn down.
        Assert.Equal(2, cut.FindAll("ul.variable-data-list > li").Count);
        Assert.Equal("true", Toggles(cut)[0].GetAttribute("aria-expanded"));
        Assert.Equal("false", SourceToggles(cut)[0].GetAttribute("aria-expanded"));

        // Closing asks the API for nothing: it is the panel that is being dropped, not the answer.
        Assert.Equal(1, client.KildeCalls);
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
        Assert.Equal(2, cut.FindAll("ul.variable-data-list > li").Count);
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

        Assert.Empty(cut.FindAll(".variable-explorer-drilldown"));
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
        Assert.Empty(cut.FindAll(".variable-explorer-detail"));

        Back(cut);
        Toggles(cut)[0].Click();

        // Closing the row takes its owner buttons with it, so there is no way back into the kilde
        // view from a row that is shut.
        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
        Assert.Empty(cut.FindAll(".variable-explorer-drilldown"));

        Toggles(cut)[0].Click();

        Assert.Empty(cut.FindAll(".variable-explorer-drilldown"));
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

        Assert.Empty(cut.FindAll(".variable-explorer-detail"));
        Assert.Empty(cut.FindAll(".variable-explorer-drilldown"));
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
        Assert.Contains(cut.FindAll(".variable-explorer-detail > button"),
                        b => b.TextContent.Contains("hele variabelen", StringComparison.Ordinal));
        Assert.Equal(0, client.KildeCalls);
        Assert.Equal(0, client.DatasamlingCalls);
    }

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
            Assert.Single(cut.FindAll(".variable-explorer-drilldown"));
            Assert.Contains("Telleenhet", SourcePanel(cut).TextContent);
            Assert.DoesNotContain("Antall datasamlinger", SourcePanel(cut).TextContent);
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
        var description = SourcePanel(cut).QuerySelector(".variable-explorer-kilde__description")!;

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
        // variable-meta__grid, rather than a new style name of ours.
        var cut = OpenOwner(TwoRows(), 0);

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassName!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        // Every name this package invents, and the list is meant to stay short: each one is a class
        // helsedata's stylesheet has never heard of, so it is inert until Stiler is asked for it.
        // The kilde view adds a layout - a main column, a sidebar - that no existing helsedata class
        // describes, which is why these exist at all. Anything inside that layout uses helsedata's
        // own names.
        Assert.Equal(
            [
                "variable-explorer",
                "variable-explorer-drilldown",
                "variable-explorer-kilde",
                "variable-explorer-kilde__header",
                "variable-explorer-kilde__identifiers",
                "variable-explorer-kilde__kildetype",
                "variable-explorer-kilde__description",
                "variable-explorer-kilde__body",
                "variable-explorer-kilde__main",
                "variable-explorer-kilde__datasamlinger",
                "variable-explorer-kilde__aside",
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
                   e => Assert.Contains(e.GetAttribute("class"), (string?[])[null, "variable-meta__grid"]));

        Back(cut);

        Assert.Contains("hd-button-square", SourceToggles(cut)[0].ClassName!);
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

        Assert.Equal("H1", cut.Find(".variable-explorer > [class*='headline']").TagName);
        Assert.Equal("BUTTON", cut.Find(".variable-data-list__item__row .variable-dataitem-main__name").TagName);
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
}
