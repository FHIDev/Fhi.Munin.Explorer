using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Xunit.Sdk;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Proves the nightly job can tell drift from data, without needing the API to break first.
/// </summary>
/// <remarks>
/// The scheduled contract test is only worth having if a real mismatch fails it and a normal
/// night does not, and neither half can be demonstrated by waiting. So each half is done here to
/// a payload we captured from the live API and then broke on purpose — a field added, a field
/// renamed, a field withdrawn — with the untouched payload as the control.
/// <para>
/// These run on every commit. Nothing here leaves the machine: they are what keeps
/// <see cref="ShapeDrift"/> honest between nights, and the last two drive the whole nightly path —
/// the real client, the recording, the comparison, the failure — against a stub.
/// </para>
/// </remarks>
public class ShapeDriftTest
{
    [Fact]
    public void Between_WhenACapturedVariableSearchIsUnchanged_ThenNothingDrifts() =>
        Assert.Empty(DriftIn<Page<VariableSummary>>(Load("variables.json")));

    [Fact]
    public void Between_WhenACapturedKildeListIsUnchanged_ThenNothingDrifts() =>
        // Also the free-form additionalProperties bag, whose keys differ from kilde to kilde. A
        // comparison that read those as fields would report every kilde as drift.
        Assert.Empty(DriftIn<IReadOnlyList<KildeSummary>>(Load("kilder.json")));

    [Fact]
    public void Between_WhenACapturedKildeIsUnchanged_ThenNothingDrifts() =>
        Assert.Empty(DriftIn<KildeDetail>(Load("kilde-med-delkilder.json")));

    [Fact]
    public void Between_WhenACapturedVariableIsUnchanged_ThenNothingDrifts() =>
        Assert.Empty(DriftIn<VariableDetail>(Load("variable.json")));

    [Fact]
    public void Between_WhenACapturedFilterSetIsUnchanged_ThenNothingDrifts() =>
        // Including datakategorier, which the contracts gained because the first nightly run found
        // the API already sending it. The capture was re-taken with it, so this is what proves the
        // wire names and types were read off the live payload correctly rather than guessed.
        Assert.Empty(DriftIn<FilterOptions>(Load("filters.json")));

    [Fact]
    public void Between_WhenACapturedKildeWithParsedOptionsIsUnchanged_ThenNothingDrifts() =>
        // The other half of the same addition: options is the parsed, language-resolved twin of
        // optionsJson, which this package used to tell callers to parse themselves.
        Assert.Empty(DriftIn<KildeDetail>(Load("kilde.json")));

    [Fact]
    public void Between_WhenACapturedKildeHierarchyIsUnchanged_ThenNothingDrifts() =>
        Assert.Empty(DriftIn<KildeHierarchy>(Load("hierarchy.json")));

    [Fact]
    public void Between_WhenACapturedDatasamlingIsUnchanged_ThenNothingDrifts() =>
        Assert.Empty(DriftIn<DatasamlingDetail>(Load("datasamling.json")));

    [Fact]
    public void Between_WhenACapturedTimelineIsUnchanged_ThenNothingDrifts() =>
        // The eighth and last shape. One control for every endpoint ContractDriftTest checks
        // nightly, and that is the count to keep: a shape with no control here can only be found
        // to be a false positive at 04:17 UTC, as a red scheduled run that files an issue telling
        // somebody to go and fix a DTO that was never wrong — and a nightly job people stop
        // believing is a nightly job people stop reading. ContractCoverageTest does not stand in
        // for these: it asks whether every wire field has somewhere to land, not whether the
        // contract declares anything the payload does not, or reads a field as the wrong kind.
        Assert.Empty(DriftIn<IReadOnlyList<VariableVersion>>(Load("timeline.json")));

    [Fact]
    public void Between_WhenTheApiHasNotCaughtUpWithTheContract_ThenNothingDrifts()
    {
        // An API older than the code reading it: this capture's datatype facets carry no
        // displayName, and here the datakategori facet is taken away as well, so the contract
        // declares two things the payload does not — one null, one empty list. That is the case the
        // comparison must let through, because both are how a contract says "nothing here" and
        // reporting them would make the job cry drift over a deployment that is merely behind.
        var live = Load("filters.json");
        live.AsObject().Remove("datakategorier");

        Assert.Empty(DriftIn<FilterOptions>(live));
    }

