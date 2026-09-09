using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The datakategori mapping, head-on. It is a copy of Kelda's <c>datakategoriIcons.ts</c> rather
/// than a shared implementation, so every rule that copy makes is asserted here: the two trees
/// showing one datasamling as two different things is the failure nobody would look for.
/// </summary>
public sealed class NodeIconsTest
{
    [Theory]
    [InlineData("PHDR")]
    [InlineData("phdr")]
    [InlineData("ehds-cat:PHDR")]
    [InlineData("EHDS-CAT:phdr")]
    [InlineData("  PHDR  ")]
    public void For_WhenACodeArrivesInAnyAuthoredForm_ThenItResolvesToTheSameGlyph(string token)
    {
        Assert.Equal(["PHDR"], DatakategoriIcons.For([token]).Select(icon => icon.Key));
    }

    [Theory]
    // Munin's seed revision 0008 crosswalk, verbatim. A read model built before it ran still ships
    // these, and dropping the table would relabel a biobank as "Annet".
    [InlineData("health-registries", "PHDR")]
    [InlineData("registries-quality-of-healthcare", "MRMR")]
    [InlineData("population-health-surveys", "RPDG")]
    [InlineData("biobanks", "EINS")]
    [InlineData("provesamling", "EINS")]
    [InlineData("biodata", "HGPD")]
    public void For_WhenARetiredSlugArrives_ThenItResolvesOntoItsSuccessor(string slug, string successor)
    {
        Assert.Equal([successor], DatakategoriIcons.For([slug]).Select(icon => icon.Key));
        Assert.Equal([successor], DatakategoriIcons.For([$"ehds-cat:{slug}"]).Select(icon => icon.Key));
    }

    [Fact]
    public void For_WhenSeveralCategoriesArriveOutOfOrder_ThenTheyDrawDeduplicatedInOneOrder()
    {
        // Authoring order must not reach the screen: the same set of categories has to look the
        // same on every datasamling that carries it, and beside Kelda's own tree.
        IReadOnlyList<string> authored = ["WELA", "ehds-cat:PHDR", "biobanks", "phdr", "EINS"];

        Assert.Equal(["PHDR", "EINS", "WELA"], DatakategoriIcons.For(authored).Select(icon => icon.Key));
    }

    [Fact]
    public void For_WhenATokenIsUnknown_ThenItDrawsTheCatchAllRatherThanNothing()
    {
        // A categorised datasamling must never read as an uncategorised one, so an unrecognised
        // token still draws a glyph.
        Assert.Equal(["other"], DatakategoriIcons.For(["ehds-cat:teapot"]).Select(icon => icon.Key));
        Assert.Equal(["PHDR", "other"], DatakategoriIcons.For(["teapot", "PHDR"]).Select(icon => icon.Key));
    }

    [Fact]
    public void For_WhenThereAreNoCategories_ThenNothingIsDrawnRatherThanTheCatchAll()
    {
        // Absence is not "Annet". Only an authored value produces the catch-all — the rule the
        // hierarchy would otherwise break by drawing a tag on every uncategorised datasamling.
        Assert.Empty(DatakategoriIcons.For(null));
        Assert.Empty(DatakategoriIcons.For([]));
        Assert.Empty(DatakategoriIcons.For(["", "   "]));
    }

    [Fact]
    public void Resolve_WhenAForeignCurieSharesALocalName_ThenItIsNotReadAsThisVocabularys()
    {
        // Matching is on the whole token. Stripping the prefix would let any vocabulary's "other"
        // — or its "biodata" — claim an EHDS glyph.
        Assert.Equal("other", DatakategoriIcons.Resolve("other"));
        Assert.Null(DatakategoriIcons.Resolve("snomed:other"));
        Assert.Null(DatakategoriIcons.Resolve("snomed:biodata"));
        Assert.Null(DatakategoriIcons.Resolve(null));
    }

    [Fact]
    public void Resolve_WhenTheGroupingGlyphIsAskedFor_ThenItIsNotOneOfTheCategories()
    {
        // A folder is not a datakategori: no authored value may resolve to it, and it may not
        // appear in the legend order the categories are drawn in.
        Assert.Null(DatakategoriIcons.Resolve(DatakategoriIcons.Grouping));
        Assert.DoesNotContain(DatakategoriIcons.Grouping, DatakategoriIcons.Order);
        Assert.NotEmpty(DatakategoriIcons.Folder.Shapes);
    }

    [Fact]
    public void Order_WhenACategoryIsAdded_ThenItHasBothAGlyphAndAWordInEitherLanguage()
    {
        // A key with no glyph draws nothing and a key with no label is read out as its raw code,
        // and both are silent: the tree renders either way.
        foreach (var key in DatakategoriIcons.Order)
        {
            Assert.NotEmpty(DatakategoriIcons.For([key]).Single().Shapes);
            Assert.False(string.IsNullOrWhiteSpace(Texts.For("no").DatakategoriNames[key]));
            Assert.False(string.IsNullOrWhiteSpace(Texts.For("en").DatakategoriNames[key]));
        }
    }

    [Fact]
    public void For_WhenTheNodeIsNotADatasamling_ThenOnlyTheGroupingLevelsCarryAGlyph()
    {
        Assert.Equal(
            [DatakategoriIcons.Grouping],
            NodeIcons.For(Node(KildeNodeKind.Delkilde, ["PHDR"])).Select(icon => icon.Key));

        // A variabelgruppe has no icon of its own, and nothing invents one for it.
        Assert.Empty(NodeIcons.For(Node(KildeNodeKind.Variabelgruppe, ["PHDR"])));
        Assert.Equal(["PHDR"], NodeIcons.For(Node(KildeNodeKind.Datasamling, ["PHDR"])).Select(icon => icon.Key));
    }

    [Theory]
    [InlineData("no", "Datakategori: Befolkningsbaserte helseregistre, Biobanker og prøvesamlinger.")]
    [InlineData("en", "Data category: Population health data registries, Health data from biobanks.")]
    public void SpokenCategories_WhenADatasamlingCarriesCategories_ThenTheGlyphsAreSaidInWords(
        string language, string expected)
    {
        var node = Node(KildeNodeKind.Datasamling, ["biobanks", "PHDR"]);

        Assert.Equal(expected, NodeIcons.SpokenCategories(node.Kind, NodeIcons.For(node), Texts.For(language)));
    }

    [Fact]
    public void SpokenCategories_WhenTheGlyphSaysNothingTheRowDoesNot_ThenThereIsNothingToRead()
    {
        // The folder repeats the nesting the list already conveys, so saying it would make every
        // grouping row announce its own type before its name.
        var delkilde = Node(KildeNodeKind.Delkilde, []);
        Assert.Null(NodeIcons.SpokenCategories(delkilde.Kind, NodeIcons.For(delkilde), Texts.For("no")));

        var uncategorised = Node(KildeNodeKind.Datasamling, []);
        Assert.Null(NodeIcons.SpokenCategories(uncategorised.Kind, NodeIcons.For(uncategorised), Texts.For("no")));
    }

    private static KildeHierarchyNode Node(KildeNodeKind kind, IReadOnlyList<string> categories) =>
        new("key", "Name", 0, null, kind, categories, []);
}
