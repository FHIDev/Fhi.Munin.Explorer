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
    private static readonly Guid OtherDatasamlingId = Guid.Parse("12121212-3434-5656-7878-909090909090");
    private const string Code = "V_HKR.KJONN";
    private const string Name = "Kjønn";

    private sealed class ShareClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public List<(string? Search, VariableFilter? Filter)> Searches { get; } = [];

        /// <summary>The version status of the one row and of its record.</summary>
        public string VersionStatus { get; init; } = "Active";

        /// <summary>The datasamling the record answers with, which can differ from the row's.</summary>
        public Guid? DetailDatasamlingId { get; init; } = DatasamlingId;

        /// <summary>The variable's datasamlinger as its record lists them.</summary>
        public IReadOnlyList<Guid> AllDatasamlinger { get; init; } = [DatasamlingId, OtherDatasamlingId];

        /// <summary>Whether the search finds the variable's row in its other datasamling as well.</summary>
        public bool FindsTheOtherRow { get; init; }

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
                        Id = VariableId,
                        Code = Code,
                        PreferredTerm = Name,
                        KildeId = KildeId,
                        KildeName = "Hjerte- og karregisteret",
                        DatasamlingId = DatasamlingId,
                        DatasamlingName = "Hjerteinfarkt",
                        VersionStatus = VersionStatus,
                    },
                    .. FindsTheOtherRow
                        ? [new VariableSummary
                        {
                            Id = VariableId,
                            Code = Code,
                            PreferredTerm = Name,
                            KildeId = KildeId,
                            DatasamlingId = OtherDatasamlingId,
                            DatasamlingName = "Hjertesvikt",
                            VersionStatus = VersionStatus,
                        }]
                        : Array.Empty<VariableSummary>(),
                ],
                TotalCount = FindsTheOtherRow ? 2 : 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Detail());

        public List<string> CodesAsked { get; } = [];

        /// <summary>Whether this Munin has the by-code route; without it every code answers null.</summary>
        public bool AnswersCodes { get; init; } = true;

        /// <summary>Holds every answer to a code until the test releases it.</summary>
        public TaskCompletionSource? CodeGate { get; set; }

        // Munin's by-code route: the code in any case finds the one variable (Fhi.Metadata-9huq2.27).
        public async Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            CodesAsked.Add(code);
            if (CodeGate is { } gate)
            {
                await gate.Task;
            }

            return AnswersCodes && string.Equals(code, Code, StringComparison.OrdinalIgnoreCase) ? Detail() : null;
        }

        private VariableDetail Detail() =>
            new()
            {
                Id = VariableId,
                Code = Code,
                PreferredTerm = Name,
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                KildeShortName = "HKR",
                DatasamlingId = DetailDatasamlingId,
                DatasamlingName = "Hjerteinfarkt",
                VersionStatus = VersionStatus,
                AllDatasamlinger = [.. AllDatasamlinger.Select(id => new DatasamlingReference { Id = id })],
            };
    }

    private IRenderedComponent<VariableExplorer> RenderExplorer(
        ShareClient? client = null, string address = "http://localhost/variabler?search=kjonn", string[]? declined = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client ?? new ShareClient());
        Services.AddSingleton<IJSRuntime>(_history = new HistoryRecorder(JSInterop.JSRuntime));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<VariableExplorer>(b => b.Add(c => c.DeclinedKeys, declined));
    }

    private static void OpenRow(IRenderedComponent<VariableExplorer> cut)
    {
        cut.WaitForElement("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();
        cut.WaitForElement(".munin-explorer-detail");
    }

    private static IElementLike ShareGroup(AngleSharp.Dom.IElement within) =>
        new(within.QuerySelectorAll("[role=group]").Single(g =>
            within.QuerySelector($"#{g.GetAttribute("aria-labelledby")}")?.TextContent.Trim() == "Del variabel"));

    private sealed record IElementLike(AngleSharp.Dom.IElement Group)
    {
        public AngleSharp.Dom.IElement Copy => Group.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke");

        public AngleSharp.Dom.IElement Mail => Group.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post");

        public string Status => Group.QuerySelector("[role=status]")!.TextContent.Trim();
    }

    /// <summary>
    /// The variable by its code, the same in every environment (Fhi.Metadata-l9l2n.182): nothing else, unless the row is
    /// another of the variable's datasamlinger than its primary one, which is then named by id.
    /// </summary>
    private static void AssertTheLinkOpensThisVariable(string link, Guid? datasamling = null)
    {
        var uri = new Uri(link);
        Assert.True(uri.IsAbsoluteUri);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal(Code, query["variabel"].ToString());
        var expected = datasamling is { } ds ? new[] { "variabel", "datasamlingId" } : ["variabel"];
        Assert.Equal(expected.Order(), query.Keys.Order());
        if (datasamling is { } named)
        {
            Assert.Equal(named.ToString(), query["datasamlingId"].ToString());
        }
    }

    private static string MailLink(IElementLike share)
    {
        var mail = Uri.UnescapeDataString(share.Mail.GetAttribute("href")!);
        return mail[mail.IndexOf("http", StringComparison.Ordinal)..];
    }

    [Fact]
    public void OpenRow_WhenSignedOut_ThenItOffersDelVariabelWithItsAccessibleNames()
    {
        var cut = RenderExplorer();

        OpenRow(cut);

        var share = ShareGroup(cut.Find(".munin-explorer-detail"));
        Assert.Equal("Kopier lenke", share.Copy.TextContent.Trim());
        Assert.StartsWith("Åpner e-postprogrammet ditt.", cut.Find($"#{share.Mail.GetAttribute("aria-describedby")}").TextContent.Trim());
        Assert.Single(share.Group.QuerySelectorAll("[role=status][aria-live=polite]"));
        cut.WaitForAssertion(() => AssertTheLinkOpensThisVariable(MailLink(ShareGroup(cut.Find(".munin-explorer-detail")))));
    }

    [Fact]
    public void CopyLink_WhenPressedOnAnOpenRow_ThenTheClipboardGetsTheLinkAndTheGroupSaysSo()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        copied.SetVoidResult();
        var cut = RenderExplorer();
        OpenRow(cut);

        ShareGroup(cut.Find(".munin-explorer-detail")).Copy.Click();

        cut.WaitForAssertion(() => AssertTheLinkOpensThisVariable((string)copied.Invocations.Single().Arguments.Single()!));
        cut.WaitForAssertion(() => Assert.Equal("Lenken er kopiert.", ShareGroup(cut.Find(".munin-explorer-detail")).Status));
    }

    [Fact]
    public void CopyLink_WhenTheBrowserRefuses_ThenTheLinkIsShownToCopyByHand()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("NotAllowedError"));
        var cut = RenderExplorer();
        OpenRow(cut);

        ShareGroup(cut.Find(".munin-explorer-detail")).Copy.Click();

        cut.WaitForAssertion(() => Assert.Equal("Nettleseren tillot ikke kopiering. Kopier lenken under.",
            ShareGroup(cut.Find(".munin-explorer-detail")).Status));
        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == "Lenke til variabelen");
        AssertTheLinkOpensThisVariable(cut.Find($"#{label.GetAttribute("for")}").GetAttribute("value")!);
    }

    [Fact]
    public void ShowWholeVariable_WhenSignedOut_ThenItOffersTheSameShareWithTheSameLinkAndMail()
    {
        var cut = RenderExplorer();
        OpenRow(cut);
        var rowMail = ShareGroup(cut.Find(".munin-explorer-detail")).Mail.GetAttribute("href");

        cut.FindAll(".munin-explorer-detail button").Single(b => b.TextContent == "Vis hele variabelen").Click();
        var share = ShareGroup(cut.WaitForElement("[id^='munin-variable-'][role=region]"));

        Assert.Equal("Kopier lenke", share.Copy.TextContent.Trim());
        var mail = Uri.UnescapeDataString(share.Mail.GetAttribute("href")!);
        Assert.Equal(Uri.UnescapeDataString(rowMail!), mail);
        Assert.StartsWith($"mailto:?subject=Variabel: {Name}&body=", mail);
        AssertTheLinkOpensThisVariable(MailLink(share));
    }

    [Fact]
    public void ShowWholeVariable_WhenTheRowIsNotThePrimaryDatasamling_ThenTheLinkNamesTheRowsDatasamling()
    {
        // The record answers with another datasamling as the primary one, so the row's own must travel by id.
        var cut = RenderExplorer(new ShareClient { DetailDatasamlingId = OtherDatasamlingId });
        OpenRow(cut);

        cut.FindAll(".munin-explorer-detail button").Single(b => b.TextContent == "Vis hele variabelen").Click();
        cut.WaitForAssertion(() => AssertTheLinkOpensThisVariable(
            MailLink(ShareGroup(cut.Find("[id^='munin-variable-'][role=region]"))), DatasamlingId));
    }

    [Fact]
    public void OpenRow_WhenTheVariableIsHistorical_ThenTheLinkStillNamesItByCodeAlone()
    {
        // Whether to show historical variables is the arrival's to work out from the variable, not the link's to carry.
        var cut = RenderExplorer(new ShareClient { VersionStatus = "Historical" }, "http://localhost/variabler?search=kjonn&includeHistorical=true");

        OpenRow(cut);

        cut.WaitForAssertion(() => AssertTheLinkOpensThisVariable(MailLink(ShareGroup(cut.Find(".munin-explorer-detail")))));
    }

    [Fact]
    public async Task NavigateTo_WhenGivenTheCopiedLink_ThenTheSearchIsForThatOneVariableWithItsRowOpen()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        copied.SetVoidResult();
        var client = new ShareClient();
        var first = RenderExplorer(client);
        OpenRow(first);
        ShareGroup(first.Find(".munin-explorer-detail")).Copy.Click();
        first.WaitForAssertion(() => Assert.Single(copied.Invocations));
        var link = (string)copied.Invocations.Single().Arguments.Single()!;
        await Renderer.DisposeComponents();

        Services.GetRequiredService<NavigationManager>().NavigateTo(link);
        var cut = Render<VariableExplorer>();

        cut.WaitForAssertion(() =>
        {
            var (search, filter) = client.Searches[^1];
            Assert.Equal(Code, search);
            Assert.Empty(filter!.KildeIds);
            Assert.Equal(Code, client.CodesAsked[^1]);
            Assert.Contains("1 variabel funnet", cut.Markup);
            Assert.Equal("true", cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").GetAttribute("aria-expanded"));
            Assert.NotNull(cut.Find(".munin-explorer-detail"));
        });
    }

    // Fhi.Metadata-l9l2n.182: a variable linked by its code, the same link in every environment.
    private HistoryRecorder? _history;

    // What the explorer last wrote to the address bar.
    private string Address(IRenderedComponent<VariableExplorer> _) => _history!.Written.LastOrDefault() ?? "";

    private static string Note(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("p.infobox.infobox--bg-yellow[role=status]").SingleOrDefault()?.TextContent.Trim() ?? "";

    [Theory]
    [InlineData("V_HKR.KJONN")]
    [InlineData("v_hkr.kjonn")]
    public void Arrive_WhenTheAddressNamesAVariableByCode_ThenItOpensThatVariableAndTheAddressKeepsTheCode(string code)
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={code}");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal([code], client.CodesAsked);
            Assert.Equal((Code, false), (client.Searches[^1].Search, client.Searches[^1].Filter!.IncludeHistorical));
            Assert.Equal("true", cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").GetAttribute("aria-expanded"));
            Assert.NotNull(cut.Find(".munin-explorer-detail"));

            // The primary row: the code alone, as Munin spells it, and no search, since the arrival makes that again.
            Assert.EndsWith($"/variabler?variabel={Code}", Address(cut));
        });
        Assert.Equal("", Note(cut));
    }

    [Fact]
    public void Arrive_WhenTheCodeNamesNoVariable_ThenTheExplorerSearchesForTheCode()
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client, "http://localhost/variabler?variabel=V_HKR.FINNES_IKKE");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("V_HKR.FINNES_IKKE", client.Searches[^1].Search);
            Assert.Empty(cut.FindAll(".munin-explorer-detail"));
            Assert.EndsWith("/variabler?search=V_HKR.FINNES_IKKE", Address(cut));
        });
    }

    [Fact]
    public void Arrive_WhenTheVariableIsHistorical_ThenItsCodeLinkShowsHistoricalVariables()
    {
        var client = new ShareClient { VersionStatus = "Historical" };
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}");

        cut.WaitForAssertion(() => Assert.True(client.Searches[^1].Filter!.IncludeHistorical));
    }

    [Fact]
    public void Arrive_WhenTheLinksDatasamlingIsNotTheVariables_ThenThePrimaryRowOpensWithANote()
    {
        // A datasamling id from another environment: the variable has two datasamlinger, so the reader is told.
        var client = new ShareClient();
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}&datasamlingId={Guid.NewGuid()}");

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find(".munin-explorer-detail"));
            Assert.Equal("Fant ikke datasamlingen lenken viste til. Variabelen vises i sin hoveddatasamling.", Note(cut));
            Assert.EndsWith($"/variabler?variabel={Code}", Address(cut));
        });
    }

    [Fact]
    public void Arrive_WhenTheVariableHasOneDatasamling_ThenAMissingDatasamlingOpensItQuietly()
    {
        // With one datasamling, a missing id can only have meant that one.
        var client = new ShareClient { AllDatasamlinger = [DatasamlingId] };
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}&datasamlingId={Guid.NewGuid()}");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".munin-explorer-detail")));
        Assert.Equal("", Note(cut));
    }

    [Fact]
    public void Navigate_WhenBackOrForwardReachesACodeLink_ThenTheCodeIsResolvedAgain()
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client);
        cut.WaitForElement("ul.munin-explorer-data-list");

        Services.GetRequiredService<NavigationManager>().NavigateTo($"http://localhost/variabler?variabel={Code}");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal([Code], client.CodesAsked);
            Assert.NotNull(cut.Find(".munin-explorer-detail"));
        });
    }

    private int Pushes => JSInterop.Invocations.Count(i => i.Identifier == "history.pushState");

    private int Backs => JSInterop.Invocations.Count(i => i.Identifier == "history.back");

    [Fact]
    public void Arrive_WhenTheCodeIsAnsweredLate_ThenTheRowIsALinksAndClosingItIsNotBack()
    {
        // A pushed entry, or a close that is Back, would take a reader who followed the link off the site.
        var client = new ShareClient { CodeGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}");
        cut.WaitForAssertion(() => Assert.Equal([Code], client.CodesAsked));

        client.CodeGate.SetResult();
        cut.WaitForElement(".munin-explorer-detail");
        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".munin-explorer-detail")));
        Assert.Equal((0, 0), (Pushes, Backs));
    }

    [Fact]
    public void Arrive_WhenTheReaderNavigatesWhileTheCodeIsLookedUp_ThenTheLateAnswerChangesNothing()
    {
        var client = new ShareClient { CodeGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}");
        cut.WaitForAssertion(() => Assert.Equal([Code], client.CodesAsked));

        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/variabler?search=foo");
        cut.WaitForAssertion(() => Assert.Equal("foo", client.Searches[^1].Search));
        client.CodeGate.SetResult();

        // The browser already shows the arrival, so nothing is written: least of all the variable the answer named.
        cut.WaitForAssertion(() => Assert.Equal("foo", client.Searches[^1].Search));
        Assert.DoesNotContain(_history!.Written, url => url?.Contains("variabel", StringComparison.Ordinal) is true);
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
    }

    [Theory]
    [InlineData("", "http://localhost/variabler")]
    [InlineData("search=kjonn&", "http://localhost/variabler?search=kjonn")]
    public void Arrive_WhenTheReaderLeavesForTheViewWithoutTheVariableWhileItIsLookedUp_ThenTheLateAnswerChangesNothing(
        string own, string left)
    {
        // Without the variable, a code arrival still being looked up reads as that view: the leaving is still a leaving.
        var client = new ShareClient { CodeGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = RenderExplorer(client, $"http://localhost/variabler?{own}variabel={Code}");
        cut.WaitForAssertion(() => Assert.Equal([Code], client.CodesAsked));

        Services.GetRequiredService<NavigationManager>().NavigateTo(left);
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));
        client.CodeGate.SetResult();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("ul.munin-explorer-data-list")));
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.DoesNotContain(_history!.Written, url => url?.Contains("variabel", StringComparison.Ordinal) is true);
    }

    [Fact]
    public void Arrive_WhenTheHostDeclinesVariabelId_ThenACodeInTheAddressIsNeitherReadNorRewritten()
    {
        // Declining either variable key leaves the open variable out of the address, so the code is not read in either.
        var client = new ShareClient();
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}", declined: ["variabelId"]);

        cut.WaitForElement("ul.munin-explorer-data-list");
        Assert.Empty(client.CodesAsked);
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.DoesNotContain(_history!.Written, url => url is not null && !url.Contains($"variabel={Code}", StringComparison.Ordinal));
    }

    [Fact]
    public void Arrive_WhenTheLinkNamesAnotherOfTheVariablesDatasamlinger_ThenThatRowOpensWithoutANote()
    {
        // The link the explorer writes for a row that is not the primary one, read back.
        var client = new ShareClient { FindsTheOtherRow = true };
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel={Code}&datasamlingId={OtherDatasamlingId}");

        cut.WaitForAssertion(() => Assert.EndsWith($"/variabler?variabel={Code}&datasamlingId={OtherDatasamlingId}", Address(cut)));
        Assert.Equal("", Note(cut));
    }

    [Fact]
    public void Arrive_WhenACodeWithFiltersNamesNoVariable_ThenTheSearchIsForTheCode()
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabel=V_HKR.FINNES_IKKE&kildeIds={KildeId}");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("V_HKR.FINNES_IKKE", client.Searches[^1].Search);
            Assert.Equal([KildeId], client.Searches[^1].Filter!.KildeIds);
            Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        });
    }

    [Fact]
    public void Arrive_WhenTheAddressCarriesTheReadersOwnSearchBesideTheCode_ThenTheSearchIsKept()
    {
        // The address the explorer writes for a row opened under the reader's search: a reload must give it back.
        var client = new ShareClient();
        var cut = RenderExplorer(client, $"http://localhost/variabler?search=kjonn&variabel={Code}");

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find(".munin-explorer-detail"));
            Assert.Equal("kjonn", client.Searches[^1].Search);
            Assert.EndsWith($"/variabler?search=kjonn&variabel={Code}", Address(cut));
        });
    }

    [Fact]
    public void Learn_WhenTheCodeIsAnsweredAfterTheFirstWrite_ThenTheAddressKeepsItsSection()
    {
        var client = new ShareClient { CodeGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = RenderExplorer(client, $"http://localhost/variabler?variabelId={VariableId}&datasamlingId={DatasamlingId}#sec");
        cut.WaitForAssertion(() => Assert.EndsWith("#sec", Address(cut)));

        client.CodeGate.SetResult();

        cut.WaitForAssertion(() => Assert.EndsWith($"/variabler?variabel={Code}#sec", Address(cut)));
    }

    [Theory]
    [InlineData("variabel=v_hkr.kjonn")]
    [InlineData("search=V_HKR.KJONN&variabelId=7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c&datasamlingId=66666666-7777-8888-9999-000000000000")]
    public void Navigate_WhenTheHostNavigatesToTheViewOnScreenSpelledAnotherWay_ThenNothingRemounts(string spelling)
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client, "http://localhost/variabler?variabel=v_hkr.kjonn");
        cut.WaitForAssertion(() => Assert.EndsWith($"/variabler?variabel={Code}", Address(cut)));
        var (searches, codes) = (client.Searches.Count, client.CodesAsked.Count);

        Services.GetRequiredService<NavigationManager>().NavigateTo($"http://localhost/variabler?{spelling}");

        Assert.Equal((searches, codes), (client.Searches.Count, client.CodesAsked.Count));
        Assert.NotNull(cut.Find(".munin-explorer-detail"));
    }

    [Fact]
    public void OpenRow_WhenMuninHasNoCodeRoute_ThenTheLinkAndTheAddressKeepTheId()
    {
        // A host on the contract's default body, or a Munin before 9huq2.27: a code link would open no variable there.
        var client = new ShareClient { AnswersCodes = false };
        var cut = RenderExplorer(client);

        OpenRow(cut);

        cut.WaitForAssertion(() => Assert.Contains(Code, client.CodesAsked));
        var query = QueryHelpers.ParseQuery(new Uri(MailLink(ShareGroup(cut.Find(".munin-explorer-detail")))).Query);
        Assert.Equal((Code, VariableId.ToString(), KildeId.ToString()), (query["search"].ToString(), query["variabelId"].ToString(), query["kildeIds"].ToString()));
        Assert.False(query.ContainsKey("variabel"));
        Assert.Contains($"variabelId={VariableId}", Address(cut));
    }

    [Fact]
    public void OpenRow_WhenTheHostDeclinesVariabel_ThenTheSharedLinkAndTheAddressNameTheVariableById()
    {
        // The host that means something else by ?variabel= keeps the id links the explorer wrote before codes.
        var client = new ShareClient();
        var cut = RenderExplorer(client, declined: ["variabel"]);

        OpenRow(cut);

        var query = QueryHelpers.ParseQuery(new Uri(MailLink(ShareGroup(cut.Find(".munin-explorer-detail")))).Query);
        Assert.Equal((Code, VariableId.ToString()), (query["search"].ToString(), query["variabelId"].ToString()));
        Assert.False(query.ContainsKey("variabel"));
        Assert.Contains($"variabelId={VariableId}", Address(cut));
        Assert.Empty(client.CodesAsked);
    }

    [Fact]
    public void Arrive_WhenTheHostDeclinesVariabel_ThenAnIdLinkStillOpensItsVariable()
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client,
            $"http://localhost/variabler?search={Code}&variabelId={VariableId}&datasamlingId={DatasamlingId}", declined: ["variabel"]);

        cut.WaitForElement(".munin-explorer-detail");
        Assert.Empty(client.CodesAsked);
        Assert.Contains($"variabelId={VariableId}", Address(cut));
    }

    [Fact]
    public void Arrive_WhenTheHostDeclinesVariabel_ThenItsOwnValueIsCarriedAndNothingIsLookedUp()
    {
        var client = new ShareClient();
        var cut = RenderExplorer(client, "http://localhost/variabler?variabel=vertens-egen&search=kjonn", declined: ["variabel"]);

        cut.WaitForElement("ul.munin-explorer-data-list");
        Assert.Empty(client.CodesAsked);
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.NotEmpty(_history!.Written);
        Assert.All(_history.Written, url => Assert.Contains("variabel=vertens-egen", url, StringComparison.Ordinal));
    }

    [Fact]
    public void OpenRow_WhenTheHostDeclinesVariabelId_ThenNeitherTheRowNorTheWholeVariableOffersSharing()
    {
        var cut = RenderExplorer(declined: ["variabelId"]);

        OpenRow(cut);
        Assert.DoesNotContain("Del variabel", cut.Find(".munin-explorer-detail").TextContent);

        cut.FindAll(".munin-explorer-detail button").Single(b => b.TextContent == "Vis hele variabelen").Click();
        Assert.DoesNotContain("Del variabel", cut.WaitForElement("[id^='munin-variable-'][role=region]").TextContent);
    }
}

