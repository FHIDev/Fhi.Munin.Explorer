namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every fixture under <c>Testdata/</c>, checked against what the API sends for it today.
/// </summary>
/// <remarks>
/// Two green gates rest on these captures: <see cref="ContractCoverageTest"/> reads them on every
/// commit, and <c>scripts/axe-stub-api.mjs</c> serves them to the accessibility scan. Neither can
/// tell a fresh capture from a stale one, so both keep passing while measuring a payload the API
/// stopped sending.
/// <para>
/// Its own category rather than <c>ContractDrift</c>'s, because the two failures have different
/// fixes and the sidecar titles its report from the answer. Sending somebody to edit DTOs when a
/// capture needed re-taking is the mistake <c>scripts/drift-failure-kind.sh</c> exists to prevent.
/// </para>
/// <para>
/// Skipped unless <see cref="LiveApi.EnabledVariable"/> is set. It runs on the devbox sidecar,
/// inside the network that <c>runa.munin.skytest.fhi.no</c> admits — never on a GitHub runner,
/// which is what #127 retired.
/// </para>
/// </remarks>
[Trait("Category", FixtureDriftTest.Category)]
public class FixtureDriftTest
{
    /// <summary>The trait the devbox sidecar filters on. Changing it changes that sidecar too.</summary>
    public const string Category = "FixtureFreshness";

    [LiveApiFact]
    public async Task VariableSearch_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        AssertFresh(Fixture.Variables, await api.BodyOfAsync(client => client.SearchVariablesAsync(null, pageSize: 25)));
    }

    [LiveApiFact]
    public async Task Filters_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        // Norwegian on purpose: the datatype facet's names are resolved server side and follow
        // Accept-Language, so the language is part of what was captured.
        AssertFresh(Fixture.Filters, await api.BodyOfAsync(client => client.GetFiltersAsync(language: "nb")));
    }

    [LiveApiFact]
    public async Task KildeList_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        AssertFresh(Fixture.Kilder, await api.BodyOfAsync(client => client.GetKilderAsync()));
    }

    [LiveApiFact]
    public async Task KildeDetails_WhenComparedWithTheLiveApi_ThenEveryCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.MostNestedKildeIdAsync(api);
        var body = await api.BodyOfAsync(client => client.GetKildeAsync(id));

        // One fetch, three captures: all three are KildeDetail payloads, and the most nested kilde
        // is the one live response that carries every key the other two could be stale about.
        foreach (var fixture in Fixture.KildeDetails)
        {
            AssertFresh(fixture, body);
        }
    }

    [LiveApiFact]
    public async Task KildeHierarchy_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.MostNestedKildeIdAsync(api);

        AssertFresh(Fixture.Hierarchy, await api.BodyOfAsync(client => client.GetKildeHierarchyAsync(id)));
    }

    [LiveApiFact]
    public async Task DatasamlingDetail_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        var kildeId = await LiveCatalogue.MostNestedKildeIdAsync(api);
        var hierarchy = await api.Client.GetKildeHierarchyAsync(kildeId);

        Assert.NotNull(hierarchy);

        var datasamlingId = LiveCatalogue.DatasamlingIds(hierarchy).FirstOrDefault();

        Assert.True(
            datasamlingId != Guid.Empty,
            $"Kilde {kildeId} has no datasamling anywhere in its tree, so there is nothing to compare against.");

        AssertFresh(Fixture.Datasamling, await api.BodyOfAsync(client => client.GetDatasamlingAsync(datasamlingId)));
    }

    [LiveApiFact]
    public async Task VariableDetail_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.AnyVariableIdAsync(api);

        AssertFresh(Fixture.Variable, await api.BodyOfAsync(client => client.GetVariableAsync(id)));
    }

    [LiveApiFact]
    public async Task VariableTimeline_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        var id = await LiveCatalogue.AnyVariableIdAsync(api);

        AssertFresh(Fixture.Timeline, await api.BodyOfAsync(client => client.GetVariableTimelineAsync(id)));
    }

    [LiveApiFact]
    public async Task KodeverkCodes_WhenComparedWithTheLiveApi_ThenTheCaptureIsStillCurrent()
    {
        using var api = LiveApiConnection.Open();

        // The only check this endpoint has: ContractDriftTest never calls it, so a change here
        // would otherwise reach the stub and the coverage test unnoticed.
        var (variableId, link) = await LiveCatalogue.AnyKodeverkLinkAsync(api);

        var body = await api.BodyOfAsync(client =>
            client.GetKodeverkCodesAsync(variableId, link.KodeverkType, link.KodeverkReference));

        AssertFresh(Fixture.KodeverkCodes, body);
    }

    private static void AssertFresh(string fixture, string liveBody)
    {
        Assert.True(
            FixtureFreshness.CarriesAnything(liveBody),
            $"The API answered with nothing, so '{fixture}' was compared against an empty document and " +
            "this run proves nothing about it. Check what the endpoint returned before trusting a pass.");

        var findings = FixtureFreshness.Against(liveBody, TestData.Read(fixture));

        if (findings.Count > 0)
        {
            Assert.Fail(Explain(fixture, findings));
        }
    }

    private static string Explain(string fixture, IReadOnlyList<string> findings) =>
        $"""
         Testdata/{fixture} no longer describes what {LiveApi.BaseUrl} sends — {findings.Count} difference(s):

         {string.Join(Environment.NewLine + Environment.NewLine, findings.Select(finding => "  * " + finding))}

         The capture is stale, not the contracts. ContractCoverageTest and scripts/axe-stub-api.mjs
         both read this file and cannot tell, so they stay green against a payload the API no longer
         serves. Re-capture it from the live endpoint and check whether the DTO needs the new field
         too — see the re-capture note on TestData.
         """;
}

/// <summary>
/// The fixtures under <c>Testdata/</c>, and which of them a live call can re-fetch.
/// </summary>
/// <remarks>
/// Named here rather than only at the call sites so <see cref="FixtureFreshnessTest"/> can hold the
/// whole directory against this list. A fixture added and forgotten is the failure this closes.
/// </remarks>
internal static class Fixture
{
    public const string Variables = "variables.json";
    public const string Filters = "filters.json";
    public const string Kilder = "kilder.json";
    public const string Hierarchy = "hierarchy.json";
    public const string Datasamling = "datasamling.json";
    public const string Variable = "variable.json";
    public const string Timeline = "timeline.json";
    public const string KodeverkCodes = "kodeverk-codes.json";

    /// <summary>The three KildeDetail captures, all comparable against one live kilde.</summary>
    public static readonly IReadOnlyList<string> KildeDetails =
        ["kilde.json", "kilde-med-delkilder.json", "kilde-barnediabetes.json"];

    public static readonly IReadOnlyList<string> CheckedLive =
        [Variables, Filters, Kilder, Hierarchy, Datasamling, Variable, Timeline, KodeverkCodes, .. KildeDetails];

    /// <summary>Fixtures no anonymous caller can re-fetch, with the reason each is out of reach.</summary>
    /// <remarks>
    /// Every <c>my/lists</c> endpoint sits behind the API's authenticated explorer policy, so checking
    /// these live would mean holding an explorer session and creating real lists on a running server.
    /// They are written from the API's own DTOs, and stay a file somebody has to remember to update.
    /// </remarks>
    public static readonly IReadOnlyList<string> OutOfReach = ["my-lists.json", "my-list-variables.json"];
}
