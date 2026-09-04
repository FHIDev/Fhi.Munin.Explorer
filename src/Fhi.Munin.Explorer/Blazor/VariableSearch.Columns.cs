using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which columns the result rows carry, and the control that turns them on and off.</summary>
public partial class VariableSearch
{
    /// <summary>
    /// A column the reader can turn off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runa's seven, in Runa's order. Navn is deliberately not among them, in Runa either: it is
    /// the row's own disclosure button as well as its first column, so hiding it would take the
    /// control that opens the panel off the screen along with the value.
    /// </para>
    /// <para>
    /// English identifiers, with the three Norwegian catalogue terms kept as they are — a
    /// <c>kilde</c> is not a source, a <c>datasamling</c> is not a collection, and renaming them
    /// here would break the link to the field names the API answers with. See AGENTS.md.
    /// </para>
    /// </remarks>
    private enum ResultColumn
    {
        Code,
        Kilde,
        Datasamling,
        Variabelgruppe,
        DataType,
        Status,
        DataPeriod,
    }

    /// <summary>The columns the picker offers, in the order it lists them.</summary>
    /// <remarks>
    /// The enum itself rather than a list restating it: two copies of one list drift apart
    /// independently, and a column added to <see cref="ResultColumn"/> without a line here would
    /// be one the reader could see but not turn off.
    /// </remarks>
    private static readonly ResultColumn[] OptionalColumns = Enum.GetValues<ResultColumn>();

    /// <summary>
    /// The columns the reader has turned off.
    /// </summary>
    /// <remarks>
    /// Not persisted and not in the host's URL, which is what Runa does today: the choice lasts as
    /// long as the page does and comes back complete on a refresh. Whether it should be remembered
    /// is a decision of its own — it needs somewhere to live, and this component deliberately owns
    /// no storage and no URL. Nothing here makes that harder: one field and one method is what a
    /// host-facing parameter would hook into.
    /// </remarks>
    private readonly HashSet<ResultColumn> _hiddenColumns = [];

    /// <summary>Whether the reader has made a choice about the Status column themselves.</summary>
    /// <remarks>
    /// Until they have, Status follows <see cref="ShowStatusColumn"/> — drawn only when historical
    /// variables can be in the list at all, because otherwise every row says the same word and a
    /// column that says the same word on every row is furniture rather than information. The one
    /// exception is <see cref="StatusIsAllThatIsLeft"/>, where furniture beats an empty row. Once
    /// the reader has pressed it, their choice wins and the filter stops moving it: a column that
    /// reappeared on its own after being turned off would be the picker undoing itself.
    /// </remarks>
    private bool _statusColumnChosen;

    /// <summary>Whether a column is on screen.</summary>
    private bool ColumnVisible(ResultColumn column) =>
        column == ResultColumn.Status && !_statusColumnChosen
            ? ShowStatusColumn || StatusIsAllThatIsLeft
            : !_hiddenColumns.Contains(column);

    /// <summary>
    /// Whether Status is the only optional column the reader has not turned off.
    /// </summary>
    /// <remarks>
    /// The one case where an untouched Status column stays on screen against the filter, and it is
    /// there to keep the filter from doing what <see cref="ColumnLocked"/> forbids the picker to
    /// do. A reader browsing historical variables can hide the other six, at which point Status is
    /// locked as the last one left; turning "Vis historiske" off again would otherwise take it too
    /// and leave rows of nothing but names — the exact state the lock exists to prevent, reached
    /// through a control that says nothing about columns. Holding Status instead means the filter
    /// changes what is in the list without changing what a row says about it.
    /// <para>
    /// Reads <see cref="_hiddenColumns"/> directly rather than asking
    /// <see cref="ColumnVisible(ResultColumn)"/>, which would call back into this. For every column
    /// but Status the two say the same thing.
    /// </para>
    /// </remarks>
    private bool StatusIsAllThatIsLeft =>
        OptionalColumns.All(c => c == ResultColumn.Status || _hiddenColumns.Contains(c));

    /// <summary>How many of the optional columns are on screen.</summary>
    private int VisibleColumnCount => OptionalColumns.Count(ColumnVisible);

