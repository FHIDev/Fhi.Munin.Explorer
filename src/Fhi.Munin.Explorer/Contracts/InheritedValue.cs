using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// An inherited kilde field a delkilde or datasamling leaves blank, and who supplied it: the nearest delkilde in the
/// enabled chain above it, or else the kilde.
/// </summary>
public sealed record InheritedValue
{
    [JsonPropertyName("value")] public string Value { get; init; } = "";

    /// <summary><c>"delkilde"</c> or <c>"kilde"</c>.</summary>
    [JsonPropertyName("sourceLevel")] public string SourceLevel { get; init; } = "";

    [JsonPropertyName("sourceId")] public Guid SourceId { get; init; }

    [JsonPropertyName("sourceName")] public string SourceName { get; init; } = "";

    /// <summary>The source's name in English; null when Munin has none, so the reader is shown <see cref="SourceName"/>.</summary>
    [JsonPropertyName("sourceNameEn")] public string? SourceNameEn { get; init; }
}
