using System.Text.Json;
using System.Text.Json.Serialization;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every field the API sends has somewhere to land.
/// </summary>
/// <remarks>
/// Deserialising normally ignores a property the contract does not know about, so a field added in
/// Munin — or one missed when these records were written — disappears silently. Reading the same
/// captured responses with unmapped members disallowed turns that into a failing test. The
/// exception message names the offending property, which is the fix.
/// <para>
/// Re-capture the files under <c>Testdata/</c> from the live API when Munin's explorer changes;
/// this test is what tells you the contracts have to change too.
/// </para>
/// </remarks>
public class ContractCoverageTest
{
    private static readonly JsonSerializerOptions Strict = new(MuninExplorerClient.Json)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static void Covers<T>(string fixture) =>
        Assert.NotNull(JsonSerializer.Deserialize<T>(TestData.Read(fixture), Strict));

    [Fact]
    public void VariableSearch_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<Page<VariableSummary>>("variables.json");

    [Fact]
    public void Filters_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<FilterOptions>("filters.json");

    [Fact]
    public void KildeList_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<IReadOnlyList<KildeSummary>>("kilder.json");

    [Fact]
    public void KildeDetail_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<KildeDetail>("kilde.json");

    [Fact]
    public void KildeDetailWithDelkilder_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        // Most kilder have no delkilder at all, so the nested branch of the contract would
        // otherwise never be exercised. This one is a study series with one delkilde per wave.
        Covers<KildeDetail>("kilde-med-delkilder.json");

    [Fact]
    public void KildeHierarchy_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<KildeHierarchy>("hierarchy.json");

    [Fact]
    public void DatasamlingDetail_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<DatasamlingDetail>("datasamling.json");

    [Fact]
    public void VariableDetail_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<VariableDetail>("variable.json");

    [Fact]
    public void Timeline_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        Covers<IReadOnlyList<VariableVersion>>("timeline.json");

    [Fact]
    public void KodeverkCodes_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        // The endpoint answers with an envelope, not with the bare array of codes it is easy to
        // assume from reading one — kodeverkType and kodeverkReference come back alongside them.
        Covers<KodeverkCodes>("kodeverk-codes.json");
}
