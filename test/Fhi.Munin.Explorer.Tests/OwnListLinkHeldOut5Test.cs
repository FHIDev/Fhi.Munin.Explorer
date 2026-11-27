using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    private sealed class SharedAndOwnClient : ListClient
    {
        public override Task<SharedList?> GetSharedListAsync(string? code, CancellationToken cancellationToken = default) =>
            Task.FromResult<SharedList?>(new SharedList("Kollegas liste", [Item("Alder ved diagnose", "V_BDR.ALDER")]));
    }

    [Fact]
    public void OwnListId_WhenAShareCodeIsAlsoSet_ThenTheSharedListWinsAndTheIdIsStillReportedUsed()
    {
        // Left unreported, the private list id would stay in the host's address beside the share code.
        var client = new SharedAndOwnClient { ListCount = 2 };
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(false));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.ShareCode, "AB12CD")
            .Add(c => c.OwnListId, ListClient.SecondListId)
            .Add(c => c.OwnListIdChanged, EventCallback.Factory.Create<Guid?>(this, id => _ownListReported.Add(id))));

        cut.WaitForAssertion(() => Assert.Contains("Alder ved diagnose", cut.Markup, StringComparison.Ordinal));
        cut.WaitForAssertion(() => Assert.Equal([null], _ownListReported));
    }
}
