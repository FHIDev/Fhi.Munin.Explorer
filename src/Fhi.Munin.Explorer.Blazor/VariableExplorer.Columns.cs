using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which columns the result rows carry, and the control that turns them on and off.</summary>
public partial class VariableExplorer
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
    private string ColumnsHintId => $"variable-explorer-columns-hint-{_instance}";

    /// <summary>
    /// The column picker: a disclosure holding one toggle per optional column.
    /// </summary>
    /// <remarks>
    /// <para>
    /// helsedata's own shape, read off their compiled stylesheets rather than guessed at.
    /// Their variable page hangs this above the results in
    /// <c>variable-explorer-header__actions</c> and draws the open list as
    /// <c>ul.dropdown-choicepicker</c> with one <c>li.dropdown-choicepicker__item</c> per choice —
    /// the list is <c>position: absolute</c> under a trigger whose container is relative, which is
    /// what the inline style below supplies, exactly as their own React does inline.
    /// </para>
    /// <para>
    /// It is deliberately NOT <c>sortable-dropdown</c>, which the bead pointed at. That name is
    /// their MOBILE sort control — <c>.sortable-dropdown { display: none }</c> site-wide, revived
    /// only under <c>max-width: 1280px</c> — so wearing it would have hidden this picker on every
    /// desktop, silently, which is the failure mode this repository keeps rediscovering.
    /// </para>
    /// <para>
    /// A <c>&lt;details&gt;</c> rather than a button and a panel, for the reason the filter facets
    /// are: their dropdown opens, closes on Escape and closes on an outside click from React state,
    /// and this package ships no script. The element does the first two natively and costs nothing;
    /// what is lost is the outside click, which leaves the list open rather than broken.
    /// </para>
    /// <para>
    /// It wears two names, not one, and both are theirs: <c>variable-explorer__dropdown</c> is the
    /// z-index that lifts the open list over the rows below it
    /// (<c>.variable-explorer__dropdown { z-index: 99 }</c>), and the bare <c>dropdown</c> is what
    /// widens the trigger to its row — <c>variables.css</c> carries
    /// <c>.variable-explorer-header__actions .dropdown { width: 100% }</c>, unconditionally, beside
    /// the <c>dropdown-choicepicker</c> rule that puts the open list 36px down. Both were read back
    /// off the compiled stylesheet; neither is ours.
    /// </para>
    /// <para>
    /// Choosing <c>&lt;details&gt;</c> costs a host outside helsedata's estate two rules the
    /// element itself makes necessary: a <c>&lt;summary&gt;</c> is <c>display: list-item</c>, so
    /// without <c>list-style: none</c> and <c>::-webkit-details-marker { display: none }</c> the
    /// trigger draws a browser disclosure triangle beside "Kolonner" that their own button does not
    /// have. Both sample hosts carry them, and the host notes say so — helsedata's own control is a
    /// button, so nothing in their <c>variables.css</c> has a reason to suppress a marker here.
    /// The filter panel's <c>&lt;details&gt;</c> is not a precedent: its summary is not dressed as
    /// a button, so its marker is wanted.
    /// </para>
    /// <para>
    /// Toggle buttons rather than checkboxes, which is what the facet values already are. Their own
    /// items are a visually-hidden <c>checkbox__input</c> with a label drawing the box, and that
    /// pattern needs the DOM's checked state and the component's to agree — a refusal to hide the
    /// last column would leave the browser showing a box the component believes is still ticked. A
    /// button carries no state of its own, so <c>aria-pressed</c> is the whole truth.
    /// </para>
    /// </remarks>
    private RenderFragment ColumnPicker() => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "variable-explorer-header");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "variable-explorer-header__actions");

        builder.OpenElement(4, "details");
        // Two of their names rather than one: `dropdown` is the width rule their own actions row
        // applies to the trigger, `variable-explorer__dropdown` the z-index under the open list.
        // Both are in the host contract — see the remarks above.
        builder.AddAttribute(5, "class", "dropdown variable-explorer__dropdown");
        // Their own inline style, not a stylesheet: the list below is absolutely positioned and
        // anchors to the nearest positioned ancestor, and helsedata's React sets exactly this on
        // the same element. Without it an open list would hang off whatever happens to be
        // positioned further up the host's page.
        builder.AddAttribute(6, "style", "position:relative");

        // Dressed as their ghost square button, which is what their own trigger is. A <summary>
        // is display: list-item, so a host has to take the disclosure marker off it — two rules,
        // both in the host notes, and both sample hosts carry them.
        builder.OpenElement(7, "summary");
        builder.AddAttribute(8, "class",
            "hd-button-square button-square--ghost variable-explorer-header__actions-button");
        builder.AddContent(9, T.Columns);
        builder.CloseElement();

        builder.OpenElement(10, "ul");
        builder.AddAttribute(11, "class", "dropdown-choicepicker dropdown-choicepicker--right");

        foreach (var column in OptionalColumns)
        {
            var locked = ColumnLocked(column);

            builder.OpenElement(12, "li");
            builder.AddAttribute(13, "class", "dropdown-choicepicker__item");

            builder.OpenElement(14, "button");
            builder.AddAttribute(15, "class", "hd-button-reset");
            builder.AddAttribute(16, "type", "button");
            builder.AddAttribute(17, "aria-pressed", ColumnVisible(column) ? "true" : "false");
            // Inert rather than disabled, the same treatment the pager's buttons and Fjern alle
            // filtre get: `disabled` takes the control out of the tab order, so the one column a
            // reader might want to ask about would be the one they could not reach. ToggleColumn
            // is what makes it true.
            builder.AddAttribute(18, "aria-disabled", locked ? "true" : null);
            builder.AddAttribute(19, "aria-describedby", locked ? ColumnsHintId : null);
            builder.AddAttribute(20, "onclick", EventCallback.Factory.Create(this, () => ToggleColumn(column)));

            // The label as the button's own text, with no element and so no class name around it.
            // An earlier draft wrapped it in a span wearing `form-control__label`, which is a name
            // nothing else in this component uses and which could not be read back off Stiler's
            // compiled stylesheet — the one thing AGENTS.md says must never be guessed at, because
            // a name Stiler has never heard of renders as a raw browser default. The wrapper bought
            // nothing either: the item is a flex row and the button an inline-flex box, so a bare
            // text node is centred by the rules already on them.
            builder.AddContent(21, ColumnLabel(column));

            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();

        // Why the last one refuses, said once and pointed at rather than repeated on every button.
        // Always in the DOM so the reference is never dangling: aria-describedby resolves against
        // hidden text, and a paragraph that appeared only when a column locked would be one more
        // node arriving in the same update as the attribute naming it.
        builder.OpenElement(22, "p");
        builder.AddAttribute(23, "class", "screenreader-only");
        builder.AddAttribute(24, "id", ColumnsHintId);
        builder.AddContent(25, T.LastColumnHint);
        builder.CloseElement();

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    };
}
