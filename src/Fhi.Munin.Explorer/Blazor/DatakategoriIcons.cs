namespace Fhi.Munin.Explorer.Blazor;

/// <summary>One shape inside a glyph: an SVG element name and the attributes that draw it.</summary>
internal sealed record IconShape(string Element, IReadOnlyList<KeyValuePair<string, string>> Attributes);

/// <summary>
/// One glyph. <paramref name="Key"/> is what a stylesheet selects on and what
/// <see cref="Texts.DatakategoriNames"/> is keyed by.
/// </summary>
internal sealed record NodeIcon(string Key, IReadOnlyList<IconShape> Shapes);

/// <summary>
/// Datakategori to glyph, mirroring Kelda's <c>datakategoriIcons.ts</c> so the two surfaces cannot
/// disagree about what a category looks like or which order several of them come in.
/// </summary>
internal static class DatakategoriIcons
{
    /// <summary>The vocabulary's own catch-all, which an authored value produces and absence does not.</summary>
    internal const string Other = "other";

    /// <summary>The grouping levels' glyph — a folder is not a datakategori and never resolves as one.</summary>
    internal const string Grouping = "kilde";

    // The EHDS v7 Article 51 order Kelda's DATAKATEGORI_VALUES declares: registries, research,
    // health services, biological, public health, then the catch-all. Declaration order is the
    // render order, so the same set of categories draws identically whatever order it arrived in.
    private static readonly string[] CanonicalOrder =
    [
        "PHDR", "MRMR", "RMMD", "HPML", "RPDG", "RQSH", "NRPE", "EHRS", "HRAD",
        "EHCT", "EINS", "HGPD", "PGEH", "IDHP", "DIOH", "EMRD", "WELA", Other
    ];

    /// <summary>The keys the legend and <see cref="Texts.DatakategoriNames"/> cover, in render order.</summary>
    internal static IReadOnlyList<string> Order => CanonicalOrder;

    private const string Prefix = "ehds-cat:";

    // The six retired ehds-cat: slugs on their v7 successor, mirroring Munin's seed revision 0008
    // verbatim. A read model built before that crosswalk ran still ships them, and without this a
    // biobank would resolve to the catch-all and read as "Annet".
    private static readonly (string Token, string Successor)[] LegacyAliases =
    [
        ("health-registries", "PHDR"),
        ("registries-quality-of-healthcare", "MRMR"),
        ("population-health-surveys", "RPDG"),
        ("biobanks", "EINS"),
        ("provesamling", "EINS"),
        ("biodata", "HGPD")
    ];

    private static readonly Dictionary<string, string> ByToken = BuildTokens();

