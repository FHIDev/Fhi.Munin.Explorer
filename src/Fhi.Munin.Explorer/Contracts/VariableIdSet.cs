using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>Every variable id one search and filter match, unpaged, as one set.</summary>
public sealed record VariableIdSet
{
    /// <summary>Empty when <see cref="TooMany"/> is true: the API sends no partial set.</summary>
    [JsonPropertyName("ids")] public IReadOnlyList<Guid> Ids { get; init; } = [];

    /// <summary>The search matched more than <see cref="MaxIds"/> variables.</summary>
    [JsonPropertyName("tooMany")] public bool TooMany { get; init; }

    /// <summary>The most ids the API returns for one search.</summary>
    [JsonPropertyName("maxIds")] public int MaxIds { get; init; }
}
