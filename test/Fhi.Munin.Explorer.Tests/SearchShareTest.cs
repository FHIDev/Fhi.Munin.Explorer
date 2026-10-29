using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// «Del variabel» on an open search row and on the whole variable, signed out: the same group as in the reader's
/// own list, carrying the link that opens this one variable (Fhi.Metadata-9huq2.9, ADO 120632).
/// </summary>
public class SearchShareTest : ExplorerTestContext
{
    private static readonly Guid VariableId = Guid.Parse("7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c");
    private static readonly Guid KildeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DatasamlingId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private const string Code = "V_HKR.KJONN";
    private const string Name = "Kjønn";

    private sealed class ShareClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public List<(string? Search, VariableFilter? Filter)> Searches { get; } = [];

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add((search, filter));
            return Task.FromResult(new Page<VariableSummary>
            {
                Items =
                [
                    new VariableSummary
                    {
                        Id = VariableId, Code = Code, PreferredTerm = Name, KildeId = KildeId, KildeName = "Hjerte- og karregisteret",
                        DatasamlingId = DatasamlingId, DatasamlingName = "Hjerteinfarkt", VersionStatus = "Aktiv",
                    },
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = VariableId,
                Code = Code,
                PreferredTerm = Name,
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                KildeShortName = "HKR",
                DatasamlingId = DatasamlingId,
                DatasamlingName = "Hjerteinfarkt",
                VersionStatus = "Aktiv",
            });
    }

    private (IRenderedComponent<VariableExplorer> Cut, ShareClient Client) RenderExplorer(string address = "http://localhost/variabler?search=kjonn")
    {
        var client = new ShareClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return (Render<VariableExplorer>(), client);
    }

    private static void OpenRow(IRenderedComponent<VariableExplorer> cut)
    {
        cut.WaitForElement("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();
        cut.WaitForElement(".munin-explorer-detail");
    }

    private static AngleSharp.Dom.IElement ShareGroup(AngleSharp.Dom.IElement within) =>
        within.QuerySelectorAll("[role=group]").Single(g =>
            within.QuerySelector($"#{g.GetAttribute("aria-labelledby")}")?.TextContent.Trim() == "Del variabel");

    private static void AssertTheLinkOpensThisVariable(string link)
    {
        var uri = new Uri(link);
        Assert.True(uri.IsAbsoluteUri);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal(Code, query["search"].ToString());
        Assert.Equal(KildeId.ToString(), query["kildeIds"].ToString());
        Assert.Equal(VariableId.ToString(), query["variabelId"].ToString());
        Assert.Equal(DatasamlingId.ToString(), query["datasamlingId"].ToString());
    }

    [Fact]
    public void OpenRow_SignedOut_ThenItOffersDelVariabelWithItsAccessibleNames()
    {
        var (cut, _) = RenderExplorer();

        OpenRow(cut);

        var group = ShareGroup(cut.Find(".munin-explorer-detail"));
        Assert.Single(group.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Kopier lenke");
        var mail = group.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post");
        Assert.StartsWith("Åpner e-postprogrammet ditt.", cut.Find($"#{mail.GetAttribute("aria-describedby")}").TextContent.Trim());
        Assert.Single(group.QuerySelectorAll("[role=status][aria-live=polite]"));
    }

    [Fact]
    public void CopyLink_OnAnOpenRow_ThenTheClipboardGetsTheLinkToThisVariableAndThatIsSaid()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        copied.SetVoidResult();
        var (cut, _) = RenderExplorer();
        OpenRow(cut);

        ShareGroup(cut.Find(".munin-explorer-detail")).QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

        cut.WaitForAssertion(() => AssertTheLinkOpensThisVariable((string)copied.Invocations.Single().Arguments.Single()!));
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("[role=status]"), s => s.TextContent.Trim() == "Lenken er kopiert."));
    }

    [Fact]
    public void CopyLink_WhenTheBrowserRefuses_ThenTheLinkIsShownToCopyByHand()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("NotAllowedError"));
        var (cut, _) = RenderExplorer();
        OpenRow(cut);

        ShareGroup(cut.Find(".munin-explorer-detail")).QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("[role=status]"),
            s => s.TextContent.Trim() == "Nettleseren tillot ikke kopiering. Kopier lenken under."));
        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == "Lenke til variabelen");
        AssertTheLinkOpensThisVariable(cut.Find($"#{label.GetAttribute("for")}").GetAttribute("value")!);
    }

    [Fact]
    public void WholeVariable_SignedOut_ThenItOffersTheSameShareWithTheSameLinkAndMail()
    {
        var (cut, _) = RenderExplorer();
        OpenRow(cut);
        var rowMail = ShareGroup(cut.Find(".munin-explorer-detail")).QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href");

        cut.FindAll(".munin-explorer-detail button").Single(b => b.TextContent == "Vis hele variabelen").Click();
        var whole = cut.WaitForElement("[id^='munin-variable-'][role=region]");

        var group = ShareGroup(whole);
        Assert.Single(group.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Kopier lenke");
        var mail = Uri.UnescapeDataString(group.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href")!);
        Assert.Equal(Uri.UnescapeDataString(rowMail!), mail);
        Assert.StartsWith($"mailto:?subject=Variabel: {Name}&body=", mail);
        AssertTheLinkOpensThisVariable(mail[(mail.IndexOf("http", StringComparison.Ordinal))..]);
    }

    [Fact]
    public async Task TheSharedLink_WhenOpened_ThenItShowsThatOneVariableWithItsRowOpen()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        copied.SetVoidResult();
        var (first, _) = RenderExplorer();
        OpenRow(first);
        ShareGroup(first.Find(".munin-explorer-detail")).QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();
        first.WaitForAssertion(() => Assert.Single(copied.Invocations));
        var link = (string)copied.Invocations.Single().Arguments.Single()!;
        await Renderer.DisposeComponents();

        Services.GetRequiredService<NavigationManager>().NavigateTo(link);
        var cut = Render<VariableExplorer>();
        var client = (ShareClient)Services.GetRequiredService<IMuninExplorerClient>();

        cut.WaitForAssertion(() =>
        {
            var (search, filter) = client.Searches[^1];
            Assert.Equal(Code, search);
            Assert.Equal([KildeId], filter!.KildeIds);
            Assert.Contains("1 variabel funnet", cut.Markup);
            Assert.Equal("true", cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").GetAttribute("aria-expanded"));
            Assert.NotNull(cut.Find(".munin-explorer-detail"));
        });
    }

    [Fact]
    public void Search_WhenTheHostDeclinesTheSearchKey_ThenThereIsNoShareGroup()
    {
        var client = new ShareClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/variabler?search=kjonn");
        var cut = Render<VariableExplorer>(b => b.Add(c => c.DeclinedKeys, ["search"]));

        cut.WaitForElement("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();
        var panel = cut.WaitForElement(".munin-explorer-detail");

        Assert.DoesNotContain("Del variabel", panel.TextContent);
    }
}
