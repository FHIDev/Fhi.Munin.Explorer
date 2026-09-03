using Fhi.Munin.Explorer.Contracts;
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
        string reader)
    {
        if (rows.Count == 0)
        {
            return;
        }

        builder.OpenElement(seq++, "table");
        builder.AddAttribute(seq++, "class", "munin-explorer-kilde__datasamlinger");

        builder.OpenElement(seq++, "thead");
        builder.OpenElement(seq++, "tr");
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

            // The name is a th, not a td: it is what the rest of the row is about, and a screen
            // reader reading a cell out of context should hear which datasamling it belongs to.
            builder.OpenElement(seq++, "th");
            builder.AddAttribute(seq++, "scope", "row");
            builder.AddAttribute(seq++, "lang", CatalogueProperties.Foreign("no", reader));
            builder.AddContent(seq++, string.IsNullOrWhiteSpace(row.ShortName)
                ? row.Name
                : $"{row.Name} ({row.ShortName})");
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
