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

    [JsonPropertyName("createdAt")] public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last change to the list itself or to what is in it.</summary>
    [JsonPropertyName("updatedAt")] public DateTimeOffset? UpdatedAt { get; init; }
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
    [JsonPropertyName("addedAt")] public DateTimeOffset? AddedAt { get; init; }

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

    // The reader's own annotation — "Ønskede data", what they want out of this variable. Stored on
    // the membership rather than resolved per page, so unlike the display fields above it survives
    // the variable leaving the catalogue: an orphaned entry still carries what its reader wrote.

    /// <summary>
    /// Which shape the annotation takes, or <see langword="null"/> when the reader has written
    /// none. <c>freeText</c> is the only value the API stores today; a value this package does not
    /// recognise means the API has grown a second shape and is not free text.
    /// </summary>
    [JsonPropertyName("desiredDataType")] public string? DesiredDataType { get; init; }

    /// <summary>
    /// The reader's own words, or <see langword="null"/> when they have written none. The API
    /// trims it and holds it to 500 characters — see
    /// <see cref="IMuninExplorerClient.SetMyListDesiredDataAsync"/> for what happens to a longer one.
    /// </summary>
    [JsonPropertyName("desiredDataFreeText")] public string? DesiredDataFreeText { get; init; }
}

/// <summary>
/// How the API answered a write of one variable's "Ønskede data" annotation.
/// </summary>
public enum DesiredDataOutcome
{
    /// <summary>Written, or cleared. What the reader sent is what the API now holds.</summary>
    Saved = 0,

    /// <summary>
    /// The signed-in user has no such list, or that list does not hold that variable. The API
    /// answers both as <c>404</c> and deliberately does not tell them apart.
    /// </summary>
    NotFound,

    /// <summary>
    /// The API refused the text itself. <see cref="DesiredDataResult.MaxLength"/> carries the
    /// ceiling when the refusal named one, which is the only refusal this client can provoke.
    /// </summary>
    Refused
}

/// <summary>
/// The answer to <see cref="IMuninExplorerClient.SetMyListDesiredDataAsync"/>.
/// </summary>
/// <remarks>
/// A result rather than a <c>bool</c> plus a throw, which is what the other list writes use, and
/// the difference is deliberate: this endpoint refuses text the reader typed, and a caller that
/// cannot tell that from a fault has nothing to put on screen but "try again" — which is advice
/// for a reader whose text will be refused identically every time. The ceiling travels with the
/// refusal so the caller can name it without writing the number down; the API is the authority on
/// what it is, and a constant here would drift the day the API moved it.
/// <para>
/// A <c>429</c> is not one of these outcomes and never arrives as one: it is thrown as
/// <see cref="MuninExplorerRateLimitedException"/>, the same as every other write. So is any other
/// fault.
/// </para>
/// </remarks>
/// <param name="Outcome">What the API did.</param>
/// <param name="MaxLength">
/// The longest text the API accepts, as the refusal named it. Null unless
/// <paramref name="Outcome"/> is <see cref="DesiredDataOutcome.Refused"/>, and null then too when
/// the refusal named no ceiling — which means it was refused for some other reason.
/// </param>
/// <param name="Received">How long the API measured the text as. Null on the same terms.</param>
public sealed record DesiredDataResult(
    DesiredDataOutcome Outcome,
    int? MaxLength = null,
    int? Received = null);
