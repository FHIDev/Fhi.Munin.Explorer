using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>One page of results from a paged Explorer endpoint.</summary>
public sealed record Side<T>
{
    [JsonPropertyName("items")] public IReadOnlyList<T> Items { get; init; } = [];
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("size")] public int Size { get; init; }
    [JsonPropertyName("totalPages")] public int TotalPages { get; init; }
}