    /// <summary>
    /// Whether this column is the only one left, and so refuses to be turned off.
    /// </summary>
    /// <remarks>
    /// The last visible column cannot be turned off, which is Runa's rule. Navn stays whatever
    /// happens, so the rule is about the seven optional ones: a row of nothing but names is a list
    /// the picker could talk the reader into and not out of, since the way back is the same control
    /// that emptied it.
    /// <para>
    /// This constrains what the READER can turn off, and the filter cannot get around it. Turning
    /// "Vis historiske" back off normally takes an untouched Status column away, which is the
    /// filter's own doing and fine — unless Status is the only column left, in which case it stays.
    /// See <see cref="StatusIsAllThatIsLeft"/>: without that, six presses in the picker and one on
    /// a filter nobody associates with columns would together empty every row down to its name.
    /// </para>
    /// </remarks>
    private bool ColumnLocked(ResultColumn column) => ColumnVisible(column) && VisibleColumnCount == 1;

    /// <summary>Turns a column on or off, unless it is the last one left.</summary>
    /// <remarks>
    /// The ordering is deliberately left alone, including when the column being turned off is the
    /// one the list is ordered by. Hiding Kilde while the list is sorted by it keeps the rows in
    /// that order and takes the header cell carrying <c>aria-sort</c> and the arrow away with the
    /// column, so the way back to reversing it is to show the column again — which is what Excel
    /// does with a sort on a hidden column, and the alternative is worse: sorting is server-side,
    /// so falling back to the default order here would mean a press on a column control firing a
    /// query and reordering the list underneath the reader, with a failure path of its own.
    /// Nothing is lost silently — <see cref="Summary"/> still names the ordering, and
    /// it is a polite live region, so hiding the column does not stop the list from saying how it
    /// is ordered.
    /// </remarks>
    private void ToggleColumn(ResultColumn column)
    {
        if (ColumnLocked(column))
        {
            return;
        }

        // Read before the write, because Status's visibility is not simply "not hidden" until the
        // reader has chosen: with the filter hiding it, adding it to _hiddenColumns on a press
        // meant to SHOW it would leave it exactly as invisible as before.
        var visible = ColumnVisible(column);

        if (column == ResultColumn.Status)
        {
            _statusColumnChosen = true;
        }

        if (visible)
        {
            _hiddenColumns.Add(column);
        }
        else
        {
            _hiddenColumns.Remove(column);
        }
    }

    /// <summary>A column's name, in the words the header above it uses.</summary>
    /// <remarks>
    /// The same strings the header cells and the rows' own screen-reader labels carry, so the
    /// picker and the column it turns off are never two names for one thing. An unknown member
    /// throws for the reason <see cref="Texts.FieldLabel"/> does: a column added to
    /// <see cref="ResultColumn"/> without a word here would sit in the list unlabelled.
    /// </remarks>
    private string ColumnLabel(ResultColumn column) => column switch
    {
        ResultColumn.Code => T.FieldCode,
        ResultColumn.Kilde => T.FieldSource,
        ResultColumn.Datasamling => T.FieldDataCollection,
        ResultColumn.Variabelgruppe => T.FieldVariableGroup,
        ResultColumn.DataType => T.FieldDataType,
        ResultColumn.Status => T.FieldStatus,
        ResultColumn.DataPeriod => T.FieldDataPeriod,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "No label for this column.")
    };

    /// <summary>The hint the locked column points at, minted per instance like the other ids.</summary>
    private string ColumnsHintId => $"munin-explorer-columns-hint-{_instance}";

    /// <summary>The column picker, drawn by the shared <see cref="Fhi.Munin.Explorer.Blazor.ColumnPicker"/>.</summary>
    /// <remarks>
    /// The markup, the borrowed Stiler names and the decisions behind them live there, because the
    /// kilde table hangs the same control over its own columns and a second copy would be a second
    /// place for them to drift. What stays here is what is this list's own: which columns there
    /// are, what each is called, and the rule that the last one cannot be turned off.
    /// </remarks>
    private RenderFragment ColumnPicker() =>
        Blazor.ColumnPicker.For(
            this,
            T.Columns,
            [.. OptionalColumns.Select(column => new Blazor.ColumnPicker.Choice(
                ColumnLabel(column),
                ColumnVisible(column),
                ColumnLocked(column),
                () => ToggleColumn(column)))],
            (ColumnsHintId, T.LastColumnHint));
}
