using System.Globalization;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The order the kilde list is in, and the column headers that change it.</summary>
public sealed partial class KildeSearch
{
    /// <summary>
    /// The order the list starts in. Set by the host, typically from its own URL; the component
    /// owns it afterwards.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, and owned by the component afterwards, exactly as
    /// <see cref="SelectedKildeId"/> is: re-rendering this mount with a different value changes
    /// nothing on screen, so it is a starting point rather than a live parameter. To follow the
    /// reader instead, take <see cref="OrderChanged"/> — or write the pair as
    /// <c>@bind-Order</c>. Unlike <see cref="Search"/> this one has that callback beside it,
    /// because an order is worth linking to: a reader who sorted by Variabler and sent the
    /// link would otherwise hand over the list as it arrived.
    /// </remarks>
    [Parameter] public KildeSortOrder Order { get; set; }

    /// <summary>
    /// Raised when the reader sorts on another column, so the host can reflect it in its own URL.
    /// The Order/OrderChanged naming gives the host <c>@bind-Order</c> for free.
    /// </summary>
    /// <remarks>
    /// The same warning <see cref="SelectedKildeIdChanged"/> carries: an
    /// <see cref="EventCallback"/> serialises to an empty delegate across a static-SSR to
    /// interactive-island boundary, and then never fires, with nothing on screen saying so.
    /// </remarks>
    [Parameter] public EventCallback<KildeSortOrder> OrderChanged { get; set; }

    /// <summary>Which way <see cref="Order"/> runs. Starts and behaves exactly as it does.</summary>
    /// <remarks>
    /// Separate from the order because a column header toggles the two independently: pressing the
    /// column the list is already sorted on reverses this and leaves the order alone. It means
    /// nothing with <see cref="KildeSortOrder.Standard"/>, which is the absence of a sort, so a
    /// host writing a URL has nothing to write there either.
    /// <para>
    /// <see cref="SortDirection"/> is the variable side's type, reused rather than doubled: it
    /// names a direction and carries no API token of its own, unlike <see cref="SortField"/> —
    /// which is why <see cref="KildeSortOrder"/> is a separate type and this is not.
    /// </para>
    /// </remarks>
    [Parameter] public SortDirection Direction { get; set; }

    /// <summary>Raised with <see cref="OrderChanged"/>, giving a host <c>@bind-Direction</c>.</summary>
    /// <remarks>
    /// Both are raised on every press, including the one that only reverses the direction, so a
    /// host that mirrors the pair into its URL cannot hold half of what the reader chose.
    /// </remarks>
    [Parameter] public EventCallback<SortDirection> DirectionChanged { get; set; }

    private KildeSortOrder _order;

    private SortDirection _direction;

    /// <summary>
    /// Sort on <paramref name="order"/>'s column: the active one again reverses the direction,
    /// another starts ascending.
    /// </summary>
    /// <remarks>
    /// The variable explorer's rule, at <c>VariableSearch.Querying.cs</c>, and two-state like it —
    /// there is no third press that returns the list to <see cref="KildeSortOrder.Standard"/>. It
    /// carries none of that one's guarding, because nothing here is fetched: this list is whole in
    /// memory and the order is applied over it on the next render, so there is no in-flight request
    /// to drop a press for and no failure to put the state back after.
    /// </remarks>
    private async Task SortAsync(KildeSortOrder order)
    {
        if (order == _order)
        {
            _direction = _direction == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _order = order;
            _direction = SortDirection.Ascending;
        }

        await RaiseAsync(OrderChanged, _order, Log);
        await RaiseAsync(DirectionChanged, _direction, Log);
    }

    /// <summary>Whether the list is sorted on this column.</summary>
    private bool IsActiveSort(KildeSortOrder order) => _order == order;

    /// <summary>
    /// The <c>aria-sort</c> the column's header cell carries, or null for every other column.
    /// </summary>
    /// <remarks>
    /// Null rather than <c>"none"</c>, so the attribute is absent: only one column is sorted, and
    /// "none" repeated across the header is noise a screen reader reads out on every cell.
    /// </remarks>
    private string? AriaSort(KildeSortOrder order) =>
        !IsActiveSort(order) ? null
        : _direction == SortDirection.Ascending ? "ascending"
        : "descending";

