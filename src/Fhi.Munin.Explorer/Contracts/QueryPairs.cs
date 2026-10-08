namespace Fhi.Munin.Explorer.Contracts;

/// <summary>One <c>name=value</c> pair of a query string, decoded beside the text it came from.</summary>
/// <param name="Raw">The pair exactly as it arrived, for a caller re-emitting what it does not own.</param>
/// <param name="Name">The decoded name; the whole decoded pair when it has no <c>=</c> after its first character.</param>
/// <param name="Value">The decoded value, or null when the pair has no name and value to tell apart.</param>
internal readonly record struct QueryPair(string Raw, string Name, string? Value);

/// <summary>
/// The one query-string splitter, written by hand because an RCL cannot take
/// Microsoft.AspNetCore.WebUtilities, which a host gets for free.
/// </summary>
internal static class QueryPairs
{
    /// <summary>
    /// The non-empty <c>&amp;</c>-separated pairs of <paramref name="query"/>, at most
    /// <paramref name="maxPairs"/> of them, with or without a leading <c>?</c>.
    /// </summary>
    /// <remarks>
    /// Lazy, and it never splits the whole string: the cap bounds the work of the parse and not only
    /// what is kept, since the query is untrusted input a crafted link can make arbitrarily long.
    /// </remarks>
    public static IEnumerable<QueryPair> Split(string? query, int maxPairs)
    {
        if (string.IsNullOrEmpty(query))
        {
            yield break;
        }

        var text = query.TrimStart('?');
        var start = 0;
        var read = 0;

        while (start < text.Length && read < maxPairs)
        {
            var end = text.IndexOf('&', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (end > start)
            {
                read++;
                yield return Read(text[start..end]);
            }

            start = end + 1;
        }
    }

    private static QueryPair Read(string raw)
    {
        var separator = raw.IndexOf('=', StringComparison.Ordinal);

        return separator <= 0
            ? new QueryPair(raw, Decode(raw), null)
            : new QueryPair(raw, Decode(raw[..separator]), Decode(raw[(separator + 1)..]));
    }

    /// <summary>One query-string token, unescaped — <c>+</c> as a space as well as <c>%XX</c>.</summary>
    /// <remarks>
    /// The <c>+</c> goes first: an HTML GET form, <c>WebUtility.UrlEncode</c> and
    /// <c>QueryHelpers.AddQueryString</c> all spell a space that way, and unescaping first would turn
    /// <c>%2B</c> into one. Without it <c>?helsefagligKodeverkReferanser=ICD+10</c> matches nothing.
    /// </remarks>
    public static string Decode(string token) => Uri.UnescapeDataString(token.Replace('+', ' '));
}
