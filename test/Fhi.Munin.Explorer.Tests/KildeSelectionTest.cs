using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Kelda's row ticks and the handover they exist for.
/// </summary>
/// <remarks>
/// <para>
/// Two things here have no visible symptom when they break, and both are about what LEAVES the
/// component rather than what it draws. The first is which ids travel: ticked rows win over the
/// filter, an unticked but filtered list travels as what is on screen, and an untouched list
/// travels as nothing at all — three cases that render identically and differ only in the argument
/// the host is handed. A test that clicked the button and asserted the callback fired would pass
/// against any of the three.
/// </para>
/// <para>
/// The second is that the ticks are not pruned. A ticked kilde the reader has since searched past
/// is still ticked, still counted and still travels; an implementation that quietly dropped it
/// would look right on screen — the row is not there to look wrong — and hand over a narrower
/// selection than the reader made.
/// </para>
/// <para>
/// The column is drawn only where the host wired <see cref="KildeExplorer.ExploreVariablesRequested"/>,
/// which is why almost every test here wires it. The one that does not is the one asserting the
/// column is absent, and that case is not hypothetical — ModernHost reached it once. An
/// <see cref="EventCallback"/> created in a statically-rendered parent and passed into an
/// interactive island serialises as <c>{"HasDelegate":true}</c> and comes back empty, so the
/// callback is there in the markup and gone in the circuit. bUnit cannot stage that boundary;
/// what the test below pins is the behaviour the component shows once it is on the wrong side of
/// one.
/// </para>
/// </remarks>
public class KildeSelectionTest : BunitContext
{
    private static KildeSummary Kilde(string name, string code, string kildetype = "sentraltHelseregister") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Kildetype = kildetype,
            IsActive = true,
            DataController = "Folkehelseinstituttet",
            DataProcessor = "Folkehelseinstituttet",
            DelkildeCount = 0,
            DatasamlingCount = 3,
            TotalVariables = 42,
            AdditionalProperties = new Dictionary<string, string?>(StringComparer.Ordinal),
        };

    private sealed class FakeClient(params KildeSummary[] kilder) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(kilder);

        public override Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PropertyMetadataEntry>>([]);
    }

    /// <summary>
    /// Render with the handover wired, and hand back the list every request appends to.
    /// </summary>
    /// <remarks>
    /// The list is the assertion surface for most of this file: what the host is handed is the
    /// whole of the contract, and the only place the three handover cases are told apart.
    /// </remarks>
    private (IRenderedComponent<KildeExplorer> Cut, List<IReadOnlyList<Guid>> Handovers) RenderSelectable(
        IMuninExplorerClient client,
        Action<ComponentParameterCollectionBuilder<KildeExplorer>>? parameters = null)
    {
        Services.AddSingleton(client);

        var handovers = new List<IReadOnlyList<Guid>>();

        var cut = Render<KildeExplorer>(b =>
        {
            b.Add(c => c.ExploreVariablesRequested,
                EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, handovers.Add));

            parameters?.Invoke(b);
        });

        return (cut, handovers);
    }

    private static IReadOnlyList<IElement> RowBoxes(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder tbody .munin-explorer-kilder__select input")];

    private static IElement HeaderBox(IRenderedComponent<KildeExplorer> cut) =>
        cut.Find(".munin-explorer-kilder thead .munin-explorer-kilder__select input");

    /// <summary>Tick the row whose name button reads <paramref name="name"/>.</summary>
    /// <remarks>
    /// Found by name on every call rather than held, for the reason the facet helper next door
    /// gives: ticking re-renders, and an element found before that belongs to the markup as it was.
    /// </remarks>
    private static void TickRow(IRenderedComponent<KildeExplorer> cut, string name, bool ticked = true) =>
        cut.FindAll(".munin-explorer-kilder tbody tr")
           .Single(row => row.QuerySelector("th button")!.TextContent.Trim() == name)
           .QuerySelector(".munin-explorer-kilder__select input")!
           .Change(ticked);

    private static string SelectionLine(IRenderedComponent<KildeExplorer> cut) =>
        cut.FindAll("p[role=status]")
           .Last()
           .TextContent
           .Trim();

    /// <summary>The handover button, and the reset beside it.</summary>
    /// <remarks>
    /// Selected as direct children of the component's own section, because `button-square--primary`
    /// is not unique on this screen: the search field's Søk button wears it too, and it comes
    /// first. A test that asked for the class alone clicked the search button, which does nothing
    /// visible — the list is already in hand — so every handover assertion failed on an empty list
    /// rather than on a wrong one.
    /// </remarks>
    private static IElement ExploreButton(IRenderedComponent<KildeExplorer> cut) =>
        cut.Find(".munin-explorer > button.button-square--primary");

    /// <inheritdoc cref="ExploreButton"/>
    private static IReadOnlyList<IElement> ResetButtons(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer > button.button-square--secondary")];

    private static IReadOnlyList<string> RowNames(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder tbody th button").Select(b => b.TextContent.Trim())];

    // ---------------------------------------------------------------------------------
    // Whether the column is there at all.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Column_WhenTheHostWiredNoHandover_ThenThereIsNoCheckboxAndNoButton()
    {
        // The ticks have one destination and this component cannot reach it alone. A column and a
        // primary button that lead nowhere would cost the reader the work of choosing before
        // telling them there was nothing to choose for.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeExplorer>();

        Assert.Empty(cut.FindAll(".munin-explorer-kilder__select"));
        Assert.DoesNotContain("Utforsk variabler for utvalget", cut.Markup);
        Assert.DoesNotContain("Nullstill utvalg", cut.Markup);
    }

    [Fact]
    public void Column_WhenTheHostWiredTheHandover_ThenEveryRowHasABoxAndTheHeaderHasOne()
    {
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        Assert.Equal(2, RowBoxes(cut).Count);
        Assert.NotNull(HeaderBox(cut));
        Assert.Contains("Utforsk variabler for utvalget", cut.Markup);
    }

    [Fact]
    public void Column_WhenTheBoxesAreRendered_ThenEachCarriesAnAccessibleNameOfItsOwn()
    {
        // A column of boxes all named "Velg" tells a reader moving from control to control which
        // column they are in and nothing about which row. The row's name is the only thing that
        // does, and there is no visible label to supply it.
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        Assert.Equal(
            ["Velg Als registeret", "Velg Dødsårsaksregisteret"],
            RowBoxes(cut).Select(box => box.GetAttribute("aria-label")));

        Assert.Equal("Velg alle synlige kilder", HeaderBox(cut).GetAttribute("aria-label"));
    }

    [Fact]
    public void Column_WhenTheReaderIsEnglish_ThenTheSelectionSpeaksEnglish()
    {
        var (cut, _) = RenderSelectable(
            new FakeClient(Kilde("Als registeret", "K_ALS")),
            b => b.Add(c => c.Language, "en"));

        TickRow(cut, "Als registeret");

        Assert.Equal("Select Als registeret", RowBoxes(cut)[0].GetAttribute("aria-label"));
        Assert.Equal("Select all visible sources", HeaderBox(cut).GetAttribute("aria-label"));
        Assert.Equal("1 source selected", SelectionLine(cut));
        Assert.Contains("Explore variables for this selection", cut.Markup);
        Assert.Contains("Clear selection", cut.Markup);
    }

    // ---------------------------------------------------------------------------------
    // The count, and the live region it is read out of.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Count_WhenNothingIsTicked_ThenTheRegionIsOnScreenAndEmpty()
    {
        // Not "absent". A polite region inserted and filled in one DOM update is announced
        // unreliably, so the first tick would be the one nobody hears — the same reason the error
        // container above it is always rendered.
        var (cut, _) = RenderSelectable(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var region = cut.FindAll("p[role=status]").Last();

        Assert.Equal(string.Empty, region.TextContent.Trim());
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Equal("true", region.GetAttribute("aria-atomic"));
    }

    [Fact]
    public void Count_WhenOneIsTicked_ThenItIsSaidInTheSingular()
    {
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");

        Assert.Equal("1 kilde valgt", SelectionLine(cut));
    }

    [Fact]
    public void Count_WhenTwoAreTicked_ThenItIsSaidInThePlural()
    {
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");
        TickRow(cut, "Dødsårsaksregisteret");

        Assert.Equal("2 kilder valgt", SelectionLine(cut));
    }

    [Fact]
    public void Count_WhenATickedKildeIsSearchedPast_ThenItIsStillCounted()
    {
        // The reason the count is in words at all. The reader ticked three, typed a word, and can
        // now see one of them — the number is the only place the other two are still visible.
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");
        cut.Find(".searchbox__freetext").Change("dødsårsak");

        Assert.Equal(["Dødsårsaksregisteret"], RowNames(cut));
        Assert.Equal("1 kilde valgt", SelectionLine(cut));
    }

    // ---------------------------------------------------------------------------------
    // Velg alle, which is over the visible rows and no others.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void SelectAll_WhenPressedUnderASearch_ThenOnlyTheRowsOnScreenAreTicked()
    {
        var (cut, handovers) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR"),
            Kilde("Norsk pasientregister", "K_NPR")));

        cut.Find(".searchbox__freetext").Change("registeret");

        Assert.Equal(["Als registeret", "Dødsårsaksregisteret"], RowNames(cut));

        HeaderBox(cut).Change(true);

        Assert.Equal("2 kilder valgt", SelectionLine(cut));

        ExploreButton(cut).Click();

        Assert.Equal(2, handovers.Single().Count);
    }

    [Fact]
    public void SelectAll_WhenEveryVisibleRowIsAlreadyTicked_ThenTheBoxIsChecked()
    {
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");

        Assert.False(HeaderBox(cut).HasAttribute("checked"));

        TickRow(cut, "Dødsårsaksregisteret");

        Assert.True(HeaderBox(cut).HasAttribute("checked"));
    }

    [Fact]
    public void SelectAll_WhenTheSearchMatchedNothing_ThenTheBoxIsNotChecked()
    {
        // All over no rows is true, and a box that ticks itself when there is nothing on screen
        // tells the reader they have selected something. The table is not drawn at all here, so the
        // assertion is that there is no header box to be wrong — and that the bar is still there,
        // because the reader may have ticks the search has hidden.
        var (cut, _) = RenderSelectable(new FakeClient(Kilde("Als registeret", "K_ALS")));

        TickRow(cut, "Als registeret");
        cut.Find(".searchbox__freetext").Change("hjortedyr");

        Assert.Empty(cut.FindAll(".munin-explorer-kilder"));
        Assert.Equal("1 kilde valgt", SelectionLine(cut));
    }

    [Fact]
    public void SelectAll_WhenPressedAgainUnderASearch_ThenTicksOutsideItSurvive()
    {
        // The half of this control that is easy to get wrong: unticking has to mean "these rows",
        // not "everything". A reader who ticked two kilder, searched, and cleared the one row the
        // search left still has the other two — anything else makes the same control mean two
        // different things depending on which way it is pressed.
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR"),
            Kilde("Norsk pasientregister", "K_NPR")));

        TickRow(cut, "Als registeret");
        TickRow(cut, "Dødsårsaksregisteret");

        cut.Find(".searchbox__freetext").Change("pasient");
        HeaderBox(cut).Change(true);

        Assert.Equal("3 kilder valgt", SelectionLine(cut));

        HeaderBox(cut).Change(false);

        Assert.Equal("2 kilder valgt", SelectionLine(cut));
    }

    // ---------------------------------------------------------------------------------
    // Nullstill utvalg.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Reset_WhenNothingIsTicked_ThenThereIsNoButtonToPress()
    {
        var (cut, _) = RenderSelectable(new FakeClient(Kilde("Als registeret", "K_ALS")));

        Assert.Empty(ResetButtons(cut));
    }

    [Fact]
    public void Reset_WhenPressed_ThenTheTicksTheSearchIsHidingGoAsWell()
    {
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");
        TickRow(cut, "Dødsårsaksregisteret");

        cut.Find(".searchbox__freetext").Change("dødsårsak");
        ResetButtons(cut).Single().Click();

        Assert.Equal(string.Empty, SelectionLine(cut));
        Assert.Empty(ResetButtons(cut));
    }

    // ---------------------------------------------------------------------------------
    // The handover: which ids leave, and in what order.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Handover_WhenRowsAreTicked_ThenThoseIdsTravelEvenWhereTheSearchHidesThem()
    {
        // Case one of three, and the one that says the ticks beat the filter. A reader who marked a
        // kilde deliberately and then typed a word to go and find another has not unmarked the
        // first.
        var als = Kilde("Als registeret", "K_ALS");
        var dar = Kilde("Dødsårsaksregisteret", "K_DAR");

        var (cut, handovers) = RenderSelectable(new FakeClient(als, dar));

        TickRow(cut, "Als registeret");
        cut.Find(".searchbox__freetext").Change("dødsårsak");
        ExploreButton(cut).Click();

        Assert.Equal([als.Id], handovers.Single());
    }

    [Fact]
    public void Handover_WhenNothingIsTickedButASearchIsInForce_ThenTheVisibleIdsTravel()
    {
        // Case two. Most of what Kelda filters on has no facet on the other side, so carrying the
        // ids the filter left is what reproduces the reader's scope over there.
        var als = Kilde("Als registeret", "K_ALS");
        var dar = Kilde("Dødsårsaksregisteret", "K_DAR");
        var npr = Kilde("Norsk pasientregister", "K_NPR");

        var (cut, handovers) = RenderSelectable(new FakeClient(als, dar, npr));

        cut.Find(".searchbox__freetext").Change("registeret");
        ExploreButton(cut).Click();

        Assert.Equal([als.Id, dar.Id], handovers.Single());
    }

    [Fact]
    public void Handover_WhenAFacetIsTickedWithNoSearch_ThenTheVisibleIdsTravel()
    {
        // The same case reached the other way. A facet narrows without touching the search box, and
        // an implementation that only looked at the search text would hand over the whole catalogue
        // from a screen showing two rows.
        var als = Kilde("Als registeret", "K_ALS");
        var kvalitet = Kilde("Norsk hjerteinfarktregister", "K_NHR", "nasjonaltMedisinskKvalitetsregister");

        var (cut, handovers) = RenderSelectable(new FakeClient(als, kvalitet));

        cut.FindAll(".munin-explorer-filters__facets [role=group]")
           .Single(group => group.QuerySelector("h4")!.TextContent.Trim() == "Kildetype")
           .QuerySelectorAll("label")
           .First(label => label.TextContent.Trim().StartsWith("Sentralt helseregister", StringComparison.Ordinal))
           .QuerySelector("input")!
           .Change(true);

        Assert.Equal(["Als registeret"], RowNames(cut));

        ExploreButton(cut).Click();

        Assert.Equal([als.Id], handovers.Single());
    }

    [Fact]
    public void Handover_WhenNothingIsTickedAndNothingIsFiltered_ThenNothingTravels()
    {
        // Case three, and the one that is a decision rather than a fallback: an empty list means
        // the whole variable catalogue, not a selection of none. Handing over every id in an
        // untouched list would produce a URL carrying 72 parameters that says exactly what no
        // parameters says.
        var (cut, handovers) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        ExploreButton(cut).Click();

        Assert.Empty(handovers.Single());
    }

    [Fact]
    public void Handover_WhenTicksAreMadeOutOfOrder_ThenTheIdsTravelInTheListsOwnOrder()
    {
        // What makes the link the host builds out of these shareable: two readers who tick the same
        // three kilder in different orders have to produce the same URL. A HashSet has no order to
        // promise, so the order is taken from the list the reader was looking at.
        var als = Kilde("Als registeret", "K_ALS");
        var dar = Kilde("Dødsårsaksregisteret", "K_DAR");
        var npr = Kilde("Norsk pasientregister", "K_NPR");

        var (cut, handovers) = RenderSelectable(new FakeClient(als, dar, npr));

        TickRow(cut, "Norsk pasientregister");
        TickRow(cut, "Als registeret");
        ExploreButton(cut).Click();

        Assert.Equal([als.Id, npr.Id], handovers.Single());
    }

    [Fact]
    public void Handover_WhenItHasBeenAskedFor_ThenTheTicksAreStillThere()
    {
        // The host may not have navigated at all — it decides what the request means. Clearing the
        // selection here would leave a reader who came back to a component that had forgotten what
        // they chose.
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");
        ExploreButton(cut).Click();

        Assert.Equal("1 kilde valgt", SelectionLine(cut));
        Assert.True(RowBoxes(cut)[0].HasAttribute("checked"));
    }

    [Fact]
    public void Handover_WhenTheHostsHandlerThrows_ThenTheComponentIsStillStanding()
    {
        // The host's exception is the host's to find in the host's logs. Letting it out of here
        // would tear down the circuit for the whole CMS page rather than for this component — the
        // reasoning RaiseAsync carries, asserted at the one new door into it.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeExplorer>(b => b.Add(
            c => c.ExploreVariablesRequested,
            EventCallback.Factory.Create<IReadOnlyList<Guid>>(
                this, _ => throw new InvalidOperationException("the host's own routing"))));

        ExploreButton(cut).Click();

        Assert.Equal(["Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Ticks_WhenAKildeIsOpenedAndClosed_ThenTheySurviveTheDrillIn()
    {
        // Nothing is torn down when the view takes over, so this is a statement about the list
        // being kept rather than refetched — and about the reader not losing a selection to a click
        // they made to check what one of the rows actually was.
        var (cut, _) = RenderSelectable(new FakeClient(
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR")));

        TickRow(cut, "Als registeret");

        cut.Find(".munin-explorer-kilder tbody th button").Click();
        cut.Find(".munin-explorer-drilldown button").Click();

        Assert.Equal("1 kilde valgt", SelectionLine(cut));
    }

    // ---------------------------------------------------------------------------------
    // Class names.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenTheColumnIsOnScreen_ThenEveryClassNameIsOneSomeStylesheetDefines()
    {
        // The state KildeExplorerTest's own guards cannot reach: they render without the handover
        // wired, so the selection column and its bar are not in their DOM at all.
        var (cut, _) = RenderSelectable(new FakeClient(Kilde("Als registeret", "K_ALS")));

        TickRow(cut, "Als registeret");

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_WhenTheColumnIsOnScreen_ThenItAddsExactlyOneInventedName()
    {
        // An exact list, like the one next door, and this is the difference between them: with the
        // handover wired the component writes one further name of its own. A second one appearing
        // here is news that has to be answered in both sample stylesheets before it ships.
        var (cut, _) = RenderSelectable(new FakeClient(Kilde("Als registeret", "K_ALS")));

        TickRow(cut, "Als registeret");

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
            "munin-explorer-kilder__select",     // this bead's, and the only one it adds
            "munin-explorer-results",            // shared
        ], invented);
    }

    [Fact]
    public void SelectColumn_WhenAHostStylesIt_ThenTheDeclarationItNeedsIsAWidth()
    {
        // Same shape as the facet fold's guard and the skip link's before it. The general guards
        // ask whether a name has a rule that declares SOMETHING, which a rule setting only a colour
        // would satisfy. What a host must actually supply is the width: a table shares itself out
        // between its columns, so one holding a single checkbox takes the same share as
        // Dataansvarlig and squeezes the eight columns that carry words.
        var rules = HostClassNames.SampleDeclarationsFor("munin-explorer-kilder__select");

        static string Squeezed(string css) => new([.. css.Where(c => !char.IsWhiteSpace(c))]);

        Assert.True(
            rules.Any(rule => Squeezed(rule.Declarations).Contains("width:", StringComparison.Ordinal)),
            "No rule gives the checkbox column a width, so it takes an equal share of the table.");
    }

    // ---------------------------------------------------------------------------------
    // The search field the ticks live under.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void SearchField_WhenItIsRendered_ThenItIsNotASearchInputWithAClearButtonWeCannotHook()
    {
        // The field is bound on change, deliberately - see the markup. A type="search" input
        // then carries a user-agent clear button this component cannot hook: the ✕ fires the DOM
        // `search` event, which is not one Blazor knows, so the box empties while the filter it
        // set stays in force. Everything downstream - the row ticks, velg-alle, the handover -
        // then operates on a subset the reader believes they have cleared, over a search box
        // reading empty. Reported against Kelda on 2026-08-27.
        //
        // Asserted on the type rather than on the behaviour because the behaviour cannot be
        // staged here: bUnit has no user agent, so nothing in this suite can press a ✕ that only
        // a browser draws. What a test CAN pin is that the element which draws one is not used.
        var cut = RenderSelectable(new FakeClient(Kilde("Als registeret", "K_ALS"))).Cut;

        var field = cut.Find(".searchbox__freetext");

        Assert.Equal("text", field.GetAttribute("type"));

        // The half of type="search" worth keeping: a soft keyboard still offers a search key.
        Assert.Equal("search", field.GetAttribute("enterkeyhint"));
    }
}
