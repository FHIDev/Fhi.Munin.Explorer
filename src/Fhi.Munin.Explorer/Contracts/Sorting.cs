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
/// Every member but <see cref="Default"/> is one of Runa's sortable columns, which since
/// Fhi.Metadata-0ayti is every column holding a fact about the variable. <see cref="Default"/> is
/// the sentinel for "no order chosen": it has no column and no wire token, so a control offering
/// the orders leaves it out and shows it as the unsorted state rather than as a choice.
/// </para>
/// <para>
/// The numeric values are fixed and part of the contract; a new member is appended with the next
/// value. So <c>Enum.GetValues</c> is not the order a control should offer: that is Runa's
/// left-to-right column order, <see cref="Name"/> first, then <see cref="Code"/> through
/// <see cref="DataPeriod"/> in value order.
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
    /// The API's own default order: kilde, then the catalogue's curated presentation order, then
    /// the display name, with the code as the tie-break. Sends no <c>sort</c> at all.
    /// </summary>
    /// <remarks>
    /// No token because the API has none for it: <c>name</c> is a real name sort since
    /// Fhi.Metadata-bgvdh, so the curated order is only what the API does when nothing is asked for.
    /// </remarks>
    Default = 0,

    /// <summary>Variable code. Sent as <c>kode</c>.</summary>
    /// <remarks>The code is also every other member's tie-break, so this order has no second key.</remarks>
    Code = 1,

    /// <summary>Kilde name, code as the tie-break. Sent as <c>kilde</c>.</summary>
    Kilde = 2,

    /// <summary>Primary datasamling name, code as the tie-break. Sent as <c>datasamling</c>.</summary>
    Datasamling = 3,

    /// <summary>Primary variabelgruppe name, code as the tie-break. Sent as <c>variabelgruppe</c>.</summary>
    Variabelgruppe = 4,

    /// <summary>
    /// Datatype, by the catalogue's own code for it, with the variable code as the tie-break. Sent
    /// as <c>datatype</c>.
    /// </summary>
    /// <remarks>
    /// The code rather than the displayed label, so the order does not move with the reader's
    /// language. A variable with no datatype — blank and absent alike — comes last whichever
    /// direction is asked for, so the group of dashes cannot lead a descending page.
    /// </remarks>
    DataType = 5,

    /// <summary>
    /// Version status, active before historical, with the code as the tie-break. Sent as
    /// <c>status</c>.
    /// </summary>
    /// <remarks>
    /// Ranked by the API's own rule for when a version is still active, not by the word the column
    /// shows, for <see cref="DataType"/>'s reason: a localised status label would reorder the list
    /// when the reader switched language.
    /// </remarks>
    Status = 6,

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
    DataPeriod = 7,

    /// <summary>Variable display name, alphabetical in Norwegian collation. Sent as <c>name</c>.</summary>
    /// <remarks>Appended rather than placed first, where its column is, so no value moved (Fhi.Metadata-bgvdh).</remarks>
    Name = 8,
}

/// <summary>Sort direction, sent as <c>sortDir</c>.</summary>
public enum SortDirection
{
    /// <summary>Ascending — the API's default.</summary>
    Ascending,

    /// <summary>Descending.</summary>
    Descending
}
