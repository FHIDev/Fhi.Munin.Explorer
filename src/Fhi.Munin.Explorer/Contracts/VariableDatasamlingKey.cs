using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// One variable as delivered from one datasamling: a result row's identity, and what a saved list holds.
/// </summary>
/// <remarks>
/// Two rows of the same variable from two datasamlinger are two different database fields to the
/// researcher who receives them, so a list saves the pair and not the variable alone.
/// </remarks>
/// <param name="VariableId">The variable.</param>
/// <param name="DatasamlingId">
/// The datasamling. Null for a variable in none, and for a saved item whose datasamling is not chosen.
/// </param>
public readonly record struct VariableDatasamlingKey(
    [property: JsonPropertyName("variabelId")] Guid VariableId,
    [property: JsonPropertyName("datasamlingId")] Guid? DatasamlingId)
{
    /// <summary>The row a search result shows.</summary>
    public static VariableDatasamlingKey Of(VariableSummary row) => new(row.Id, row.DatasamlingId);

    /// <summary>The item a saved list holds.</summary>
    public static VariableDatasamlingKey Of(VariableListItem item) => new(item.VariableId, item.DatasamlingId);
}

/// <summary>Every (variable, datasamling) row one search and filter match, unpaged, as one set.</summary>
public sealed record VariableRowSet
{
    /// <summary>Empty when <see cref="TooMany"/> is true: the API sends no partial set.</summary>
    [JsonPropertyName("rader")] public IReadOnlyList<VariableDatasamlingKey> Rows { get; init; } = [];

    /// <summary>The search matched more than <see cref="MaxRows"/> rows.</summary>
    [JsonPropertyName("tooMany")] public bool TooMany { get; init; }

    /// <summary>The most rows the API returns for one search.</summary>
    [JsonPropertyName("maxRader")] public int MaxRows { get; init; }
}
