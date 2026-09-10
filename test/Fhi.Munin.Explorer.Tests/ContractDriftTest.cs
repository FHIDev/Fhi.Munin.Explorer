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
    public async Task Filters_WhenReadInBothLanguages_ThenTheKildetypeFacetIsResolvedProseInEachOfThem()
    {
        using var api = LiveApiConnection.Open();

        // What the panel leans on since Fhi.Metadata-3n6e1: the kildetype word is the API's rather
        // than Texts.KildeTypeNames'. An API back to echoing the enum name would put that fallback
        // silently in charge of the page again, with nothing else here going red.

        // The client rather than RoundTripAsync, so this keeps answering while the shape check above
        // is red — they fail for unrelated reasons and the second is no use once the first is out.
        var norwegian = await api.Client.GetFiltersAsync(language: "nb");
        var english = await api.Client.GetFiltersAsync(language: "en");

        Assert.NotEmpty(norwegian.KildeTyper);

        // Named here rather than guarded for inside the loop: a value offered in one language and
        // not the other is drift of its own, and this says which value while a null two lines below
        // would only say that one of them went missing.
        Assert.Equal(
            norwegian.KildeTyper.Select(type => type.Value).OrderBy(value => value, StringComparer.Ordinal),
            english.KildeTyper.Select(type => type.Value).OrderBy(value => value, StringComparer.Ordinal));

        foreach (var type in norwegian.KildeTyper)
        {
            var abroad = english.KildeTyper.Single(
                other => string.Equals(other.Value, type.Value, StringComparison.Ordinal));

            Assert.False(
                string.Equals(type.DisplayName, type.Value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(abroad.DisplayName, abroad.Value, StringComparison.OrdinalIgnoreCase),
                $"'{type.Value}' came back as its own enum name rather than as a resolved label, so "
                + "the facet is being drawn from the shipped fallback table instead of the "
                + "Kilde-scoped Kildetype PropertyDefinition.");
        }

        // Biobank is spelled the same in both, so the whole set rather than any one value: a
        // response that ignored Accept-Language would repeat every word, not just that one.
        Assert.NotEqual(
            norwegian.KildeTyper.Select(type => type.DisplayName).OrderBy(name => name, StringComparer.Ordinal),
            english.KildeTyper.Select(type => type.DisplayName).OrderBy(name => name, StringComparer.Ordinal));
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

        // A kilde with delkilder, because the nested half of KildeDetail only exists in the payload
        // of a kilde that has some — and almost none do. Picking the first kilde in the list would
        // leave that half unchecked on almost every run.
        var id = await LiveCatalogue.KildeWithDelkilderIdAsync(api);

        var kilde = await api.RoundTripAsync(client => client.GetKildeAsync(id));

        Assert.NotNull(kilde);
    }

    [LiveApiFact]
    public async Task KildeHierarchy_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.KildeWithDelkilderIdAsync(api);

        var hierarchy = await api.RoundTripAsync(client => client.GetKildeHierarchyAsync(id));

        Assert.NotNull(hierarchy);
    }

    [LiveApiFact]
    public async Task DatasamlingDetail_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var datasamlingId = await LiveCatalogue.AnyDatasamlingIdAsync(api);

        var datasamling = await api.RoundTripAsync(client => client.GetDatasamlingAsync(datasamlingId));

        Assert.NotNull(datasamling);
    }

    [LiveApiFact]
    public async Task VariableDetail_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.AnyVariableIdAsync(api);

        var variable = await api.RoundTripAsync(client => client.GetVariableAsync(id));

        Assert.NotNull(variable);
    }

    [LiveApiFact]
    public async Task VariableTimeline_WhenReadFromTheLiveApi_ThenTheContractStillFitsIt()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.AnyVariableIdAsync(api);

        var timeline = await api.RoundTripAsync(client => client.GetVariableTimelineAsync(id));

        // Every published variable has at least the version it is published as. An empty timeline
        // means the endpoint answered about something else.
        Assert.NotEmpty(timeline);
    }

    /// <summary>
    /// The whole life of one annotation — written, edited, refused for length, cleared — against
    /// the endpoint itself.
    /// </summary>
    /// <remarks>
    /// The one arm here that writes, and the only check anywhere that the component's annotation
    /// column agrees with the API rather than with a fake we wrote from the same reading of it.
    /// It works on a list it creates and deletes, so it leaves the reader's own lists alone.
    /// </remarks>
    [LiveListsFact]
    public async Task DesiredData_WhenWrittenToTheLiveApi_ThenItSurvivesAReadBack()
    {
        using var api = LiveApiConnection.Open(token: LiveApi.Token);

        var variableId = await LiveCatalogue.AnyVariableIdAsync(api);
        var list = await api.Client.CreateMyListAsync($"contract drift {DateTimeOffset.UtcNow:O}");

        try
        {
            Assert.True(await api.Client.AddVariablesToMyListAsync(list.Id, [variableId]));

            // Padded on purpose: the client trims, the API trims, and a caller that believed
            // either one alone would report a length the other never measured.
            var written = await api.Client.SetMyListDesiredDataAsync(list.Id, variableId, "  Kun 2019  ");

            Assert.Equal(DesiredDataOutcome.Saved, written.Outcome);
            Assert.Equal("Kun 2019", await ReadAnnotationAsync(api, list.Id));

            var edited = await api.Client.SetMyListDesiredDataAsync(list.Id, variableId, "Kun 2020, aggregert");

            Assert.Equal(DesiredDataOutcome.Saved, edited.Outcome);
            Assert.Equal("Kun 2020, aggregert", await ReadAnnotationAsync(api, list.Id));

            // The ceiling is the API's to name. Asserting a number here would be the constant the
            // contract deliberately does not carry, so this asks only that a refusal names one.
            var refused = await api.Client.SetMyListDesiredDataAsync(list.Id, variableId, new string('x', 5_000));

            Assert.Equal(DesiredDataOutcome.Refused, refused.Outcome);
            Assert.NotNull(refused.MaxLength);

            // A refusal must not have written anything either: a reader whose text was too long
            // still has the text they had before it.
            Assert.Equal("Kun 2020, aggregert", await ReadAnnotationAsync(api, list.Id));

            var cleared = await api.Client.SetMyListDesiredDataAsync(list.Id, variableId, null);

            Assert.Equal(DesiredDataOutcome.Saved, cleared.Outcome);
            Assert.Null(await ReadAnnotationAsync(api, list.Id));
        }
        finally
        {
            await api.Client.DeleteMyListAsync(list.Id);
        }
    }

    /// <summary>Reads the list back and hands over the one item's annotation, shape-checked.</summary>
    private static async Task<string?> ReadAnnotationAsync(LiveApiConnection api, Guid listId)
    {
        var page = await api.RoundTripAsync(async client =>
            await client.GetMyListVariablesAsync(listId, page: 1, pageSize: 25)
            ?? throw new InvalidOperationException($"The API does not have list {listId} as ours."));

        return Assert.Single(page.Items).DesiredDataFreeText;
    }
}
