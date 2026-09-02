namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The comparison behind <see cref="FixtureDriftTest"/>, proved offline.
/// </summary>
/// <remarks>
/// The live half runs once a night on a devbox nobody watches, so what it does on a payload that
/// has really drifted has to be pinned somewhere that runs on every commit. These are those pins,
/// plus the guard that the fixture directory has not grown a file no check ever looks at.
/// <para>
/// What the two directory guards prove is that every file is accounted for, not that every name
/// in <see cref="Fixture.CheckedLive"/> reaches an <c>[LiveApiFact]</c>. Adding a name without its
/// test would satisfy both. They are declared beside each other in one file to keep that visible.
/// </para>
/// </remarks>
public class FixtureFreshnessTest
{
    [Fact]
    public void Against_WhenTheApiGrowsAField_ThenTheFixtureIsReportedStale()
    {
        var findings = FixtureFreshness.Against(
            """{"id":"1","navn":"Kilde","healthDcatScore":42}""",
            """{"id":"1","navn":"Kilde"}""");

        var finding = Assert.Single(findings);

        Assert.Contains("$.healthDcatScore", finding, StringComparison.Ordinal);
        Assert.Contains("42", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void Against_WhenTheApiRenamesAField_ThenTheNewNameIsReported()
    {
        var findings = FixtureFreshness.Against(
            """{"kortNavn":"MFR"}""",
            """{"shortName":"MFR"}""");

        // The withdrawn half is deliberately silent, so a rename reads as one finding, not two.
        var finding = Assert.Single(findings);

        Assert.Contains("$.kortNavn", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void Against_WhenTheFixtureMatches_ThenNothingIsReported() =>
        Assert.Empty(FixtureFreshness.Against(
            """{"id":"1","delkilder":[{"navn":"A"}]}""",
            """{"id":"2","delkilder":[{"navn":"B"},{"navn":"C"}]}"""));

    [Fact]
    public void Against_WhenAnArrayElementCarriesANewField_ThenItIsReportedOnce() =>
        // One finding for sixty elements: the path collapses to "[]" so a field missing from all of
        // them is one line rather than sixty.
        Assert.Single(FixtureFreshness.Against(
            """{"items":[{"id":1,"score":9},{"id":2,"score":8},{"id":3,"score":7}]}""",
            """{"items":[{"id":1},{"id":2}]}"""));

    [Fact]
    public void Against_WhenTheFixtureHasFieldsTheLiveCallDidNotFill_ThenNothingIsReported() =>
        // The other direction is data, not staleness: the fixture captured one entity and the live
        // call fetched another, so a key empty today says nothing about the contract.
        Assert.Empty(FixtureFreshness.Against(
            """{"navn":"Kilde","delkilder":[]}""",
            """{"navn":"Kilde","delkilder":[{"navn":"A"}],"kortNavn":"K"}"""));

    [Fact]
    public void Against_WhenTheFixtureIsEmptyWhereTheApiIsNot_ThenItsInteriorIsNotCompared() =>
        // A kilde without delkilder is a kilde without delkilder. Reporting every key inside the
        // live ones would make the nightly job fire on data, which is a job nobody reads.
        Assert.Empty(FixtureFreshness.Against(
            """{"delkilder":[{"navn":"A","kortNavn":"B"}]}""",
            """{"delkilder":[]}"""));

    [Fact]
    public void Against_WhenTheApiSendsNullOrEmpty_ThenThereIsNothingToBeStaleAbout() =>
        Assert.Empty(FixtureFreshness.Against(
            """{"navn":"Kilde","additionalProperties":null,"delkilder":[],"meta":{}}""",
            """{"navn":"Kilde"}"""));

    [Fact]
    public void Against_WhenAnOpenBagCarriesDifferentKeys_ThenNothingIsReported() =>
        // additionalProperties is curated per-entity metadata: one kilde carries NavnEngelsk and the
        // next does not. Its keys are the catalogue, not the API, and reporting them made every
        // kilde fixture fail on the first live run.
        Assert.Empty(FixtureFreshness.Against(
            """{"navn":"A","additionalProperties":{"NavnEngelsk":"The Tromsø Study","Opprettet":"1974"}}""",
            """{"navn":"B","additionalProperties":{"Kontaktperson":"Nobody"}}"""));

    [Fact]
    public void Against_WhenATranslationMapLosesALanguage_ThenItIsReported() =>
        // Only additionalProperties is opaque, not every dictionary the contracts declare. A
        // translation map is keyed by language code, a closed vocabulary, and a capture that
        // predates the English half is the staleness this exists to catch.
        Assert.Single(FixtureFreshness.Against(
            """{"key":"NavnEngelsk","displayNameTranslations":{"no":"Navn","en":"Name"}}""",
            """{"key":"NavnEngelsk","displayNameTranslations":{"no":"Navn"}}"""));

    [Fact]
    public void Against_WhenAnOpenBagIsMissingEntirely_ThenItIsStillReported() =>
        // Opaque inside, not invisible: the bag itself is a field the contracts declare, so a
        // fixture that predates it is still stale.
        Assert.Single(FixtureFreshness.Against(
            """{"navn":"A","additionalProperties":{"NavnEngelsk":"x"}}""",
            """{"navn":"B"}"""));

    [Fact]
    public void CarriesAnything_WhenTheApiAnswersWithNothing_ThenItSaysSo()
    {
        Assert.False(FixtureFreshness.CarriesAnything("[]"));
        Assert.False(FixtureFreshness.CarriesAnything("null"));
        Assert.True(FixtureFreshness.CarriesAnything("""[{"id":1}]"""));
    }

    [Fact]
    public void EveryFixture_IsEitherCheckedAgainstTheLiveApiOrRecordedAsOutOfReach()
    {
        // The guard that can see what is missing. Without it a new fixture joins the two gates that
        // read this directory and nothing ever asks whether it still matches the API.
        var accounted = Fixture.CheckedLive.Concat(Fixture.OutOfReach).ToHashSet(StringComparer.Ordinal);

        var unaccounted = TestData.Names().Where(name => !accounted.Contains(name)).Order(StringComparer.Ordinal);

        Assert.True(
            !unaccounted.Any(),
            $"Testdata/ holds fixtures no freshness check knows about: {string.Join(", ", unaccounted)}. " +
            $"Add each to {nameof(FixtureDriftTest)} with the live call that re-fetches it, or to " +
            $"{nameof(Fixture)}.{nameof(Fixture.OutOfReach)} with the reason it cannot be fetched.");
    }

    [Fact]
    public void EveryFixtureNamed_IsAFileThatExists()
    {
        // The same guard read backwards: a renamed file would otherwise leave a name in the list
        // that no longer points at anything, and the live check would fail for the wrong reason.
        var present = TestData.Names().ToHashSet(StringComparer.Ordinal);

        var missing = Fixture.CheckedLive.Concat(Fixture.OutOfReach)
            .Where(name => !present.Contains(name))
            .Order(StringComparer.Ordinal);

        Assert.True(!missing.Any(), $"Named as fixtures but not present under Testdata/: {string.Join(", ", missing)}.");
    }
}
