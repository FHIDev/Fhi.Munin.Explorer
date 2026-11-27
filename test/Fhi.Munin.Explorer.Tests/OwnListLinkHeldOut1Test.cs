using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class OwnListLinkHeldOut1Test : ExplorerTestContext
{
    private sealed class BareClient : EmptyMuninExplorerClient;

    private static readonly Guid LinkedList = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ListeId_WhenSignedOutWithTheBrowserListOn_ThenTheListTabIsSelectedAndTheSignInSentenceShows()
    {
        // Without the list tab the reader never sees the sentence that tells them why the link opened nothing.
        Services.AddSingleton<IMuninExplorerClient>(new BareClient());
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/?listeId=" + LinkedList);

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, false).Add(c => c.Language, "no"));

        cut.WaitForAssertion(() =>
        {
            var tab = cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste");
            Assert.Equal("true", tab.GetAttribute("aria-selected"));
            Assert.Contains("Logg inn for å åpne variabellisten lenken viser til.", cut.Markup, StringComparison.Ordinal);
        });
    }
}
