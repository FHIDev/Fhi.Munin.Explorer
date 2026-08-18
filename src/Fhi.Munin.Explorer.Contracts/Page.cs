using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>One page of results from a paged Explorer endpoint.</summary>
public sealed record Page<T>
{
    [JsonPropertyName("items")] public IReadOnlyList<T> Items { get; init; } = [];
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }

    /// <summary>
    /// The 1-based page this result is. Named <c>PageNumber</c> rather than <c>Page</c> because a
    /// member cannot share its enclosing type's name; the wire name is unchanged.
    /// </summary>
    [JsonPropertyName("page")] public int PageNumber { get; init; }

    [JsonPropertyName("size")] public int Size { get; init; }
    [JsonPropertyName("totalPages")] public int TotalPages { get; init; }
}
