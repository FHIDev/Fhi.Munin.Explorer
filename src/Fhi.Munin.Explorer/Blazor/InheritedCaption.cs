using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The «Arvet fra kilden …» line under a value its record leaves to the kilde or a delkilde.</summary>
/// <param name="Lead">«Arvet fra kilden» or «Arvet fra delkilden», in the reader's language.</param>
/// <param name="Name">The source's name, or empty where Munin sent none.</param>
/// <param name="NameLanguage">A <c>lang</c> for <paramref name="Name"/> when it is not in the reader's language.</param>
/// <param name="Language">The reader's own tag, written on the caption since the value around it may be marked otherwise.</param>
internal readonly record struct InheritedCaption(string Lead, string Name, string? NameLanguage, string Language)
{
    /// <summary>
    /// The caption as a quiet paragraph inside the value's <c>dd</c>. Consumes seven sequence numbers.
    /// </summary>
    /// <remarks>
    /// Stiler's own <c>caption</c>, the grey of the hero row's note, so no new class name needs a rule.
    /// </remarks>
    internal void Render(RenderTreeBuilder builder, int seq)
    {
        builder.OpenElement(seq, "p");
        builder.AddAttribute(seq + 1, "class", "caption margin--none");
        builder.AddAttribute(seq + 2, "lang", Language);
        builder.AddContent(seq + 3, string.IsNullOrWhiteSpace(Name) ? Lead : $"{Lead} ");

        if (!string.IsNullOrWhiteSpace(Name))
        {
            builder.OpenElement(seq + 4, "span");
            builder.AddAttribute(seq + 5, "lang", NameLanguage);
            builder.AddContent(seq + 6, Name);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}

/// <summary>
/// Which of a datasamling's drawn values come from an ancestor, by catalogue key or <see cref="ValidityPeriod"/>, and
/// from whom.
/// </summary>
internal static class InheritedCaptions
{
    /// <summary>The public kilde fields Munin sends in <see cref="DatasamlingDetail.InheritedProperties"/>.</summary>
    internal const string Homepage = "Hjemmeside";

    /// <inheritdoc cref="Homepage"/>
    internal const string DataControllerOrgNumber = "OrgnrDataansvarlig";

    /// <inheritdoc cref="Homepage"/>
    internal const string DataControllerHerId = "HERidDataansvarlig";

    /// <inheritdoc cref="Homepage"/>
    internal const string DataSourceHerId = "HERidDatakilde";

    /// <summary>
    /// Fields normally set only on the kilde, so a caption would repeat on every page and add little.
    /// </summary>
    /// <remarks>Kildetype has no datasamling column at all. The list is the display owner's to confirm.</remarks>
    internal static readonly IReadOnlySet<string> Omitted = new HashSet<string>(StringComparer.Ordinal)
    {
        CatalogueColumns.Kildetype,
        CatalogueColumns.LegalBasis,
        CatalogueColumns.DataController,
        CatalogueColumns.DataProcessor,
        DataControllerOrgNumber,
        DataSourceHerId,
    };

    /// <summary>The key of the one row that draws gyldig fra and gyldig til together; no catalogue key.</summary>
    internal const string ValidityPeriod = "GyldigFra+GyldigTil";

    /// <summary>
    /// A caption for every inherited value the page can draw, less <paramref name="omitted"/> (by default <see cref="Omitted"/>).
    /// </summary>
    /// <remarks>
    /// Lovverk, dataansvarlig and databehandler fall back to the kilde alone, so they name it. Validity and identification
    /// level go through the delkilde chain, so Munin names who answered; where it sends no source, nothing is captioned.
    /// </remarks>
    internal static IReadOnlyDictionary<string, InheritedCaption> For(DatasamlingDetail datasamling, Texts text,
                                                                      string reader, IReadOnlySet<string>? omitted = null)
    {
        var captions = new Dictionary<string, InheritedCaption>(StringComparer.Ordinal);
        var kilde = Caption(text.InheritedFromKilde, datasamling.ParentKildeName, datasamling.ParentKildeNameEn, reader);

        Inherit(captions, CatalogueColumns.LegalBasis, datasamling.LegalBasis, datasamling.EffectiveLegalBasis, kilde);
        Inherit(captions, CatalogueColumns.DataController, datasamling.DataController,
                datasamling.EffectiveDataController, kilde);
        Inherit(captions, CatalogueColumns.DataProcessor, datasamling.DataProcessor,
                datasamling.EffectiveDataProcessor, kilde);
        Inherit(captions, CatalogueColumns.Kildetype, null, datasamling.EffectiveKildetype, kilde);

        var from = datasamling.EffectiveValidFrom is null ? null : datasamling.ValidFromSource;
        var to = datasamling.EffectiveValidTo is null ? null : datasamling.ValidToSource;
        Inherit(captions, CatalogueColumns.ValidFrom, from, text, reader);
        Inherit(captions, CatalogueColumns.ValidTo, to, text, reader);
        Inherit(captions, CatalogueColumns.PersonIdentification,
                string.IsNullOrWhiteSpace(datasamling.EffectivePersonIdentificationLevel)
                    ? null
                    : datasamling.PersonIdentificationLevelSource, text, reader);

        // One caption under both ends, so only when every end shown came from the same place.
        static bool Own(DateTimeOffset? end, ValueSource? source) => end is not null && source is null;
        if (!Own(datasamling.EffectiveValidFrom, from) && !Own(datasamling.EffectiveValidTo, to)
            && (from ?? to) is { } period
            && (from is null || to is null || (from.SourceLevel, from.SourceId) == (to.SourceLevel, to.SourceId)))
        {
            Inherit(captions, ValidityPeriod, period, text, reader);
        }

        foreach (var (key, value) in datasamling.InheritedProperties)
        {
            var lead = Lead(value.SourceLevel, text);

            if (lead is not null && !string.IsNullOrWhiteSpace(value.Value) && !HoldsItself(datasamling, key))
            {
                captions[key] = Caption(lead, value.SourceName, value.SourceNameEn, reader);
            }
        }

        captions.Keys.Where((omitted ?? Omitted).Contains).ToList().ForEach(key => captions.Remove(key));

        return captions;
    }

    // The row's own value is the one drawn, so a caption naming an ancestor would be wrong.
    private static bool HoldsItself(DatasamlingDetail datasamling, string key) =>
        datasamling.OwnKildeProperties.TryGetValue(key, out var own) && !string.IsNullOrWhiteSpace(own);

    private static void Inherit(Dictionary<string, InheritedCaption> captions, string key, string? own,
                                string? effective, InheritedCaption source)
    {
        if (string.IsNullOrWhiteSpace(own) && !string.IsNullOrWhiteSpace(effective))
        {
            captions[key] = source;
        }
    }

    private static void Inherit(Dictionary<string, InheritedCaption> captions, string key, ValueSource? source,
                                Texts text, string reader)
    {
        if (source is not null && Lead(source.SourceLevel, text) is { } lead)
        {
            captions[key] = Caption(lead, source.SourceName, source.SourceNameEn, reader);
        }
    }

    // A level this package does not know could be either, and a wrong name is worse than none.
    private static string? Lead(string level, Texts text) => level switch
    {
        "kilde" => text.InheritedFromKilde,
        "delkilde" => text.InheritedFromDelkilde,
        _ => null,
    };

    private static InheritedCaption Caption(string lead, string? name, string? nameEn, string reader)
    {
        var (text, norwegian) = ReaderLanguage.Pick(name, nameEn, reader);

        return new(lead, text ?? "", CatalogueProperties.Foreign(norwegian, reader), reader);
    }
}
