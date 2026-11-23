using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary><c>MuninExplorer:Features:CatalogueFormatting</c>, read by both <c>AddMuninExplorer</c> overloads and off by default.</summary>
public class CatalogueFormattingSwitchTest
{
    private const string Key = "MuninExplorer:Features:CatalogueFormatting";

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void AddMuninExplorer_WhenTheDelegateOverloadsHostHasTheKey_ThenFormattingFollowsIt(string? value, bool on)
    {
        var services = new ServiceCollection();
        var settings = new Dictionary<string, string?>();
        if (value is not null)
        {
            settings[Key] = value;
        }

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var provider = services.AddMuninExplorer(o => o.ApiBaseUrl = "https://explorer.munin.skytest.fhi.no").BuildServiceProvider();

        Assert.Equal(on, provider.GetRequiredService<CatalogueFormattingSwitch>().On);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("true", true)]
    [InlineData("yes", false)]
    public void AddMuninExplorer_WhenTheConfigurationOverloadsSectionHasTheKey_ThenFormattingFollowsIt(string? value, bool on)
    {
        var settings = new Dictionary<string, string?> { ["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no" };
        if (value is not null)
        {
            settings[Key] = value;
        }

        var provider = new ServiceCollection()
            .AddMuninExplorer(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();

        Assert.Equal(on, provider.GetRequiredService<CatalogueFormattingSwitch>().On);
    }

    [Fact]
    public void AddMuninExplorer_WhenTheDelegateSetsFormatting_ThenItWinsOverTheKey()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "true" }).Build());

        var provider = services.AddMuninExplorer(o =>
        {
            o.ApiBaseUrl = "https://explorer.munin.skytest.fhi.no";
            o.Features.CatalogueFormatting = false;
        }).BuildServiceProvider();

        Assert.False(provider.GetRequiredService<CatalogueFormattingSwitch>().On);
    }
}
