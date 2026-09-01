using System.Collections.ObjectModel;
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

        Table(builder, variable, texts);
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
    private static void Table(RenderTreeBuilder builder, VariableDetail variable, Texts texts)
    {
        builder.OpenElement(10, "table");
        builder.AddAttribute(11, "class", "munin-explorer-statistics");

        builder.OpenElement(12, "thead");
        builder.OpenElement(13, "tr");
        HeaderCell(builder, 20, texts.FieldYear);
        HeaderCell(builder, 30, texts.FieldMinimum);
        HeaderCell(builder, 40, texts.FieldMaximum);
        HeaderCell(builder, 50, texts.FieldMean);
        HeaderCell(builder, 60, texts.FieldStandardDeviation);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(14, "tbody");

        var seq = 100;

        foreach (var statistic in variable.Statistics)
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
