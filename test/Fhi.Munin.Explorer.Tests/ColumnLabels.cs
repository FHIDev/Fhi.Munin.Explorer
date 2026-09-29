using AngleSharp.Dom;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one check that a table's cells name their columns, for Stiler's cards at 767px and below.
/// </summary>
/// <remarks>
/// Compared against the header's own text rather than against a list of words, so a column added
/// or renamed is covered without this file changing. (Fhi.Metadata-n8ygv)
/// </remarks>
internal static class ColumnLabels
{
    internal static void AssertEveryCellNamesItsColumn(IElement table)
    {
        var headers = table.QuerySelectorAll("thead th").Select(th => th.TextContent.Trim()).ToList();
        var rows = table.QuerySelectorAll("tbody tr");

        Assert.NotEmpty(table.QuerySelectorAll("tbody td"));

        foreach (var row in rows)
        {
            var cells = row.Children.ToList();

            Assert.Equal(headers.Count, cells.Count);

            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i].TagName == "TD")
                {
                    Assert.Equal(headers[i], cells[i].GetAttribute("data-label"));
                }
            }
        }
    }
}
