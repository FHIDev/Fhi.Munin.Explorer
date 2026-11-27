using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A link that opens one of the reader's own lists (Fhi.Metadata-9huq2.61): helsedata's application form links back
/// to each of a reader's lists, and the link has to open that list, or say why it cannot.
/// </summary>
public partial class VariableListViewTest
{
    private readonly List<Guid?> _ownListReported = [];

    private IRenderedComponent<VariableListView> RenderOwnListLink(
        ListClient client, Guid ownListId, bool flag = true, bool signedIn = true, bool browserList = false)
    {
        Services.AddSingleton(new ListLinkSwitch(flag));
        Services.AddSingleton(new CodePickerSwitch(browserList));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, signedIn)
            .Add(c => c.OwnListId, ownListId)
            .Add(c => c.OwnListIdChanged, EventCallback.Factory.Create<Guid?>(this, id => _ownListReported.Add(id))));
    }

    private static string ShownHeading(IRenderedComponent<VariableListView> cut) =>
        cut.Find("h2[id^='munin-explorer-list-heading-'], h3[id^='munin-explorer-list-heading-']").TextContent.Trim();

    [Fact]
    public void OwnListId_WhenItIsOneOfTheReadersLists_ThenThatListIsShownAndTheIdIsReportedUsed()
    {
        // Without this the link opens the reader's first list, which is the wrong list for every link but one.
        var client = new ListClient { ListCount = 3 };

        var cut = RenderOwnListLink(client, ListClient.SecondListId);

        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", ShownHeading(cut)));
        Assert.Contains(ListClient.SecondListId, client.VariablesAskedFor);
        Assert.Equal([null], _ownListReported);
        Assert.DoesNotContain("Fant ikke variabellisten", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnListId_WhenTheListIsNotTheReaders_ThenTheViewSaysItWasNotFound()
    {
        // Somebody else's list id, or a deleted one: the reader must be told, not silently shown a list of their own.
        var client = new ListClient { ListCount = 2 };

        var cut = RenderOwnListLink(client, Guid.NewGuid());

        cut.WaitForAssertion(() => Assert.Contains(
            "Fant ikke variabellisten lenken viser til.", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Equal([null], _ownListReported);
    }

    [Fact]
    public void OwnListId_WhenTheReaderHasNoListsAtAll_ThenTheViewSaysItWasNotFound()
    {
        var client = new ListClient { HasList = false };

        var cut = RenderOwnListLink(client, ListClient.SecondListId);

        cut.WaitForAssertion(() => Assert.Contains(
            "Fant ikke variabellisten lenken viser til.", cut.Markup, StringComparison.Ordinal));
        Assert.Equal([null], _ownListReported);
    }

    [Fact]
    public void OwnListId_WhenTheReaderIsSignedOut_ThenTheyAreAskedToSignInAndTheIdIsKept()
    {
        // Reported used here, the host would drop the id from its address, and signing in would open no list.
        var client = new ListClient { ListCount = 2 };

        var cut = RenderOwnListLink(client, ListClient.SecondListId, signedIn: false, browserList: true);

        cut.WaitForAssertion(() => Assert.Contains(
            "Logg inn for å åpne variabellisten lenken viser til.", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(_ownListReported);
        Assert.DoesNotContain(ListClient.SecondListId, client.VariablesAskedFor);
    }

    [Fact]
    public void OwnListId_WhenTheFlagIsOff_ThenTheIdIsIgnored()
    {
        var client = new ListClient { ListCount = 2 };

        var cut = RenderOwnListLink(client, ListClient.SecondListId, flag: false);

        cut.WaitForAssertion(() => Assert.Equal("Mine hjertevariabler", ShownHeading(cut)));
        Assert.Empty(_ownListReported);
        Assert.DoesNotContain(ListClient.SecondListId, client.VariablesAskedFor);
        Assert.DoesNotContain("Fant ikke variabellisten", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnListId_WhenTheReaderLaterChoosesAnotherList_ThenTheLinkDoesNotTakeThemBack()
    {
        // Followed once per id: a render after the reader's own choice must not snap the view back to the linked list.
        var client = new ListClient { ListCount = 3 };
        var cut = RenderOwnListLink(client, ListClient.SecondListId);
        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", ShownHeading(cut)));

        cut.Find("select").Change(ListClient.ThirdListId.ToString());
        cut.Render();

        cut.WaitForAssertion(() => Assert.Equal("Kreft og svulster", ShownHeading(cut)));
    }

    private sealed class HeldSwitchClient : ListClient
    {
        public TaskCompletionSource Release { get; } = new();

        public override async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            if (id == SecondListId)
            {
                await Release.Task;
            }

            return await base.GetMyListVariablesAsync(id, page, pageSize, kildeIds, cancellationToken);
        }
    }

    [Fact]
    public void OwnListId_WhenTheHostDropsItWhileTheListIsStillLoading_ThenTheLateAnswerReportsNothing()
    {
        // A late report would clear the id the host holds by then, which in the explorer is a newer link's.
        var client = new HeldSwitchClient { ListCount = 2 };
        var cut = RenderOwnListLink(client, ListClient.SecondListId);
        cut.WaitForAssertion(() => Assert.Contains(ListClient.SecondListId, client.VariablesAskedFor));

        cut.Render(p => p.Add(c => c.OwnListId, (Guid?)null));
        client.Release.SetResult();

        cut.WaitForAssertion(() => Assert.Equal("Hjerte og kar", ShownHeading(cut)));
        Assert.Empty(_ownListReported);
    }

    [Fact]
    public void OwnListId_WhenASignedOutReaderOpensASharedListBesideIt_ThenTheIdIsKeptForTheSignIn()
    {
        // Dropped here, the address the host's sign-in returns to would no longer name the reader's list.
        var client = new SharedAndOwnClient { ListCount = 2 };
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(false));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, false)
            .Add(c => c.ShareCode, "AB12CD")
            .Add(c => c.OwnListId, ListClient.SecondListId)
            .Add(c => c.OwnListIdChanged, EventCallback.Factory.Create<Guid?>(this, id => _ownListReported.Add(id))));

        cut.WaitForAssertion(() => Assert.Contains("Alder ved diagnose", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(_ownListReported);
    }

    [Fact]
    public void OwnListId_WhenTheReaderActsAfterTheNotFoundSentence_ThenTheSentenceGoes()
    {
        // Ranked above most sentences in the alert region, a sentence that stayed would hide every later one.
        var client = new ListClient { ListCount = 2 };
        var cut = RenderOwnListLink(client, Guid.NewGuid());
        cut.WaitForAssertion(() => Assert.Contains("Fant ikke variabellisten", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));

        cut.Find("select").Change(ListClient.SecondListId.ToString());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Hjerte og kar", ShownHeading(cut));
            Assert.DoesNotContain("Fant ikke variabellisten", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        });
    }
}
