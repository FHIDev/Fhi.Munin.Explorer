using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    [Fact]
    public void OwnListId_WhenTheHostDropsTheIdWhileTheSwitchReadIsOut_ThenNothingIsRaisedForTheOldId()
    {
        // A late null would clear the id a newer link put in the explorer's address.
        var client = new ListClient { ListCount = 3, StallVariablesFor = ListClient.SecondListId };
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(false));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        var reported = new List<Guid?>();

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.OwnListId, ListClient.SecondListId)
            .Add(c => c.OwnListIdChanged, EventCallback.Factory.Create<Guid?>(this, id => reported.Add(id))));
        cut.WaitForAssertion(() => Assert.Contains(ListClient.SecondListId, client.VariablesAskedFor));

        cut.Render(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.OwnListId, (Guid?)null)
            .Add(c => c.OwnListIdChanged, EventCallback.Factory.Create<Guid?>(this, id => reported.Add(id))));
        client.ReleaseVariables();
        cut.WaitForAssertion(() => Assert.True(client.VariablesAnswered >= 1));
        Thread.Sleep(500);
        cut.Render();

        Assert.Empty(reported);
    }
}
