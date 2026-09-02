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
    /// <summary>The kilde with the most delkilder, whose payload exercises the nested half of the contract.</summary>
    /// <remarks>Most kilder have none, so the first in the list would leave that half unchecked on almost every run.</remarks>
    public static async Task<Guid> MostNestedKildeIdAsync(LiveApiConnection api)
    {
        var kilder = await api.Client.GetKilderAsync();

        Assert.NotEmpty(kilder);

        return kilder.OrderByDescending(kilde => kilde.DelkildeCount)
                     .ThenByDescending(kilde => kilde.DatasamlingCount)
                     .First()
                     .Id;
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
    public static IEnumerable<Guid> DatasamlingIds(KildeHierarchy hierarchy) =>
        hierarchy.DirectDatasamlinger.Select(datasamling => datasamling.Id)
            .Concat(hierarchy.Delkilder.SelectMany(DatasamlingIds));

    private static IEnumerable<Guid> DatasamlingIds(HierarchyDelkilde delkilde) =>
        delkilde.Datasamlinger.Select(datasamling => datasamling.Id)
            .Concat(delkilde.Children.SelectMany(DatasamlingIds));
}
