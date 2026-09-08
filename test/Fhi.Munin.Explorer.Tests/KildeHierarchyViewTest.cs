using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public sealed class KildeHierarchyViewTest : BunitContext
{
    private sealed class Client : EmptyMuninExplorerClient
    {
        internal Func<Guid, CancellationToken, Task<KildeHierarchy?>> Fetch { get; set; } =
            (id, _) => Task.FromResult<KildeHierarchy?>(new() { KildeId = id });
        internal List<(Guid Id, CancellationToken Token)> Calls { get; } = [];

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Calls.Add((id, cancellationToken));
            return Fetch(id, cancellationToken);
        }

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(new()
            {
                Id = id,
                PreferredTerm = "Register",
                Datasamlinger =
                [new() { Id = Guid.NewGuid(), Name = "Collection", Description = "Retained description" }]
            });
    }

    private IRenderedComponent<KildeHierarchyView> Mount(Client client, Guid id, string language = "nb")
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        return Render<KildeHierarchyView>(p => p.Add(c => c.KildeId, id).Add(c => c.Language, language));
    }

    [Fact]
    public void Render_WhenAllNodeKindsOccur_ThenTheirAncestryAndCollapsedDisclosuresArePreserved()
    {
        var id = Guid.NewGuid();
        var cut = Mount(new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) }, id);

        Assert.Equal(7, cut.FindAll("li").Count);
        Assert.All(cut.FindAll("ul"), list => Assert.Equal("list", list.GetAttribute("role")));
        Assert.Equal(3, cut.FindAll("details").Count);
        Assert.All(cut.FindAll("details"), d => Assert.False(d.HasAttribute("open")));
        Assert.Empty(cut.FindAll("[role=tree], input, button"));
        Assert.Contains("Orphan", cut.Find("li > details > ul").TextContent);
        Assert.Contains("Nested group", cut.Find("li > details > ul > li > details > ul").TextContent);
        Assert.All(cut.FindAll("summary"), summary => Assert.False(string.IsNullOrWhiteSpace(AccessibleName.Of(summary))));
        Assert.All(cut.FindAll(".munin-explorer-hierarchy__leaf"), leaf =>
            Assert.Null(leaf.QuerySelector("summary")));
        Assert.All(cut.FindAll(".munin-explorer-hierarchy__count"), count =>
            Assert.Contains("variabler", count.TextContent));
    }

    [Fact]
    public void From_WhenSiblingTypesAreMixed_ThenCuratedOrderPrecedesDeterministicNorwegianNameOrder()
    {
        var hierarchy = new KildeHierarchy
        {
            Delkilder = [new() { Name = "Ås" }, new() { Name = "First", PresentationOrder = 1 }],
            DirectDatasamlinger = [new() { Name = "Zulu", PresentationOrder = 2 }, new() { Name = "Ægir" }, new() { Name = "Beta" }]
        };
        Assert.Equal(["First", "Zulu", "Beta", "Ægir", "Ås"], KildeHierarchyNode.From(hierarchy).Select(n => n.Name));
    }

    [Fact]
    public void From_WhenAGroupOccursUnderTwoOwners_ThenItsPositionsHaveDifferentKeys()
    {
        var group = new HierarchyVariabelgruppe { Id = Guid.NewGuid(), Name = "Shared" };
        var hierarchy = new KildeHierarchy
        {
            DirectDatasamlinger =
            [new() { Id = Guid.NewGuid(), Variabelgrupper = [group] }, new() { Id = Guid.NewGuid(), Variabelgrupper = [group] }]
        };
        var nodes = KildeHierarchyNode.From(hierarchy);
        Assert.NotEqual(nodes[0].Children[0].Key, nodes[1].Children[0].Key);
    }

    [Fact]
    public async Task Render_WhenTheSourceChangesDuringFetch_ThenLateResultsCannotReplaceTheNewSource()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var pending = new TaskCompletionSource<KildeHierarchy?>();
        var client = new Client { Fetch = (id, _) => id == first ? pending.Task : Task.FromResult<KildeHierarchy?>(Hierarchy(id)) };
        var cut = Mount(client, first);
        Assert.Contains("Laster", cut.Find("[role=status]").TextContent);

        cut.Render(p => p.Add(c => c.KildeId, second));
        Assert.True(client.Calls[0].Token.IsCancellationRequested);
        await cut.InvokeAsync(() => pending.SetResult(new() { KildeId = first }));
        Assert.Equal(7, cut.FindAll("li").Count);
        Assert.Equal("false", cut.Find(".munin-explorer-hierarchy").GetAttribute("aria-busy"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_WhenAnOldRequestFailsAfterSwitching_ThenTheCurrentHierarchyStillRenders(bool rateLimited)
    {
        var first = Guid.NewGuid();
        var pending = new TaskCompletionSource<KildeHierarchy?>();
        var client = new Client { Fetch = (id, _) => id == first ? pending.Task : Task.FromResult<KildeHierarchy?>(Hierarchy(id)) };
        var cut = Mount(client, first);
        cut.Render(p => p.Add(c => c.KildeId, Guid.NewGuid()));
        await cut.InvokeAsync(() => pending.SetException(rateLimited ? new MuninExplorerRateLimitedException() : new HttpRequestException("Late failure")));
        Assert.Equal(7, cut.FindAll("li").Count);
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Render_WhenLanguageChanges_ThenLabelsChangeWithoutRefetching()
    {
        var id = Guid.NewGuid();
        var client = new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) };
        var cut = Mount(client, id);
        cut.Render(p => p.Add(c => c.Language, "en"));
        Assert.Single(client.Calls);
        Assert.Contains("variables", cut.Find(".munin-explorer-hierarchy__count").TextContent);
        Assert.Equal("no", cut.Find("summary > span").GetAttribute("lang"));
    }

    [Theory]
    [InlineData("nb", "Ingen delkilder")]
    [InlineData("en", "No sub-sources")]
    public void Render_WhenHierarchyIsEmpty_ThenTheLocalizedEmptyStateHasNoDisclosures(string language, string expected)
    {
        var cut = Mount(new Client(), Guid.NewGuid(), language);
        Assert.Contains(expected, cut.Find("[role=status]").TextContent);
        Assert.Empty(cut.FindAll("details"));
    }

    [Fact]
    public async Task Retry_WhenFetchFailed_ThenASecondRequestCanRestoreTheHierarchy()
    {
        var client = new Client { Fetch = (_, _) => throw new HttpRequestException() };
        var cut = Mount(client, Guid.NewGuid());
        Assert.Contains("Kunne ikke laste", cut.Find("[role=status]").TextContent);
        Assert.Equal("Prøv å laste strukturen på nytt", AccessibleName.Of(cut.Find("button")));
        client.Fetch = (id, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id));
        await cut.Find("button").ClickAsync(new());
        Assert.Equal(2, client.Calls.Count);
        Assert.Equal(7, cut.FindAll("li").Count);
    }

    [Theory]
    [InlineData("nb")]
    [InlineData("en")]
    public async Task Retry_WhenItSucceeds_ThenTheControlRemainsAndCompletionIsAnnounced(string language)
    {
        var id = Guid.NewGuid();
        var client = new Client { Fetch = (_, _) => throw new HttpRequestException() };
        var cut = Mount(client, id, language);
        var button = cut.Find("button");
        var pending = new TaskCompletionSource<KildeHierarchy?>();
        client.Fetch = (_, _) => pending.Task;
        var retry = button.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal("true", button.GetAttribute("aria-disabled")));
        Assert.False(button.HasAttribute("disabled"));
        await button.ClickAsync(new());
        Assert.Equal(2, client.Calls.Count);
        await cut.InvokeAsync(() => pending.SetResult(Hierarchy(id)));
        await retry;
        Assert.Single(cut.FindAll("button"));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal(Texts.For(language).HierarchyLoaded, cut.Find("[role=status]").TextContent);
        await button.ClickAsync(new());
        Assert.Equal(2, client.Calls.Count);
    }

    [Theory]
    [InlineData("nb")]
    [InlineData("en")]
    public void Render_WhenRateLimited_ThenItExplainsThrottlingWithoutOfferingRetry(string language)
    {
        var client = new Client { Fetch = (_, _) => throw new MuninExplorerRateLimitedException() };
        var cut = Mount(client, Guid.NewGuid(), language);
        Assert.Equal(Texts.For(language).RateLimitError, cut.Find("[role=status]").TextContent);
        Assert.Empty(cut.FindAll("button"));
        cut.Render();
        Assert.Single(client.Calls);
        client.Fetch = (id, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id));
        cut.Render(p => p.Add(c => c.KildeId, Guid.NewGuid()));
        Assert.Equal(7, cut.FindAll("li").Count);
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public async Task Retry_WhenRateLimited_ThenTheFocusedControlRemainsButCannotRequestAgain()
    {
        var client = new Client { Fetch = (_, _) => throw new HttpRequestException() };
        var cut = Mount(client, Guid.NewGuid());
        var button = cut.Find("button");
        client.Fetch = (_, _) => throw new MuninExplorerRateLimitedException();
        await button.ClickAsync(new());
        Assert.Single(cut.FindAll("button"));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal(Texts.For("nb").RateLimitError, cut.Find("[role=status]").TextContent);
        await button.ClickAsync(new());
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public void Render_WhenHierarchyIsMissing_ThenItReportsFailureRatherThanAnEmptyCatalogue()
    {
        var cut = Mount(new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(null) }, Guid.NewGuid());
        Assert.Contains("Kunne ikke laste", cut.Find("[role=status]").TextContent);
        Assert.Single(cut.FindAll("button"));
    }

    [Fact]
    public async Task Dispose_WhenFetchIsPending_ThenItCancelsAndIgnoresLateCompletion()
    {
        var pending = new TaskCompletionSource<KildeHierarchy?>();
        var client = new Client { Fetch = (_, _) => pending.Task };
        var cut = Mount(client, Guid.NewGuid());
        await DisposeAsync();
        Assert.True(client.Calls[0].Token.IsCancellationRequested);
        pending.SetResult(new());
        await pending.Task;
    }

    [Fact]
    public void KildeSearch_WhenOpeningDetails_ThenHierarchyAndMetadataDisclosureAreBothAvailable()
    {
        var client = new Client { Fetch = (id, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) };
        Services.AddSingleton<IMuninExplorerClient>(client);
        var cut = Render<KildeSearch>(p => p.Add(c => c.SelectedKildeId, Guid.NewGuid()));
        Assert.Single(cut.FindAll(".munin-explorer-hierarchy"));
        var metadata = cut.Find("details.munin-explorer-hierarchy__metadata");
        Assert.False(metadata.HasAttribute("open"));
        Assert.Contains("Retained description", metadata.TextContent);
        Assert.Equal("Beskrivelser og gyldighetsperioder", AccessibleName.Of(metadata.QuerySelector("summary")!));
    }

    private static KildeHierarchy Hierarchy(Guid id) => new()
    {
        KildeId = id,
        Delkilder =
        [new()
        {
            Id = Guid.NewGuid(), Name = "Parent", VariableCount = 8,
            Children = [new() { Id = Guid.NewGuid(), Name = "Child" }],
            UnassignedVariabelgrupper = [new() { Id = Guid.NewGuid(), Name = "Orphan" }],
            Datasamlinger = [new()
            {
                Id = Guid.NewGuid(), Name = "Collection", VariableCount = 4,
                Variabelgrupper = [new()
                {
                    Id = Guid.NewGuid(), Name = "Group", VariableCount = 4,
                    ChildVariabelgrupper = [new() { Id = Guid.NewGuid(), Name = "Nested group" }]
                }]
            }]
        }],
        DirectDatasamlinger = [new() { Id = Guid.NewGuid(), Name = "Direct", VariableCount = 2 }]
    };
}
