using System.Globalization;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The order the kilde list is in, and the control that changes it.</summary>
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
    /// because an order is worth linking to: a reader who sorted by Flest variabler and sent the
    /// link would otherwise hand over the list as it arrived.
    /// </remarks>
    [Parameter] public KildeSortOrder Order { get; set; }

    /// <summary>
    /// Raised when the reader chooses another order, so the host can reflect it in its own URL.
    /// The Order/OrderChanged naming gives the host <c>@bind-Order</c> for free.
    /// </summary>
    /// <remarks>
    /// The same warning <see cref="SelectedKildeIdChanged"/> carries: an
    /// <see cref="EventCallback"/> serialises to an empty delegate across a static-SSR to
    /// interactive-island boundary, and then never fires, with nothing on screen saying so.
    /// </remarks>
    [Parameter] public EventCallback<KildeSortOrder> OrderChanged { get; set; }

    private KildeSortOrder _order;

    /// <summary>The orders the control offers, in the order it lists them.</summary>
    /// <remarks>
    /// The enum itself rather than a list restating it, for the reason
    /// <see cref="OptionalColumns"/> is: an order added there without a line here would be one the
    /// reader could arrive on by link and never choose.
    /// </remarks>
    private static readonly KildeSortOrder[] OfferedOrders = Enum.GetValues<KildeSortOrder>();

    private string SortSelectId => $"munin-explorer-sort-{_instance}";

    /// <summary>The order the reader picked, or the catalogue's own for anything unreadable.</summary>
    /// <remarks>
    /// A <c>&lt;select&gt;</c> can only send a value this component wrote into it, so the fallback
    /// is defensive rather than expected. It falls back rather than throwing for the reason the
    /// URL parse does: what arrives here is a browser's string, and the list is still a list.
    /// </remarks>
    private async Task ChooseOrderAsync(ChangeEventArgs args)
    {
        var chosen = Enum.TryParse<KildeSortOrder>(args.Value as string, ignoreCase: true, out var parsed)
                     && Enum.IsDefined(parsed)
            ? parsed
            : KildeSortOrder.Standard;

        if (chosen == _order)
        {
            return;
        }

        _order = chosen;

        await RaiseAsync(OrderChanged, _order);
    }

    /// <summary>
    /// <paramref name="kilder"/> in <paramref name="order"/>.
    /// </summary>
    /// <remarks>
    /// Applied to whatever the search and the facets left rather than to a page of it: this
    /// endpoint is not paged and the whole catalogue is already in hand, so sorting a filtered list
    /// sorts every row that survived the filter. What each order reads, where a missing value goes
    /// and why every one of them is total are on <see cref="KildeSortOrder"/> itself.
    /// </remarks>
    internal static IReadOnlyList<KildeSummary> Sorted(
        IReadOnlyList<KildeSummary> kilder, KildeSortOrder order) => order switch
    {
        // Handed back untouched, not copied into a new list of the same rows: this is the order the
        // API sent, and it is what the list has always shown.
        KildeSortOrder.Standard => kilder,
        KildeSortOrder.Name => [.. ByName(kilder)],
        KildeSortOrder.Variables => [.. ByValue(kilder, kilde => (int?)kilde.TotalVariables)],
        KildeSortOrder.SourceUpdated => [.. ByValue(kilder, SourceChangedOn)],
        KildeSortOrder.Established => [.. ByValue(kilder, EstablishedYear)],
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "No ordering for this kilde order.")
    };

    /// <summary>Ascending by the name on screen, in the catalogue's own collation.</summary>
    /// <remarks>
    /// <see cref="CatalogueProperties.CatalogueOrder"/> — <c>nb-NO</c>, pinned — and never the
    /// thread's culture or the reader's. So æ, ø, å and the digraph aa all sort at the end of the
    /// alphabet everywhere, where in <c>Fhi.Metadata-dpc6h</c> that was the machine's choice.
    /// </remarks>
    private static IOrderedEnumerable<KildeSummary> ByName(IEnumerable<KildeSummary> kilder) =>
        kilder.OrderBy(DisplayName, CatalogueProperties.CatalogueOrder)
              .ThenBy(kilde => kilde.Code, StringComparer.Ordinal);

    /// <summary>Descending by <paramref name="value"/>, with the kilder that have none last.</summary>
    /// <remarks>
    /// Two keys rather than a sentinel: coalescing a missing value to <c>0</c> or to
    /// <see cref="DateTime.MinValue"/> would file "not recorded" among the smallest, where a
    /// recorded zero belongs and nothing else does.
    /// </remarks>
    private static IOrderedEnumerable<KildeSummary> ByValue<TValue>(
        IEnumerable<KildeSummary> kilder, Func<KildeSummary, TValue?> value)
        where TValue : struct, IComparable<TValue> =>
        kilder.OrderByDescending(kilde => value(kilde).HasValue)
              .ThenByDescending(value)
              .ThenBy(DisplayName, CatalogueProperties.CatalogueOrder)
              .ThenBy(kilde => kilde.Code, StringComparer.Ordinal);

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
