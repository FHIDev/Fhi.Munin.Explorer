using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// One of the signed-in user's saved variable lists, as returned by
/// <c>GET /api/explorer/my/lists</c>.
/// </summary>
/// <remarks>
/// The owner is not part of the payload and cannot be: every <c>my/lists</c> endpoint resolves the
/// user from the token and answers only for that user's lists, so a list that comes back is by
/// construction the caller's own. A list belonging to somebody else is answered as
/// <c>404 Not Found</c> rather than as a refusal, which is what keeps a caller from probing for
/// which list ids exist.
/// </remarks>
public sealed record VariableList
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>The name the user gave the list. The API trims it and holds it to 200 characters.</summary>
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change to the list itself or to what is in it.</summary>
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// One variable in a saved list, as returned by <c>GET /api/explorer/my/lists/{id}/variables</c>.
/// </summary>
/// <remarks>
/// <para>
/// The list itself still stores only the id and the time it was added — a saved list survives a
/// variable being unpublished, and a stored copy of a name would be shown as though it were
/// current. The display fields below are not stored: the API resolves them from the read model as
/// it answers, for the page it is answering with.
/// </para>
/// <para>
/// They are all optional and may be <see langword="null"/> together, which means that id has no row
/// in the read model — retracted, unpublished, or not yet projected. Such an entry is still
/// returned rather than dropped, so the paging totals stay honest, and a caller decides what to
/// draw for it.
/// </para>
/// </remarks>
public sealed record VariableListItem
{
    /// <summary>
    /// The variable this entry points at. Spelled <c>variabelId</c> on the wire, which is why the
    /// property carries an explicit name rather than relying on the default.
    /// </summary>
    [JsonPropertyName("variabelId")] public Guid VariableId { get; init; }

    /// <summary>When the variable was put in the list.</summary>
    [JsonPropertyName("addedAt")] public DateTimeOffset AddedAt { get; init; }

    // The display fields, resolved by the API as it answers. Named as they are on the wire, which
    // keeps the Norwegian stem the rest of this contract already uses — variabelId, not variableId.

    [JsonPropertyName("variabelCode")] public string? VariableCode { get; init; }

    [JsonPropertyName("variabelName")] public string? VariableName { get; init; }

    [JsonPropertyName("kildeId")] public Guid? KildeId { get; init; }

    [JsonPropertyName("kildeName")] public string? KildeName { get; init; }

    [JsonPropertyName("kildeKortNavn")] public string? KildeShortName { get; init; }

    [JsonPropertyName("datasamlingName")] public string? DatasamlingName { get; init; }

    [JsonPropertyName("variabelgruppeName")] public string? VariabelgruppeName { get; init; }

    [JsonPropertyName("dataType")] public string? DataType { get; init; }

    [JsonPropertyName("dataFrom")] public DateTimeOffset? DataFrom { get; init; }

    [JsonPropertyName("dataTo")] public DateTimeOffset? DataTo { get; init; }

    /// <summary>
    /// Spelled as a string rather than an enum, the same way <see cref="VariableSummary.VersionStatus"/>
    /// is: the package deserialises with <c>JsonSerializerDefaults.Web</c>, which carries no
    /// string-enum converter, so an enum here would need one registered by every host.
    /// </summary>
    [JsonPropertyName("versjonStatus")] public string? VersionStatus { get; init; }
}
