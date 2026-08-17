namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// The fields <c>GET /api/explorer/variables</c> will sort on.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a closed set rather than a string. The API takes <c>sort</c> as free text and
/// silently falls back to the name sort for anything it does not recognise, so a typo would not
/// fail — it would quietly return a different order than the one the UI says it is showing.
/// </para>
/// <para>
/// The set matches Runa's sortable columns exactly. Code, datatype, status and data period are
/// absent on purpose: they are not sortable there either, and offering them here would promise
/// an ordering the API does not implement.
/// </para>
/// </remarks>
public enum Sorteringsfelt
{
    /// <summary>
    /// The API's default order — kilde, then the catalogue's curated presentation order, then the
    /// display name, with the code as the tie-break. Sent as <c>name</c>.
    /// </summary>
    Navn,

    /// <summary>Kilde name, code as the tie-break. Sent as <c>kilde</c>.</summary>
    Kilde,

    /// <summary>Primary datasamling name, code as the tie-break. Sent as <c>datasamling</c>.</summary>
    Datasamling,

    /// <summary>Primary variabelgruppe name, code as the tie-break. Sent as <c>variabelgruppe</c>.</summary>
    Variabelgruppe
}

/// <summary>Sort direction, sent as <c>sortDir</c>.</summary>
public enum Sorteringsretning
{
    /// <summary>Ascending — the API's default.</summary>
    Stigende,

    /// <summary>Descending.</summary>
    Synkende
}
