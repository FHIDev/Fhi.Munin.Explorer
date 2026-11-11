using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// A blank code is not a second code, so one real code plus one blank draws no code on either row (Fhi.Metadata-l9l2n.161).
public class SharedNameBlankCodeHeldOutTest : ExplorerTestContext
{
    private const string Name = "Tiltak kodet via ICD-10";

    private sealed class FakeClient(VariableSummary[] rows) : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = rows,
                TotalCount = rows.Length,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });
    }

    private static VariableSummary Variable(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        PreferredTerm = Name,
        KildeName = "Parkinsonregisteret",
        DatasamlingName = "Oppfølging",
    };

    [Fact]
    public void Rows_WhenSharedNameHasOneRealCodeAndOneBlank_ThenNeitherRowDrawsACode()
    {
        Services.AddSingleton<IMuninExplorerClient>(
            new FakeClient([Variable("V_PARKINSON.F7.NPR_PROSEDYRER_1"), Variable(" ")]));
        var cut = Render<VariableSearch>();

        var toggles = cut.FindAll("ul.munin-explorer-data-list > li button.munin-explorer-dataitem-main__name");

        Assert.Equal(2, toggles.Count);
        Assert.Empty(cut.FindAll(".munin-explorer-list-code"));
        Assert.Equal($"Vis detaljer for {Name}", AccessibleName.Of(toggles[0]));
    }
}
