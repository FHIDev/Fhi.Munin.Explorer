using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The result table's column header, and the one way these tests press a sort.
/// </summary>
/// <remarks>
/// <para>
/// Shared rather than owned by one test class because two of them press sorts, and a copy each
/// meant the header's class name lived in two files. Renaming it in
/// <c>VariableExplorer.razor.cs</c> would then break them independently, with nothing in the
/// second pointing at the first — which is the same drift the sample stylesheets went through,
/// in a place with even less to notice it.
/// </para>
/// <para>
/// Every selector here is scoped to the header, and both reasons to keep it that way are easy to
/// undo by accident. The header and the filter panel are both Stiler's <c>form-fieldset</c>, so a
/// selector that does not say which one it means silently starts asserting about the filters. And
/// "Kilde" labels two controls now: the header cell that sorts by it, and the column picker's
/// toggle that turns it off. The picker comes first in the DOM, so an unscoped search for the word
/// presses the wrong one — hiding the column instead of reordering, which makes a sort test fail
/// and a no-sort-reported test pass for the wrong reason.
/// </para>
/// </remarks>
internal static class SortHeader
{
    /// <summary>The header row the sort buttons live in.</summary>
    public const string SortControl = ".munin-explorer-data-list__header";

    /// <summary>The sort buttons, in the order they are rendered.</summary>
    public static IReadOnlyList<IElement> SortButtons(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll($"{SortControl} button");

    /// <summary>
    /// Reorders the list by <paramref name="label"/>, from the column header a reader would press,
    /// whatever direction suffix it currently carries.
    /// </summary>
    public static void ClickSort(IRenderedComponent<VariableExplorer> cut, string label) =>
        SortButtons(cut).Single(k => k.TextContent.StartsWith(label, StringComparison.Ordinal)).Click();
}
