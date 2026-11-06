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

/// <summary>
/// One page of a variable's code list from the search or the tree:
/// <c>GET …/codes/search?q=</c> and <c>GET …/codes/children?node=</c>.
/// </summary>
public sealed record KodeverkCodePage
{
    [JsonPropertyName("kodeverkType")] public string KodeverkType { get; init; } = "";

    [JsonPropertyName("kodeverkReference")] public string KodeverkReference { get; init; } = "";

    /// <summary>How many codes match in all, across every page.</summary>
    [JsonPropertyName("totalt")] public int Total { get; init; }

    /// <summary>How many codes the whole list has, so a short list can be told from a long one without fetching it; 0 from an API that does not send it.</summary>
    [JsonPropertyName("antallKoder")] public int CodeCount { get; init; }

    [JsonPropertyName("skip")] public int Skip { get; init; }

    [JsonPropertyName("take")] public int Take { get; init; }

    [JsonPropertyName("koder")] public IReadOnlyList<KodeverkCodeHit> Codes { get; init; } = [];
}

/// <summary>A code in a search or tree page, with whether codes sit under it.</summary>
public sealed record KodeverkCodeHit
{
    [JsonPropertyName("verdi")] public string Value { get; init; } = "";

    [JsonPropertyName("navn")] public string Name { get; init; } = "";

    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>When it stopped applying; null while it still does. A code with an end date can still be chosen.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    /// <summary>True when the code is a node in the tree: choosing it with everything under it, and the children call, apply.</summary>
    [JsonPropertyName("harUnderkoder")] public bool HasChildren { get; init; }

    /// <summary>How many codes it covers with everything under it; null for a code with nothing under it, and from an older API.</summary>
    [JsonPropertyName("antall")] public int? Count { get; init; }

    /// <summary>The groups and codes above it, top first; empty at the top level and from an older API.</summary>
    [JsonPropertyName("foreldre")] public IReadOnlyList<string> Ancestors { get; init; } = [];

    /// <summary>True for an ICD-10 chapter or block, which can only be chosen with everything under it.</summary>
    [JsonPropertyName("gruppe")] public bool IsGroup { get; init; }

    /// <summary>True only when the code and every code under it have expired, unlike <see cref="ValidTo"/> alone.</summary>
    [JsonPropertyName("utgatt")] public bool Expired { get; init; }
}

/// <summary>The answer of <c>GET …/codes/count</c>: how many codes one choice covers.</summary>
public sealed record KodeverkCodeCount
{
    [JsonPropertyName("antall")] public int Count { get; init; }
}
