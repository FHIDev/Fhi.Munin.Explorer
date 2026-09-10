using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Finds things to ask the live API about, discovered rather than written down.
/// </summary>
/// <remarks>
/// A hard-coded id is a row somebody can unpublish, and the test following it would then fail — or
/// quietly check a 404 — for a reason that has nothing to do with what it is measuring. Shared by
/// <see cref="ContractDriftTest"/> and <see cref="FixtureDriftTest"/> so the two ask about the same
/// entities and cannot drift apart.
/// </remarks>
internal static class LiveCatalogue
{
    /// <summary>The kilde carrying the most delkilder, whose detail payload exercises the nested half.</summary>
    /// <remarks>
    /// Filtered rather than only sorted, so a catalogue with none anywhere fails saying so instead
    /// of handing back a kilde without any. Measured 2026-09-10: four of the 98 report one.
    /// </remarks>
    public static async Task<Guid> KildeWithDelkilderIdAsync(LiveApiConnection api)
    {
        var kilder = await api.Client.GetKilderAsync();

        Assert.NotEmpty(kilder);

        var kilde = kilder.Where(candidate => candidate.DelkildeCount > 0)
                          .OrderByDescending(candidate => candidate.DelkildeCount)
                          .ThenByDescending(candidate => candidate.DatasamlingCount)
                          .FirstOrDefault();

        Assert.True(
            kilde is not null,
            $"None of the {kilder.Count} kilder reports a delkilde, so no KildeDetail payload here has " +
            "the nested half this asks about. Either the catalogue changed shape or delkildeCount " +
            "stopped being set.");

        return kilde!.Id;
    }

    /// <summary>The first datasamling reachable from a kilde's hierarchy, richest kilde first.</summary>
    /// <remarks>
    /// A count is not a tree: measured 2026-09-10, three of the 63 kilder reporting datasamlingCount
    /// above zero serve a hierarchy with none in it, so this fetches until one really carries some.
    /// </remarks>
    public static async Task<Guid> AnyDatasamlingIdAsync(LiveApiConnection api)
    {
        var kilder = await api.Client.GetKilderAsync();

        Assert.NotEmpty(kilder);

        var candidates = kilder.Where(candidate => candidate.DatasamlingCount > 0)
                               .OrderByDescending(candidate => candidate.DatasamlingCount)
                               .ToList();

        foreach (var candidate in candidates)
        {
            var hierarchy = await api.Client.GetKildeHierarchyAsync(candidate.Id);
            var datasamlingId = hierarchy is null ? Guid.Empty : DatasamlingIds(hierarchy).FirstOrDefault();

            if (datasamlingId != Guid.Empty)
            {
                return datasamlingId;
            }
        }

        Assert.Fail(
            $"None of the {candidates.Count} kilder reporting a datasamling has one anywhere in its " +
            $"hierarchy, out of {kilder.Count} in the catalogue, so there is nothing to open. Either the " +
            "catalogue changed shape or the hierarchy endpoint stopped returning children.");

        return default;
    }

    public static async Task<Guid> AnyVariableIdAsync(LiveApiConnection api)
    {
        var page = await api.Client.SearchVariablesAsync(null, pageSize: 1);

        Assert.NotEmpty(page.Items);

        return page.Items[0].Id;
    }

    /// <summary>The first variable on the first page that links a kodeverk with codes behind it.</summary>
    /// <remarks>
    /// Most variables link none, so this walks the page rather than trusting the first. Measured
    /// 2026-09-02: the twelfth variable was the first with codes.
    /// </remarks>
    public static async Task<(Guid VariableId, KodeverkLink Link)> AnyKodeverkLinkAsync(LiveApiConnection api)
    {
        var page = await api.Client.SearchVariablesAsync(null, pageSize: 25);

        Assert.NotEmpty(page.Items);

        foreach (var summary in page.Items)
        {
            var variable = await api.Client.GetVariableAsync(summary.Id);
            var link = variable?.KodeverkLinks.FirstOrDefault(candidate => candidate.HasCodeValues);

            if (link is not null)
            {
                return (summary.Id, link);
            }
        }

        Assert.Fail(
            $"None of the first {page.Items.Count} variables links a kodeverk carrying codes, so there is " +
            "nothing to fetch. Either the catalogue changed shape or harKodeverdier stopped being set.");

        return default;
    }

    /// <summary>Every datasamling in the tree, direct ones first, then down through the delkilder.</summary>
    private static IEnumerable<Guid> DatasamlingIds(KildeHierarchy hierarchy) =>
        hierarchy.DirectDatasamlinger.Select(datasamling => datasamling.Id)
            .Concat(hierarchy.Delkilder.SelectMany(DatasamlingIds));

    private static IEnumerable<Guid> DatasamlingIds(HierarchyDelkilde delkilde) =>
        delkilde.Datasamlinger.Select(datasamling => datasamling.Id)
            .Concat(delkilde.Children.SelectMany(DatasamlingIds));
}
