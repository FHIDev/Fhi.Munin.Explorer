using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// The browser list reaches a host only when it asks for it (Fhi.Metadata-emsc0.3.12), and is off by default.
public class AnonymousListsOptionTest
{
    private static bool On(Dictionary<string, string?>? hostSettings = null, Action<MuninExplorerOptions>? extra = null)
    {
        var services = new ServiceCollection();
        if (hostSettings is not null)
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(hostSettings).Build());
        }

        return services.AddMuninExplorer(o =>
        {
            o.ApiBaseUrl = "https://explorer.munin.skytest.fhi.no";
            extra?.Invoke(o);
        }).BuildServiceProvider().GetRequiredService<AnonymousListsSwitch>().On;
    }

    [Fact]
    public void AddMuninExplorer_WhenNothingSetsIt_ThenTheBrowserListIsOff() => Assert.False(On());

    [Fact]
    public void AddMuninExplorer_WhenTheOptionIsSet_ThenTheBrowserListIsOn() =>
        Assert.True(On(extra: o => o.Features.AnonymousLists = true));

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void AddMuninExplorer_WhenTheHostHasTheKey_ThenTheBrowserListFollowsIt(string value, bool on) =>
        Assert.Equal(on, On(new Dictionary<string, string?> { ["MuninExplorer:Features:AnonymousLists"] = value }));

    [Fact]
    public void AddMuninExplorer_WhenTheConfigurationOverloadHasTheKey_ThenTheBrowserListIsOn()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no",
            ["MuninExplorer:Features:AnonymousLists"] = "true",
        }).Build();

        Assert.True(new ServiceCollection().AddMuninExplorer(configuration).BuildServiceProvider()
            .GetRequiredService<AnonymousListsSwitch>().On);
    }

    [Fact]
    public void AddMuninExplorer_WhenTheDelegateSetsTheFeature_ThenItWinsOverTheKey() =>
        Assert.False(On(new Dictionary<string, string?> { ["MuninExplorer:Features:AnonymousLists"] = "true" },
            o => o.Features.AnonymousLists = false));
}
