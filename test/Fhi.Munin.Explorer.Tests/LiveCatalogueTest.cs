using System.Net;
using System.Text;
using Xunit.Sdk;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The discovery helpers the nightly job leans on, driven against a stub on every commit.
/// </summary>
/// <remarks>
/// Every branch in <see cref="LiveCatalogue"/> otherwise runs only under <c>MUNIN_EXPLORER_LIVE</c>,
/// once a night, against whatever the catalogue looks like then — so a helper that quietly picks an
/// entity without the half its caller measures keeps every run green, which is the defect
/// <c>Fhi.Metadata-l9l2n.90</c> fixed. <see cref="DriftRanGuardTest"/> states the principle: what
/// the nightly does is pinned where it runs on every commit.
/// </remarks>
public class LiveCatalogueTest
{
    /// <summary>For the arms that never reach the hierarchy endpoint.</summary>
    private static readonly Dictionary<Guid, string?> NoHierarchies = [];

    [Fact]
    public async Task KildeWithDelkilderIdAsync_WhenTheDelkildeHeaviestKildeHasNoDatasamling_ThenOneWithBothIsPreferred()
    {
        using var api = LiveApiConnection.Open(Serving(
            [Kilde(Id(1), delkildeCount: 4), Kilde(Id(2), delkildeCount: 2, datasamlingCount: 15)],
            NoHierarchies));

        // Why the sort values both counts rather than delkilder alone: a KildeDetail with no
        // datasamling in it leaves every datasamling key in that payload unchecked, one level below
        // the defect this helper was split to fix.
        Assert.Equal(Id(2), await LiveCatalogue.KildeWithDelkilderIdAsync(api));
    }

    [Fact]
    public async Task KildeWithDelkilderIdAsync_WhenNoKildeReportsADelkilde_ThenItFailsSayingSo()
    {
        using var api = LiveApiConnection.Open(Serving(
            [Kilde(Id(1), datasamlingCount: 9), Kilde(Id(2), datasamlingCount: 3)],
            NoHierarchies));

        // The regression the filter exists to prevent: sorted only, this handed back a kilde with
        // no delkilder at all and the nested arms went on checking a payload that had no nested half.
        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => LiveCatalogue.KildeWithDelkilderIdAsync(api));

