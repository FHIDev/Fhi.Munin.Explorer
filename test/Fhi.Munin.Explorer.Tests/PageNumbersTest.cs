using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The run of numbers the pager draws, which is the shape decided in Fhi.Metadata-ejcbi: the RCL
/// showed "Side 1 av 907" between two buttons, so page 453 was 452 presses away.
/// </summary>
/// <remarks>
/// Tested apart from the markup because it is where the reachability lives. A run that quietly
/// stopped including the last page would still render, still read as a pager, and still leave the
/// end of a 907-page list unreachable in one press.
/// </remarks>
public class PageNumbersTest : BunitContext
{
    [Fact]
    public void Window_WhenOnTheFirstPageOfMany_ThenItRunsFromOneAndSkipsToTheLast()
    {
        // helsedata.no's own run, which is the one this mimics: 1 2 3 … 100.
        Assert.Equal([1, 2, 3, null, 100], PageNumbers.Window(page: 1, totalPages: 100));
    }

    [Fact]
    public void Window_WhenDeepInTheList_ThenBothEndsAreStillOnePressAway()
    {
        // The whole point of the shape. Neither 1 nor 100 may fall out of the run when the reader
        // walks into the middle, or the way back is as long as the way in.
        Assert.Equal([1, null, 49, 50, 51, null, 100], PageNumbers.Window(page: 50, totalPages: 100));
    }

    [Fact]
    public void Window_WhenOnTheLastPage_ThenTheRunSlidesRatherThanShrinking()
    {
        // Three numbers at both ends, not two: a run that shrank at the end would move the buttons
        // under the reader's pointer on the last press of a walk to the end of the list.
        Assert.Equal([1, null, 98, 99, 100], PageNumbers.Window(page: 100, totalPages: 100));
    }

    [Fact]
    public void Window_WhenASkipWouldStandForOnePageOnly_ThenThatPageIsDrawnInstead()
    {
        // "1 … 3" is no narrower than "1 2 3" and hides a page that is one press away.
        Assert.Equal([1, 2, 3, 4, 5, null, 100], PageNumbers.Window(page: 4, totalPages: 100));
    }

    [Fact]
    public void Window_WhenEveryPageFits_ThenThereIsNoSkipAtAll()
    {
        Assert.Equal([1, 2, 3, 4], PageNumbers.Window(page: 2, totalPages: 4));
    }

    [Fact]
    public void Window_WhenThereIsOnlyOnePage_ThenItIsTheWholeRun()
    {
        // Reached through the retreat, which keeps a one-page pager on screen rather than pulling
        // it out from under the finger that pressed Neste.
        Assert.Equal([1], PageNumbers.Window(page: 1, totalPages: 1));
    }

    [Theory]
    [InlineData(99, 3)]
    [InlineData(0, 3)]
    [InlineData(-5, 3)]
    public void Window_WhenThePageIsOutOfRange_ThenTheRunIsStillTheOneTheListHas(int page, int total)
    {
        // A stale link carries page 99 of a result that shrank to 3. The clamp is what keeps the
        // run from repeating a number or opening a skip past the end.
        Assert.Equal([1, 2, 3], PageNumbers.Window(page, total));
    }

    [Fact]
    public void Window_WhateverThePage_ThenNoNumberIsDrawnTwiceAndBothEndsAreInIt()
    {
        // The invariants behind every case above, over the whole range rather than at the three
        // points the examples happen to pick. A duplicate is a duplicate DOM key as well as a
        // second button for one page.
        for (var total = 1; total <= 40; total++)
        {
            for (var page = 1; page <= total; page++)
            {
                var numbers = PageNumbers.Window(page, total)
                    .Where(number => number is not null)
                    .Select(number => number!.Value)
                    .ToList();

                Assert.Equal(numbers.Distinct().Count(), numbers.Count);
                Assert.Equal(numbers.Order(), numbers);
                Assert.Contains(1, numbers);
                Assert.Contains(total, numbers);
                Assert.Contains(page, numbers);
            }
        }
    }

    private Microsoft.AspNetCore.Components.RenderFragment Run(int page, int totalPages) =>
        PageNumbers.Write(
            this,
            page,
            totalPages,
            _ => Task.CompletedTask,
            number => $"Gå til side {number}",
            number => $"Viser side {number}");

    [Fact]
    public void Write_WhenThePageIsOutOfRange_ThenTheRunStillSaysWhichPageItIsDrawnFor()
    {
        // Window clamps and the markup did not, so a run drawn for a page the result no longer has
        // came out with no aria-current at all and every number labelled as a jump. Reachable: a
        // shared link carrying page 40 of a result that shrank renders once before the correcting
        // fetch lands, and that render is a pager that cannot say where the reader is.
        var cut = Render(Run(page: 99, totalPages: 3));

        var numbers = cut.FindAll("button");
        var current = Assert.Single(numbers, number => number.GetAttribute("aria-current") == "page");

        Assert.Equal("3", current.TextContent);
        Assert.Equal("Viser side 3", current.GetAttribute("aria-label"));
    }

    [Fact]
    public void Write_WhenThePageIsInRange_ThenItIsTheOneMarked()
    {
        var cut = Render(Run(page: 2, totalPages: 3));

        var current = Assert.Single(cut.FindAll("button"), n => n.GetAttribute("aria-current") == "page");

        Assert.Equal("2", current.TextContent);
    }
}
