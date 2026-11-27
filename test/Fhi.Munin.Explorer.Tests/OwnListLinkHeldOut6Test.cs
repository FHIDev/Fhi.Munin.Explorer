using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class OwnListLinkHeldOut6Test : ExplorerTestContext
{
    private sealed class PlainClient : EmptyMuninExplorerClient;

    private static readonly Guid ListId = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Navigation_WhenASignedOutReaderReturnsToAnAddressWithTheSameListeId_ThenTheResultsTabStaysSelected()
    {
        // Back lands on an address still carrying the id; the remount must not re-follow it over the reader's choice.
        Services.AddSingleton<IMuninExplorerClient>(new PlainClient());
        Services.AddSingleton(new ListLinkSwitch(true));
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"http://localhost/?listeId={ListId}");

        var cut = Render<VariableExplorer>(p => p
            .Add(c => c.IsAuthenticated, false)
            .Add(c => c.Language, "no"));
        bool Selected(string label) => cut.FindAll("[role=tab]")
            .Single(t => t.TextContent.Trim() == label).GetAttribute("aria-selected") == "true";
        cut.WaitForAssertion(() => Assert.True(Selected("Variabelliste")));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Søkeresultat").Click();
        Assert.True(Selected("Søkeresultat"));

        cut.InvokeAsync(() => nav.NavigateTo($"http://localhost/?search=alder&listeId={ListId}"));
        Thread.Sleep(500);
        cut.Render();

        Assert.True(Selected("Søkeresultat"), "the results tab should stay selected after the return");
    }
}
