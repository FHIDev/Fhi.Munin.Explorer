using Fhi.Munin.Explorer.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The host's bearer token rides on every call to ApiBaseUrl, so a plain-http address off the machine is refused at
/// startup (Fhi.Metadata-ayqlp.12, R2-L9).
/// </summary>
public class ApiBaseUrlSchemeTest
{
    [Theory]
    [InlineData("http://explorer.munin.skytest.fhi.no")]
    [InlineData("http://10.0.0.5:8080")]
    [InlineData("ftp://explorer.munin.skytest.fhi.no")]
    [InlineData("ftp://localhost:5199")]
    [InlineData("http://localhost@evil.example")]
    [InlineData("http://127.0.0.1.evil.example")]
    [InlineData("explorer.munin.skytest.fhi.no")]
    [InlineData("/api")]
    public void Registration_WhenTheBaseUrlIsNotHttpsOffTheMachine_ThenStartupFails(string baseUrl)
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddMuninExplorer(o => o.ApiBaseUrl = baseUrl));

        Assert.Contains("https", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://explorer.munin.skytest.fhi.no")]
    [InlineData("HTTPS://explorer.munin.skytest.fhi.no/")]
    [InlineData("http://127.0.0.1:5199")]
    [InlineData("http://localhost:5199")]
    [InlineData("http://[::1]:5199")]
    public void Registration_WhenTheBaseUrlIsHttpsOrLoopbackHttp_ThenItIsAccepted(string baseUrl)
    {
        var thrown = Record.Exception(() => new ServiceCollection().AddMuninExplorer(o => o.ApiBaseUrl = baseUrl));

        Assert.Null(thrown);
    }
}
