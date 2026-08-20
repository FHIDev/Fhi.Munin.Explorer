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
/// <see cref="ShapeDrift"/> honest between nights, and the last test drives the whole nightly
/// path — the real client, the recording, the comparison, the failure — against a stub.
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
    public void Between_WhenTheApiHasNotCaughtUpWithTheContract_ThenNothingDrifts() =>
        // filters.json was captured before the datatype facet gained displayName and before the
        // datakategori facet existed at all, so the contract declares two things this payload does
        // not carry — one null, one empty list. That is the case the comparison must let through:
        // both are how a contract says "nothing here", and reporting them would make the job cry
        // drift over an API that is merely older than the code reading it.
        Assert.Empty(DriftIn<FilterOptions>(Load("filters.json")));

    [Fact]
    public void Between_WhenTheApiSendsTheDatakategoriFacet_ThenNothingDrifts()
    {
        // datakategorier was added to the contracts because the nightly check found the API already
        // sending it, and every capture under Testdata/ is older than that. Pasting the live shape
        // back in is what proves the wire names and types were read off it correctly, instead of
        // waiting a night to find out they were not.
        var live = Load("filters.json");
        live["datakategorier"] = JsonNode.Parse("""
            [{ "value": "ehds-cat:health-registries", "count": 38 }]
            """);

        Assert.Empty(DriftIn<FilterOptions>(live));
    }

    [Fact]
    public void Between_WhenTheApiSendsTheParsedPropertyOptions_ThenNothingDrifts()
    {
        // The other half of the same addition, and the same reasoning: options is the parsed,
        // language-resolved twin of optionsJson, which this package used to tell callers to parse
        // themselves.
        var live = Load("kilde.json");
        live["propertyMetadata"]![0]!["options"] = JsonNode.Parse("""
            [{ "value": "sentraltHelseregister", "displayName": "Sentralt helseregister" }]
            """);

        Assert.Empty(DriftIn<KildeDetail>(live));
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
