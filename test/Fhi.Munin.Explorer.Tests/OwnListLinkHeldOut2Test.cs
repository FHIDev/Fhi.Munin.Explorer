using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class OwnListLinkHeldOut2Test : ExplorerTestContext
{
    private static readonly Guid LinkedList = new("22222222-2222-2222-2222-222222222222");

    private sealed class TwoListsClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
            [
                new VariableList { Id = new Guid("11111111-1111-1111-1111-111111111111"), Name = "Mine hjertevariabler" },
                new VariableList { Id = LinkedList, Name = "Til søknaden" },
            ]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem> { Items = [], TotalCount = 0, PageNumber = 1, Size = pageSize, TotalPages = 1 });
    }

    [Fact]
    public void ListeId_WhenTheReaderSignsInAfterTheFirstRender_ThenTheListTabOpensOnTheLinkedList()
    {
        // The id is kept for the sign-in, so a reader who is signed in in the same circuit must still get the list.
        Services.AddSingleton<IMuninExplorerClient>(new TwoListsClient());
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(false));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/?listeId=" + LinkedList);
        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, false).Add(c => c.Language, "no"));

        cut.Render(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.Language, "no"));

        cut.WaitForAssertion(() =>
        {
            var tab = cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste");
            Assert.Equal("true", tab.GetAttribute("aria-selected"));
            Assert.Contains("Til søknaden", cut.Find("[id^='munin-explorer-list-heading-']").TextContent, StringComparison.Ordinal);
        });
    }
}
