using System.Text.Json;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// "Alle metadatafelt": the section that lists the whole record, so no field a reader came for is
/// missing from the page.
/// </summary>
/// <remarks>
/// NOT the placement leftovers: the seeded catch-all holds 53 of the Tromsø payload's 73 definitions,
/// so rows come from every entry with a value, duplicates included. That duplication is the
/// requirement, so nothing here asserts a field appears once (Fhi.Metadata-35w0p.21).
/// </remarks>
public class CompleteRecordTest : ExplorerTestContext
{
    public CompleteRecordTest() =>
        Services.AddSingleton<IMuninExplorerClient>(new HierarchyClient());

    /// <summary>The kilde view fetches its tree on its own; an empty one is enough to render.</summary>
    private sealed class HierarchyClient : EmptyMuninExplorerClient
    {
        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new() { KildeId = id });
    }

    /// <summary>
    /// Tromsøundersøkelsen, captured from runa after groupKey shipped — the one fixture whose
    /// payload carries the field at all.
    /// </summary>
    private static KildeDetail Tromso() =>
        JsonSerializer.Deserialize<KildeDetail>(
            TestData.Read("kilde-med-delkilder.json"), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException("kilde-med-delkilder.json no longer reads as a KildeDetail.");

    /// <summary>
    /// Barnediabetes as an API from before placements sends it: no groupKey, no groupSortOrder, no
    /// sections.
    /// </summary>
    /// <remarks>
    /// Derived from the capture rather than captured, since no environment serves that shape any more
    /// and the capture itself must stay free to refresh (Fhi.Metadata-l9l2n.118).
    /// </remarks>
    private static KildeDetail UnplacedBarnediabetes()
    {
        var kilde = JsonSerializer.Deserialize<KildeDetail>(
                        TestData.Read("kilde-barnediabetes.json"), MuninExplorerClient.Json)
                    ?? throw new InvalidOperationException("kilde-barnediabetes.json no longer reads as a KildeDetail.");

        return kilde with
        {
            PropertyMetadata = [.. kilde.PropertyMetadata.Select(entry => entry with { GroupKey = null, GroupSortOrder = null })],
            Sections = [],
        };
    }

    /// <summary>The datasamling from before placements, which <see cref="DatasamlingBefore"/> was counted on.</summary>
    private static DatasamlingDetail Datasamling() =>
        JsonSerializer.Deserialize<DatasamlingDetail>(
            TestData.Read(Fixture.DatasamlingUnplaced), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException($"{Fixture.DatasamlingUnplaced} no longer reads as a DatasamlingDetail.");

    private static VariableDetail Variable() =>
        JsonSerializer.Deserialize<VariableDetail>(
            TestData.Read("variable.json"), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException("variable.json no longer reads as a VariableDetail.");

    private IRenderedComponent<KildeView> RenderKilde(KildeDetail kilde, string? language = null) =>
        Render<KildeView>(b => b.Add(c => c.Kilde, kilde).Add(c => c.Language, language));

    private const string Disclosure = "details.munin-explorer-complete-record";

    private const string Lead = "p.munin-explorer-complete-record__lead";

    private const string Fields = "dl.munin-explorer-complete-record__fields";

    // -----------------------------------------------------------------------
    // Criterion 1 — the contract reads groupKey, proved over a payload nobody typed

    [Fact]
    public void GroupKey_WhenReadFromTheCapturedPayload_ThenEveryEntryCarriesTheSectionItWasPlacedIn()
    {
        // Over the fixture rather than over an entry built here: a hand-made PropertyMetadataEntry
        // proves the record agrees with itself and says nothing about the wire shape. Strict
        // deserialisation rejects unmapped members and never absent ones, so a test over a fixture
        // without the field passes whether or not the contract maps it.
        var entries = Tromso().PropertyMetadata;

        Assert.Equal(73, entries.Count);
        Assert.DoesNotContain(entries, entry => string.IsNullOrWhiteSpace(entry.GroupKey));
        Assert.Equal(53, entries.Count(entry => entry.GroupKey == CatalogueProperties.CatchAllGroupKey));
        Assert.Contains("om-registeret", entries.Select(entry => entry.GroupKey));
    }

    // -----------------------------------------------------------------------
    // Criterion 2 — confirmed rather than rebuilt: Fhi.Metadata-35w0p.19 shipped the gathering by key

    [Fact]
    public void Groups_WhenTwoSectionsShareAHeading_ThenTheKeyStillTellsThemApart()
    {
        // Shipped by .19 and covered in CataloguePropertiesTest. Asserted again here because the
        // catch-all is recognised the same way, so an implementation that slid back to matching
        // headings would break both at once.
        PropertyMetadataEntry[] metadata =
        [
            Entry("A", "Innhold", "innhold-kilde"),
            Entry("B", "Innhold", "innhold-samling"),
        ];

        var groups = CatalogueProperties.Groups(metadata, Bag(("A", "en"), ("B", "to")), "no");

        Assert.Equal(["innhold-kilde", "innhold-samling"], groups.Select(group => group.Key));
    }

    // -----------------------------------------------------------------------
    // Criteria 3 and 4 — the disclosure, and the rename that must not switch it off

    [Fact]
    public void CatchAll_WhenThePayloadKeysIt_ThenTheLeadAndACollapsedDisclosureAreDrawn()
    {
        var cut = RenderKilde(Tromso());

        var details = cut.Find(Disclosure);

        Assert.Contains("bortsett fra felt som holder strukturerte data", cut.Find(Lead).TextContent, StringComparison.Ordinal);
        Assert.False(details.HasAttribute("open"));
        Assert.Equal("Alle felt fra Munin, slik de er registrert",
                     AccessibleName.Of(details.QuerySelector("summary")!));
    }

    [Fact]
    public void CatchAll_WhenACuratorRenamesTheSection_ThenTheLeadAndTheDisclosureSurviveIt()
    {
        // The criterion that kills a `group.Name == "Alle metadatafelt"` implementation. Munin's
        // curator API can rename a section (Fhi.Metadata-8w2xu), and a rename moves
        // groupTranslations while leaving groupKey exactly where it was.
        var cut = RenderKilde(Renamed(Tromso(), Retitled));

        Assert.Contains("bortsett fra felt som holder strukturerte data", cut.Find(Lead).TextContent, StringComparison.Ordinal);
        Assert.False(cut.Find(Disclosure).HasAttribute("open"));
        Assert.Contains(Retitled, Headings(cut));
        Assert.DoesNotContain("Alle metadatafelt", Headings(cut));
    }

    [Fact]
    public void CatchAll_WhenAnotherSectionIsTitledLikeIt_ThenThatSectionIsStillAnOrdinaryGroup()
    {
        // The same rule read from the other side: the heading is a curator's to write anywhere, so
        // a group merely CALLED "Alle metadatafelt" has to draw the way every other group draws.
        var kilde = Tromso() with
        {
            PropertyMetadata = [Entry("A", "Alle metadatafelt", "om-registeret")],
            AdditionalProperties = Bag(("A", "en")),
        };

        var cut = RenderKilde(kilde);

        Assert.Contains("Alle metadatafelt", Headings(cut));
        Assert.Empty(cut.FindAll(Disclosure));
        Assert.Empty(cut.FindAll(Lead));
    }

    [Fact]
    public void CatchAll_WhenADatasamlingCarriesIt_ThenThatPageDrawsItToo()
    {
        // Munin seeds a catch-all on all three surfaces, and the three views loop their groups
        // identically, so recognition lives in the shared code or in none of them. The unplaced
        // datasamling carries no groupKey, so the payload is built here rather than read.
        var datasamling = Datasamling() with
        {
            PropertyMetadata = [Entry("A", "Alle metadatafelt", CatalogueProperties.CatchAllGroupKey)],
            AdditionalProperties = Bag(("A", "en")),
        };

        var cut = Render<DatasamlingView>(b => b.Add(c => c.Datasamling, datasamling));

        Assert.Contains("datasamlingen", cut.Find(Lead).TextContent, StringComparison.Ordinal);
        Assert.False(cut.Find(Disclosure).HasAttribute("open"));
    }

    [Fact]
    public void CatchAll_WhenAVariableCarriesIt_ThenThatPageDrawsItToo()
    {
        // The third surface, for the reason the second one is here.
        var variable = Variable() with
        {
            PropertyMetadata = [Entry("A", "Alle metadatafelt", CatalogueProperties.CatchAllGroupKey)],
            AdditionalProperties = Bag(("A", "en")),
        };

        var cut = Render<VariableView>(b => b.Add(c => c.Variable, variable));

        Assert.Contains("variabelen", cut.Find(Lead).TextContent, StringComparison.Ordinal);
        Assert.False(cut.Find(Disclosure).HasAttribute("open"));
    }

    [Fact]
    public void CatchAll_WhenThePayloadPredatesGroupKey_ThenNoSectionClaimsToBeTheCompleteRecord()
    {
        // Nothing in a pre-placement payload can be recognised as the catch-all, and inventing one
        // out of the leftovers would put the completeness sentence over a page where it is not true.
        var cut = RenderKilde(UnplacedBarnediabetes());

        Assert.NotEmpty(Headings(cut));
        Assert.Empty(cut.FindAll(Disclosure));
        Assert.Empty(cut.FindAll(Lead));
    }

    // -----------------------------------------------------------------------
    // Criterion 5 — the complete record, measured

    /// <summary>
    /// What the section draws on the Tromsø payload: the 30 of its 73 definitions that have a
    /// value, plus the four facts no property definition carries.
    /// </summary>
    /// <remarks>
    /// Pinned rather than bounded. The two wrong answers this bead has been rewritten against are
    /// 53 — the placement leftovers — and the 8 this payload drew while the catch-all was an
    /// ordinary group, so a count that only said "more than a handful" would not tell them apart.
    /// </remarks>
    private const int TromsoRecordRows = 34;

    [Fact]
    public void CompleteRecord_OnTheCapturedPayload_ThenItListsEveryFieldThatHasAValue()
    {
        var cut = RenderKilde(Tromso());

        Assert.Equal(TromsoRecordRows, cut.FindAll($"{Fields} > div").Count);
    }

    [Fact]
    public void CompleteRecord_OnTheCapturedPayload_ThenItRepeatsTheFieldsTheNamedSectionsDrew()
    {
        // The specified duplication. Formål and Anbefalte bruksområder are drawn under "Om
        // registeret" above and have to be in the record as well, or the sentence over it is false.
        var cut = RenderKilde(Tromso());

        Assert.Contains("Formål", RecordLabels(cut));
        Assert.Contains("Anbefalte bruksområder", RecordLabels(cut));
        Assert.Contains("Formål", NamedSectionLabels(cut));
        Assert.Contains("Anbefalte bruksområder", NamedSectionLabels(cut));
    }

    [Fact]
    public void CompleteRecord_OnTheCapturedPayload_ThenTheIdentityColumnsAreInItToo()
    {
        // The four Munin keeps in columns of their own rather than in the bag. They reach no named
        // section on this payload, and the page header draws them as a title and a badge, so
        // without these the record would be missing the fields a reader looks for first.
        var labels = RecordLabels(RenderKilde(Tromso()));

        Assert.Contains("Foretrukken term", labels);
        Assert.Contains("Kode", labels);
        Assert.Contains("Kortnavn", labels);
        Assert.Contains("Kildetype", labels);
    }

    [Fact]
    public void CompleteRecord_OnTheCapturedPayload_ThenTheFourComputedFactsCloseIt()
    {
        // No placement can ever carry these — they are counts and dates rather than curated
        // properties — so the renderer appends them or they are in no section at all.
        var labels = RecordLabels(RenderKilde(Tromso()));

        Assert.Equal(
            ["Totalt antall variabler", "Antall datasamlinger", "Dataperiode", "Sist oppdatert i Munin"],
            labels.Skip(labels.Count - 4));
    }

    // -----------------------------------------------------------------------
    // Criterion 6 — no field is lost

    /// <summary>
    /// What each page drew before this section existed, counted on <c>aa21596</c> as (dt, dd): the
    /// source out of kilde-med-delkilder.json, and the two payloads carrying no groupKey at all.
    /// </summary>
    private static readonly (int Terms, int Definitions) KildeBefore = (34, 36);

    /// <inheritdoc cref="KildeBefore"/>
    private static readonly (int Terms, int Definitions) DatasamlingBefore = (17, 17);

    /// <inheritdoc cref="KildeBefore"/>
    private static readonly (int Terms, int Definitions) VariableBefore = (14, 14);

    [Theory]
    [InlineData("kilde")]
    [InlineData("datasamling")]
    [InlineData("variabel")]
    public void Pages_AfterTheSectionArrives_ThenNoneOfThemDrawsFewerFieldsThanBefore(string page)
    {
        // The criterion the section exists for. Equal is a pass: a payload without a groupKey
        // cannot grow a catch-all, and must not shrink either.
        var (before, after) = page switch
        {
            "kilde" => (KildeBefore, Pairs(RenderKilde(Tromso()))),
            "datasamling" => (DatasamlingBefore,
                              Pairs(Render<DatasamlingView>(b => b.Add(c => c.Datasamling, Datasamling())))),
            _ => (VariableBefore, Pairs(Render<VariableView>(b => b.Add(c => c.Variable, Variable())))),
        };

        Assert.True(after.Terms >= before.Terms && after.Definitions >= before.Definitions,
                    $"{page} draws {after} where it drew {before} before the complete record existed.");
    }

    // -----------------------------------------------------------------------
    // Criterion 7 — every other section byte-identical

    [Fact]
    public void NamedSections_AfterTheSectionArrives_ThenTheirMarkupIsWhatItWas()
    {
        // Captured off aa21596, before any of this existed, and compared character for character:
        // "the page still looks right" by eye is what let four defects through on 2026-09-03.
        Assert.Equal(Golden(), NamedSectionsMarkup(RenderKilde(Tromso())));
    }

    /// <summary>The recorded markup, its provenance header stripped.</summary>
    private static string Golden()
    {
        var lines = File.ReadAllLines(Repo.In("test", "named-sections-kilde-med-delkilder.html"));
        var markup = lines.SkipWhile(line => !line.TrimEnd().EndsWith("-->", StringComparison.Ordinal)).Skip(1);

        return string.Join("\n", markup);
    }

    [Fact]
    public void CompleteRecord_WhenTheDescriptionsCarryLinks_ThenItsRowsRenderThemRatherThanTheSource()
    {
        // The one section meant to repeat the descriptions, so it must draw them as the ingress does.
        var tromso = Tromso();
        var values = new Dictionary<string, string?>(tromso.AdditionalProperties)
        {
            ["BeskrivelseFlerspraklig"] = """{"nb":"Se [UiT](https://uit.no/nb)."}""",
        };

        var fields = RenderKilde(tromso with { Description = "Se [UiT](https://uit.no).", AdditionalProperties = values })
            .Find(Fields);

        var hrefs = fields.QuerySelectorAll("a").Select(a => a.GetAttribute("href")).ToList();

        Assert.Contains("https://uit.no", hrefs);
        Assert.Contains("https://uit.no/nb", hrefs);
        Assert.DoesNotContain("](https://uit.no", fields.TextContent, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Criterion 8 — the claim is true in the language it is made in

    [Fact]
    public void Lead_WhenTheReaderIsEnglish_ThenTheClaimIsMadeInTheirLanguage()
    {
        // The sentence is this package's own prose, so it is translated rather than marked foreign.
        var lead = RenderKilde(Tromso(), "en").Find(Lead);

        Assert.Contains("apart from fields that hold structured data", lead.TextContent, StringComparison.Ordinal);
        Assert.Null(lead.GetAttribute("lang"));
    }

    // -----------------------------------------------------------------------
    // Helpers

    /// <summary>The heading a curator's rename puts over the section in the two tests that use it.</summary>
    private const string Retitled = "Hele registreringen";

    private static (int Terms, int Definitions) Pairs<T>(IRenderedComponent<T> cut) where T : IComponent =>
        (cut.FindAll("dt").Count, cut.FindAll("dd").Count);

    /// <summary>The labels the complete record draws, in the order it draws them.</summary>
    private static IReadOnlyList<string> RecordLabels<T>(IRenderedComponent<T> cut) where T : IComponent =>
        [.. cut.FindAll($"{Fields} > div > dt").Select(dt => dt.TextContent)];

    /// <summary>The labels the named sections draw — a different list, wearing a different class.</summary>
    private static IReadOnlyList<string> NamedSectionLabels<T>(IRenderedComponent<T> cut) where T : IComponent =>
        [.. cut.FindAll("dl.munin-explorer-page__fields > div > dt").Select(dt => dt.TextContent)];

    /// <summary>Every group heading the metadata section draws.</summary>
    private static IReadOnlyList<string> Headings<T>(IRenderedComponent<T> cut) where T : IComponent =>
        [.. cut.FindAll("h4.munin-explorer-group").Select(heading => heading.TextContent)];

    /// <summary>
    /// The metadata section down to the catch-all's heading, which is the markup criterion 7 pins.
    /// </summary>
    /// <remarks>
    /// Cut at the heading rather than at a class name, so one rule reads the page as it was before
    /// this section existed and as it is now — which is what makes it a before-and-after comparison.
    /// </remarks>
    private static string NamedSectionsMarkup<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        var parts = new List<string>();

        foreach (var child in cut.Find($"#{DetailSectionIds.Metadata}").Children)
        {
            if (child.TextContent is "Alle metadatafelt" or Retitled)
            {
                break;
            }

            parts.Add(child.OuterHtml);
        }

        return string.Join("\n", parts);
    }

    /// <summary>The same payload with the catch-all retitled and its key left alone.</summary>
    private static KildeDetail Renamed(KildeDetail kilde, string heading) => kilde with
    {
        PropertyMetadata =
        [
            .. kilde.PropertyMetadata.Select(entry =>
                entry.GroupKey == CatalogueProperties.CatchAllGroupKey
                    ? entry with { GroupTranslations = new Dictionary<string, string> { ["no"] = heading } }
                    : entry),
        ],
    };

    private static PropertyMetadataEntry Entry(string key, string heading, string groupKey) => new()
    {
        Key = key,
        SortOrder = 10,
        DisplayNameTranslations = new Dictionary<string, string> { ["no"] = key },
        GroupTranslations = new Dictionary<string, string> { ["no"] = heading },
        GroupKey = groupKey,
    };

    private static IReadOnlyDictionary<string, string?> Bag(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => (string?)value.Value, StringComparer.Ordinal);
}
