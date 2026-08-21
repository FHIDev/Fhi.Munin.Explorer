using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// The code values behind one kodeverk link, as returned by
/// <c>GET /api/explorer/variables/{id}/kodeverk/{type}/{reference}/codes</c>.
/// </summary>
/// <remarks>
/// The envelope repeats the type and the reference the call asked for. Redundant to a caller that
/// already holds the <see cref="KodeverkLink"/> — and kept anyway, because dropping a field the API
/// sends is what <c>ContractCoverageTest</c> exists to catch, and a payload that names itself can be
/// matched against the link it was fetched for rather than trusted to be the right one.
/// <para>
/// Not part of <see cref="VariableDetail"/>: a kodeverk can run to hundreds of codes — Kommunenummer
/// is 885 — and most readers never open one. The endpoint is asked only when a reader says so.
/// </para>
/// </remarks>
public sealed record KodeverkCodes
{
    /// <summary>The kind of link these codes came from — the same token <see cref="KodeverkLink.KodeverkType"/> carries.</summary>
    [JsonPropertyName("kodeverkType")] public string KodeverkType { get; init; } = "";

    /// <summary>The reference within that type, echoed back from the request.</summary>
    [JsonPropertyName("kodeverkReference")] public string KodeverkReference { get; init; } = "";

    /// <summary>The codes themselves, in the order the catalogue returns them. Empty when there are none.</summary>
    [JsonPropertyName("koder")] public IReadOnlyList<KodeverkCode> Codes { get; init; } = [];
}

/// <summary>One code value in a kodeverk.</summary>
public sealed record KodeverkCode
{
    /// <summary>The value as it is stored in the data, e.g. <c>"0101"</c>.</summary>
    [JsonPropertyName("verdi")] public string Value { get; init; } = "";

    /// <summary>What the value means, e.g. <c>"Halden"</c>.</summary>
    [JsonPropertyName("navn")] public string Name { get; init; } = "";

    /// <summary>When the code took effect. Null for a kodeverk that records no start dates.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>When it stopped applying; null while it still does.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }
}
