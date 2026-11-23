using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A kilde's description cut for the top of its page: what shows at once, and what waits behind
/// «Les mer». Cut from characters and paragraphs, never measured in the browser (Fhi.Metadata-nebok).
/// </summary>
internal static partial class DescriptionLead
{
    /// <summary>Visible characters shown at once: about eight lines at the description's 70ch.</summary>
    internal const int Limit = 560;

    /// <summary>
    /// Whole paragraphs while they fit within <see cref="Limit"/>, or whole blocks where a formatted list is drawn;
    /// where the first alone is longer, up to its last whole sentence that fits. <c>Folded</c> is null where nothing is held back.
    /// </summary>
    internal static (string Lead, string? Folded) Split(string text, bool formatted = false)
    {
        // Past the cap Render draws plain lines; split halves would each fall under it and parse.
        if (text.Length > CatalogueMarkdown.MaxParsedLength)
        {
            return (text, null);
        }

        if (CatalogueMarkdown.DrawsList(text, formatted))
        {
            return BetweenBlocks(text);
        }

        var paragraphs = ParagraphBreak().Split(BrTag().Replace(text.Replace("\r\n", "\n"), "\n").Trim())
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToList();

        if (paragraphs.Sum(Visible) <= Limit)
        {
            return (text, null);
        }

        var shown = 0;

        for (var length = 0; shown < paragraphs.Count && length + Visible(paragraphs[shown]) <= Limit; shown++)
        {
            length += Visible(paragraphs[shown]);
        }

        if (shown > 0)
        {
            return (Join(paragraphs.Take(shown)), Join(paragraphs.Skip(shown)));
        }

        var first = paragraphs[0];
        var cut = SentenceCut(first, formatted);

        // One sentence too long to cut: the first paragraph whole, and the rest at the paragraph break.
        if (cut == 0)
        {
            return paragraphs.Count > 1 ? (first, Join(paragraphs.Skip(1))) : (text, null);
        }

        return (first[..cut].TrimEnd(), Join(paragraphs.Skip(1).Prepend(first[cut..].TrimStart())));
    }

    /// <summary>
    /// Where a first paragraph longer than the limit is cut: after its last sentence that fits, never inside
    /// one, and after the first sentence where even that is longer. Zero where it has no sentence end.
    /// </summary>
    private static int SentenceCut(string first, bool formatted)
    {
        var links = InlineLink().Matches(first).ToList();
        var emphasis = formatted ? CatalogueMarkdown.EmphasisSpans(first) : [];
        var ends = SentenceEnd().Matches(first)
            .Where(match => !Abbreviated(first, match.Index)
                            && !InsideBrackets(first, match.Index)
                            && !emphasis.Any(span => match.Index >= span.Start && match.Index <= span.End)
                            && !links.Any(link => match.Index >= link.Index && match.Index < link.Index + link.Length))
            .Select(match => match.Index + 1)
            .ToList();
        var cut = ends.LastOrDefault(end => Visible(first[..end]) <= Limit);

        return cut == 0 ? ends.FirstOrDefault() : cut;
    }

    /// <summary>Whole top-level blocks while they fit, the first always, so a drawn list is never cut inside; a
    /// first paragraph longer than the limit is cut after a sentence, as unformatted text is.</summary>
    private static (string Lead, string? Folded) BetweenBlocks(string text)
    {
        var (source, blocks) = CatalogueMarkdown.TopBlocks(text);
        var lengths = blocks.Select(block => Visible(source[block.Span.Start..(block.Span.End + 1)])).ToList();

        if (lengths.Sum() <= Limit)
        {
            return (text, null);
        }

        if (lengths[0] > Limit && !blocks[0].IsList
            && SentenceCut(source[blocks[0].Span.Start..(blocks[0].Span.End + 1)], formatted: true) is > 0 and var cut)
        {
            var at = blocks[0].Span.Start + cut;

            return (source[..at].TrimEnd(), source[at..].Trim());
        }

        var shown = 1;

        for (var length = lengths[0]; shown < blocks.Count && length + lengths[shown] <= Limit; shown++)
        {
            length += lengths[shown];
        }

        // Up to where the next block starts, so a reference definition between the two stays in the lead.
        return shown == blocks.Count
            ? (text, null)
            : (source[..blocks[shown].Span.Start].TrimEnd(), source[blocks[shown].Span.Start..].Trim());
    }

    /// <summary>The characters a reader sees: a markdown link counts by its label, not its address.</summary>
    private static int Visible(string paragraph) => InlineLink().Replace(paragraph, "$1").Trim().Length;

    /// <summary>Whether the full stop at <paramref name="stop"/> closes an abbreviation or an initial.</summary>
    private static bool Abbreviated(string text, int stop)
    {
        var start = text.LastIndexOfAny([' ', '\n', '\t', '(', '«'], Math.Max(stop - 1, 0)) + 1;
        var word = text[start..stop].ToLowerInvariant();

        return word.Length == 1 || Abbreviations.Contains(word);
    }

    /// <summary>Whether <paramref name="index"/> is inside an unclosed <c>[</c>, nested link labels included.</summary>
    private static bool InsideBrackets(string text, int index)
    {
        var depth = 0;

        foreach (var c in text.AsSpan(0, index))
        {
            depth = c switch { '[' => depth + 1, ']' => Math.Max(depth - 1, 0), _ => depth };
        }

        return depth > 0;
    }

    private static readonly HashSet<string> Abbreviations =
    [
        "bl.a", "ca", "dr", "evt", "ev", "f.eks", "f.o.m", "ifm", "inkl", "jf", "jfr", "kap", "m.fl", "mfl",
        "m.m", "mv", "m.v", "nr", "osv", "pkt", "prof", "st", "t.o.m", "vedr",
    ];

    private static string Join(IEnumerable<string> paragraphs) => string.Join("\n\n", paragraphs);

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTag();

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex ParagraphBreak();

    /// <summary>A full stop, question or exclamation mark before a capital: «1. januar» and «f.eks. en» are not ends.</summary>
    [GeneratedRegex(@"[.!?](?=\s+[A-ZÆØÅ«(])")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex InlineLink();
}
