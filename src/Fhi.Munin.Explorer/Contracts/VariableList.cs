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
/// The id and the time it was added are all the list itself holds — the API stores no copy of the
/// variable's name, code or kilde, and does not join to the catalogue when it answers. A caller
/// showing more than a date therefore fetches the variables it wants to show, with
/// <see cref="IMuninExplorerClient.GetVariableAsync"/> or by searching for the ids. That is a
/// deliberate property of the store rather than an omission: a saved list survives a variable
/// being unpublished, and a stale copy of a name would be shown as though it were current.
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
}
