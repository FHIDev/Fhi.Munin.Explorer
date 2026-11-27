using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class OwnListLinkHeldOut8Test : ExplorerTestContext
{
    private static readonly Guid FirstList = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondList = new("22222222-2222-2222-2222-222222222222");

    private sealed class TwoListsClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
            [
                new VariableList { Id = FirstList, Name = "Mine hjertevariabler" },
                new VariableList { Id = SecondList, Name = "Til søknaden" },
            ]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
    }

    private static bool ListTabSelected(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").GetAttribute("aria-selected") == "true";

    private IRenderedComponent<VariableExplorer> RenderAt(string query)
    {
        Services.AddSingleton<IMuninExplorerClient>(new TwoListsClient());
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(false));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/" + query);

        return Render<VariableExplorer>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "no"));
    }

    [Fact]
    public void Render_WhenASignedInReaderGoesBackToAnAddressWithAUsedListeId_ThenTheirChosenListAndTabStay()
    {
        // A link already followed must not be followed again when Back remounts the search with the id in the address.
        var cut = RenderAt($"?listeId={SecondList}");
        cut.WaitForAssertion(() =>
        {
            Assert.True(ListTabSelected(cut));
            Assert.Contains("Til søknaden", cut.Find("[id^='munin-explorer-list-heading-']").TextContent, StringComparison.Ordinal);
        });
        cut.Find("select").Change(FirstList.ToString());
        cut.WaitForAssertion(() =>
            Assert.Contains("Mine hjertevariabler", cut.Find("[id^='munin-explorer-list-heading-']").TextContent, StringComparison.Ordinal));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Søkeresultat").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo($"http://localhost/?search=alder&listeId={SecondList}");

        cut.WaitForAssertion(() => Assert.Contains("alder", cut.Find("input[type=search], input[type=text]").GetAttribute("value") ?? "", StringComparison.Ordinal));
        Assert.False(ListTabSelected(cut));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Mine hjertevariabler", cut.Find("[id^='munin-explorer-list-heading-']").TextContent, StringComparison.Ordinal));
    }
}
