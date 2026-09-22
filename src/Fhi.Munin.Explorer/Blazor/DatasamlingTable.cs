using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One level's datasamlinger, as a table.
/// </summary>
/// <remarks>
/// Shared rather than private to the kilde view, because the kilde explorer's expanded row reports
/// the same datasamlinger about the same kilde and a reader comparing two rows should not be shown
/// different columns than when they open one. Written twice they would look alike and could stop
/// being alike without anything failing — the reason <see cref="StatisticsBlock"/> is shared too.
/// </remarks>
internal static class DatasamlingTable
{
    /// <summary>The marks a reader can put on these rows, and who to tell about a press.</summary>
    /// <remarks>
    /// Optional rather than a second table: <see cref="KildeView"/> renders the same rows with no
    /// marks at all, and two tables would be two places for the columns to stop agreeing. Which
    /// kilde a mark belongs to is the caller's to remember — see <c>KildeSearch.Selection.cs</c>,
    /// where the pair is what the address carries. (Fhi.Metadata-75yov)
    /// </remarks>
    internal sealed record Selection(
        Func<Guid, bool> IsMarked,
        Func<Guid, bool, Task> Toggle,
        IHandleEvent Receiver);

    /// <summary>The table as a fragment, for callers that write markup rather than build it.</summary>
    internal static RenderFragment For(
        IReadOnlyList<KildeDatasamling> rows,
        Texts texts,
        string? language,
        string reader,
        Selection? selection = null) => builder =>
    {
        var seq = 0;

        Render(builder, ref seq, rows, texts, language, reader, selection);
    };

    /// <summary>
    /// Each table keeps its own <c>thead</c>: a table is what ties a cell to its column heading for
    /// a screen reader, so one borrowing another's has none.
    /// </summary>
    internal static void Render(
        RenderTreeBuilder builder,
        ref int seq,
        IReadOnlyList<KildeDatasamling> rows,
        Texts texts,
        string? language,
        string reader,
        Selection? selection = null)
    {
        if (rows.Count == 0)
        {
            return;
        }

        builder.OpenElement(seq++, "table");

        // The modifier, and not the leading cell's class alone: Stiler sizes this table's columns
        // by position, so a column in front of Navn moves every one of those rules along and the
        // plain table has to keep today's. (Fhi.Metadata-h6dx7)
        builder.AddAttribute(seq++, "class", selection is null
            ? "munin-explorer-kilde__datasamlinger"
            : "munin-explorer-kilde__datasamlinger munin-explorer-kilde__datasamlinger--selectable");

        builder.OpenElement(seq++, "thead");
        builder.OpenElement(seq++, "tr");

        if (selection is not null)
        {
            SelectHeaderCell(builder, ref seq, texts.SelectDatasamlingColumn);
        }

        HeaderCell(builder, ref seq, texts.FieldName);
        HeaderCell(builder, ref seq, texts.FieldDescription);
        HeaderCell(builder, ref seq, texts.FieldValidity);
        HeaderCell(builder, ref seq, texts.FieldTotalVariables);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(seq++, "tbody");

        foreach (var row in rows)
        {
            builder.OpenElement(seq++, "tr");

            if (selection is not null)
            {
                SelectCell(builder, ref seq, row, texts, selection);
            }

            // The name is a th, not a td: it is what the rest of the row is about, and a screen
            // reader reading a cell out of context should hear which datasamling it belongs to.
            // A datasamling carries no code, so the short name is what stands in - and an empty th
            // would cost every other cell in the row its row header, which is the one place the
            // bead was right about the header (Fhi.Metadata-w13lk).
            var named = texts.Named(row.Name, row.ShortName);

            builder.OpenElement(seq++, "th");
            builder.AddAttribute(seq++, "scope", "row");
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign(named.Norwegian, reader));
            builder.AddContent(seq++, named.Norwegian && !string.IsNullOrWhiteSpace(row.ShortName)
                ? $"{named.Text} ({row.ShortName})"
                : named.Text);
            builder.CloseElement();

            DescriptionCell(builder, ref seq, row.Description, reader);
            Cell(builder, ref seq,
                 CatalogueDate.Period(row.EffectiveValidFrom, row.EffectiveValidTo, language, texts),
                 reader,
                 norwegian: false);
            Cell(builder, ref seq, $"{row.VariableCount} {texts.VariableCountSuffix}", reader, norwegian: false);

            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// The leading heading, which holds a word a sighted reader never sees.
    /// </summary>
    /// <remarks>
    /// Empty would be the tidier-looking cell and is the wrong one: a screen reader announces a
    /// checkbox with the column it stands in, and a nameless column names none of them.
    /// </remarks>
    private static void SelectHeaderCell(RenderTreeBuilder builder, ref int seq, string label)
    {
        builder.OpenElement(seq++, "th");
        builder.AddAttribute(seq++, "scope", "col");
        builder.AddAttribute(seq++, "class", "munin-explorer-kilde__datasamling-select");
        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "screenreader-only");
        builder.AddContent(seq++, label);
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>One row's mark. A td, not a th: the box is a control over the row, not its label.</summary>
    /// <remarks>
    /// <c>SetUpdatesAttributeName</c> for <see cref="ColumnPicker"/>'s reason: the browser ticks
    /// the box itself before any handler runs, so a render equal to the one before it leaves the
    /// DOM saying something the component does not.
    /// </remarks>
    private static void SelectCell(
        RenderTreeBuilder builder,
        ref int seq,
        KildeDatasamling row,
        Texts texts,
        Selection selection)
    {
        var named = texts.Named(row.Name, row.ShortName);

        builder.OpenElement(seq++, "td");
        builder.AddAttribute(seq++, "class", "munin-explorer-kilde__datasamling-select");

        builder.OpenElement(seq++, "input");
        builder.AddAttribute(seq++, "type", "checkbox");
        builder.AddAttribute(seq++, "aria-label", texts.SelectDatasamling(named.Text));
        builder.AddAttribute(seq++, "checked", selection.IsMarked(row.Id));
        builder.AddAttribute(seq++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
            selection.Receiver, e => selection.Toggle(row.Id, e.Value is true)));
        builder.SetUpdatesAttributeName("checked");
        builder.CloseElement();

        builder.CloseElement();
    }

    private static void HeaderCell(RenderTreeBuilder builder, ref int seq, string label)
    {
        builder.OpenElement(seq++, "th");
        builder.AddAttribute(seq++, "scope", "col");
        builder.AddContent(seq++, label);
        builder.CloseElement();
    }

    /// <summary>
    /// The beskrivelse column. Catalogue authors write markdown links and line breaks into that
    /// field (FHIDev/Munin#5385), and the fragment scopes its own sequence numbers, so the varying
    /// markdown structure never shifts the cells after it.
    /// </summary>
    private static void DescriptionCell(RenderTreeBuilder builder, ref int seq, string? value, string reader)
    {
        builder.OpenElement(seq++, "td");
        builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", reader));
        builder.AddContent(seq++, CatalogueMarkdown.Render(value));
        builder.CloseElement();
    }

    private static void Cell(
        RenderTreeBuilder builder, ref int seq, string? value, string reader, bool norwegian)
    {
        builder.OpenElement(seq++, "td");

        if (norwegian)
        {
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", reader));
        }

        builder.AddContent(seq++, value);
        builder.CloseElement();
    }
}
