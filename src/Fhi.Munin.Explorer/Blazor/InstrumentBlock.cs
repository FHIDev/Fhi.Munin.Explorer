using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// An instrument's name and description in the reader's language, and the list of them a variable
/// carries.
/// </summary>
/// <remarks>
/// <para>
/// Munin keeps the Norwegian name in a column of its own and the English as a curated property, so
/// the two halves arrive apart and are put back together here into the translation bag
/// <see cref="CatalogueProperties.Localised"/> already resolves — rather than by a second rule of
/// this package's own.
/// </para>
/// <para>
/// Here once because three surfaces draw the same name: the variable page, the row's detail panel
/// and the instrument page itself. A reader following a link must not find it worded differently at
/// the other end, and <see cref="KildeTrailBlock"/> is the same bargain for the kilde trail.
/// </para>
/// </remarks>
internal static class InstrumentBlock
{
    /// <summary>The curated key Munin holds an instrument's English name under.</summary>
    internal const string EnglishNameKey = "NavnEngelsk";

    /// <summary>The curated key for its English description.</summary>
    internal const string EnglishDescriptionKey = "BeskrivelseEngelsk";

    internal static (string? Text, string Language) Name(InstrumentReference instrument, string reader) =>
        Resolve(instrument.PreferredTerm, instrument.AdditionalProperties, EnglishNameKey, reader);

    internal static (string? Text, string Language) Name(InstrumentDetail instrument, string reader) =>
        Resolve(instrument.PreferredTerm, instrument.AdditionalProperties, EnglishNameKey, reader);

    internal static (string? Text, string Language) Description(InstrumentDetail instrument, string reader) =>
        Resolve(instrument.Description, instrument.AdditionalProperties, EnglishDescriptionKey, reader);

    /// <summary>
    /// A variable's instruments as a list, each name in the reader's language and linking to the
    /// instrument's own page.
    /// </summary>
    /// <remarks>
    /// Without an address the name is written as words rather than as a link that goes nowhere —
    /// the rule <see cref="DetailBlocks.LinkedFacts"/> follows, for the reason it gives: this
    /// package owns no URL and the surface above may have none to give.
    /// </remarks>
    /// <param name="instruments">The references off the variable's payload, in the order it sent them.</param>
    /// <param name="reader">The reader's language, which decides the name and its marking.</param>
    /// <param name="href">Where one instrument's page is, by id, or null where the caller knows of none.</param>
    /// <param name="cssClass">The list's class, or null for the bare list the drill-in panel styles.</param>
    internal static RenderFragment Write(
        IReadOnlyList<InstrumentReference> instruments,
        string reader,
        Func<Guid, string>? href,
        string? cssClass = null) => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", cssClass);

        var seq = 10;

        foreach (var instrument in instruments)
        {
            var named = Name(instrument, reader);

            // The code standing in where the catalogue named the instrument in neither language.
            // A code is nobody's language, so it is left unmarked, as T.Named leaves one.
            var text = named.Text ?? instrument.Code;
            var lang = named.Text is null ? null : CatalogueProperties.Foreign(named.Language, reader);
            var target = href?.Invoke(instrument.Id);

            builder.OpenElement(seq, "li");

            builder.OpenElement(seq + 1, string.IsNullOrWhiteSpace(target) ? "span" : "a");
            builder.AddAttribute(seq + 2, "href", string.IsNullOrWhiteSpace(target) ? null : target);
            builder.AddAttribute(seq + 3, "lang", lang);
            builder.AddContent(seq + 4, text);
            builder.CloseElement();

            builder.CloseElement();

            seq += 10;
        }

        builder.CloseElement();
    };

    /// <summary>
    /// The Norwegian column and the English property as one bag, resolved for <paramref name="reader"/>.
    /// </summary>
    /// <remarks>
    /// A blank half is left out rather than added empty, which is what makes the fallback work: an
    /// English reader whose instrument has no <c>NavnEngelsk</c> gets the Norwegian marked as
    /// Norwegian rather than an empty line.
    /// </remarks>
    private static (string? Text, string Language) Resolve(
        string? norwegian,
        IReadOnlyDictionary<string, string?>? properties,
        string englishKey,
        string reader)
    {
        var bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(norwegian))
        {
            bag[ReaderLanguage.Norwegian] = norwegian;
        }

        if (properties is not null
            && properties.TryGetValue(englishKey, out var english)
            && !string.IsNullOrWhiteSpace(english))
        {
            bag[ReaderLanguage.English] = english;
        }

        return CatalogueProperties.Localised(bag, reader);
    }
}
