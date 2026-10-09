using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>«Last ned utvalg»: the whole search result as helsedata.no's file, signed in or not. (Fhi.Metadata-idusm)</summary>
public class DownloadSelectionTest : ExplorerTestContext
{
    private static VariableSummary Variable(string name) =>
        new() { Id = Guid.NewGuid(), Code = name.ToUpperInvariant(), PreferredTerm = name, KildeName = "Abortregisteret" };

    private sealed class Client(int totalCount) : EmptyMuninExplorerClient
    {
        public readonly List<(string? Search, VariableFilter? Filter, ExportFormat Format, bool IncludeKodeverk)> Exports = [];
        public Exception? Refuse { get; init; }

        /// <summary>Searches after the first wait on this while it is set, so a test can press mid-fetch.</summary>
        public TaskCompletionSource? HoldSearches { get; init; }

        private int _searches;

        public override async Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            if (_searches++ > 0 && HoldSearches is { } hold)
            {
                await hold.Task;
            }

            return new Page<VariableSummary>
            {
                Items = totalCount == 0 ? [] : [Variable("Alder")],
                TotalCount = totalCount,
                PageNumber = 1,
                Size = 25,
                TotalPages = Math.Max(1, (totalCount + 24) / 25)
            };
        }

        public override Task<ExportedList> ExportVariablesAsync(
            string? search, VariableFilter? filter, ExportFormat format = ExportFormat.Xlsx,
            bool includeKodeverk = false, CancellationToken cancellationToken = default)
        {
            Exports.Add((search, filter, format, includeKodeverk));
            return Refuse is { } refusal
                ? Task.FromException<ExportedList>(refusal)
                : Task.FromResult(new ExportedList([1, 2, 3], "application/zip", "Variabler_2026-10-09.zip"));
        }
    }

    /// <summary>bUnit refuses <c>Setup&lt;IJSObjectReference&gt;</c>; the blob and the anchor are not modules.</summary>
    private sealed class ObjectHandler : JSRuntimeInvocationHandler<IJSObjectReference>
    {
        public ObjectHandler(string identifier, string method, IJSObjectReference result)
            : base(i => i.Identifier == identifier && i.InvocationMethodName == method, isCatchAllHandler: false) =>
            SetResult(result);
    }

    private IRenderedComponent<VariableSearch> Render(Client client, bool signedIn = false)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        JSInterop.AddInvocationHandler(new ObjectHandler("Blob", "InvokeConstructorAsync", new RecordingJsObject()));
        JSInterop.Setup<string>("URL.createObjectURL", _ => true).SetResult("blob:test");
        JSInterop.AddInvocationHandler(new ObjectHandler("document.createElement", "InvokeAsync", new RecordingJsObject()));

        return Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IElement? Toggle(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Last ned utvalg");

    private static IElement Panel(IRenderedComponent<VariableSearch> cut) =>
        cut.Find($"#{Toggle(cut)!.GetAttribute("aria-controls")}");

    private static IElement Button(IRenderedComponent<VariableSearch> cut, string text) =>
        Panel(cut).QuerySelectorAll("button").Single(b => b.TextContent.Trim() == text);

    private static string Alert(IRenderedComponent<VariableSearch> cut) =>
        Panel(cut).ParentElement!.QuerySelector("[role=alert]")!.TextContent.Trim();

    private static void Search(IRenderedComponent<VariableSearch> cut, string term)
    {
        cut.Find(".searchbox__freetext").Change(term);
        cut.Find("form").Submit();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Download_IsOfferedSignedInOrNot(bool signedIn)
    {
        var cut = Render(new Client(80), signedIn);

        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Assert.Equal("false", Toggle(cut)!.GetAttribute("aria-expanded"));
        Assert.True(Panel(cut).HasAttribute("hidden"));
    }

    [Fact]
    public void Download_WithNoResults_IsNotOffered()
    {
        var cut = Render(new Client(0));

        cut.WaitForAssertion(() => Assert.NotEqual("", cut.Find(".munin-explorer-results__toolbar .caption").TextContent.Trim()));
        Assert.Null(Toggle(cut));
    }

    [Fact]
    public void Download_ExcelWithCodebooks_ExportsTheExecutedSearchAndOffersTheFile()
    {
        var client = new Client(80);
        var cut = Render(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Assert.False(Panel(cut).HasAttribute("hidden"));
        Panel(cut).QuerySelector("input[type=checkbox]")!.Change(true);
        Button(cut, "Last ned som Excel").Click();

        cut.WaitForAssertion(() => Assert.Single(client.Exports));
        var export = client.Exports[0];
        Assert.Equal(ExportFormat.Xlsx, export.Format);
        Assert.True(export.IncludeKodeverk);
        Assert.NotNull(export.Filter);
        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("URL.createObjectURL"));
        Assert.Equal("", Alert(cut));
    }

    [Fact]
    public void Download_Csv_AsksForCsvWithoutCodebooksByDefault()
    {
        var client = new Client(80);
        var cut = Render(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Button(cut, "Last ned som CSV").Click();

        cut.WaitForAssertion(() => Assert.Single(client.Exports));
        Assert.Equal(ExportFormat.Csv, client.Exports[0].Format);
        Assert.False(client.Exports[0].IncludeKodeverk);
    }

    [Theory]
    [InlineData(VariableSearch.WarnDownloadFrom + 1, true)]
    [InlineData(VariableSearch.WarnDownloadFrom, false)]
    public void Download_WarnsOnlyPastTheThreshold(int hits, bool warns)
    {
        var cut = Render(new Client(hits));
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();

        Assert.Equal(warns, Panel(cut).TextContent.Contains($"Utvalget har {hits} treff", StringComparison.Ordinal));
        var describedBy = Button(cut, "Last ned som Excel").GetAttribute("aria-describedby");
        Assert.Equal(warns, describedBy is not null && cut.Find($"#{describedBy}").TextContent.Contains($"{hits} treff", StringComparison.Ordinal));
    }

    [Fact]
    public void Download_WhileASearchIsBeingFetched_DoesNothing()
    {
        var client = new Client(80) { HoldSearches = new TaskCompletionSource() };
        var cut = Render(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Toggle(cut)!.Click();

        Search(cut, "høyde");
        Assert.Equal("true", Button(cut, "Last ned som Excel").GetAttribute("aria-disabled"));
        Button(cut, "Last ned som Excel").Click();

        Assert.Empty(client.Exports);
        client.HoldSearches!.SetResult();
        cut.WaitForAssertion(() => Assert.Null(Button(cut, "Last ned som Excel").GetAttribute("aria-disabled")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Download_AFailure_IsNotShownUnderTheNextSearch(bool newText)
    {
        var client = new Client(80) { Refuse = new HttpRequestException("503") };
        var cut = Render(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Toggle(cut)!.Click();
        Button(cut, "Last ned som Excel").Click();
        cut.WaitForAssertion(() => Assert.NotEqual("", Alert(cut)));

        if (newText)
        {
            Search(cut, "høyde");
        }
        else
        {
            cut.FindAll("input[type=checkbox]").Single(i => i.ParentElement!.TextContent.Contains("Vis historiske", StringComparison.Ordinal)).Change(true);
        }

        cut.WaitForAssertion(() => Assert.Equal("", Alert(cut)));
    }

    [Fact]
    public void Download_WhenTheApiFails_SaysSo()
    {
        var client = new Client(80) { Refuse = new HttpRequestException("503") };
        var cut = Render(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Button(cut, "Last ned som Excel").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke laste ned nå. Prøv igjen om litt.", Alert(cut)));
    }

    [Fact]
    public void Download_WhenRateLimited_SaysWhy()
    {
        var client = new Client(80) { Refuse = new MuninExplorerRateLimitedException() };
        var cut = Render(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Button(cut, "Last ned som Excel").Click();

        cut.WaitForAssertion(() => Assert.NotEqual("Kunne ikke laste ned nå. Prøv igjen om litt.", Alert(cut)));
        Assert.NotEqual("", Alert(cut));
    }

    [Fact]
    public void Download_EscapeClosesThePanelAndReturnsFocusToTheButton()
    {
        var cut = Render(new Client(80));
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Panel(cut).ParentElement!.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.True(Panel(cut).HasAttribute("hidden"));
        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("Blazor._internal.domWrapper.focus"));
    }
}
