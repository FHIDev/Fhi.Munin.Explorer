namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which shape <see cref="StatisticsBlock.For"/> draws a variable's statistics in.</summary>
internal enum StatisticsLayout
{
    /// <summary>Every row as a table, with a frequency table per row: the whole-variable view.</summary>
    Table,

    /// <summary>
    /// The newest statistic as a coverage line, frequency bars and figures: the row drawer's Data tab.
    /// </summary>
    Drawer,
}