    [Fact]
    public void Between_WhenTheApiAddsAFieldTheContractDoesNotKnow_ThenItDrifts()
    {
        var live = Load("kilder.json");
        live[0]!["nyttFelt"] = "something nobody has modelled";

        var finding = Assert.Single(DriftIn<IReadOnlyList<KildeSummary>>(live));

        Assert.Contains("nyttFelt", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void Between_WhenTheApiRenamesAField_ThenBothHalvesAreReported()
    {
        var live = Load("variables.json");
        var first = live["items"]![0]!.AsObject();
        var renamed = first["preferredTerm"]!.DeepClone();

        first.Remove("preferredTerm");
        first["foretrukketTerm"] = renamed;

        var drift = DriftIn<Page<VariableSummary>>(live);

        // A rename is the one that hurts most and looks least like anything: the page keeps
        // rendering, with an empty heading where the name was. Both halves are reported because
        // either one alone reads as something else — an added field, or a withdrawn one.
        Assert.Equal(2, drift.Count);
        Assert.Contains(drift, finding => finding.Contains("foretrukketTerm", StringComparison.Ordinal));
        Assert.Contains(drift, finding => finding.Contains("preferredTerm", StringComparison.Ordinal));
    }

    [Fact]
    public void Between_WhenTheApiStopsSendingAFieldTheContractRequires_ThenItDrifts()
    {
        var live = Load("kilder.json");
        live[0]!.AsObject().Remove("code");

        var finding = Assert.Single(DriftIn<IReadOnlyList<KildeSummary>>(live));

        Assert.Contains("code", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void Between_WhenTheApiStopsSendingAnOptionalField_ThenNothingDrifts()
    {
        var live = Load("kilder.json");
        live[0]!.AsObject().Remove("kortNavn");

        // The counterpart to the test above, and the reason it is worth stating twice: whether a
        // withdrawn field is drift depends on whether the contract had anywhere to put "absent".
        Assert.Empty(DriftIn<IReadOnlyList<KildeSummary>>(live));
    }

    [Fact]
    public void Between_WhenTheApiStopsSendingACollectionAltogether_ThenNothingDrifts()
    {
        var live = Load("kilde-med-delkilder.json");
        live.AsObject().Remove("delkilder");

        // What the rule above costs, written down rather than left to be found out. An empty list
        // is how this contract says "none", so a collection withdrawn wholesale reads exactly like
        // one that happens to be empty, and this kilde losing its five delkilder passes. The
        // remarks on ShapeDrift argue why that trade is the right way round; if the argument stops
        // holding, this is the test that has to change with it.
        Assert.Empty(DriftIn<KildeDetail>(live));
    }

    [Fact]
    public void Between_WhenANumberStartsArrivingAsAString_ThenItDrifts()
    {
        var live = Load("variables.json");
        live["items"]![0]!["presentationOrder"] = "3";

        // Web defaults read a number out of a string without complaining, so this one deserialises
        // cleanly and would otherwise pass unnoticed until something sorted on it.
        var finding = Assert.Single(DriftIn<Page<VariableSummary>>(live));

        Assert.Contains("presentationOrder", finding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoundTripAsync_WhenTheApiSendsAFieldTheContractLacks_ThenTheNightlyJobFails()
    {
        var live = Load("kilder.json");
        live[0]!["nyttFelt"] = "something nobody has modelled";

        using var api = LiveApiConnection.Open(StubHttpHandler.Ok(live.ToJsonString()));

        // The whole nightly path, minus the network: the real client asks, the handler records,
        // the answer is compared, and the difference fails the build. This is the test that would
        // have to be deleted for a DTO/API mismatch to pass unnoticed.
        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => api.RoundTripAsync(client => client.GetKilderAsync()));

        Assert.Contains("nyttFelt", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoundTripAsync_WhenTheEndpointAnswers404_ThenTheNightlyJobFails()
    {
        using var api = LiveApiConnection.Open(StubHttpHandler.Status(HttpStatusCode.NotFound));

        // The quietest failure the whole job exists to catch: an endpoint that moves answers with
        // no body, the body deserialises to a default DTO, and a default DTO round-trips against
        // nothing perfectly. Without the status check above the comparison, a moved endpoint is a
        // green run. This is the test that keeps that check from being deleted as redundant.
        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => api.RoundTripAsync(client => client.GetKilderAsync()));

        Assert.Contains("404", failure.Message, StringComparison.Ordinal);
    }

    private static JsonNode Load(string fixture) =>
        JsonNode.Parse(TestData.Read(fixture))
        ?? throw new InvalidOperationException($"Test data '{fixture}' is not JSON.");

    /// <summary>
    /// Runs a payload through the same two steps the nightly job does — deserialise with the
    /// client's own options, serialise straight back — and reports the shape difference.
    /// </summary>
    private static IReadOnlyList<string> DriftIn<T>(JsonNode live)
    {
        var json = live.ToJsonString();

        return ShapeDrift.Against(json, JsonSerializer.Deserialize<T>(json, MuninExplorerClient.Json));
    }
}
