namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Orders the delkilder and datasamlinger that share one parent as a single sequence, the way the
/// API's <c>displayOrder</c> ranks them.
/// </summary>
/// <remarks>
/// Delkilder and datasamlinger under one parent are siblings of each other, and Munin resolves one
/// rank across both kinds — so sorting each kind by its own rank and concatenating, or merging two
/// per-kind <c>presentationOrder</c> sequences, puts them in the wrong order. This helper only
/// consumes the resolved rank: whether that rank came from an import or from a curator's manual
/// ordering is decided by the API, and nothing here second-guesses it.
/// </remarks>
public static class SiblingOrder
{
    /// <summary>
    /// Merges one parent's delkilder and datasamlinger into one sequence, in display order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Siblings carrying a <c>displayOrder</c> come first, ascending by it. Two siblings with the
    /// same rank are ordered by name, ordinal, and then by id, so the result does not depend on
    /// which endpoint sent them or in what order.
    /// </para>
    /// <para>
    /// <b>Legacy fallback.</b> A server predating <c>displayOrder</c> sends no rank at all, and
    /// then the siblings keep the order the payload listed them in: every delkilde as sent, then
    /// every datasamling as sent. That is the imported order the older server already applied,
    /// and it is deliberately not re-sorted by name. A sibling without a rank beside siblings that
    /// have one is placed after all of them, in that same payload order.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">
    /// The node type both kinds are projected to; project each kind first when they differ.
    /// </typeparam>
    /// <param name="delkilder">The parent's delkilder, in payload order.</param>
    /// <param name="datasamlinger">The parent's datasamlinger, in payload order.</param>
    /// <param name="displayOrder">The API's resolved rank, or null where the payload has none.</param>
    /// <param name="name">The name compared ordinally when two ranks are equal.</param>
    /// <param name="id">The final tie-breaker, so equal ranks and names still order the same way.</param>
    /// <returns>A new list holding every element of both inputs exactly once.</returns>
    public static IReadOnlyList<T> Merge<T>(
        IEnumerable<T> delkilder,
        IEnumerable<T> datasamlinger,
        Func<T, int?> displayOrder,
        Func<T, string> name,
        Func<T, Guid> id)
    {
        ArgumentNullException.ThrowIfNull(delkilder);
        ArgumentNullException.ThrowIfNull(datasamlinger);
        ArgumentNullException.ThrowIfNull(displayOrder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(id);

        return
        [
            .. delkilder.Concat(datasamlinger)
                .Select((item, index) => (Item: item, Index: index, Rank: displayOrder(item)))
                .OrderBy(e => e.Rank is null)
                .ThenBy(e => e.Rank ?? 0)
                .ThenBy(e => e.Rank is null ? e.Index : 0)
                .ThenBy(e => name(e.Item), StringComparer.Ordinal)
                .ThenBy(e => id(e.Item))
                .Select(e => e.Item)
        ];
    }
}
