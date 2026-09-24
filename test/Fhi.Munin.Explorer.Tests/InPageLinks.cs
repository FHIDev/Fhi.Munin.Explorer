using Bunit;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Whether the in-page links a component renders land on something it rendered.</summary>
internal static class InPageLinks
{
    /// <summary>
    /// Asserts that <paramref name="cut"/> renders at least one link carrying a fragment, and that
    /// each fragment is the id of exactly one element in the same markup.
    /// </summary>
    public static void AssertEachHasOneTarget<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        var fragments = cut.FindAll("a[href*='#']")
                           .Select(link => link.GetAttribute("href")!)
                           .Select(href => Uri.UnescapeDataString(href[(href.IndexOf('#', StringComparison.Ordinal) + 1)..]))
                           .ToList();

        Assert.NotEmpty(fragments);
        Assert.All(fragments, fragment =>
            Assert.True(cut.FindAll($"[id='{fragment}']").Count == 1,
                        $"#{fragment} does not name exactly one element in the rendered markup."));
    }
}
