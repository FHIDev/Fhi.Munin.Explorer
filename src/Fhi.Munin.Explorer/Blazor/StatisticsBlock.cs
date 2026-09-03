using System.Collections.ObjectModel;
using System.Globalization;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A variable's statistics — the heading and the table — or nothing at all when it has none.
/// </summary>
/// <remarks>
/// Shared by the full variable view and the result row's Data tab, which report the same numbers
/// about the same variable. Written twice they would look alike and could stop being alike without
/// anything failing, which is the reason <see cref="RowCell"/> exists too.
/// <para>
/// The heading is inside the guard rather than beside it at each call site. "Nothing at all" is the
/// half that is easy to get wrong — a heading drawn over an empty table passes any test written
/// with rich data only — so the emptiness question is answered once here instead of once per
/// caller.
/// </para>
/// <para>
/// Level and class are the caller's because the block sits at two different depths in two
/// different neighbourhoods: a section of the full view, and a peer of the kodeverk groups inside
/// a result row's tab. Everything that makes it the statistics block rather than some other table
/// is here; only where it sits is not.
/// </para>
/// </remarks>
internal static class StatisticsBlock
{
    /// <summary>The block for <paramref name="variable"/>, or an empty fragment when it has none.</summary>
    internal static RenderFragment For(
        VariableDetail? variable, int headingLevel, string headingClass, Texts texts) => builder =>
    {
        if (variable is not { Statistics.Count: > 0 })
        {
            return;
        }

        builder.OpenElement(0, $"h{headingLevel}");
        builder.AddAttribute(1, "class", headingClass);
        builder.AddContent(2, Heading(variable.DatasamlingStatisticsType, texts));
        builder.CloseElement();

        var rows = Rows(variable);

        Table(builder, rows, variable.DatasamlingStatisticsType, texts);

        var seq = 10_000;

        foreach (var statistic in rows)
        {
            seq = FrequencyTable(builder, seq, statistic, texts);
        }
    };

    /// <summary>
    /// The heading, which names the kind of statistics rather than just saying "Statistikk".
    /// </summary>
    /// <remarks>
    /// Runa writes "Statistikk (Årsbasert)". The kind matters to a reader deciding what the numbers
    /// mean: a yearly set is one row per year, and an accumulated one is a running total that only
    /// its last row describes.
    /// <para>
    /// Shared with <see cref="DatasamlingView"/> rather than private: the type it names belongs to
    /// the datasamling, and a variable reports the one it is pinned into, so two spellings of this
    /// heading would be two spellings of one fact.
    /// </para>
    /// </remarks>
    internal static string Heading(string? statisticsType, Texts texts) =>
        statisticsType is { } type && !string.IsNullOrWhiteSpace(type)
            ? $"{texts.HeadingStatistics} ({texts.StatisticsTypeLabel(type)})"
            : texts.HeadingStatistics;

