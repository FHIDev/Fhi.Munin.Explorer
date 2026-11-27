using Bunit;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    [Fact]
    public void OwnListId_WhenTheHostClearsItAndSetsTheSameIdAgain_ThenTheListOpensAgainAndIsReportedAgain()
    {
        // A second link to the same list in one circuit is a new request, not a repeat the view may ignore.
        var client = new ListClient { ListCount = 3 };
        var cut = RenderOwnListLink(client, ListClient.SecondListId);
        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", ShownHeading(cut)));
        cut.Render(p => p.Add(c => c.OwnListId, null));
        cut.Find("select").Change(ListClient.ThirdListId.ToString());
        cut.WaitForAssertion(() => Assert.Equal("Kreft og svulster", ShownHeading(cut)));

        cut.Render(p => p.Add(c => c.OwnListId, ListClient.SecondListId));

        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", ShownHeading(cut)));
        Assert.Equal([null, null], _ownListReported);
    }
}
