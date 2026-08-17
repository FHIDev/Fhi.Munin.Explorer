namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// The orders <c>GET /api/explorer/variables</c> will sort by.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a closed set rather than a string. The API takes <c>sort</c> as free text and
/// silently falls back to its default order for anything it does not recognise, so a typo would not
/// fail — it would quietly return a different order than the one the UI says it is showing.
/// </para>
/// <para>
/// The set matches Runa's sortable columns exactly. Code, datatype, status and data period are
/// absent on purpose: they are not sortable there either, and offering them here would promise
/// an ordering the API does not implement.
/// </para>
/// <para>
/// The members are declared in the order a UI should offer them, so a control built from
/// <c>Enum.GetValues</c> needs no second list to keep in step with this one.
/// </para>
/// </remarks>
public enum SortField
{
    /// <summary>
    /// The API's own default order, sent as <c>name</c>: kilde, then the catalogue's curated
    /// presentation order, then the display name, with the code as the tie-break.
    /// </summary>
    /// <remarks>
    /// Named for what it does rather than for the token it sends. Calling this a name sort — which
    /// the wire token invites — would misdescribe it in any label built from this member: the
    /// primary key is kilde, and what separates it from <see cref="Kilde"/> is only the ordering
    /// inside a kilde, where this one follows the catalogue's curated sequence.
    /// </remarks>
    Default,

    /// <summary>Kilde name, code as the tie-break. Sent as <c>kilde</c>.</summary>
    Kilde,

    /// <summary>Primary datasamling name, code as the tie-break. Sent as <c>datasamling</c>.</summary>
    Datasamling,

    /// <summary>Primary variabelgruppe name, code as the tie-break. Sent as <c>variabelgruppe</c>.</summary>
    Variabelgruppe
}

/// <summary>Sort direction, sent as <c>sortDir</c>.</summary>
public enum SortDirection
{
    /// <summary>Ascending — the API's default.</summary>
    Ascending,

    /// <summary>Descending.</summary>
    Descending
}