    /// <summary>
    /// One sortable column heading: a real <c>th</c>, with the control inside it.
    /// </summary>
    /// <remarks>
    /// The variable explorer's <c>HeaderCell</c> logic on Kelda's own element. What is deliberately
    /// not copied is that method's <c>role="columnheader"</c>: it carries the role because its
    /// header is a <c>div</c> in a flex pseudo-table, and a <c>th</c> in a <c>thead</c> is a column
    /// header already — an explicit role restating a native one is a defect rather than parity.
    /// <para>
    /// <c>munin-explorer-kilder__sort</c> and not the variable explorer's
    /// <c>munin-explorer-dataitem-header__button</c>: that name means the pseudo-table, and
    /// <c>Fhi.Helsedata.Stiler</c>'s two rules for it are scoped to that prefix, so a button here
    /// wearing it would be both mislabelled and unstyled (Fhi.Metadata-l9l2n.106).
    /// </para>
    /// </remarks>
    private RenderFragment SortableHeader(KildeSortOrder order, string? columnClass = null) => builder =>
    {
        builder.OpenElement(0, "th");
        builder.AddAttribute(1, "scope", "col");
        builder.AddAttribute(2, "class", columnClass);

        // On the cell and never on the button: it describes the COLUMN's state, and it is what a
        // screen reader reads when moving across the header.
        builder.AddAttribute(3, "aria-sort", AriaSort(order));

        // A real button, so Tab reaches it and Enter and Space press it with nothing written here.
        // A keydown handler would be the sign the wrong element had been reached for.
        builder.OpenElement(4, "button");

        // hd-button-reset is Stiler's own "this is a button but draw nothing" class, the same one
        // the row's name button and the expand toggle wear.
        builder.AddAttribute(5, "class", "hd-button-reset munin-explorer-kilder__sort");
        builder.AddAttribute(6, "type", "button");

        // What Stiler's rule for the sorted heading selects on, and the variable explorer's button
        // carries it for the same reason.
        builder.AddAttribute(7, "aria-current", IsActiveSort(order) ? "true" : null);
        builder.AddAttribute(8, "onclick", EventCallback.Factory.Create(this, () => SortAsync(order)));

        // The word over the column, which is the word the status line names the order by — one
        // label for both, so the sentence cannot come to name an order no header shows.
        builder.AddContent(9, T.KildeOrderLabel(order));

        if (IsActiveSort(order))
        {
            // Bare and classless, drawn as the variable explorer draws it: the character is the
            // whole of it, aria-sort above says the same thing in words, and it needs no CSS.
            builder.OpenElement(10, "span");
            builder.AddAttribute(11, "aria-hidden", "true");
            builder.AddContent(12, _direction == SortDirection.Ascending ? " \u2191" : " \u2193");
            builder.CloseElement();
        }

        builder.CloseElement();

        builder.CloseElement();
    };