    private static Dictionary<string, string> BuildTokens()
    {
        // Both the bare and the ehds-cat: form of every token, because the catalogue authors both.
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in CanonicalOrder)
        {
            map[key] = key;
            map[Prefix + key] = key;
        }
        foreach (var (token, successor) in LegacyAliases)
        {
            map[token] = successor;
            map[Prefix + token] = successor;
        }
        return map;
    }

    /// <summary>
    /// The canonical key for one raw token, or null when this is not a category we draw. Matched
    /// whole: <c>snomed:other</c> is a foreign vocabulary's term and not this one's catch-all.
    /// </summary>
    internal static string? Resolve(string? token) =>
        token is not null && ByToken.TryGetValue(token.Trim(), out var key) ? key : null;

    /// <summary>
    /// The glyphs for a datasamling's datakategorier — one per distinct category, deduplicated and
    /// in <see cref="Order"/>. No categories yields none: absence is not <see cref="Other"/>, which
    /// only an authored value produces. An unrecognised token does fall back to it, so a
    /// categorised datasamling never reads as uncategorised.
    /// </summary>
    internal static IReadOnlyList<NodeIcon> For(IEnumerable<string>? categories)
    {
        if (categories is null)
        {
            return [];
        }

        var resolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in categories)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                resolved.Add(Resolve(token) ?? Other);
            }
        }

        return resolved.Count == 0
            ? []
            : [.. CanonicalOrder.Where(resolved.Contains).Select(key => Glyphs[key])];
    }

    /// <summary>The muted folder the grouping levels wear. Deliberately outside <see cref="For"/>.</summary>
    internal static NodeIcon Folder => Glyphs[Grouping];

    private static IconShape Path(string d) => new("path", [new("d", d)]);

    private static IconShape Circle(string cx, string cy, string r, string? fill = null) =>
        new("circle", fill is null
            ? [new("cx", cx), new("cy", cy), new("r", r)]
            : [new("cx", cx), new("cy", cy), new("r", r), new("fill", fill)]);

    private static IconShape Ellipse(string cx, string cy, string rx, string ry) =>
        new("ellipse", [new("cx", cx), new("cy", cy), new("rx", rx), new("ry", ry)]);

    private static IconShape Rect(string x, string y, string width, string height, string radius) =>
        new("rect", [new("x", x), new("y", y), new("width", width), new("height", height),
            new("rx", radius), new("ry", radius)]);

    // Geometry copied from lucide, the icon set Kelda draws these same categories with, so both
    // surfaces show one shape per category. Copied rather than depended on because this package
    // ships no asset bundle; the licences it arrives under are in README.md.
    private static readonly Dictionary<string, NodeIcon> Glyphs = new(StringComparer.Ordinal)
    {
        [Grouping] = new(Grouping,
        [
            Path("M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z")
        ]),
        ["PHDR"] = new("PHDR",
        [
            Ellipse("12", "5", "9", "3"),
            Path("M3 5V19A9 3 0 0 0 21 19V5"),
            Path("M3 12A9 3 0 0 0 21 12")
        ]),
        ["MRMR"] = new("MRMR",
        [
            Path("M2 9.5a5.5 5.5 0 0 1 9.591-3.676.56.56 0 0 0 .818 0A5.49 5.49 0 0 1 22 9.5c0 2.29-1.5 4-3 5.5l-5.492 5.313a2 2 0 0 1-3 .019L5 15c-1.5-1.5-3-3.2-3-5.5"),
            Path("M3.22 13H9.5l.5-1 2 4.5 2-7 1.5 3.5h5.27")
        ]),
        ["RMMD"] = new("RMMD",
        [
            Path("m10.5 20.5 10-10a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z"),
            Path("m8.5 8.5 7 7")
        ]),
        ["HPML"] = new("HPML",
        [
            Path("M3.85 8.62a4 4 0 0 1 4.78-4.77 4 4 0 0 1 6.74 0 4 4 0 0 1 4.78 4.78 4 4 0 0 1 0 6.74 4 4 0 0 1-4.77 4.78 4 4 0 0 1-6.75 0 4 4 0 0 1-4.78-4.77 4 4 0 0 1 0-6.76Z"),
            Path("m16 9-5.5 5.5L8 12")
        ]),
        ["RPDG"] = new("RPDG",
        [
            Path("M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"),
            Path("M16 3.128a4 4 0 0 1 0 7.744"),
            Path("M22 21v-2a4 4 0 0 0-3-3.87"),
            Circle("9", "7", "4")
        ]),
        ["RQSH"] = new("RQSH",
        [
            Rect("8", "2", "8", "4", "1"),
            Path("M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"),
            Path("M12 11h4"),
            Path("M12 16h4"),
            Path("M8 11h.01"),
            Path("M8 16h.01")
        ]),
        ["NRPE"] = new("NRPE",
        [
            Path("M12 5v16"),
            Path("M20.001 19A2 2 0 0022 17V5a2 2 0 00-1.999-2L16 3.002A5 5 0 0012 5a5 5 0 00-4-2H4a2 2 0 00-2 2v12a2 2 0 001.999 2H8a5 5 0 014 2 5 5 0 014-2z")
        ]),
        ["EHRS"] = new("EHRS",
        [
            Path("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z"),
            Path("M14 2v5a1 1 0 0 0 1 1h5"),
            Path("M10 9H8"),
            Path("M16 13H8"),
            Path("M16 17H8")
        ]),
        ["HRAD"] = new("HRAD",
        [
            Path("M12 17V7"),
            Path("M16 8h-6a2 2 0 0 0 0 4h4a2 2 0 0 1 0 4H8"),
            Path("M4 3a1 1 0 0 1 1-1 1.3 1.3 0 0 1 .7.2l.933.6a1.3 1.3 0 0 0 1.4 0l.934-.6a1.3 1.3 0 0 1 1.4 0l.933.6a1.3 1.3 0 0 0 1.4 0l.933-.6a1.3 1.3 0 0 1 1.4 0l.934.6a1.3 1.3 0 0 0 1.4 0l.933-.6A1.3 1.3 0 0 1 19 2a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1 1.3 1.3 0 0 1-.7-.2l-.933-.6a1.3 1.3 0 0 0-1.4 0l-.934.6a1.3 1.3 0 0 1-1.4 0l-.933-.6a1.3 1.3 0 0 0-1.4 0l-.933.6a1.3 1.3 0 0 1-1.4 0l-.934-.6a1.3 1.3 0 0 0-1.4 0l-.933.6a1.3 1.3 0 0 1-.7.2 1 1 0 0 1-1-1z")
        ]),
        ["EHCT"] = new("EHCT",
        [
            Path("M13.744 17.736a6 6 0 1 1-7.48-7.48"),
            Path("M15 6h1v4"),
            Path("m6.134 14.768.866-.5 2 3.464"),
            Circle("16", "8", "6")
        ]),
        ["EINS"] = new("EINS",
        [
            Path("M14 2v6a2 2 0 0 0 .245.96l5.51 10.08A2 2 0 0 1 18 22H6a2 2 0 0 1-1.755-2.96l5.51-10.08A2 2 0 0 0 10 8V2"),
            Path("M6.453 15h11.094"),
            Path("M8.5 2h7")
        ]),
        ["HGPD"] = new("HGPD",
        [
            Path("m10 16 1.5 1.5"),
            Path("m14 8-1.5-1.5"),
            Path("M15 2c-1.798 1.998-2.518 3.995-2.807 5.993"),
            Path("m16.5 10.5 1 1"),
            Path("m17 6-2.891-2.891"),
            Path("M2 15c6.667-6 13.333 0 20-6"),
            Path("m20 9 .891.891"),
            Path("M3.109 14.109 4 15"),
            Path("m6.5 12.5 1 1"),
            Path("m7 18 2.891 2.891"),
            Path("M9 22c1.798-1.998 2.518-3.995 2.807-5.993")
        ]),
        ["PGEH"] = new("PGEH",
        [
            Path("M6 18h8"),
            Path("M3 22h18"),
            Path("M14 22a7 7 0 1 0 0-14h-1"),
            Path("M9 14h2"),
            Path("M9 12a2 2 0 0 1-2-2V6h6v4a2 2 0 0 1-2 2Z"),
            Path("M12 6V3a1 1 0 0 0-1-1H9a1 1 0 0 0-1 1v3")
        ]),
        ["IDHP"] = new("IDHP",
        [
            Path("M12 20v-9"),
            Path("M14 7a4 4 0 0 1 4 4v3a6 6 0 0 1-12 0v-3a4 4 0 0 1 4-4z"),
            Path("M14.12 3.88 16 2"),
            Path("M21 21a4 4 0 0 0-3.81-4"),
            Path("M21 5a4 4 0 0 1-3.55 3.97"),
            Path("M22 13h-4"),
            Path("M3 21a4 4 0 0 1 3.81-4"),
            Path("M3 5a4 4 0 0 0 3.55 3.97"),
            Path("M6 13H2"),
            Path("m8 2 1.88 1.88"),
            Path("M9 7.13V6a3 3 0 1 1 6 0v1.13")
        ]),
        ["DIOH"] = new("DIOH",
        [
            Path("M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.48 19 2c1 2 2 4.18 2 8 0 5.5-4.78 10-10 10Z"),
            Path("M2 21c0-3 1.85-5.36 5.08-6C9.5 14.52 12 13 13 12")
        ]),
        ["EMRD"] = new("EMRD",
        [
            Path("M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2")
        ]),
        ["WELA"] = new("WELA",
        [
            Rect("5", "2", "14", "20", "2"),
            Path("M12 18h.01")
        ]),
        [Other] = new(Other,
        [
            Path("M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z"),
            Circle("7.5", "7.5", ".5", "currentColor")
        ])
    };
}
