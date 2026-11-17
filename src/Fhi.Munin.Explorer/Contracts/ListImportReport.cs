using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// What an imported list's import left behind, as returned by
/// <c>GET /api/explorer/my/lists/{id}/import-report</c>: the rows that matched no variable, and the items whose
/// notes or desired data were left off. Hiding it sets <see cref="Dismissed"/>; the report stays readable.
/// </summary>
public sealed record ListImportReport
{
    /// <summary>The system the list came from, such as <c>helsedata-legacy</c>.</summary>
    [JsonPropertyName("importedFrom")] public string ImportedFrom { get; init; } = "";

    [JsonPropertyName("importedAt")] public DateTimeOffset? ImportedAt { get; init; }

    /// <summary>Whether the reader has hidden the report.</summary>
    [JsonPropertyName("dismissed")] public bool Dismissed { get; init; }

    /// <summary>The imported rows that matched no variable, which are not on the list.</summary>
    [JsonPropertyName("notFound")] public IReadOnlyList<ListImportNotFound> NotFound { get; init; } = [];

    /// <summary>Items on the list that came over without some of their fields.</summary>
    [JsonPropertyName("skipped")] public IReadOnlyList<ListImportSkipped> Skipped { get; init; } = [];
}

/// <summary>One imported row that matched no variable, with what the reader had written on it.</summary>
public sealed record ListImportNotFound
{
    /// <summary>The row's id in the report, which <see cref="IMuninExplorerClient.RemoveMyListImportEntryAsync"/> takes.</summary>
    [JsonPropertyName("entryId")] public Guid EntryId { get; init; }

    /// <summary>The row's place in the imported list, from 0.</summary>
    [JsonPropertyName("index")] public int Index { get; init; }

    /// <summary>
    /// Why it matched nothing: one of <see cref="ListImportStatuses"/>. A value this package does not know is a reason
    /// the API has added since.
    /// </summary>
    [JsonPropertyName("status")] public string Status { get; init; } = "";

    /// <summary>The variable code as imported, cut to 255 characters; null when the row had none.</summary>
    [JsonPropertyName("variableCode")] public string? VariableCode { get; init; }

    /// <summary>The datasamling code as imported, cut to 255 characters; null when the row had none.</summary>
    [JsonPropertyName("dataCollectionCode")] public string? DatasamlingCode { get; init; }

    /// <summary>The reader's note on the row, or null.</summary>
    [JsonPropertyName("notes")] public string? Notes { get; init; }

    /// <summary>The reader's desired data on the row, or null.</summary>
    [JsonPropertyName("desiredDataFreeText")] public string? DesiredDataFreeText { get; init; }

    /// <summary>The row's fields left off for Munin's limits: <see cref="ListImportFields"/>. Empty when none were.</summary>
    [JsonPropertyName("skipped")] public IReadOnlyList<string> Skipped { get; init; } = [];
}

/// <summary>An item on the list whose named fields the import left off; a later edit of the item does not clear it.</summary>
public sealed record ListImportSkipped
{
    /// <summary>The item, as <see cref="VariableListItem.ItemId"/> names it.</summary>
    [JsonPropertyName("itemId")] public Guid ItemId { get; init; }

    [JsonPropertyName("variabelId")] public Guid VariableId { get; init; }

    /// <summary>The variable's code; null from an API that does not send it.</summary>
    [JsonPropertyName("variableCode")] public string? VariableCode { get; init; }

    /// <summary>The item's datasamling code; null when the item has none, and from an API that does not send it.</summary>
    [JsonPropertyName("dataCollectionCode")] public string? DatasamlingCode { get; init; }

    /// <summary>The fields left off: <see cref="ListImportFields"/>.</summary>
    [JsonPropertyName("fields")] public IReadOnlyList<string> Fields { get; init; } = [];
}

/// <summary>Why an imported row matched no variable, as the API spells it.</summary>
public static class ListImportStatuses
{
    /// <summary>No variable has the code.</summary>
    public const string UnknownVariable = "unknownVariable";

    /// <summary>The variable is not in the named datasamling.</summary>
    public const string UnknownDatasamling = "unknownDatasamling";

    /// <summary>The code fits more than one variable.</summary>
    public const string Ambiguous = "ambiguous";
}

/// <summary>The fields an import can leave off, as the API spells them.</summary>
public static class ListImportFields
{
    /// <summary>The reader's note.</summary>
    public const string Notes = "notes";

    /// <summary>The reader's desired data.</summary>
    public const string DesiredData = "desiredData";
}
