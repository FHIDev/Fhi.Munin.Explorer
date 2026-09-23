using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A detail payload's values as the property renderer wants them: the <c>additionalProperties</c>
/// bag, with the values Munin keeps in columns of their own merged in under the catalogue's keys.
/// </summary>
/// <remarks>
/// A column-backed property never reaches the bag — Munin routes it to its own column and it
/// arrives as a typed contract field — so <see cref="CatalogueProperties.Rows"/>, which draws a key
/// only when the bag answers for it, had nothing to draw and a section holding only such keys came
/// out empty (Fhi.Metadata-bct95).
/// <para>
/// Merged on this side rather than projected into the bag by the API: the bag means the keys that
/// are <em>not</em> modelled as typed fields, and a payload carrying one value in both places gives
/// every other consumer two sources to reconcile. Which of the two a page draws is a rendering
/// question, so it is answered where the rendering is.
/// </para>
/// </remarks>
internal static class CatalogueColumns
{
    /// <summary>The catalogue's own keys for the column-backed properties the detail views draw.</summary>
    /// <remarks>
    /// Spelled as Munin spells them, which is Norwegian: these are keys in someone else's data, not
    /// identifiers of ours, and a key that disagrees with the payload's simply never matches.
    /// </remarks>
    internal const string Description = "Beskrivelse";

    /// <inheritdoc cref="Description"/>
    internal const string LegalBasis = "Lovverk";

    /// <inheritdoc cref="Description"/>
    internal const string DataController = "Dataansvarlig";

    /// <inheritdoc cref="Description"/>
    internal const string DataProcessor = "Databehandler";

    /// <inheritdoc cref="Description"/>
    internal const string PersonIdentification = "GradAvPersonidentifikasjon";

    /// <inheritdoc cref="Description"/>
    internal const string ValidFrom = "GyldigFra";

    /// <inheritdoc cref="Description"/>
    internal const string ValidTo = "GyldigTil";

    /// <inheritdoc cref="Description"/>
    /// <remarks>
    /// Read twice on one page: <see cref="DatasamlingView"/> yields its Statistikk row to a
    /// placement as it does the others, and names the Statistikk heading off the same field
    /// whether or not the key is placed — a heading is what makes the section findable rather than
    /// the fact repeated.
    /// </remarks>
    internal const string StatisticsType = "StatistikkType";

    /// <inheritdoc cref="Description"/>
    internal const string CountingUnit = "TelleEnhet";

    /// <inheritdoc cref="Description"/>
    internal const string Frequency = "Frekvens";

    /// <summary>A source's columns, keyed as the catalogue's property definitions key them.</summary>
    /// <remarks>
    /// The reader's language rather than the normalised reader tag, because the only thing this
    /// consumes it for is formatting the two dates — and the fact box one line away formats the same
    /// fields from the same argument, so the two are demonstrably one decision.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string?> Values(KildeDetail kilde, string? language) =>
        Merge(kilde.AdditionalProperties,
              (Description, kilde.Description),
              (LegalBasis, kilde.LegalBasis),
              (DataController, kilde.DataController),
              (DataProcessor, kilde.DataProcessor),
              (PersonIdentification, kilde.PersonIdentificationLevel),
              (ValidFrom, Day(kilde.ValidFrom, language)),
              (ValidTo, Day(kilde.ValidTo, language)));

    /// <summary>
    /// A collection's columns, every inherited one taken from its <c>Effective…</c> value.
    /// </summary>
    /// <remarks>
    /// The rule <see cref="DatasamlingView"/> already follows for the same fields: the own value is
    /// null where nothing is set at this level, so the own value would report "not stated" for a
    /// controller that is perfectly well known one level up. The language argument is the reader's
    /// own tag, for the reason the source overload gives.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string?> Values(DatasamlingDetail datasamling, string? language)
    {
        var values = Merge(datasamling.AdditionalProperties,
              (Description, datasamling.Description),
              (LegalBasis, datasamling.EffectiveLegalBasis),
              (DataController, datasamling.EffectiveDataController),
              (DataProcessor, datasamling.EffectiveDataProcessor),
              (PersonIdentification, datasamling.EffectivePersonIdentificationLevel),
              (ValidFrom, Day(datasamling.EffectiveValidFrom, language)),
              (ValidTo, Day(datasamling.EffectiveValidTo, language)),
              (StatisticsType, datasamling.StatisticsType),
              (CountingUnit, datasamling.CountingUnit),
              (Frequency, datasamling.Frequency));

        var definition = datasamling.PropertyMetadata.FirstOrDefault(entry => entry.Key == PersonIdentification);
        var effective = datasamling.EffectivePersonIdentificationLevel;
        var reader = ReaderLanguage.Of(language);

        // Munin's legacy enum ordinals are 0–3. A defined numeric option is still curated data;
        // only an unrecognised ordinal yields to an effective value the catalogue can resolve.
        if (values.TryGetValue(PersonIdentification, out var raw)
            && raw is "0" or "1" or "2" or "3"
            && definition is not null
            && !string.IsNullOrWhiteSpace(effective)
            && CatalogueProperties.Word(definition, raw, reader) is null
            && CatalogueProperties.Word(definition, effective, reader) is not null)
        {
            values[PersonIdentification] = effective;
        }

        return values;
    }

    /// <summary>
    /// A variable's columns, which is its description and nothing else.
    /// </summary>
    /// <remarks>
    /// The catalogue places GyldigFra and GyldigTil on this surface too, and they belong to a
    /// version rather than to the variable — <see cref="VariableDetail"/> carries no such field to
    /// merge, and the version history draws every version's pair already.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string?> Values(VariableDetail variable) =>
        Merge(variable.AdditionalProperties, (Description, variable.Description));

    /// <summary>The bag, with every column that has a value added to it.</summary>
    /// <remarks>
    /// The bag wins where it holds the key: what the payload curated is the payload's own answer,
    /// and a column overwriting it would change a row that renders correctly today. An empty column
    /// adds no key at all, so a property nobody filled in is absent rather than blank — which is
    /// what lets <see cref="CatalogueProperties.Rows"/> keep skipping it and its group keep
    /// collapsing.
    /// </remarks>
    private static Dictionary<string, string?> Merge(
        IReadOnlyDictionary<string, string?>? bag,
        params (string Key, string? Value)[] columns)
    {
        // AdditionalProperties is declared non-nullable and can still arrive null — see the remarks
        // on CatalogueProperties.Rows for how, and why every caller of it guards.
        var values = bag is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(bag, StringComparer.Ordinal);

        foreach (var (key, value) in columns)
        {
            if (string.IsNullOrWhiteSpace(value)
                || (values.TryGetValue(key, out var curated) && !string.IsNullOrWhiteSpace(curated)))
            {
                continue;
            }

            values[key] = value;
        }

        return values;
    }

    /// <summary>A date column as the day it fell on, or nothing where the catalogue holds none.</summary>
    /// <remarks>
    /// Written here rather than left to the renderer, which formats no dates at all: the column
    /// holds an instant, and the same field is already read as a day everywhere else on the page.
    /// </remarks>
    private static string? Day(DateTimeOffset? value, string? language) =>
        CatalogueDate.DayOrNothing(value, language);
}
