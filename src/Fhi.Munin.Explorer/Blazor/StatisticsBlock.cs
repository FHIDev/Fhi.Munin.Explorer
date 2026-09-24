using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A variable's statistics — the heading and the table, or the drawer's coverage, bars and
/// figures — or nothing at all when it has none.
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
    /// <remarks>
    /// The drawer draws the round-5 summary of one statistic; the whole-variable view keeps the
    /// table, which has the room for every year set and the standard deviation (Fhi.Metadata-9mxmw).
    /// </remarks>
    internal static RenderFragment For(
        VariableDetail? variable, int headingLevel, string headingClass, Texts texts,
        StatisticsLayout layout = StatisticsLayout.Table) => builder =>
    {
        if (!AnyStatistics(variable, layout))
        {
            return;
        }

        builder.OpenElement(0, $"h{headingLevel}");
        builder.AddAttribute(1, "class", headingClass);
        builder.AddContent(2, HeadingFor(variable, texts));
        builder.CloseElement();

        var rows = Rows(variable);

        if (layout is StatisticsLayout.Drawer)
        {
            Summary(builder, Newest(rows.Where(Says)), LatestYearSet(variable), texts);
            return;
        }

        Table(builder, rows, variable.DatasamlingStatisticsType, texts);

        var seq = 10_000;

        foreach (var statistic in rows)
        {
            seq = FrequencyTable(builder, seq, statistic, texts);
        }
    };

    /// <summary>
    /// Whether <see cref="For"/> would draw anything, so a caller can wrap it in a section.
    /// </summary>
    /// <remarks>
    /// The guard inside <see cref="For"/> is this same call: a wrapper that answered the emptiness
    /// question for itself could leave a section holding a heading and nothing else. Not to be
    /// confused with <see cref="DatasamlingView"/>'s own <c>AnyStatistics</c>, which asks
    /// <see cref="DetailBlocks.AnyFacts"/> of a datasamling's fact rows: a different question about
    /// a different type, spelled the same because both gate a statistics section.
    /// <para>
    /// A statistic counts only when it has something to draw or a recorded reason for having
    /// nothing. A bare year set is neither, and must read as absent rather than as withheld: a
    /// suppression note over it would claim FHI holds data it may not hold (Fhi.Metadata-35w0p.36).
    /// </para>
    /// <para>
    /// Asked per layout because the table has a standard deviation column and the drawer does not:
    /// a statistic holding only STD is something to draw in one and nothing in the other.
    /// </para>
    /// </remarks>
    internal static bool AnyStatistics(
        [NotNullWhen(true)] VariableDetail? variable, StatisticsLayout layout = StatisticsLayout.Table) =>
        variable is { Statistics.Count: > 0 }
        && Rows(variable).Any(layout is StatisticsLayout.Drawer ? Says : statistic =>
            Says(statistic) || Raw(statistic.AdditionalProperties, "STD") is not null);

    private static bool Says(Statistic statistic)
    {
        var properties = statistic.AdditionalProperties ?? ReadOnlyDictionary<string, string?>.Empty;

        return statistic.DisclosureControl is not null
            || statistic.CodeFrequencies is { Count: > 0 }
            || Coverage(properties) is not null
            || Figures(properties).Any();
    }

    /// <summary>
    /// The newest SisteOppdaterteAarssett across every statistics row, or null when none has one.
    /// </summary>
    /// <remarks>
    /// Compared as numbers where both parse, so "2022" beats "2019" and "999" does not beat "2022";
    /// a value that does not parse falls back to ordinal order rather than being dropped.
    /// </remarks>
    internal static string? LatestYearSet(VariableDetail variable)
    {
        string? latest = null;

        foreach (var statistic in variable.Statistics)
        {
            if (Raw(statistic.AdditionalProperties, "SisteOppdaterteAarssett") is not { } candidate)
            {
                continue;
            }

            candidate = candidate.Trim();

            if (latest is null || CompareYearSets(candidate, latest) > 0)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    private static int CompareYearSets(string a, string b) =>
        long.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
        && long.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            ? x.CompareTo(y)
            : string.CompareOrdinal(a, b);

    /// <summary>
    /// The heading <see cref="For"/> will emit for this variable, for a caller that has to name
    /// the block without drawing it.
    /// </summary>
    /// <remarks>
    /// The contents nav labels its statistics entry with this. Read rather than rebuilt from
    /// <see cref="Heading"/> at the call site: which field the heading is named from is this
    /// block's answer, so a second spelling of it here would drift with nothing failing.
    /// </remarks>
    internal static string HeadingFor(VariableDetail? variable, Texts texts) =>
        Heading(variable?.DatasamlingStatisticsType, texts);

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
            ? $"{texts.HeadingStatistics} ({KindWord(type, texts)})"
            : texts.HeadingStatistics;

    // Our word for a kind in lower case, as UI asked (Fhi.Metadata-35w0p.24); a token we have no
    // word for keeps the spelling it arrived in, as the row beside the heading shows it.
    private static string KindWord(string type, Texts texts) =>
        IsAccumulated(type) || type.Equals("yearly", StringComparison.OrdinalIgnoreCase)
            ? texts.StatisticsTypeLabel(type).ToLowerInvariant()
            : type;

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

        // Several of these stack on a yearly variable. screenreader-only is Stiler's own class,
        // so the name is announced without a second visible heading over each table.
        builder.OpenElement(seq + 2, "caption");
        builder.AddAttribute(seq + 3, "class", "screenreader-only");
        builder.AddContent(seq + 4, texts.FrequencyCaption(Raw(statistic.AdditionalProperties, "SisteOppdaterteAarssett")));
        builder.CloseElement();

        builder.OpenElement(seq + 5, "thead");
        builder.OpenElement(seq + 6, "tr");
        HeaderCell(builder, seq + 7, texts.ColumnCodeValue);
        HeaderCell(builder, seq + 10, texts.ColumnCategory);
        HeaderCell(builder, seq + 13, texts.ColumnShareOfValid);
        HeaderCell(builder, seq + 16, texts.ColumnCount);
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(seq + 19, "tbody");

        var row = seq + 22;

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

    // Value() answers with a dash, which is right in a cell and wrong inside a sentence.
    private static string? Raw(IReadOnlyDictionary<string, string?>? properties, string key) =>
        properties is not null
        && properties.TryGetValue(key, out var value)
        && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

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

    // The newest year set among the rows given, compared as LatestYearSet compares; a row with no
    // year set gives way to any later row and never displaces one that has. With no year set
    // anywhere, the last row, as an accumulated set's is.
    private static Statistic Newest(IEnumerable<Statistic> rows)
    {
        Statistic? newest = null;
        string? newestYear = null;

        foreach (var statistic in rows)
        {
            var year = Raw(statistic.AdditionalProperties, "SisteOppdaterteAarssett")?.Trim();

            if (newest is null || newestYear is null || (year is not null && CompareYearSets(year, newestYear) >= 0))
            {
                newest = statistic;
                newestYear = year;
            }
        }

        return newest!;
    }

    private static void Summary(RenderTreeBuilder builder, Statistic statistic, string? latestYearSet, Texts texts)
    {
        var properties = statistic.AdditionalProperties ?? ReadOnlyDictionary<string, string?>.Empty;

        // Om variabelen names the newest year set even when it holds no statistics, so statistics
        // drawn from an older one say which, or a reader takes them for the year named there.
        if (Raw(properties, "SisteOppdaterteAarssett")?.Trim() is { } year
            && latestYearSet is not null
            && CompareYearSets(year, latestYearSet) < 0)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "caption");
            builder.AddContent(2, texts.StatisticsFromOlderYearSet(year));
            builder.CloseElement();
        }

        builder.OpenRegion(100);
        CoverageLine(builder, properties, texts);
        builder.CloseRegion();

        builder.OpenRegion(200);
        if (statistic.DisclosureControl is Statistic.CategoriesNotSummarised)
        {
            SuppressedNote(builder, texts.SuppressedCategories);
        }
        else
        {
            Distribution(builder, statistic.CodeFrequencies, texts);
        }
        builder.CloseRegion();

        builder.OpenRegion(300);
        if (statistic.DisclosureControl is Statistic.DescriptiveStatisticsNotGiven)
        {
            SuppressedNote(builder, texts.SuppressedDescriptiveStatistics);
        }
        else
        {
            FigureList(builder, properties, texts);
        }
        builder.CloseRegion();
    }

    // Both counts or no line: coverage is valid over valid plus missing, and summing the
    // frequencies instead gives the valid cases alone, so it would always read 100 %.
    private static (long Valid, long Missing, int Share)? Coverage(IReadOnlyDictionary<string, string?> properties)
    {
        if (Count(properties, "GyldigeTilfeller") is not { } valid
            || Count(properties, "ManglendeTilfeller") is not { } missing
            || (double)valid + missing == 0)
        {
            return null;
        }

        var share = (int)Math.Round(valid * 100.0 / ((double)valid + missing), MidpointRounding.AwayFromZero);

        return (valid, missing, share);
    }

    private static void CoverageLine(RenderTreeBuilder builder, IReadOnlyDictionary<string, string?> properties, Texts texts)
    {
        if (Coverage(properties) is not { } coverage)
        {
            return;
        }

        var (valid, missing, share) = coverage;

        var validText = texts.CaseCount(valid);
        var missingText = texts.CaseCount(missing);

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-coverage");

        builder.OpenElement(2, "p");
        builder.AddAttribute(3, "class", "munin-explorer-coverage__line");
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "munin-explorer-coverage__valid");
        builder.AddContent(6, validText);
        builder.CloseElement();
        builder.AddContent(7, " " + texts.CoverageValid + " ");
        builder.OpenElement(8, "span");
        builder.AddAttribute(9, "class", "munin-explorer-coverage__missing");
        builder.AddContent(10, texts.CoverageMissing(missingText));
        builder.CloseElement();
        builder.AddContent(11, " ");
        builder.OpenElement(12, "span");
        builder.AddAttribute(13, "class", "munin-explorer-coverage__share");
        builder.AddContent(14, texts.CoverageShare(share));
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(15, "div");
        builder.AddAttribute(16, "class", "munin-explorer-coverage__bar");
        builder.AddAttribute(17, "role", "img");
        builder.AddAttribute(18, "aria-label", texts.CoverageBarLabel(validText, missingText, share));
        builder.OpenElement(19, "div");
        builder.AddAttribute(20, "class", "munin-explorer-coverage__bar-fill");
        builder.AddAttribute(21, "style", $"width:{share.ToString(CultureInfo.InvariantCulture)}%");
        builder.CloseElement();
        builder.CloseElement();

        builder.CloseElement();
    }

    // In the API's order, which is Munin's display order. The bar is relative to the largest count
    // and is aria-hidden: its fill is 1.13:1 against the drawer, so the count text carries it.
    private static void Distribution(RenderTreeBuilder builder, IReadOnlyList<CodeFrequency>? frequencies, Texts texts)
    {
        if (frequencies is not { Count: > 0 })
        {
            return;
        }

        var counts = frequencies
            .Select(frequency => Count(frequency.AdditionalProperties, "GyldigeTilfeller"))
            .ToList();
        var largest = counts.Max() ?? 0;

        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "munin-explorer-distribution");
        builder.AddAttribute(2, "aria-label", texts.FrequencyCaption(null));

        for (var i = 0; i < frequencies.Count; i++)
        {
            var frequency = frequencies[i];
            var count = counts[i];
            var width = count is { } n && largest > 0 ? n * 100.0 / largest : 0;

            builder.OpenElement(3, "li");
            builder.AddAttribute(4, "class", "munin-explorer-distribution__item");

            builder.OpenElement(5, "span");
            builder.AddAttribute(6, "class", "munin-explorer-distribution__label");
            builder.AddContent(7, FrequencyLabel(frequency));
            builder.CloseElement();

            builder.OpenElement(8, "span");
            builder.AddAttribute(9, "class", "munin-explorer-distribution__count");
            builder.AddContent(10, count is { } shown ? texts.CaseCount(shown) : "—");
            builder.CloseElement();

            builder.OpenElement(11, "span");
            builder.AddAttribute(12, "class", "munin-explorer-distribution__bar");
            builder.AddAttribute(13, "aria-hidden", "true");
            builder.OpenElement(14, "span");
            builder.AddAttribute(15, "class", "munin-explorer-distribution__bar-fill");
            builder.AddAttribute(16, "style", $"width:{width.ToString("0.#", CultureInfo.InvariantCulture)}%");
            builder.CloseElement();
            builder.CloseElement();

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    // The local code and not Code, which is fully qualified and 43 characters wide (Fhi.Metadata-e3e2d).
    private static string FrequencyLabel(CodeFrequency frequency) =>
        !string.IsNullOrWhiteSpace(frequency.PreferredTerm)
            ? frequency.PreferredTerm
            : Raw(frequency.AdditionalProperties, "KodeverkLokalID") ?? frequency.Code;

    // MED is Munin's key for the median (PropertyCatalog.cs), not MEDIAN.
    private static readonly string[] FigureKeys = ["MIN", "MED", "MAX", "AVG"];

    private static IEnumerable<(string Key, string Value)> Figures(IReadOnlyDictionary<string, string?> properties) =>
        FigureKeys
            .Select(key => (Key: key, Value: Raw(properties, key)?.Trim()))
            .Where(figure => figure.Value is not null)
            .Select(figure => (figure.Key, figure.Value!));

    private static string FigureTerm(string key, Texts texts) => key switch
    {
        "MIN" => texts.FigureMinimum,
        "MED" => texts.FigureMedian,
        "MAX" => texts.FigureMaximum,
        _ => texts.FigureMean,
    };

    private static void FigureList(RenderTreeBuilder builder, IReadOnlyDictionary<string, string?> properties, Texts texts)
    {
        var figures = Figures(properties).ToList();

        if (figures.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", "munin-explorer-figures");

        foreach (var (key, value) in figures)
        {
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "class", "munin-explorer-figures__item");
            builder.OpenElement(4, "dt");
            builder.AddAttribute(5, "class", "munin-explorer-figures__term");
            builder.AddContent(6, FigureTerm(key, texts));
            builder.CloseElement();
            builder.OpenElement(7, "dd");
            builder.AddAttribute(8, "class", "munin-explorer-figures__value");
            builder.AddContent(9, value);
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private static void SuppressedNote(RenderTreeBuilder builder, string text)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(
            1, "class", "caption munin-explorer-figures__note munin-explorer-figures__note--suppressed");
        builder.AddContent(2, text);
        builder.CloseElement();
    }

    /// <summary>A count of cases, or null where the catalogue holds no whole, non-negative number.</summary>
    private static long? Count(IReadOnlyDictionary<string, string?>? properties, string key) =>
        Number(properties, key) is { } value && value >= 0 && value < long.MaxValue && value == Math.Floor(value)
            ? (long)value
            : null;

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
