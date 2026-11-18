using System.Text.Json;
using System.Text.RegularExpressions;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The README tells hosts exactly which fields a browser-list row stores (Fhi.Metadata-emsc0.3.12); this holds that
/// sentence to what the code writes, so a field added to or dropped from the row cannot leave the prose behind.
/// </summary>
public partial class StoredRowFieldsTest
{
    [Fact]
    public void StoredFields_WhenEveryFieldIsSet_ThenTheStoredNamesAreExactlyTheReadmes()
    {
        var everything = new VariableListItem
        {
            VariableId = Guid.NewGuid(),
            VariableCode = "c",
            VariableName = "n",
            KildeId = Guid.NewGuid(),
            KildeName = "k",
            KildeShortName = "ks",
            DatasamlingId = Guid.NewGuid(),
            DatasamlingName = "d",
            VariabelgruppeName = "g",
            DataType = "t",
            DataFrom = DateTimeOffset.UnixEpoch,
            DataTo = DateTimeOffset.UnixEpoch,
            VersionStatus = "s",
            Notes = "private",
            DesiredDataFreeText = "private",
            DesiredDataType = "freeText",
            DesiredDataCodes = [new DesiredCodeView { KodeverkType = "t", KodeverkReference = "r", Kind = "code", Code = "1" }],
            ItemId = Guid.NewGuid(),
            DatasamlingCode = "dc",
            HasKodeverk = true,
            HasStatistics = true,
            AddedAt = DateTimeOffset.UnixEpoch,
        };
        var row = JsonSerializer.SerializeToElement(MuninExplorerClient.SharedItemBody.From(VariableListState.StoredFields(everything)));
        var stored = row.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null).Select(p => p.Name).Order();

        var line = File.ReadAllLines(Repo.In("README.md")).Single(l => l.StartsWith("Stored row fields: ", StringComparison.Ordinal));
        var documented = Backticked().Matches(line).Select(m => m.Groups[1].Value).Order();

        Assert.Equal(documented, stored);
    }

    [GeneratedRegex("`([A-Za-z]+)`")]
    private static partial Regex Backticked();
}
