namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Which of the two languages a <c>Language</c> parameter is asking for.
/// </summary>
/// <remarks>
/// <para>
/// One place, because the answer is needed in three — the words (<see cref="Texts.For"/>), the
/// <c>lang</c> marking and date formats (<see cref="CatalogueProperties"/>), and the
/// <c>Accept-Language</c> the facet call sends. Three copies of the same comparison is three
/// chances for one of them to disagree with the other two, and a page that is English in its
/// labels and Norwegian in its dates is a bug nobody reports as one.
/// </para>
/// <para>
/// The match is on the primary subtag rather than the whole token, so <c>en</c>, <c>en-GB</c> and
/// <c>en-US</c> all mean English. helsedata's CMS reports the short branch name — their own
/// <c>ErrorPageController</c> compares <c>contentLanguage.Name</c> with <c>"no"</c> — but the same
/// solution carries a second representation for the questionnaire path, where
/// <c>LanguageExtensions</c> returns <c>nb-NO</c>/<c>en-GB</c> and the PDF generator builds full
/// <c>CultureInfo</c>s. Our mount point is not settled yet, and an exact match on <c>en</c> would
/// send an English page down the Norwegian path with nothing thrown and no test failing.
/// </para>
/// <para>
/// String comparison rather than <see cref="System.Globalization.CultureInfo"/> parsing on purpose:
/// <c>no</c> is not the culture name for Norwegian, so the token the host actually sends would have
/// to be special-cased anyway, and a malformed token is a fallback here rather than an exception
/// thrown mid-render.
/// </para>
/// </remarks>
internal static class ReaderLanguage
{
    /// <summary>The default. helsedata's own token, which is <c>no</c> rather than <c>nb</c>.</summary>
    internal const string Norwegian = "no";

    internal const string English = "en";

    /// <summary>Whether this token asks for English. Anything else — including nothing — does not.</summary>
    internal static bool IsEnglish(string? language) =>
        Primary(language).Equals(English, StringComparison.OrdinalIgnoreCase);

    /// <summary>The reader's language as the tag this package uses: <c>en</c> or <c>no</c>.</summary>
    internal static string Of(string? language) => IsEnglish(language) ? English : Norwegian;

    /// <summary>
    /// The part of a language tag before its first subtag separator, trimmed.
    /// </summary>
    /// <remarks>
    /// <c>_</c> as well as <c>-</c>: it is not what BCP 47 says, but it is what .NET resource file
    /// names and a good deal of hand-written configuration use, and reading <c>en_US</c> as
    /// Norwegian would be the silent failure this whole type exists to prevent.
    /// </remarks>
    private static ReadOnlySpan<char> Primary(string? language)
    {
        var token = language.AsSpan().Trim();
        var separator = token.IndexOfAny('-', '_');

        return separator < 0 ? token : token[..separator];
    }
}