/// <summary>The share group on its own: what it says belongs to the link it was asked about.</summary>
public class VariableShareTest : ExplorerTestContext
{
    [Fact]
    public void CopyLink_WhenTheClipboardAnswersAfterTheLinkChanged_ThenNothingIsSaid()
    {
        var copy = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        var cut = Render<VariableShare>(p => p
            .Add(c => c.Link, "https://helsedata.example/variabler?variabelId=1")
            .Add(c => c.Name, "Kjønn")
            .Add(c => c.HeadingId, "share-heading")
            .Add(c => c.FieldId, "share-field"));

        cut.Find("button").Click();
        cut.Render(p => p.Add(c => c.Link, "https://helsedata.example/variabler?variabelId=2"));
        copy.SetVoidResult();

        cut.WaitForAssertion(() => Assert.Single(copy.Invocations));
        Assert.Equal("", cut.Find("[role=status]").TextContent.Trim());
    }

    [Fact]
    public void Render_Always_ThenTheGroupIsNamedByItsHeadingAndTheMailLinkByItsHint()
    {
        var cut = Render<VariableShare>(p => p
            .Add(c => c.Link, "https://helsedata.example/variabler?variabelId=1")
            .Add(c => c.Name, "Kjønn")
            .Add(c => c.HeadingId, "share-heading")
            .Add(c => c.FieldId, "share-field"));

        Assert.Equal("share-heading", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
        Assert.Equal("Del variabel", cut.Find("#share-heading").TextContent.Trim());
        Assert.Equal("share-heading-mail-hint", cut.Find("a").GetAttribute("aria-describedby"));
        Assert.StartsWith("mailto:?subject=Variabel%3A%20Kj%C3%B8nn&body=", cut.Find("a").GetAttribute("href"));
    }
}
