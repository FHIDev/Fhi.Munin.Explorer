using Bunit;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

public partial class VariableListViewTest
{
    private sealed class LinkedExportClient(params VariableListItem[] items) : ListClient(items), IMuninExplorerClient
    {
        public Uri? ExportPage { get; private set; }

        public Task<ExportedList?> ExportMyListAsync(Guid id, Uri? explorerPageUrl,
            ExportFormat format = ExportFormat.Xlsx, bool includeKodeverk = false,
            IReadOnlyCollection<Guid>? kildeIds = null, CancellationToken cancellationToken = default)
        {
            ExportPage = explorerPageUrl;
            return ExportMyListAsync(id, format, includeKodeverk, kildeIds, cancellationToken);
        }
    }

    [Fact]
    public async Task View_WhenDownloadingWithAHostPage_ThenTheExportReceivesThatPage()
    {
        var client = new LinkedExportClient(Item("Age", "V_AGE"));
        var cut = RenderView(client);
        var page = new Uri("https://test.example/en/variables/");
        cut.Render(p => p.Add(c => c.ExplorerPageUrl, page));

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Excel", StringComparison.Ordinal)).Click());

        Assert.Equal(page, client.ExportPage);
        Assert.Equal(ListId, client.ExportedListId);
    }
}