    /// <summary>
    /// The columns Runa shows, in Runa's order.
    /// </summary>
    /// <remarks>
    /// An absent number is a dash rather than a blank, so a reader can tell "not measured" from a
    /// cell that failed to draw.
    /// </remarks>
    private static void Table(
        RenderTreeBuilder builder, IReadOnlyList<Statistic> rows, string? statisticsType, Texts texts)
    {
        builder.OpenElement(10, "table");
        builder.AddAttribute(11, "class", "munin-explorer-statistics");

        builder.OpenElement(12, "thead");
        builder.OpenElement(13, "tr");
        HeaderCell(
            builder, 20, IsAccumulated(statisticsType) ? texts.ColumnLastUpdated : texts.FieldYear);
        HeaderCell(builder, 30, texts.FieldMinimum);
        HeaderCell(builder, 40, texts.FieldMaximum);
        HeaderCell(builder, 50, texts.FieldMean);
        HeaderCell(builder, 60, texts.FieldStandardDeviation);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(14, "tbody");

        var seq = 100;

        foreach (var statistic in rows)
        {
            // Null-coalesced although Statistic.AdditionalProperties is declared non-nullable — see
            // that declaration for how a null gets in, and NullAsEmptyCollections for what stops it
            // arriving from this package's own client. A host can substitute that client, and
            // unguarded one such statistic throws while rendering, past the try/catch around the
            // fetch, which on a Blazor Server host takes the circuit and the page it is mounted in
            // down. Read as the empty bag it means, the row draws the dash Value already gives a
            // key the catalogue holds no number for.
            var props = statistic.AdditionalProperties ?? ReadOnlyDictionary<string, string?>.Empty;

            builder.OpenElement(seq, "tr");

            // The year heads its own row: every other cell is a number about that year, and a
            // screen reader reading one out of context should hear which year it belongs to.
            builder.OpenElement(seq + 1, "th");
            builder.AddAttribute(seq + 2, "scope", "row");
            builder.AddContent(seq + 3, Value(props, "SisteOppdaterteAarssett"));
            builder.CloseElement();

            Cell(builder, seq + 10, Value(props, "MIN"));
            Cell(builder, seq + 20, Value(props, "MAX"));
            Cell(builder, seq + 30, Value(props, "AVG"));
            Cell(builder, seq + 40, Value(props, "STD"));

            builder.CloseElement();
            seq += 100;
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    // An accumulated set is a running total, so only its last row describes the data — and in prod
    // that is the common shape, not an edge (Fhi.Metadata-e3e2d).
    private static IReadOnlyList<Statistic> Rows(VariableDetail variable) =>
        IsAccumulated(variable.DatasamlingStatisticsType) && variable.Statistics.Count > 0
            ? [variable.Statistics[^1]]
            : variable.Statistics;

    /// <remarks>Both spellings, matching <see cref="Texts.StatisticsTypeLabel"/>.</remarks>
    private static bool IsAccumulated(string? type) =>
        type is { } kind
        && (kind.Equals("accumulated", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("akkumulert", StringComparison.OrdinalIgnoreCase));

    // Verdi is KodeverkLokalID and NOT Code, which is fully qualified; the share divides by the
    // statistic's own GyldigeTilfeller, never the row sum, and its bar is clipped where the number
    // is not; Beskrivelse is null on every row. Measurements for all four: Fhi.Metadata-e3e2d.
    private static int FrequencyTable(RenderTreeBuilder builder, int seq, Statistic statistic, Texts texts)
    {
        var frequencies = statistic.CodeFrequencies;

        if (frequencies is not { Count: > 0 })
        {
            return seq;
        }

        var valid = Number(statistic.AdditionalProperties, "GyldigeTilfeller");

        builder.OpenElement(seq, "table");
        builder.AddAttribute(seq + 1, "class", "munin-explorer-frequency");

        builder.OpenElement(seq + 2, "thead");
        builder.OpenElement(seq + 3, "tr");
        HeaderCell(builder, seq + 4, texts.ColumnCodeValue);
        HeaderCell(builder, seq + 7, texts.ColumnCategory);
        HeaderCell(builder, seq + 10, texts.ColumnShareOfValid);
        HeaderCell(builder, seq + 13, texts.ColumnCount);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(seq + 16, "tbody");

        var row = seq + 20;

        foreach (var frequency in frequencies)
        {
            var properties = frequency.AdditionalProperties ?? ReadOnlyDictionary<string, string?>.Empty;
            var count = Number(properties, "GyldigeTilfeller");

            builder.OpenElement(row, "tr");

            // The code value heads its own row: the cells beside it are numbers about that value,
            // and a screen reader reading one out of context should hear which value it belongs to.
            builder.OpenElement(row + 1, "th");
            builder.AddAttribute(row + 2, "scope", "row");
            builder.AddContent(row + 3, Value(properties, "KodeverkLokalID"));
            builder.CloseElement();

            Cell(builder, row + 10, string.IsNullOrWhiteSpace(frequency.PreferredTerm) ? "—" : frequency.PreferredTerm);
            ShareCell(builder, row + 20, count, valid, texts);
            Cell(builder, row + 40, count is { } number ? Value(properties, "GyldigeTilfeller") : "—");

            builder.CloseElement();
            row += 50;
        }

        builder.CloseElement();
        builder.CloseElement();

        return row + 50;
    }

    // A missing or zero denominator draws a dash, not a bar of no length: "cannot be worked out"
    // and "never occurs" are different facts, and an empty bar states the second.
    private static void ShareCell(
        RenderTreeBuilder builder, int seq, double? count, double? valid, Texts texts)
    {
        builder.OpenElement(seq, "td");

        if (count is { } numerator && valid is { } denominator && denominator > 0)
        {
            var share = numerator / denominator * 100;

            builder.AddContent(seq + 1, texts.ShareOfValid(share));

            builder.OpenElement(seq + 2, "span");
            builder.AddAttribute(seq + 3, "class", "munin-explorer-frequency__track");
            builder.OpenElement(seq + 4, "span");
            builder.AddAttribute(seq + 5, "class", "munin-explorer-frequency__fill");
            builder.AddAttribute(
                seq + 6,
                "style",
                $"width:{Math.Clamp(share, 0, 100).ToString("0.#", CultureInfo.InvariantCulture)}%");
            builder.CloseElement();
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(seq + 7, "—");
        }

        builder.CloseElement();
    }

    /// <summary>A property as a number, or null where the catalogue holds none that parses.</summary>
    private static double? Number(IReadOnlyDictionary<string, string?>? properties, string key) =>
        properties is not null
        && properties.TryGetValue(key, out var raw)
        && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>A statistic's value, or a dash where the catalogue holds none.</summary>
    private static string Value(IReadOnlyDictionary<string, string?> properties, string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "—";

    private static void HeaderCell(RenderTreeBuilder builder, int seq, string label)
    {
        builder.OpenElement(seq, "th");
        builder.AddAttribute(seq + 1, "scope", "col");
        builder.AddContent(seq + 2, label);
        builder.CloseElement();
    }

    private static void Cell(RenderTreeBuilder builder, int seq, string? value)
    {
        builder.OpenElement(seq, "td");
        builder.AddContent(seq + 1, value);
        builder.CloseElement();
    }
}
