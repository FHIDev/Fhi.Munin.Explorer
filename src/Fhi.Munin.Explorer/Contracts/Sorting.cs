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
/// The set matches Runa's sortable columns exactly, which since Fhi.Metadata-0ayti is every column
/// holding a fact about the variable — code, datatype, status and data period included. The one
/// column with no member is the signed-in reader's save column, which holds a control rather than a
/// value, so there is nothing for the API to order by.
/// </para>
/// <para>
/// The members are declared in the order a UI should offer them, so a control built from
/// <c>Enum.GetValues</c> needs no second list to keep in step with this one. That order is Runa's
/// left-to-right column order, which is why the four newer members are interleaved rather than
/// appended — appending them would have offered the list in an order no table is in.
/// </para>
/// <para>
/// Ordering happens in the API's own SQL, for every member alike. Nothing here or in
/// <c>MuninExplorerClient</c> compares two rows, so a Norwegian name sorts by the catalogue
/// database's collation rather than by whatever culture the host's thread happens to carry.
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

    /// <summary>Variable code. Sent as <c>kode</c>.</summary>
    /// <remarks>The code is also every other member's tie-break, so this order has no second key.</remarks>
    Code,

    /// <summary>Kilde name, code as the tie-break. Sent as <c>kilde</c>.</summary>
    Kilde,

    /// <summary>Primary datasamling name, code as the tie-break. Sent as <c>datasamling</c>.</summary>
    Datasamling,

    /// <summary>Primary variabelgruppe name, code as the tie-break. Sent as <c>variabelgruppe</c>.</summary>
    Variabelgruppe,

    /// <summary>
    /// Datatype, by the catalogue's own code for it, with the variable code as the tie-break. Sent
    /// as <c>datatype</c>.
    /// </summary>
    /// <remarks>
    /// The code rather than the displayed label, so the order does not move with the reader's
    /// language. A variable with no datatype — blank and absent alike — comes last whichever
    /// direction is asked for, so the group of dashes cannot lead a descending page.
    /// </remarks>
    DataType,

    /// <summary>
    /// Version status, active before historical, with the code as the tie-break. Sent as
    /// <c>status</c>.
    /// </summary>
    /// <remarks>
    /// Ranked by the API's own rule for when a version is still active, not by the word the column
    /// shows, for <see cref="DataType"/>'s reason: a localised status label would reorder the list
    /// when the reader switched language.
    /// </remarks>
    Status,

    /// <summary>
    /// The start of the period the data covers, with the code as the tie-break. Sent as
    /// <c>dataperiode</c>.
    /// </summary>
    /// <remarks>
    /// The start date, never the text the column draws: a period reads as a range, often
    /// open-ended, so ordering it as written would put "1999" after "2021 – Pågående".
    /// <para>
    /// The period the DATA covers, and deliberately not the version's validity window — those are
    /// two different facts about a variable and the API carries both. Ordering this column by the
    /// validity window would order it by a fact it does not display, and nothing on screen would
    /// look wrong.
    /// </para>
    /// <para>
    /// An open-ended period is not missing anything this order reads: it is ordered by its start
    /// like any other, so a period that is still running sits among the ones that began with it
    /// rather than at either end. A variable with no start date at all is the missing case, and it
    /// comes last whichever direction is asked for, the same as an absent datatype — a period that
    /// has never been recorded is not the earliest one, and a null leading the descending page is
    /// how this goes wrong.
    /// </para>
    /// </remarks>
    DataPeriod
}

/// <summary>Sort direction, sent as <c>sortDir</c>.</summary>
public enum SortDirection
{
    /// <summary>Ascending — the API's default.</summary>
    Ascending,

    /// <summary>Descending.</summary>
    Descending
}
