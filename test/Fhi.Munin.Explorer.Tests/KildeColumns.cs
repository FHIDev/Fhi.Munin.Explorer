using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilder table's columns, as a test drives them: the picker's checkboxes and the header cells
/// they draw.
/// </summary>
/// <remarks>
/// One copy, for the reason <see cref="RazorSource"/> is one copy: two files sweep every column on,
/// and written twice the next fix to what a toggle press does reaches one sweep and leaves the other
/// asserting against a gesture the component no longer has.
///
/// Kelda's rules, not the variable explorer's: ten optional columns, three on to begin with, and no
/// last-column lock — Navn, Status and Opprettet are drawn whatever the picker says. (Fhi.Metadata-ay3zz)
/// </remarks>
internal static class KildeColumns
{
    /// <summary>The picker's checkboxes, in the order it lists them.</summary>
    internal static IReadOnlyList<IElement> ColumnToggles(IRenderedComponent<KildeSearch> cut) =>
        cut.FindAll(".dropdown-choicepicker__item input[type=checkbox]");

    /// <summary>A checkbox's column, which is the label beside it rather than its own text.</summary>
    internal static string ColumnName(IElement toggle) =>
        toggle.ParentElement!.QuerySelector(".form-control__label")!.TextContent.Trim();

    /// <summary>Whether the column is on screen, as the rendered attribute has it.</summary>
    internal static bool Ticked(IElement toggle) => toggle.HasAttribute("checked");

    /// <summary>The toggle for one named column, refetched so it is never a stale node.</summary>
    /// <remarks>
    /// <c>Change</c> and not <c>Click</c>: bUnit raises MissingEventHandlerException for a click
    /// on an element handling only <c>onchange</c>, and names the event it does handle.
    /// </remarks>
    internal static void ToggleColumn(IRenderedComponent<KildeSearch> cut, string label)
    {
        var box = ColumnToggles(cut).Single(b => ColumnName(b) == label);

        box.Change(!Ticked(box));
    }

    /// <summary>
    /// Every column the picker has not already got on screen, named rather than held: each tick
    /// re-renders the picker, so a node carried across that render is a stale one.
    /// </summary>
    internal static void TurnEveryColumnOn(IRenderedComponent<KildeSearch> cut)
    {
        foreach (var label in ColumnToggles(cut).Where(box => !Ticked(box)).Select(ColumnName).ToList())
        {
            ToggleColumn(cut, label);
        }
    }

    /// <summary>The header cells the table actually drew, which is what the count modifier counts.</summary>
    internal static IReadOnlyList<string> Headers(IRenderedComponent<KildeSearch> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder thead th").Select(th => th.TextContent.Trim())];

    /// <summary>The buttons the four sortable headings hold, in the order the table draws them.</summary>
    internal static IReadOnlyList<IElement> SortButtons(IRenderedComponent<KildeSearch> cut) =>
        cut.FindAll(".munin-explorer-kilder thead .munin-explorer-kilder__sort");

    /// <summary>
    /// Sort the list the way a reader does: press the heading that says <paramref name="heading"/>.
    /// </summary>
    /// <remarks>
    /// By the word on screen rather than by position, so a test naming a column is asserting the
    /// reader can reach that order from that column. <c>StartsWith</c> because the sorted heading
    /// carries the arrow after its word, and refetched on every call since each press re-renders
    /// the whole head.
    /// </remarks>
    internal static void SortBy(IRenderedComponent<KildeSearch> cut, string heading) =>
        SortButtons(cut)
            .Single(button => button.TextContent.Trim().StartsWith(heading, StringComparison.Ordinal))
            .Click();

    /// <summary>The same press, named by the order rather than by the word over the column.</summary>
    internal static void SortBy(IRenderedComponent<KildeSearch> cut, KildeSortOrder order) =>
        SortBy(cut, HeadingFor(order));

    /// <summary>The Norwegian heading over an order's column, which is the word a reader presses.</summary>
    /// <remarks>
    /// <see cref="KildeSortOrder.Standard"/> has no column and no heading, which is what moving the
    /// sort onto them cost (Fhi.Metadata-l9l2n.88): it throws rather than answering with a word no
    /// heading says.
    /// </remarks>
    internal static string HeadingFor(KildeSortOrder order) => order switch
    {
        KildeSortOrder.Name => "Navn",
        KildeSortOrder.Variables => "Variabler",
        KildeSortOrder.SourceUpdated => "Sist endret",
        KildeSortOrder.Established => "Opprettet",
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "This order has no column.")
    };
}
