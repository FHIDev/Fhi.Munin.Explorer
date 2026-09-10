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
    /// <summary>How many kilder a discovery walk will fetch a hierarchy for before giving up.</summary>
    /// <remarks>
    /// Bounded like <see cref="AnyKodeverkLinkAsync"/>'s page, so the worst case is a number written
    /// down here rather than however large the catalogue has grown — 64 kilder qualified on
    /// 2026-09-10, and on the failure path every one of them costs a live round trip.
    /// </remarks>
    private const int CandidateLimit = 25;

    /// <summary>A kilde carrying delkilder, preferring one that carries datasamlinger as well.</summary>
    /// <remarks>
    /// Both halves are nested, and delkilder alone do not reach the datasamling one: of the four
    /// kilder in 98 reporting a delkilde on 2026-09-10, one reports no datasamling at all and ties
    /// the richest on delkildeCount, so a sort led by that count can land on it.
    /// </remarks>
    public static async Task<Guid> KildeWithDelkilderIdAsync(LiveApiConnection api)
    {
        var kilder = await api.FetchAsync(client => client.GetKilderAsync());

        Assert.NotEmpty(kilder);

        // Filtered rather than only sorted, so a catalogue with no delkilder anywhere fails saying
        // so instead of handing back a kilde without any. Id last for a total order: without it the
        // pick among equals is whatever order the API happened to list them in.
        var kilde = kilder.Where(candidate => candidate.DelkildeCount > 0)
                          .OrderByDescending(candidate => candidate.DatasamlingCount > 0)
                          .ThenByDescending(candidate => candidate.DelkildeCount)
                          .ThenByDescending(candidate => candidate.DatasamlingCount)
                          .ThenByDescending(candidate => candidate.Id)
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
    /// A count is not a tree: measured 2026-09-10, three of the 64 kilder reporting datasamlingCount
    /// above zero serve a hierarchy with none in it, so this fetches until one really carries some.
    /// </remarks>
    public static async Task<Guid> AnyDatasamlingIdAsync(LiveApiConnection api)
    {
        var kilder = await api.FetchAsync(client => client.GetKilderAsync());

        Assert.NotEmpty(kilder);

        // Id last for a total order: two kilder tie on the count above it today, and which of them
        // the two drift suites compare a fixture against should be decided here rather than by the
        // order the API happened to list them in.
        var qualifying = kilder.Where(candidate => candidate.DatasamlingCount > 0)
                               .OrderByDescending(candidate => candidate.DatasamlingCount)
                               .ThenByDescending(candidate => candidate.Id)
                               .ToList();

        var candidates = qualifying.Take(CandidateLimit).ToList();

        var unanswered = 0;

        foreach (var candidate in candidates)
        {
            var hierarchy = await api.FetchAsync(client => client.GetKildeHierarchyAsync(candidate.Id));

            // Counted rather than skipped silently, so the failure below cannot claim hierarchies
            // were searched when none was served.
            if (hierarchy is null)
            {
                unanswered++;

                continue;
            }

            var datasamlingId = DatasamlingIds(hierarchy).FirstOrDefault();

            if (datasamlingId != Guid.Empty)
            {
                return datasamlingId;
            }
        }

        // The bound is named because it is a cause of its own: read as "25 of 98" the message sends
        // whoever picks the nightly issue up looking for a catalogue change, with no sign that the
        // qualifying kilder past the twenty-fifth went untried.
        Assert.Fail(
            $"None of the {candidates.Count} kilder tried — those reporting the most datasamlinger, of " +
            $"the {qualifying.Count} reporting any, of the {kilder.Count} in the catalogue, and " +
            $"{nameof(CandidateLimit)} stops the walk at {CandidateLimit} — has one anywhere in its " +
            $"hierarchy, and {unanswered} of them served no hierarchy at all, so there is nothing to " +
            $"open. Either {nameof(CandidateLimit)} is now too small to reach one, the catalogue " +
            "changed shape, the hierarchy endpoint stopped returning children, or it stopped answering.");

        return default;
    }

    public static async Task<Guid> AnyVariableIdAsync(LiveApiConnection api)
    {
        var page = await api.FetchAsync(client => client.SearchVariablesAsync(null, pageSize: 1));

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
        var page = await api.FetchAsync(client => client.SearchVariablesAsync(null, pageSize: 25));

        Assert.NotEmpty(page.Items);

        foreach (var summary in page.Items)
        {
            var variable = await api.FetchAsync(client => client.GetVariableAsync(summary.Id));
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
