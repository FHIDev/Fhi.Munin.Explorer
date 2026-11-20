using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>A create Munin refuses at the person's list quota says so, not that saving failed (Fhi.Metadata-oiips.61).</summary>
public partial class VariableListViewTest
{
    private sealed class QuotaListClient(VariableListItem item) : ListClient(item)
    {
        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new MuninExplorerListQuotaException(ListQuotaKind.Lists, 50, "Du kan ha høyst 50 lister.");
        }
    }

    [Theory]
    [InlineData("no")]
    [InlineData("en")]
    public void Create_WhenMuninRefusesItAtTheListLimit_ThenTheAlertSaysTheLimitRatherThanThatSavingFailed(string language)
    {
        var texts = Texts.For(language);
        var cut = RenderView(new QuotaListClient(Item("Alder ved diagnose", "V_BDR.ALDER")), language: language);

        CreateField(cut).Change("Hjerte og kar");
        Press(cut, texts.CreateList);

        var alert = cut.Find("[role='alert']").TextContent.Trim();
        Assert.Equal(texts.ListLimitReached(50), alert);
        Assert.Equal("Hjerte og kar", cut.Find("input[id^='munin-explorer-new-list-']").GetAttribute("value"));
    }
}
