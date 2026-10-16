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
    /// Whole paragraphs while they fit within <see cref="Limit"/>; where the first alone does not, it
    /// up to its last whole sentence that does. <c>Folded</c> is null where nothing is held back.
    /// </summary>
    internal static (string Lead, string? Folded) Split(string text)
    {
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

        // A first paragraph longer than the limit: cut after a sentence, never inside one, and after
        // the first sentence where even that is longer than the limit.
        var first = paragraphs[0];
        var ends = SentenceEnd().Matches(first).Select(match => match.Index + 1).ToList();
        var cut = ends.LastOrDefault(end => Visible(first[..end]) <= Limit);

        if (cut == 0)
        {
            cut = ends.FirstOrDefault();
        }

        if (cut == 0)
        {
            return (text, null);
        }

        return (first[..cut].TrimEnd(), Join(paragraphs.Skip(1).Prepend(first[cut..].TrimStart())));
    }

    /// <summary>The characters a reader sees: a markdown link counts by its label, not its address.</summary>
    private static int Visible(string paragraph) => InlineLink().Replace(paragraph, "$1").Trim().Length;

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
