using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    [Fact]
    public async Task OwnListId_WhenTheFirstReadFailsAndTheRetrySucceeds_ThenTheLinkedListShowsAndTheIdIsReportedUsed()
    {
        // The link is followed once the lists are read, and a retried read is the only one a refused first read gets.
        var client = new ListClient { ListCount = 3, ListsThrow = true };
        var cut = RenderOwnListLink(client, ListClient.SecondListId);
        await cut.InvokeAsync(() => { });
        Assert.NotNull(RetryButton(cut));

        client.ListsThrow = false;
        await PressAsync(cut, "Prøv igjen");

        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", ShownHeading(cut)));
        Assert.Equal([null], _ownListReported);
    }
}
