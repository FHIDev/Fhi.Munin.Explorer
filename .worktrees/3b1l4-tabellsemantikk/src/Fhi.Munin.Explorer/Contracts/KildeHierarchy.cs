using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// The navigation tree for one kilde, as returned by
/// <c>GET /api/explorer/kilder/{id}/hierarchy</c>.
/// </summary>
/// <remarks>
/// Deliberately thinner than <see cref="KildeDetail"/>: ids, names and counts only, because this
/// is what a filter tree needs and the detail payload is an order of magnitude larger. Fetch this
/// to draw the tree, and the detail endpoint when the user opens something.
/// </remarks>
public sealed record KildeHierarchy
{
    [JsonPropertyName("kildeId")] public Guid KildeId { get; init; }
    [JsonPropertyName("kildeName")] public string KildeName { get; init; } = "";

    /// <summary>Visible published variables under the whole kilde.</summary>
    [JsonPropertyName("totalVariableCount")] public int TotalVariableCount { get; init; }

    [JsonPropertyName("delkilder")] public IReadOnlyList<HierarchyDelkilde> Delkilder { get; init; } = [];

    /// <summary>Datasamlinger that belong to the kilde itself rather than to any delkilde.</summary>
    [JsonPropertyName("directDatasamlinger")] public IReadOnlyList<HierarchyDatasamling> DirectDatasamlinger { get; init; } = [];
}

/// <summary>A delkilde node in the tree.</summary>
public sealed record HierarchyDelkilde
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>Visible published variables under this delkilde, including its children.</summary>
    [JsonPropertyName("variableCount")] public int VariableCount { get; init; }

    [JsonPropertyName("datasamlinger")] public IReadOnlyList<HierarchyDatasamling> Datasamlinger { get; init; } = [];

    /// <summary>
    /// Variabelgrupper under the delkilde that are not tied to any of its datasamlinger. They
    /// would otherwise be invisible in a tree drawn datasamling-first.
    /// </summary>
    [JsonPropertyName("unassignedVariabelgrupper")] public IReadOnlyList<HierarchyVariabelgruppe> UnassignedVariabelgrupper { get; init; } = [];

    /// <summary>Nested delkilder — walk recursively.</summary>
    [JsonPropertyName("children")] public IReadOnlyList<HierarchyDelkilde> Children { get; init; } = [];

    /// <summary>Curated display order; null when unordered.</summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }
}

/// <summary>A datasamling node in the tree.</summary>
public sealed record HierarchyDatasamling
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("variableCount")] public int VariableCount { get; init; }

    /// <summary>Top-level variabelgrupper; each carries its own children.</summary>
    [JsonPropertyName("variabelgrupper")] public IReadOnlyList<HierarchyVariabelgruppe> Variabelgrupper { get; init; } = [];

    /// <summary>Curated display order; null when unordered.</summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }

    /// <summary>
    /// Datakategori tokens for the datasamling, normally EHDS CURIEs such as
    /// <c>ehds-cat:population-health-surveys</c>. A token authored with another prefix is passed
    /// through unchanged, so match whole tokens rather than on the <c>ehds-cat:</c> prefix. Empty
    /// means no category — it does not mean <c>ehds-cat:other</c>, which only an explicitly
    /// authored value produces.
    /// </summary>
    [JsonPropertyName("categories")] public IReadOnlyList<string> Categories { get; init; } = [];
}

/// <summary>A variabelgruppe node in the tree.</summary>
public sealed record HierarchyVariabelgruppe
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("variableCount")] public int VariableCount { get; init; }

    /// <summary>Nested groups — walk recursively.</summary>
    [JsonPropertyName("childVariabelgrupper")] public IReadOnlyList<HierarchyVariabelgruppe> ChildVariabelgrupper { get; init; } = [];
}
