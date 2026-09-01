using Bunit;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The raised wait budget is only there if something ran it.
/// </summary>
/// <remarks>
/// A module initializer is invisible from the tests it protects, so deleting it — or lowering the
/// value back — costs nothing at the point of the edit and buys back an intermittent red run that
/// nobody can attribute to a change. (Fhi.Metadata-p7est)
/// </remarks>
public class WaitBudgetTest
{
    [Fact]
    public void Budget_WhenTheTestsRun_ThenItIsWellAboveBunitsOwnDefault()
    {
        Assert.Equal(WaitBudget.Allowed, BunitContext.DefaultWaitTimeout);
        Assert.True(BunitContext.DefaultWaitTimeout >= TimeSpan.FromSeconds(5));
    }
}
