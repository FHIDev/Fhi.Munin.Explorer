using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// The picker reaches a host only when it asks for it (Fhi.Metadata-l9l2n.170); helsedata turns it on per environment.
public class CodePickerOptionTest
{
    private static ServiceProvider ViaDelegate(Dictionary<string, string?>? hostSettings = null, Action<MuninExplorerOptions>? extra = null)
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
        }).BuildServiceProvider();
    }

    private static bool OnFromOptions(Action<MuninExplorerOptions>? extra = null) =>
        ViaDelegate(extra: extra).GetRequiredService<CodePickerSwitch>().On;

    private static bool OnFromConfiguration(Dictionary<string, string?> settings)
    {
        settings["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddMuninExplorer(configuration).BuildServiceProvider()
            .GetRequiredService<CodePickerSwitch>().On;
    }

    [Fact]
    public void AddMuninExplorer_WhenTheOptionIsNotSet_ThenThePickerIsOff() => Assert.False(OnFromOptions());

    [Fact]
    public void AddMuninExplorer_WhenTheOptionIsSet_ThenThePickerIsOn() => Assert.True(OnFromOptions(o => o.Features.CodePicker = true));

    // helsedata calls the delegate overload and switches per environment in appsettings, as it does eIDAS.
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void AddMuninExplorer_WhenTheDelegateOverloadsHostHasTheKey_ThenThePickerFollowsIt(string? value, bool on)
    {
        var settings = new Dictionary<string, string?>();
        if (value is not null)
        {
            settings["MuninExplorer:Features:CodePicker"] = value;
        }

        Assert.Equal(on, ViaDelegate(settings).GetRequiredService<CodePickerSwitch>().On);
    }

    [Fact]
    public void AddMuninExplorer_WhenTheDelegateSetsTheFeature_ThenItWinsOverTheKey()
    {
        var settings = new Dictionary<string, string?> { ["MuninExplorer:Features:CodePicker"] = "true" };

        Assert.False(ViaDelegate(settings, o => o.Features.CodePicker = false).GetRequiredService<CodePickerSwitch>().On);
    }

    [Fact]
    public void AddMuninExplorer_WhenTheHostsSectionHasAnEmptyApiBaseUrl_ThenTheDelegatesUrlStands()
    {
        var settings = new Dictionary<string, string?> { ["MuninExplorer:ApiBaseUrl"] = "", ["MuninExplorer:Features:CodePicker"] = "true" };
        var client = ViaDelegate(settings).GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IMuninExplorerClient));

        Assert.Equal(new Uri("https://explorer.munin.skytest.fhi.no/"), client.BaseAddress);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("yes", false)]
    public void AddMuninExplorer_WhenTheConfigurationOverloadsSectionHasTheKey_ThenThePickerFollowsIt(string? value, bool on)
    {
        var settings = new Dictionary<string, string?>();
        if (value is not null)
        {
            settings["MuninExplorer:Features:CodePicker"] = value;
        }

        Assert.Equal(on, OnFromConfiguration(settings));
    }
}
