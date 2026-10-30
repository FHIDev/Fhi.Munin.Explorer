using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Who supplied a value a datasamling leaves blank: the nearest delkilde in the enabled chain above it, or else the
/// kilde. <see cref="InheritedValue"/> without the value, for fields the record already carries.
/// </summary>
public sealed record ValueSource
{
    /// <summary><c>"delkilde"</c> or <c>"kilde"</c>.</summary>
    [JsonPropertyName("sourceLevel")] public string SourceLevel { get; init; } = "";

    /// <summary>The id of the delkilde or kilde <see cref="SourceLevel"/> names.</summary>
    [JsonPropertyName("sourceId")] public Guid SourceId { get; init; }

    [JsonPropertyName("sourceName")] public string SourceName { get; init; } = "";

    /// <summary>The source's name in English; null when Munin has none, so the reader is shown <see cref="SourceName"/>.</summary>
    [JsonPropertyName("sourceNameEn")] public string? SourceNameEn { get; init; }
}
