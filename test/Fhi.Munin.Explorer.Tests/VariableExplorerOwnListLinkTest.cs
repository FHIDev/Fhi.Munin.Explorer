using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// <c>?listeId=</c> on the composed explorer (Fhi.Metadata-9huq2.61): the link helsedata's application form makes back
/// to one of a reader's lists. The address is the half the list view's own tests cannot see.
/// </summary>
public class VariableExplorerOwnListLinkTest : ExplorerTestContext
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
                Items = [], TotalCount = 0, PageNumber = 1, Size = pageSize, TotalPages = 1,
            });
    }

    private IRenderedComponent<VariableExplorer> RenderAt(string query, bool flag, bool signedIn = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(new TwoListsClient());
        Services.AddSingleton(new ListLinkSwitch(flag));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/" + query);

        return Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, signedIn).Add(c => c.Language, "no"));
    }

    /// <summary>The address the explorer last wrote, or null when it wrote none.</summary>
    private string? LastWritten() =>
        JSInterop.Invocations
            .Where(i => i.Identifier is "history.replaceState" or "history.pushState")
            .Select(i => i.Arguments[2]?.ToString())
            .LastOrDefault();

    private static bool ListTabSelected(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").GetAttribute("aria-selected") == "true";

    [Fact]
    public void ListeId_WhenTheFlagIsOnAndTheListIsTheReaders_ThenItOpensAndTheIdLeavesTheAddress()
    {
        // Left in the address, a link the reader copies later would carry a private list's id to whoever gets it.
        var cut = RenderAt($"?listeId={SecondList}", flag: true);

        cut.WaitForAssertion(() =>
        {
            Assert.True(ListTabSelected(cut));
            Assert.Contains("Til søknaden", cut.Find("[id^='munin-explorer-list-heading-']").TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("listeId", LastWritten() ?? "listeId", StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ListeId_WhenTheReaderIsSignedOut_ThenTheyAreAskedToSignInAndTheIdStays()
    {
        // The host's sign-in returns to this address, so the id has to be in it for the list to open afterwards.
        var cut = RenderAt($"?listeId={SecondList}", flag: true, signedIn: false);

        cut.WaitForAssertion(() => Assert.Contains(
            "Logg inn for å åpne variabellisten lenken viser til.", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Logg inn for å lage og bruke egne variabellister.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains($"listeId={SecondList}", LastWritten() ?? $"listeId={SecondList}", StringComparison.Ordinal);
    }

    [Fact]
    public void ListeId_WhenTheFlagIsOff_ThenItIsNeitherReadNorTakenOutOfTheAddress()
    {
        var cut = RenderAt($"?listeId={SecondList}", flag: false);

        Assert.False(ListTabSelected(cut));
        Assert.Contains($"listeId={SecondList}", LastWritten() ?? $"listeId={SecondList}", StringComparison.Ordinal);

        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Mine hjertevariabler", cut.Find("[id^='munin-explorer-list-heading-']").TextContent, StringComparison.Ordinal));
    }
}