        Assert.Contains("delkildeCount", failure.Message, StringComparison.Ordinal);
        Assert.Contains("2 kilder", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KildeWithDelkilderIdAsync_WhenTwoKilderTieOnEveryCount_ThenTheApiListOrderDoesNotDecideIt()
    {
        var first = Kilde(Id(1), delkildeCount: 4, datasamlingCount: 6);
        var second = Kilde(Id(2), delkildeCount: 4, datasamlingCount: 6);

        using var listed = LiveApiConnection.Open(Serving([first, second], NoHierarchies));
        using var reversed = LiveApiConnection.Open(Serving([second, first], NoHierarchies));

        Assert.Equal(Id(2), await LiveCatalogue.KildeWithDelkilderIdAsync(listed));
        Assert.Equal(Id(2), await LiveCatalogue.KildeWithDelkilderIdAsync(reversed));
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenTheRichestKildeServesNoDatasamling_ThenTheWalkMovesOnToTheNext()
    {
        var wanted = Id(81);

        using var api = LiveApiConnection.Open(Serving(
            [Kilde(Id(1), datasamlingCount: 17), Kilde(Id(2), datasamlingCount: 1)],
            new Dictionary<Guid, string?>
            {
                [Id(1)] = Hierarchy(Id(1)),
                [Id(2)] = Hierarchy(Id(2), direct: Datasamlinger(wanted))
            }));

        // A count is not a tree, and three of the 64 live candidates were like Id(1) on 2026-09-10.
        // Without the walk this returns Guid.Empty and GetDatasamlingAsync is asked for it.
        Assert.Equal(wanted, await LiveCatalogue.AnyDatasamlingIdAsync(api));
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenTheOnlyDatasamlingSitsUnderANestedDelkilde_ThenTheWalkFindsIt()
    {
        var wanted = Id(82);
        var nested = Delkilde(Id(11), datasamlinger: Datasamlinger(wanted));

        using var api = LiveApiConnection.Open(Serving(
            [Kilde(Id(1), datasamlingCount: 4)],
            new Dictionary<Guid, string?>
            {
                [Id(1)] = Hierarchy(Id(1), delkilder: $"[{Delkilde(Id(10), children: $"[{nested}]")}]")
            }));

        // The recursion DatasamlingIds performs. A hierarchy this deep is ordinary in the catalogue
        // and nothing else here descends past the first delkilde.
        Assert.Equal(wanted, await LiveCatalogue.AnyDatasamlingIdAsync(api));
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenNoCandidateCarriesOne_ThenItFailsRatherThanHandingBackAnEmptyId()
    {
        using var api = LiveApiConnection.Open(Serving(
            [Kilde(Id(1), datasamlingCount: 17), Kilde(Id(2), datasamlingCount: 3)],
            new Dictionary<Guid, string?> { [Id(1)] = Hierarchy(Id(1)), [Id(2)] = Hierarchy(Id(2)) }));

        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => LiveCatalogue.AnyDatasamlingIdAsync(api));

        Assert.Contains("2 kilder tried", failure.Message, StringComparison.Ordinal);
        Assert.Contains("0 of them served no hierarchy", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenNoHierarchyIsServedAtAll_ThenTheFailureCountsThatRatherThanClaimingTheTreesWereSearched()
    {
        using var api = LiveApiConnection.Open(Serving(
            [Kilde(Id(1), datasamlingCount: 17), Kilde(Id(2), datasamlingCount: 3)],
            new Dictionary<Guid, string?> { [Id(1)] = null, [Id(2)] = null }));

        // Folded into "no datasamling anywhere", an endpoint that stopped answering reads as the
        // catalogue changing shape, and whoever picks the report up goes looking at the wrong thing.
        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => LiveCatalogue.AnyDatasamlingIdAsync(api));

        Assert.Contains("2 of them served no hierarchy", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenTheCatalogueOutgrowsTheWalk_ThenItStopsAtItsBoundRatherThanFetchingEveryCandidate()
    {
        var kilder = Enumerable.Range(1, 40).Select(seed => Kilde(Id(seed), datasamlingCount: seed)).ToList();
        var hierarchies = Enumerable.Range(1, 40)
            .ToDictionary(seed => Id(seed), seed => (string?)Hierarchy(Id(seed)));

        var handler = Serving(kilder, hierarchies);
        using var api = LiveApiConnection.Open(handler);

        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => LiveCatalogue.AnyDatasamlingIdAsync(api));

        // One list plus one hierarchy per candidate. Unbounded, the failure path costs a live round
        // trip per qualifying kilde and grows silently with the catalogue, in two suites.
        Assert.Equal(26, handler.Calls);
        Assert.Contains("25 kilder tried", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenTwoKilderTieOnTheCount_ThenTheApiListOrderDoesNotDecideIt()
    {
        var hierarchies = new Dictionary<Guid, string?>
        {
            [Id(1)] = Hierarchy(Id(1), direct: Datasamlinger(Id(81))),
            [Id(2)] = Hierarchy(Id(2), direct: Datasamlinger(Id(82)))
        };

        var first = Kilde(Id(1), datasamlingCount: 17);
        var second = Kilde(Id(2), datasamlingCount: 17);

        using var listed = LiveApiConnection.Open(Serving([first, second], hierarchies));
        using var reversed = LiveApiConnection.Open(Serving([second, first], hierarchies));

        // FixtureDriftTest compares Testdata/datasamling.json against whatever this returns, so a
        // pick resting on a stable sort over the API's own order moves a fixture with no code change.
        Assert.Equal(Id(82), await LiveCatalogue.AnyDatasamlingIdAsync(listed));
        Assert.Equal(Id(82), await LiveCatalogue.AnyDatasamlingIdAsync(reversed));
    }

    [Fact]
    public async Task AnyDatasamlingIdAsync_WhenTheApiStopsAnsweringPartWayThroughTheWalk_ThenTheFailureSaysUnreachable()
    {
        using var api = LiveApiConnection.Open(new StubHttpHandler(request =>
            AsksForHierarchy(request)
                ? throw new HttpRequestException("No such host is known.")
                : Json($"[{Kilde(Id(1), datasamlingCount: 17)}]")));

        // The discovery calls are outside RoundTripAsync, so nothing else translates an outage here.
        // Unmarked, scripts/drift-failure-kind.sh titles the night's issue drift and sends somebody
        // to edit DTOs that were never wrong (Fhi.Metadata-ghxh4).
        var failure = await Assert.ThrowsAnyAsync<XunitException>(
            () => LiveCatalogue.AnyDatasamlingIdAsync(api));

        Assert.Contains(LiveApi.UnreachableMarker, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no longer matches", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Ids that sort the way the test says they do, rather than however a random one falls.</summary>
    private static Guid Id(int seed) => new($"00000000-0000-0000-0000-{seed:D12}");

    /// <summary>A stub API: one kilde list, and a hierarchy per kilde — null for one it will not serve.</summary>
    private static StubHttpHandler Serving(
        IReadOnlyList<string> kilder,
        IReadOnlyDictionary<Guid, string?> hierarchies) =>
        new(request =>
        {
            if (!AsksForHierarchy(request))
            {
                return Json($"[{string.Join(",", kilder)}]");
            }

            var id = Guid.Parse(request.RequestUri!.AbsolutePath.Split('/')[^2]);

            return hierarchies[id] is { } body ? Json(body) : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    private static bool AsksForHierarchy(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/hierarchy", StringComparison.Ordinal);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string Kilde(Guid id, int delkildeCount = 0, int datasamlingCount = 0) =>
        $$"""
          {"id":"{{id}}","code":"K_TEST","navn":"Kilde","aktiv":true,"totalVariables":0,
           "datasamlingCount":{{datasamlingCount}},"delkildeCount":{{delkildeCount}}}
          """;

    private static string Hierarchy(Guid kildeId, string delkilder = "[]", string direct = "[]") =>
        $$"""
          {"kildeId":"{{kildeId}}","kildeName":"Kilde","totalVariableCount":0,
           "delkilder":{{delkilder}},"directDatasamlinger":{{direct}}}
          """;

    private static string Delkilde(Guid id, string datasamlinger = "[]", string children = "[]") =>
        $$"""
          {"id":"{{id}}","name":"Delkilde","variableCount":0,"unassignedVariabelgrupper":[],
           "datasamlinger":{{datasamlinger}},"children":{{children}}}
          """;

    private static string Datasamlinger(Guid id) =>
        $$"""[{"id":"{{id}}","name":"Datasamling","variableCount":0,"variabelgrupper":[],"categories":[]}]""";
}
