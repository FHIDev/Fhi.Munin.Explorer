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
/// <para>
/// What it cannot tell you is whether those captures are still what the API serves — they are a
/// snapshot, and the API is in another repository. <see cref="ContractDriftTest"/> is the nightly
/// half that asks the live API the same question.
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
    public void VariabelgruppeSurfaces_WhenReadFromTheDocumentedResponse_ThenEveryFieldIsCovered() =>
        // Inline rather than from a fixture: both variabelgruppe collections answer empty in every
        // environment probed so far, so filters.json above reads neither and a field added to the
        // facet would land nowhere (Fhi.Metadata-0ecep). Writing a row into that capture is what
        // would make this gate and the freshness one agree about a payload the API does not send,
        // so the shape is taken from the API's own documented response instead — which makes this
        // weaker evidence than the captures, on the same terms as the my/lists pair below: it pins
        // that the contract reads the shape Munin documents, not that Munin still sends it.
        Assert.NotNull(JsonSerializer.Deserialize<FilterOptions>(
            """
            {
              "variabelgrupper": [
                {
                  "id": "8e4507de-d725-4471-aeb9-97999cf411ce",
                  "name": "Gruppe tilbudt",
                  "parentId": null,
                  "count": 1,
                  "filter": "1",
                  "global": false,
                  "owners": [
                    {
                      "kildeId": "c368f9fb-2fbc-43c2-b0ba-c6db2b1d2f68",
                      "delkildeId": null,
                      "datasamlingId": "25acaea1-6ba9-4417-b514-e014e4b53011"
                    }
                  ]
                }
              ],
              "hierarkiVariabelgrupper": [
                {
                  "id": "09489d56-c805-420f-898b-211a335724b8",
                  "name": "Gruppe opt-out",
                  "parentId": null,
                  "count": 1,
                  "filter": "2",
                  "global": false,
                  "owners": [
                    {
                      "kildeId": "c368f9fb-2fbc-43c2-b0ba-c6db2b1d2f68",
                      "delkildeId": "c1cc6266-ac04-4e7c-8b33-427dbb9d6871",
                      "datasamlingId": "f9262831-e1a9-405f-8599-0d0eb6e9b8ed"
                    }
                  ]
                }
              ],
              "totalCount": 3
            }
            """,
            Strict));

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
    public void MyLists_WhenReadFromTheDocumentedResponse_ThenEveryFieldIsCovered() =>
        // Weaker evidence than the fixtures above it, and worth saying so. Every my/lists endpoint
        // is authenticated, so this payload could not be captured from the anonymous test API the
        // others came from — it is written from the API's own MyListDto, and the nightly
        // ContractDriftTest cannot reach these endpoints either, since checking them live would
        // mean holding an explorer session and creating and deleting real lists on a running
        // server. So what this pins is that VariableList reads the shape Munin's controller
        // declares, not that Munin still declares it. When the my/lists contract changes, this is
        // a file somebody has to remember to update rather than a build that notices.
        Covers<IReadOnlyList<VariableList>>("my-lists.json");

    [Fact]
    public void MyListVariables_WhenReadFromTheDocumentedResponse_ThenEveryFieldIsCovered() =>
        // Same caveat as above. Note that the envelope carries no totalPages, which is why the
        // client derives one — see MyListsClientTest, where that is pinned; the strict read here
        // would pass either way, because an absent member is not an unmapped one.
        Covers<Page<VariableListItem>>("my-list-variables.json");

    [Fact]
    public void KodeverkCodes_WhenReadFromARealResponse_ThenEveryFieldIsCovered() =>
        // The endpoint answers with an envelope, not with the bare array of codes it is easy to
        // assume from reading one — kodeverkType and kodeverkReference come back alongside them.
        Covers<KodeverkCodes>("kodeverk-codes.json");
}
