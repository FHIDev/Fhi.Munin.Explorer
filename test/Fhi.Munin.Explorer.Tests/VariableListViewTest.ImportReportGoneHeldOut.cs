// Held out: written from the behaviour, not the code (Fhi.Metadata-9huq2.37).
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    /// <summary>Munin answers 404 to both import-report writes: the thing is already gone.</summary>
    private sealed class GoneImportClient(params VariableListItem[] items) : ListClient(items)
    {
        public override async Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            [.. (await base.GetMyListsAsync(cancellationToken)).Select(l => l.Id == ListId
                ? l with { ImportedFrom = "helsedata-legacy", ImportNotFoundCount = 4, ImportSkippedCount = 0 }
                : l)];

        public override Task<ListImportReport?> GetMyListImportReportAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ListImportReport?>(id == ListId ? FourNotFound() : null);

        public override Task<bool> DismissMyListImportReportAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public override Task<bool> RemoveMyListImportEntryAsync(Guid id, Guid entryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private IRenderedComponent<VariableListView> RenderGone()
    {
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(new GoneImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        Services.AddScoped<VariableListState>();
        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
    }

    private static void AssertNoFailureSentence(IRenderedComponent<VariableListView> cut)
    {
        Assert.DoesNotContain("Kunne ikke endre listen", cut.Markup);
        Assert.All(cut.FindAll("[role=alert]"), a => Assert.Equal("", Text(a)));
    }

    [Fact]
    public async Task ImportReport_WhenRemoveAnswersNotFound_ThenTheEntryLeavesWithoutAFailure()
    {
        var cut = RenderGone();
        cut.WaitForElement(ImportNotice);
        Assert.Equal(4, cut.FindAll($"{ImportNotice} li").Count);

        await cut.InvokeAsync(() => cut.FindAll($"{ImportNotice} li button")[0].Click());

        cut.WaitForAssertion(() =>
        {
            var entries = cut.FindAll($"{ImportNotice} li").Select(Text).ToList();
            Assert.Equal(3, entries.Count);
            Assert.DoesNotContain(entries, e => e.Contains("V_BORTE"));
        });
        AssertNoFailureSentence(cut);
    }

    [Fact]
    public async Task ImportReport_WhenHideAnswersNotFound_ThenTheNoticeGoesWithoutAFailure()
    {
        var cut = RenderGone();
        cut.WaitForElement(ImportNotice);

        await cut.InvokeAsync(() => cut.Find($"{ImportNotice} > button").Click());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ImportNotice)));
        AssertNoFailureSentence(cut);
    }
}
