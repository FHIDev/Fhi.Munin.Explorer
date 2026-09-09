using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
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
    public void From_WhenVariabelgrupperCarryCuratedOrder_ThenTheyFollowItAndTheUnorderedFallBackToName()
    {
        // The level Fhi.Metadata-l9l2n.61 fixed. The two above it honoured presentationOrder from
        // the day the tree was drawn; a variabelgruppe was left in name order because the contract
        // had nowhere to put the number, so this node built with a literal null instead.
        var hierarchy = new KildeHierarchy
        {
            DirectDatasamlinger =
            [new()
            {
                Id = Guid.NewGuid(),
                Variabelgrupper =
                [
                    new() { Id = Guid.NewGuid(), Name = "ALCOHOL", PresentationOrder = 646 },
                    new() { Id = Guid.NewGuid(), Name = "GENERAL INFORMATION", PresentationOrder = 537 },
                    new()
                    {
                        Id = Guid.NewGuid(), Name = "BLOOD SAMPLES", PresentationOrder = 572,
                        ChildVariabelgrupper =
                        [
                            new() { Id = Guid.NewGuid(), Name = "Serum", PresentationOrder = 647 },
                            new() { Id = Guid.NewGuid(), Name = "Åpen" },
                            new() { Id = Guid.NewGuid(), Name = "Plasma", PresentationOrder = 609 }
                        ]
                    }
                ]
            }]
        };

        var groups = KildeHierarchyNode.From(hierarchy)[0].Children;

        Assert.Equal(["GENERAL INFORMATION", "BLOOD SAMPLES", "ALCOHOL"], groups.Select(n => n.Name));

        // The fallback the delkilder and datasamlinger already had: unordered sorts after every
        // ordered sibling rather than before it, and among themselves by the catalogue's collation.
        Assert.Equal(["Plasma", "Serum", "Åpen"], groups[1].Children.Select(n => n.Name));
    }

    [Fact]
    public void From_WhenADelkildesUnassignedGroupsCarryOrder_ThenTheyStaySortedBehindItsRealStructure()
    {
        // The other place reading a group's presentationOrder. Its number counts a sequence of its
        // own — the capture's Tromsø4 numbers datasamlinger 1..2 and groups 537..1189 — so feeding
        // it to the shared sort would let an orphan outrank a datasamling it cannot be compared to.
        var hierarchy = new KildeHierarchy
        {
            Delkilder =
            [new()
            {
                Id = Guid.NewGuid(), Name = "Parent",
                Children = [new() { Id = Guid.NewGuid(), Name = "Child", PresentationOrder = 3 }],
                Datasamlinger = [new() { Id = Guid.NewGuid(), Name = "Collection", PresentationOrder = 2 }],
                UnassignedVariabelgrupper =
                [
                    new() { Id = Guid.NewGuid(), Name = "Åpen gruppe" },
                    new() { Id = Guid.NewGuid(), Name = "Late orphan", PresentationOrder = 537 },
                    new() { Id = Guid.NewGuid(), Name = "Early orphan", PresentationOrder = 1 }
                ]
            }]
        };

        Assert.Equal(
            ["Collection", "Child", "Early orphan", "Late orphan", "Åpen gruppe"],
            KildeHierarchyNode.From(hierarchy)[0].Children.Select(n => n.Name));
    }

    [Fact]
    public void Render_WhenTheTreeIsTheCapturedPayload_ThenItsVariabelgrupperAreDrawnInTheCuratedOrder()
    {
        var hierarchy = JsonSerializer.Deserialize<KildeHierarchy>(
                TestData.Read("hierarchy.json"), MuninExplorerClient.Json)
            ?? throw new InvalidOperationException("hierarchy.json no longer reads as a KildeHierarchy.");

        // The capture's own kilde id: the view treats an answer about another kilde as a failure.
        var cut = Mount(
            new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(hierarchy) }, hierarchy.KildeId);

        // Read off the capture rather than a fixture, because the claim is about the catalogue: the
        // 32 groups under Tromsø4's first visit are a questionnaire's own sequence, and in name
        // order the same list opens on ALCOHOL, BLOOD SAMPLES, COFFEE.
        var firstVisit = cut.FindAll("details")
            .Single(d => Name(d) == "Tromsø4 - The Fourth Tromsø Study - first visit");

        Assert.Equal(
            ["GENERAL INFORMATION", "PHYSICAL EXAMINATION", "BLOOD SAMPLES"],
            firstVisit.QuerySelector("ul")!.Children.Take(3).Select(Name));
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
        // The name span, not the decorative icon slot that now opens the summary in front of it.
        Assert.Equal(
            "no",
            cut.Find("summary > span:not([aria-hidden]):not(.screenreader-only)").GetAttribute("lang"));
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

    [Fact]
    public void Render_WhenEveryNodeKindOccurs_ThenTheIconsFollowTheLevelTheRowCameFrom()
    {
        var id = Guid.NewGuid();
        var cut = Mount(new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) }, id);

        Assert.Equal(["kilde"], Icons(Row(cut, "Parent")));
        Assert.Equal(["kilde"], Icons(Row(cut, "Child")));

        // Deduplicated, and in the legend's order rather than the order the catalogue authored.
        Assert.Equal(["PHDR", "EINS"], Icons(Row(cut, "Collection")));

        // A datasamling with no datakategori draws no glyph: absence is not the catch-all. A
        // variabelgruppe has none of its own at any time, and nothing invents one.
        Assert.Empty(Icons(Row(cut, "Direct")));
        Assert.Empty(Icons(Row(cut, "Group")));
        Assert.Empty(Icons(Row(cut, "Orphan")));
    }

    [Fact]
    public void Render_WhenIconsAreDrawn_ThenTheyAreDecorativeAndNotKeyboardStops()
    {
        var id = Guid.NewGuid();
        var cut = Mount(new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) }, id);

        Assert.NotEmpty(cut.FindAll("svg"));
        Assert.All(cut.FindAll(".munin-explorer-hierarchy__icons"), slot =>
            Assert.Equal("true", slot.GetAttribute("aria-hidden")));
        Assert.All(cut.FindAll("svg"), icon =>
        {
            Assert.Equal("true", icon.GetAttribute("aria-hidden"));
            Assert.Equal("false", icon.GetAttribute("focusable"));
            Assert.False(icon.HasAttribute("tabindex"));

            // Sized and coloured off the words around it, so a host with no rule for the class
            // draws a glyph the size of one rather than an <svg>'s own 300x150 default.
            Assert.Equal("1em", icon.GetAttribute("width"));
            Assert.Equal("1em", icon.GetAttribute("height"));
            Assert.Equal("currentColor", icon.GetAttribute("stroke"));
        });

        // A folder says only what the nesting already says, so it adds nothing to the row's name.
        Assert.Equal("Parent 8 variabler", AccessibleName.Of(Row(cut, "Parent")));
    }

    [Fact]
    public void Render_WhenADatasamlingCarriesCategories_ThenItsRowSaysThemInWordsExactlyOnce()
    {
        // The glyphs are aria-hidden, so without this the datakategori is information only a
        // sighted reader gets - and axe cannot see the absence of something.
        var id = Guid.NewGuid();
        var cut = Mount(new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) }, id);
        var row = Row(cut, "Collection");

        Assert.Equal(
            ["Datakategori: Befolkningsbaserte helseregistre, Biobanker og prøvesamlinger."],
            row.QuerySelectorAll(".screenreader-only")
                .Select(span => span.TextContent.Trim())
                .Where(text => text.StartsWith("Datakategori", StringComparison.Ordinal)));

        Assert.Equal(
            "Collection Datakategori: Befolkningsbaserte helseregistre, "
            + "Biobanker og prøvesamlinger. 4 variabler",
            AccessibleName.Of(row));
    }

    [Fact]
    public void Render_WhenNodeIconsAreTurnedOff_ThenTheVariableCountsAreUntouched()
    {
        // Two separate offers: a reader who wants no glyphs still wants to know how much sits under
        // a node, and a toggle that took the counts with it would be read as a bug in the tree.
        var id = Guid.NewGuid();
        Services.AddSingleton<IMuninExplorerClient>(
            new Client { Fetch = (_, _) => Task.FromResult<KildeHierarchy?>(Hierarchy(id)) });
        var cut = Render<KildeHierarchyView>(p => p
            .Add(c => c.KildeId, id)
            .Add(c => c.Language, "nb")
            .Add(c => c.ShowNodeIcons, false));

        Assert.Empty(cut.FindAll("svg, .munin-explorer-hierarchy__icons"));
        Assert.DoesNotContain("Datakategori", cut.Markup);
        Assert.Equal(7, cut.FindAll("li").Count);
        Assert.Equal(
            ["2 variabler", "4 variabler", "4 variabler", "8 variabler"],
            cut.FindAll(".munin-explorer-hierarchy__count")
                .Select(count => count.TextContent.Trim())
                .OrderBy(text => text, StringComparer.Ordinal));
    }

    /// <summary>A node's own name, without the icons or the variable count drawn beside it.</summary>
    private static string Name(IElement node) =>
        node.QuerySelector(
            "summary > span:not([aria-hidden]):not(.screenreader-only), "
            + ".munin-explorer-hierarchy__leaf > span:not([aria-hidden]):not(.screenreader-only)")!
            .TextContent.Trim();

    /// <summary>The summary or leaf one named node is drawn in.</summary>
    private static IElement Row(IRenderedComponent<KildeHierarchyView> cut, string name) =>
        cut.FindAll("summary, .munin-explorer-hierarchy__leaf").Single(row => Name(row) == name);

    /// <summary>Which glyphs a row draws, in the order it draws them.</summary>
    private static IEnumerable<string?> Icons(IElement row) =>
        row.QuerySelectorAll("svg").Select(icon => icon.GetAttribute("data-node-icon"));

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
                // Authored the three ways the catalogue really authors them: prefixed, as a retired
                // slug, and again under a spelling already covered.
                Categories = ["ehds-cat:PHDR", "biobanks", "PHDR"],
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
