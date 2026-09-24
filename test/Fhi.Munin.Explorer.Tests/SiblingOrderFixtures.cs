namespace Fhi.Munin.Explorer.Tests;

/// <summary>One delkilde or datasamling as <see cref="Contracts.SiblingOrder"/> sees it.</summary>
internal sealed record Sibling(bool IsDelkilde, string Name, Guid Id, int? DisplayOrder);

/// <summary>One parent's children in payload order, and the order a reader must see them in.</summary>
internal sealed record SiblingScope(
    IReadOnlyList<Sibling> Delkilder, IReadOnlyList<Sibling> Datasamlinger, IReadOnlyList<string> Expected);

/// <summary>
/// K_KK's waves and datasamlinger as the API's shared resolver ranks them, for every surface that
/// merges the two kinds (Fhi.Metadata-oty1h). Payload order is per kind, as the API sends it.
/// </summary>
internal static class SiblingOrderFixtures
{
    internal static readonly Guid Wave1 = new("4b000000-0000-0000-0000-000000000001");
    internal static readonly Guid Wave2 = new("4b000000-0000-0000-0000-000000000002");
    internal static readonly Guid Wave3 = new("4b000000-0000-0000-0000-000000000003");
    internal static readonly Guid Wave4 = new("4b000000-0000-0000-0000-000000000004");
    internal static readonly Guid Wave5 = new("4b000000-0000-0000-0000-000000000005");
    internal static readonly Guid AllWaves = new("4bd50000-0000-0000-0000-000000000001");
    internal static readonly Guid Death = new("4bd50000-0000-0000-0000-000000000002");
    internal static readonly Guid Cancer = new("4bd50000-0000-0000-0000-000000000003");

    private static readonly string[] SourceSequence =
        ["Wave 1", "Wave 2", "Wave 3", "Wave 4", "All waves - derived variables", "Death", "Cancer"];

    /// <summary>The kildemetadata workbook's own numbers for K_KK, sparse as the spreadsheet has them.</summary>
    internal static SiblingScope KildeKK() => new(
        [
            new(true, "Wave 1", Wave1, 2), new(true, "Wave 2", Wave2, 36),
            new(true, "Wave 3", Wave3, 49), new(true, "Wave 4", Wave4, 57)
        ],
        [
            new(false, "All waves - derived variables", AllWaves, 60), new(false, "Cancer", Cancer, 62),
            new(false, "Death", Death, 61)
        ],
        SourceSequence);

    /// <summary>A curator moved Cancer to the top; everything else keeps its imported order behind it.</summary>
    internal static SiblingScope ManualCancerFirst() => new(
        [
            new(true, "Wave 1", Wave1, 2), new(true, "Wave 2", Wave2, 3),
            new(true, "Wave 3", Wave3, 4), new(true, "Wave 4", Wave4, 5)
        ],
        [
            new(false, "All waves - derived variables", AllWaves, 6), new(false, "Cancer", Cancer, 1),
            new(false, "Death", Death, 7)
        ],
        ["Cancer", "Wave 1", "Wave 2", "Wave 3", "Wave 4", "All waves - derived variables", "Death"]);

    /// <summary>
    /// A reimport added Wave 5 after the manual ordering was saved: the resolver keeps the manual
    /// prefix and appends the newcomer, although the payload lists it among the other waves.
    /// </summary>
    internal static SiblingScope NewChildAfterManualPrefix() => new(
        [
            new(true, "Wave 1", Wave1, 2), new(true, "Wave 2", Wave2, 3), new(true, "Wave 3", Wave3, 4),
            new(true, "Wave 4", Wave4, 5), new(true, "Wave 5", Wave5, 8)
        ],
        [
            new(false, "All waves - derived variables", AllWaves, 6), new(false, "Cancer", Cancer, 1),
            new(false, "Death", Death, 7)
        ],
        ["Cancer", "Wave 1", "Wave 2", "Wave 3", "Wave 4", "All waves - derived variables", "Death", "Wave 5"]);

    /// <summary>The manual ordering was reset, so the ranks are the latest source sequence again, densified.</summary>
    internal static SiblingScope ResetToSource() => new(
        [
            new(true, "Wave 1", Wave1, 1), new(true, "Wave 2", Wave2, 2),
            new(true, "Wave 3", Wave3, 3), new(true, "Wave 4", Wave4, 4)
        ],
        [
            new(false, "All waves - derived variables", AllWaves, 5), new(false, "Cancer", Cancer, 7),
            new(false, "Death", Death, 6)
        ],
        SourceSequence);

    internal static SiblingScope Named(string name) => name switch
    {
        nameof(KildeKK) => KildeKK(),
        nameof(ManualCancerFirst) => ManualCancerFirst(),
        nameof(NewChildAfterManualPrefix) => NewChildAfterManualPrefix(),
        nameof(ResetToSource) => ResetToSource(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such sibling-order fixture.")
    };
}
