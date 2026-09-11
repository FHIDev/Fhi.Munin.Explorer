namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The orders the kilde list can be shown in. Every one of them is applied in the browser.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here is sent to the API.</b>
/// <see cref="Contracts.IMuninExplorerClient.GetKilderAsync"/> answers with the whole list in one
/// array rather than a page of it, so the rows are already in hand and a <c>sort</c> parameter
/// would be a contract with no caller — one that could, moreover, only ever order what a page
/// held. <see cref="Contracts.SortField"/> is the other kind and the reason this type is not it:
/// that one goes to the variable endpoint, which is paged and does its own ordering.
/// </para>
/// <para>
/// <b>Which way an order runs is not here.</b> Every member below names a key rather than a
/// direction, because the reader reverses it by pressing the column heading again — the direction
/// travels beside it as a <see cref="Contracts.SortDirection"/>, exactly as it does on the variable
/// side. That is why none of them reads "most first" any more: they did while a select offered
/// them, and a heading a press reverses cannot carry the word.
/// </para>
/// <para>
/// <b>Which way one runs first is not here either, but it is per member.</b> A count or a date
/// column opens on its largest value and the name column on A, which is what those select labels
/// meant and what a link carrying an order and no direction still means. The one place that is
/// decided is <see cref="KildeSearch.InitialDirection"/>.
/// </para>
/// <para>
/// <b>Where a value is missing it sorts last, in every order.</b> A kilde the catalogue has not
/// given an established year, or a source-system change date, or one whose value is not a date at
/// all, comes after every kilde that has one — never first and never mixed in, so "not recorded"
/// cannot pass for "oldest" or for "newest". A recorded <em>zero</em> is a value and sorts as one:
/// a kilde with 0 variabler sits at the bottom of the recorded rows and above the unrecorded.
/// Note that <see cref="Contracts.KildeSummary.TotalVariables"/> is a non-nullable
/// <see langword="int"/>, so the contract has no way to say "not counted" for that column — an
/// absent count arrives as 0 and is ordered as 0.
/// </para>
/// <para>
/// <b>Every order is total.</b> Equal values fall back to the display name in Norwegian collation
/// and then to the kilde's code, so two kilder with the same count do not swap places between
/// renders.
/// </para>
/// <para>
/// The members are declared in the order the table draws their columns, and every one but
/// <see cref="Standard"/> has a column to be pressed: <see cref="Name"/> is Navn,
/// <see cref="Variables"/> is Variabler, <see cref="SourceUpdated"/> is Sist endret and
/// <see cref="Established"/> is Opprettet. An order added here without a sortable heading would be
/// one a reader could arrive on by link and never choose.
/// </para>
/// </remarks>
public enum KildeSortOrder
{
    /// <summary>
    /// The order the catalogue sent, which is what the list showed before it could be sorted at
    /// all — and so the one order that is never written to a host's URL.
    /// </summary>
    /// <remarks>
    /// The absence of a sort rather than a sort, and the only member with no column: it is what a
    /// clean URL and a first load give, and since the column headings replaced the select there is
    /// nothing in the page that returns to it (Fhi.Metadata-l9l2n.88).
    /// </remarks>
    Standard,

    /// <summary>
    /// Display name, collated as Norwegian so æ, ø and å come at the end of the alphabet whatever
    /// language the reader has asked for.
    /// </summary>
    Name,

    /// <summary>The count of the kilde's visible published variables.</summary>
    Variables,

    /// <summary>
    /// When the kilde last changed in the source system — the catalogue's own <c>SistOppdatert</c>,
    /// which is the table's Sist endret column, and not Munin's row timestamp.
    /// </summary>
    SourceUpdated,

    /// <summary>
    /// The founding year the import file states, which is the table's Opprettet column, and not
    /// when Munin's own row was written.
    /// </summary>
    Established,
}
