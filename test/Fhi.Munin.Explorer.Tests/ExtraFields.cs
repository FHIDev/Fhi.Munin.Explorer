using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Munin's other fields on a variable: one with a value, one blank, one absent, and one the tab already shows.</summary>
internal static class ExtraFields
{
    private static PropertyMetadataEntry Field(string key, string name, string type, int order) => new()
    {
        Key = key,
        Type = type,
        SortOrder = order,
        DisplayNameTranslations = new Dictionary<string, string> { ["no"] = name },
    };

    public static IReadOnlyList<PropertyMetadataEntry> Metadata { get; } =
    [
        Field("DataType", "Datatype", "String", 4004),
        Field("MaaleEnhet", "Måleenhet", "String", 9012),
        Field("Presisjon", "Presisjon", "Number", 9018),
        Field("KommentarEngelsk", "Kommentar på engelsk", "Text", 9009),
    ];

    public static IReadOnlyDictionary<string, string?> Values { get; } = new Dictionary<string, string?>
    {
        ["DataType"] = "1",
        ["MaaleEnhet"] = "kilo",
        ["Presisjon"] = " ",
    };

    public static VariableDetail On(VariableDetail detail) =>
        detail with { PropertyMetadata = Metadata, AdditionalProperties = Values };

    /// <summary>The labels in a tab panel, in order.</summary>
    public static List<string> Labels(AngleSharp.Dom.IElement panel) =>
        [.. panel.QuerySelectorAll("dt").Select(t => t.TextContent.Trim())];
}