    /// <summary>
    /// <paramref name="kilder"/> in <paramref name="order"/>, running <paramref name="direction"/>.
    /// </summary>
    /// <remarks>
    /// Applied to whatever the search and the facets left rather than to a page of it: this
    /// endpoint is not paged and the whole catalogue is already in hand, so sorting a filtered list
    /// sorts every row that survived the filter. What each order reads, where a missing value goes
    /// and why every one of them is total are on <see cref="KildeSortOrder"/> itself.
    /// </remarks>
    internal static IReadOnlyList<KildeSummary> Sorted(
        IReadOnlyList<KildeSummary> kilder, KildeSortOrder order, SortDirection direction) => order switch
        {
            // Handed back untouched, not copied into a new list of the same rows: this is
            // the order the API sent, and it is what the list has always shown. The direction is
            // not applied to it — there is no key to reverse, only the sequence that arrived.
            KildeSortOrder.Standard => kilder,
            KildeSortOrder.Name => [.. ByName(kilder, direction)],
            KildeSortOrder.Variables => [.. ByValue(kilder, kilde => (int?)kilde.TotalVariables, direction)],
            KildeSortOrder.SourceUpdated => [.. ByValue(kilder, SourceChangedOn, direction)],
            KildeSortOrder.Established => [.. ByValue(kilder, EstablishedYear, direction)],
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, "No ordering for this kilde order.")
        };

    /// <summary>By the name on screen, in the catalogue's own collation.</summary>
    /// <remarks>
    /// <see cref="CatalogueProperties.CatalogueOrder"/> — <c>nb-NO</c>, pinned — and never the
    /// thread's culture or the reader's. So æ, ø, å and the digraph aa all sort at the end of the
    /// alphabet everywhere, where in <c>Fhi.Metadata-dpc6h</c> that was the machine's choice.
    /// </remarks>
    private static IOrderedEnumerable<KildeSummary> ByName(
        IEnumerable<KildeSummary> kilder, SortDirection direction) =>
        (direction == SortDirection.Ascending
            ? kilder.OrderBy(DisplayName, CatalogueProperties.CatalogueOrder)
            : kilder.OrderByDescending(DisplayName, CatalogueProperties.CatalogueOrder))
        .ThenBy(kilde => kilde.Code, StringComparer.Ordinal);

    /// <summary>By <paramref name="value"/>, with the kilder that have none last either way.</summary>
    /// <remarks>
    /// Two keys rather than a sentinel: coalescing a missing value to <c>0</c> or to
    /// <see cref="DateTime.MinValue"/> would file "not recorded" among the smallest, where a
    /// recorded zero belongs and nothing else does. The first key is outside the direction on
    /// purpose — reversing it would send the rows with no value to the top, which is the one place
    /// <see cref="KildeSortOrder"/> promises they never are.
    /// </remarks>
    private static IOrderedEnumerable<KildeSummary> ByValue<TValue>(
        IEnumerable<KildeSummary> kilder, Func<KildeSummary, TValue?> value, SortDirection direction)
        where TValue : struct, IComparable<TValue>
    {
        var recorded = kilder.OrderByDescending(kilde => value(kilde).HasValue);

        // The tiebreak stays ascending in both directions: it is what makes the order total rather
        // than part of what the reader asked for, and rows sharing a value would otherwise swap
        // places for a reason nothing on screen explains.
        return (direction == SortDirection.Ascending
                ? recorded.ThenBy(value)
                : recorded.ThenByDescending(value))
            .ThenBy(DisplayName, CatalogueProperties.CatalogueOrder)
            .ThenBy(kilde => kilde.Code, StringComparer.Ordinal);
    }

    /// <summary>The name the row draws, which is the code where the catalogue left the name empty.</summary>
    /// <remarks>
    /// The first two arms of <see cref="Texts.Named"/>, and it has to be: sorting the raw name
    /// would file an unnamed kilde before every other row while the screen shows it under its code
    /// (Fhi.Metadata-w13lk). The third arm is not repeated, because <see cref="Texts.NotSpecified"/>
    /// is in the reader's language and an order that changed with it would not be one order.
    /// </remarks>
    private static string DisplayName(KildeSummary kilde) =>
        string.IsNullOrWhiteSpace(kilde.Name) ? kilde.Code : kilde.Name;

    /// <summary>The founding year the Opprettet column draws, as a number, or null.</summary>
    /// <remarks>
    /// Null where the key is absent and where its value is not a number at all. What the catalogue
    /// wrote is ordered as written otherwise, "2916" and "0" included: the column beside this draws
    /// them verbatim, so an order that quietly repaired them would disagree with what is on screen.
    /// </remarks>
    private static int? EstablishedYear(KildeSummary kilde) =>
        int.TryParse(Property(kilde, "Opprettet")?.Trim(), NumberStyles.Integer,
                     CultureInfo.InvariantCulture, out var year)
            ? year
            : null;

    /// <summary>The day the Sist endret column draws, or null.</summary>
    /// <remarks>
    /// The same <c>yyyyMMdd</c> parse <see cref="CatalogueDate.SourceSystemDay"/> renders through,
    /// so a value that column shows as a date is one this can order. One it shows raw, because it
    /// did not parse, has no place on a timeline and sorts last.
    /// </remarks>
    private static DateTime? SourceChangedOn(KildeSummary kilde) =>
        DateTime.TryParseExact(Property(kilde, "SistOppdatert")?.Trim(), "yyyyMMdd",
                               CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;
}
