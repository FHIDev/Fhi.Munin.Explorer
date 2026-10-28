using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The legacy samples are what a host copies, so they keep the two settings a .NET 10 legacy host needs to recover
/// from an expired circuit (README, «A legacy Blazor Server host on .NET 10»).
/// </summary>
public class LegacyHostCircuitResumeGuardTest
{
    [Theory]
    [InlineData("LegacyHost")]
    [InlineData("HostileHost")]
    public void Sample_WhenItRegistersBlazorServer_ThenItStoresNoCircuitState(string sample)
    {
        var program = File.ReadAllText(Repo.In("samples", sample, "Program.cs"));

        Assert.Matches(new Regex(@"PersistedCircuitInMemoryMaxRetained\s*=\s*0\b"), program);
    }

    [Theory]
    [InlineData("LegacyHost")]
    [InlineData("HostileHost")]
    public void Sample_WhenItLoadsBlazorServer_ThenItHasTheErrorBarWithAReloadLink(string sample)
    {
        var layout = File.ReadAllText(Repo.In("samples", sample, "Views", "Shared", "_Layout.cshtml"));

        var bar = layout.IndexOf("id=\"blazor-error-ui\"", StringComparison.Ordinal);
        var script = layout.IndexOf("_framework/blazor.server.js", StringComparison.Ordinal);
        Assert.True(bar >= 0, $"{sample}'s layout has no #blazor-error-ui");
        Assert.True(script >= 0, $"{sample}'s layout does not load blazor.server.js");
        Assert.True(bar < script, "the bar comes before the script");
        var element = layout[bar..layout.IndexOf("</div>", bar, StringComparison.Ordinal)];
        Assert.Matches(new Regex("class=\"reload\""), element);
        Assert.Matches(new Regex(@"display:\s*none"), element);
    }
}
