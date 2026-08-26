using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every endpoint the component calls, checked against the API that is running right now.
/// </summary>
/// <remarks>
/// The API and this package live in different repositories and release on different days. In one
/// repository a change to <c>/api/explorer/*</c> would break a build the same afternoon; across
/// two it breaks nothing until a page renders wrongly on helsedata.no, and the only person who
/// can see that is a visitor. This is the build that closes the gap, so it runs nightly whether or
/// not anybody touched either side. See <c>docs/contract-drift.md</c>.
/// <para>
/// Skipped unless <see cref="LiveApi.EnabledVariable"/> is set — the offline sibling,
/// <see cref="ContractCoverageTest"/>, is the one that runs on every commit, against payloads
/// captured under <c>Testdata/</c>. Together they answer two different questions: whether the
/// contracts still match what we last saw, and whether what we last saw is still what is served.
/// </para>
/// <para>
/// Ids are discovered from the API rather than written down. A hard-coded id is a kilde somebody
/// can unpublish, and the test that follows it would then fail — or worse, quietly check a 404 —
/// for a reason that has nothing to do with the contract.
/// </para>
/// </remarks>
[Trait("Category", ContractDriftTest.Category)]
public class ContractDriftTest
{
    /// <summary>The trait the scheduled workflow filters on. Changing it changes that workflow too.</summary>
    public const string Category = "ContractDrift";

    [LiveApiFact]
    public async Task VariableSearch_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var page = await api.RoundTripAsync(client => client.SearchVariablesAsync(null, pageSize: 25));

        Assert.NotEmpty(page.Items);
    }

    [LiveApiFact]
    public async Task Filters_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        // In Norwegian on purpose: the datatype facet's names are resolved server side from
        // editable master data and follow Accept-Language, so a language is part of what is
        // being checked rather than an incidental default.
        var filters = await api.RoundTripAsync(client => client.GetFiltersAsync(language: "nb"));

        Assert.NotEmpty(filters.Kilder);
    }

    [LiveApiFact]
    public async Task KildeList_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var kilder = await api.RoundTripAsync(client => client.GetKilderAsync());

        Assert.NotEmpty(kilder);
    }

    [LiveApiFact]
    public async Task KildePropertyMetadata_WhenReadFromTheLiveApi_ThenTheTwoCodedFacetsStillHaveAVocabulary()
    {
        using var api = LiveApiConnection.Open();

        var entries = await api.RoundTripAsync(client => client.GetKildePropertyMetadataAsync());

        Assert.NotEmpty(entries);

        // Both keys, in one response, and both carrying optionsJson — which is more than "the
        // contract fits", and deliberately so. The two facets Kelda draws from the kilde list read
        // their words out of exactly these two vocabularies, and the API composes the response from
        // definitions at two different scopes: healthCategory's kilde-scoped definition is retired,
        // so its vocabulary has to come from the datasamling-scoped row. An answer carrying only
        // accessRights is the shape that mistake takes, and it is not one an empty-check would see.
        foreach (var key in new[] { "healthCategory", "accessRights" })
        {
            var entry = entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

            Assert.True(
                entry is not null,
                $"The kilde list's vocabulary no longer carries '{key}', which is a facet Kelda "
                + "draws words for. Without it the choices fall back to raw EHDS and EU CURIEs.");

            Assert.False(
                string.IsNullOrWhiteSpace(entry.OptionsJson) || entry.OptionsJson == "[]",
                $"'{key}' arrived with no options in optionsJson, so there is nothing to label a "
                + "choice with. The component reads optionsJson rather than options, because it "
                + "renders one response to readers in both languages.");
        }
    }

    [LiveApiFact]
    public async Task KildeDetail_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        // The kilde with the most delkilder, because the nested half of KildeDetail only exists in
        // the payload of a kilde that has some — and most do not. Picking the first kilde in the
        // list would leave that half unchecked on almost every run.
        var id = await MostNestedKildeIdAsync(api);

        var kilde = await api.RoundTripAsync(client => client.GetKildeAsync(id));

        Assert.NotNull(kilde);
    }

    [LiveApiFact]
    public async Task KildeHierarchy_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var id = await MostNestedKildeIdAsync(api);

        var hierarchy = await api.RoundTripAsync(client => client.GetKildeHierarchyAsync(id));

        Assert.NotNull(hierarchy);
    }

    [LiveApiFact]
    public async Task DatasamlingDetail_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var kildeId = await MostNestedKildeIdAsync(api);
        var hierarchy = await api.Client.GetKildeHierarchyAsync(kildeId);

        Assert.NotNull(hierarchy);

        var datasamlingId = DatasamlingIds(hierarchy).FirstOrDefault();

        Assert.True(
            datasamlingId != Guid.Empty,
            $"Kilde {kildeId} has no datasamling anywhere in its tree, so there is nothing to open. " +
            "Either the catalogue changed shape or the hierarchy endpoint stopped returning children.");

        var datasamling = await api.RoundTripAsync(client => client.GetDatasamlingAsync(datasamlingId));

        Assert.NotNull(datasamling);
    }

    [LiveApiFact]
    public async Task VariableDetail_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var id = await AnyVariableIdAsync(api);

        var variable = await api.RoundTripAsync(client => client.GetVariableAsync(id));

        Assert.NotNull(variable);
    }

    [LiveApiFact]
    public async Task VariableTimeline_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var id = await AnyVariableIdAsync(api);

        var timeline = await api.RoundTripAsync(client => client.GetVariableTimelineAsync(id));

        // Every published variable has at least the version it is published as. An empty timeline
        // means the endpoint answered about something else.
        Assert.NotEmpty(timeline);
    }

    private static async Task<Guid> MostNestedKildeIdAsync(LiveApiConnection api)
    {
        var kilder = await api.Client.GetKilderAsync();

        Assert.NotEmpty(kilder);

        return kilder.OrderByDescending(kilde => kilde.DelkildeCount)
                     .ThenByDescending(kilde => kilde.DatasamlingCount)
                     .First()
                     .Id;
    }

    private static async Task<Guid> AnyVariableIdAsync(LiveApiConnection api)
    {
        var page = await api.Client.SearchVariablesAsync(null, pageSize: 1);

        Assert.NotEmpty(page.Items);

        return page.Items[0].Id;
    }

    /// <summary>Every datasamling in the tree, direct ones first, then down through the delkilder.</summary>
    private static IEnumerable<Guid> DatasamlingIds(KildeHierarchy hierarchy) =>
        hierarchy.DirectDatasamlinger.Select(datasamling => datasamling.Id)
            .Concat(hierarchy.Delkilder.SelectMany(delkilde => DatasamlingIds(delkilde)));

    private static IEnumerable<Guid> DatasamlingIds(HierarchyDelkilde delkilde) =>
        delkilde.Datasamlinger.Select(datasamling => datasamling.Id)
            .Concat(delkilde.Children.SelectMany(child => DatasamlingIds(child)));
}
