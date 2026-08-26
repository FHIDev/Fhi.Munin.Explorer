using Fhi.Munin.Explorer.Contracts;
namespace Fhi.Munin.Explorer.Blazor;

/// <remarks>
/// Lifted out of <see cref="VariableExplorer"/> so a second explorer can share it. Kelda, the
/// kildeutforsker, ships from this same package and needs these strings; while this was a private
/// nested type it could not reach them, and the alternative was a second copy that would have
/// drifted from this one the first time either was edited.
/// </remarks>
/// <summary>
/// Self-contained translations. Deliberately not IStringLocalizer — see <see cref="VariableExplorer.Language"/>.
/// </summary>
internal sealed record Texts(
    string Title,
    string SearchLabel,
    string SearchPlaceholder,
    string SearchButton,
    string SortBy,
    string Loading,
    string Error,
    string NotSpecified,
    string SortDefault,
    // The first column's header. Runa calls it Navn; helsedata calls the same column
    // Variabel. Runa decides what the component says.
    // The panel's two tabs and the groups inside the first, named as Runa names them.
    // "still running" — a period with no end date.
    // The way out of the kilde view, back to the list of variables.
    string BackToVariables,
    string Ongoing,

    // Runa's own words for two fields we had named differently. The trail through the
    // catalogue is a Kildesti, not a Datakilde: it is the path, not the source. And the
    // period in the panel is the Dataperiode — "Periode" alone could be any period.
    string FieldKildePath,
    string FieldDataPeriod,
    string TabDetails,
    string TabData,
    string GroupIdentification,
    string GroupPlacement,
    string GroupProperties,
    string ColumnVariable,
    // The column picker: Runa's own name for the control ("Kolonner", where helsedata's button
    // says "Vis kolonner"), and the sentence the last column left points at to say why it refuses
    // to go. Named ...Hint because it is a sentence rather than a column's name, which is what
    // every other string around it is. Runa decides what the component says; helsedata decides
    // what it looks like.
    string Columns,
    string LastColumnHint,
    string FieldDataType,
    string FieldStatus,
    string FieldCode,
    string FieldSource,
    string FieldDataCollection,
    string FieldVariableGroup,
    string FieldPeriod,
    string FieldDescription,
    // The Data tab's kodeverk section, Runa's words throughout. The reference is labelled
    // rather than left to stand on its own, and a link the API resolved no name for says so
    // instead of letting the reference impersonate the name.
    string FieldKodeverkReference,
    string KodeverkUnnamed,
    string NoKodeverk,
    string ShowCodes,
    string HideCodes,
    string CodesLoading,
    string CodesError,
    string NoCodes,
    string ColumnCodeValue,
    string ColumnCodeName,
    string ColumnValidFrom,
    string ColumnValidTo,
    // The detail panel. Its labels are the card's own words wherever it names the same thing —
    // Datakilde, Variabelgruppe, Periode — so opening a row renames nothing.
    string ShowDetails,
    string HideDetails,
    string DetailLoading,
    string DetailError,
    string DetailMissing,
    string Kildekodeverk,
    // The kilde and datasamling panel, one level in from the variable's own. Where it names
    // something the card or the facets already name — Datakilde, Datasamling, Beskrivelse,
    // Periode, Type datakilde — it borrows their words rather than minting a synonym, so moving
    // inwards renames nothing. What is here is what only the owners have.
    string ShowKilde,
    string HideKilde,
    string ShowDatasamling,
    string HideDatasamling,
    string KildeLoading,
    string DatasamlingLoading,
    string KildeError,
    string DatasamlingError,
    string KildeMissing,
    string DatasamlingMissing,
    string FieldLegalBasis,
    string FieldDataController,
    string FieldDataProcessor,
    string FieldPersonIdentification,
    string FieldValidity,
    string FieldInclusionCriteria,
    string FieldFrequency,
    string FieldCountingUnit,
    string FieldVariableCount,
    string FieldDataCollections,

    // Headings and fields the kilde view needs, which the variable panel has no use for.
    string HeadingMetadata,
    string HeadingSourceInformation,
    string HeadingStatistics,
    string FieldLastUpdated,
    string FieldTotalVariables,
    string HeadingDataCollections,
    string FieldName,
    string VariableCountSuffix,

    // The variable detail view: its sidebar, and the statistics table's columns.
    string FieldKildeName,
    string FieldKildeShortName,
    string FieldVariableGroups,
    string FieldYear,
    string FieldMinimum,
    string FieldMaximum,
    string FieldMean,
    string FieldStandardDeviation,
    string StatisticsYearly,
    string StatisticsAccumulated,
    string ShowWholeVariable,
    // The row's save action, in both of its states. One control, two words: the button says
    // what pressing it does, not what the variable currently is.
    string SaveToList,
    string RemoveFromList,
    // The list made for a reader who saves before they have made one themselves.
    string FirstListName,
    // Said in the row, beside the button that failed - the rest of the results are unaffected.
    string SaveError,
    // The saved-list view: its heading, the picker, the create form, and what it says when
    // there is nothing to show yet.
    string MyListsHeading,
    string ChooseList,
    string NewListName,
    string CreateList,
    string NoListsYet,
    string EmptyList,
    string RemoveFromThisList,
    string ListLoadError,
    // Shown for an entry whose variable is no longer in the catalogue - retracted, unpublished,
    // or not yet projected. The row stays so the paging totals keep meaning what they say.
    string VariableNoLongerAvailable,
    string HeadingKodeverk,

    // The version history in the whole-variable view.
    string HeadingVersionHistory,
    string VersionUnnamed,
    string VersionCurrent,
    string VersionActive,
    string VersionHistorical,
    string FieldValidFrom,
    string FieldValidTo,
    // Prose for the identification level, which the API reports as a raw token the same way
    // kildetype is. A token missing from this falls back to what the API sent.
    IReadOnlyDictionary<string, string> PersonIdentificationNames,
    // The filter panel. FieldSource and FieldVariableGroup name two of the facets as well as two
    // of the card fields — deliberately the same word for the same thing in both places.
    string FiltersTitle,
    string ClearFilters,
    string FilterError,
    string FacetKildeType,
    string FacetFilter,
    string FacetDataType,
    string FacetHelsefagligKodeverk,
    string FacetAdministrativtKodeverk,
    string FacetInstrument,
    string FacetOther,
    string HasKildekodeverk,
    string IncludeHistorical,
    string NoVariabelgrupper,
    // The hierarchy trail over the results. FieldSource, FieldDataCollection and
    // FieldVariableGroup name three of its four levels already — the same word for the same thing
    // as in the facets and the columns — so only the delkilde needs one of its own, and it is
    // needed as the step's own fallback label rather than as a facet: the panel nests delkilder
    // under their kilde instead of giving them a facet to head.
    string FieldDelkilde,
    string HierarchyTrail,
    string ClearHierarchy,
    // Prose for the two facets the API reports as raw tokens: kildetype as its enum name, and
    // datatype as a bare code with no label at all. Both are Munin's own explorer wording, so
    // the two UIs name the same value the same way. A token missing from either falls back to
    // what the API sent rather than to nothing.
    IReadOnlyDictionary<string, string> KildeTypeNames,
    IReadOnlyDictionary<string, string> DataTypeNames,
    string Ascending,
    string Descending,
    string Pagination,
    string SkipToPagination,
    string Previous,
    string Next,
    // The buttons' accessible names. Longer than the words on them because "Forrige" on its own
    // does not say forrige what — and each one starts with the visible text, so a speech-input
    // user saying what they can see still hits the button (WCAG 2.5.3).
    string PreviousLabel,
    string NextLabel,
    // (page, totalPages) — the pager's own "Side 2 av 13".
    Func<int, int, string> PageOf,
    // (from, to, total, search, filters, field, direction) — the whole result sentence. The
    // ordering clause is part of it rather than appended by the caller, so a language whose
    // grammar puts the ordering first can say it that way instead of inheriting Norwegian's
    // clause order. The filter count is in it for the same reason the ordering is: with the
    // facets collapsed, the sentence is the only place that says the list is narrowed at all.
    Func<int, int, int, string?, int, string, string, string> ResultSummary,
    // (text, others) — a trail step standing for more than one selected value on its level, e.g.
    // "Dødsårsaksregisteret (+2)". Assembled here rather than in C# because where the count goes,
    // and whether a language writes it as a suffix at all, is that language's business.
    Func<string, int, string> CrumbMore,
    // (text) — a trail step's accessible name, which has to say what pressing it does. It starts
    // with the step's visible text so a speech-input user saying what they can see still hits the
    // control (WCAG 2.5.3), which is a constraint on the whole sentence and therefore belongs in
    // the sentence rather than in the caller.
    Func<string, string> CrumbLabel,
    // (search, filters) — the empty state. It names the filters because a search that matches
    // nothing *with three filters on* is a different thing to be told than one that matches
    // nothing at all, and the second reads as "this catalogue does not have it".
    Func<string?, int, string> NoResults,

    // Kelda, the kildeutforsker, which ships from this package beside Runa. The wording is read
    // off Munin's own Kelda rather than translated afresh, so the two UIs name the same thing the
    // same way — down to "Kildetype" for a column whose facet on the variable page helsedata calls
    // "Type datakilde". Where Kelda names something the strings above already name — Navn, Status,
    // Dataansvarlig, Databehandler, Datasamlinger, Søk — the existing one is reused rather than
    // duplicated under a second name that could drift from it.
    string KildeTitle,
    string KildeSearchLabel,
    string KildeSearchPlaceholder,
    string KildeListLoading,
    string KildeListError,
    string NoKilder,
    // The way out of an opened kilde, back to the list.
    string BackToKilder,
    // The status column's two values. A kilde that is no longer collecting data is kept for
    // historical reference rather than removed, so this says which it is rather than hiding one.
    string StatusActive,
    string StatusPassive,
    string ColumnKildetype,
    string ColumnDelkilder,
    // ...VariableCount rather than ...Variables, though the column is headed "Variabler": every
    // member of this record is a string, so a name one letter from ColumnVariable above — the
    // variable list's first column, "Navn" — would swap for it silently and put "Navn" over a
    // column of numbers. The counts elsewhere in this file say so in their names for the same
    // reason: FieldTotalVariables, FieldVariableCount, VariableCountSuffix.
    string ColumnVariableCount,
    // Kelda's filter panel. Its heading is FiltersTitle, and two of its four facet headings are
    // strings this record already holds: the kildetype facet is headed with ColumnKildetype and
    // the databehandler facet with FieldDataProcessor, because each facet is a filter over the
    // very column beside it — renaming the column has to rename the facet, and two strings could
    // not promise that. The two below name nothing else in this package.
    //
    // Headings only. The choices under them are EHDS and EU CURIEs, and their words are the
    // catalogue's own — fetched with the list from the vocabulary the API serves beside it, the
    // same editable master data the kilde view reads. This record held a copy of both vocabularies
    // for a while and it is deliberately gone: a table transcribed here is right on the day it is
    // written and drifts from then on, and the drift showed as a raw CURIE in the facet beside the
    // Norwegian word in the panel one click away.
    string FacetCategory,
    string FacetAccessLevel,
    // The panel's own disclosure, which is one control saying two things: the panel is folded away
    // on a narrow screen and this is what unfolds it. Both wordings are needed because a button
    // still reading "Vis filtre" over an open panel tells the reader the opposite of what pressing
    // it does.
    string ShowFilters,
    string HideFilters,
    // Runa says "Datasamlinger" over a kilde's datasamling table; Kelda says "Delkilder og
    // datasamlinger" over the same rows. One word of difference is not worth a second table — see
    // KildeView.DataCollectionsHeading.
    string HeadingDelkilderAndDataCollections,
    // The sections Kelda has over a kilde and Runa has not, measured on the same source in both on
    // 2026-08-20. They are markup Kelda hands to KildeView.Sections rather than markup inside that
    // component, so their words sit here beside the rest of Kelda's rather than in the shared core.
    // Its own member rather than ColumnVariableCount reused, though both read "Variabler": that one
    // heads a column of numbers in the kilde list and is named for it, so a column that becomes
    // "Antall variabler" must not rename a section heading on its way past.
    string HeadingVariables,
    string HeadingAccessCriteria,
    string HeadingPrices,
    // The bodies of the two static blocks. Munin's Kelda writes them as markdown with links out to
    // helsedata.no and fhi.no; what stands here is one plain sentence each, because the blocks
    // themselves are Fhi.Metadata-ay3zz — where the open question is whether they belong at all in a
    // component embedded on helsedata.no, which is where their links point. A heading with nothing
    // under it reads as a rendering fault, so the sections say the one thing that is true either way
    // and name no URL this package would then own.
    //
    // Body... rather than the bare noun, for the reason ColumnVariableCount above carries its role:
    // these two sit three positions from the headings of the very sections they are the body of and
    // have the same type, so a bare `Prices` reads as a heading and swaps for HeadingPrices in
    // silence. The prefix is what makes the transposition visible at the construction site.
    string BodyAccessCriteria,
    string BodyPrices,
    // (count) — the variable section's one line. Assembled here rather than at the call site for the
    // reason KildeCount is: the singular is this language's business and not C#'s.
    Func<int, string> KildeVariableCount,
    // (count) — the kilde list's own "{n} kilder", which is the whole of what it says about its
    // result set: no row range, because the list is never paged, and no ordering, because it is
    // never sorted. Assembled here rather than glued together at the call site for the reason
    // ResultSummary is: the plural is this language's business and not C#'s.
    Func<int, string> KildeCount,
    // (search, filters) — the kilde list's empty state, for a search or a set of facets that
    // matched none of them. A catalogue holding no kilder at all says NoKilder instead: the two ask
    // the reader to do different things. The facet count is in the sentence for the reason it is in
    // NoResults above — nothing matching *with two facets ticked* is a different thing to be told
    // than nothing matching at all, and the second reads as "this catalogue does not have it".
    Func<string?, int, string> NoKilderMatch)
{
    /// <summary>
    /// The label for a sort order. The three that name one field use the same words the result
    /// cards label that value with, so the button and the line it orders say the same thing.
    /// </summary>
    /// <remarks>
    /// Every member has its own arm, and an unknown one throws rather than falling through to
    /// the default order's label: a member added to <see cref="SortField"/> without a label here
    /// would otherwise put a button on screen claiming an order it does not ask for.
    /// </remarks>
    public string FieldLabel(SortField sort) => sort switch
    {
        // Not "Navn". The API's default order leads with kilde, not the name — see the remarks
        // on SortField.Default — so a button labelled Navn would describe an order the list is
        // not in, which is the one thing the live-region announcement exists to get right.
        SortField.Default => SortDefault,
        SortField.Kilde => FieldSource,
        SortField.Datasamling => FieldDataCollection,
        SortField.Variabelgruppe => FieldVariableGroup,
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "No label for this sort field.")
    };

    /// <summary>
    /// The kind of statistics a datasamling keeps, as a word rather than an API token.
    /// </summary>
    /// <remarks>
    /// Only <c>yearly</c> has ever been observed on the test API — 140 variables sampled on
    /// 2026-08-21 — so an unrecognised kind is shown as it arrived rather than hidden. A heading
    /// reading "Statistikk (accumulated)" is ugly and honest; one silently reading "Statistikk"
    /// would tell a reader these numbers mean something they may not.
    /// </remarks>
    public string StatisticsTypeLabel(string type) => type.ToLowerInvariant() switch
    {
        "yearly" => StatisticsYearly,
        "accumulated" or "akkumulert" => StatisticsAccumulated,
        _ => type
    };

    /// <summary>A version's status as a word, or as it arrived when we have not seen it before.</summary>
    /// <remarks>
    /// Only <c>Active</c> has come back from the test API - every version on every variable
    /// sampled, including ones long superseded. Historical is in the vocabulary and is handled, but
    /// has never been observed, so anything else is shown raw rather than guessed at or hidden.
    /// </remarks>
    public string VersionStatusLabel(string status) => status.ToLowerInvariant() switch
    {
        "active" => VersionActive,
        "historical" or "historisk" => VersionHistorical,
        _ => status
    };

    /// <summary>
    /// Prose for a kildetype token, falling back to what the API called it.
    /// </summary>
    /// <remarks>
    /// A fallback rather than a throw, unlike <see cref="FieldLabel"/>: the tokens are Munin's
    /// kildetype enum and a new member appearing there is a catalogue change, not a bug in this
    /// component. "SentraltHelseregister" on a button is poor prose but it is the truth, where
    /// dropping the value would take a filter off the screen that the API is still counting.
    /// </remarks>
    public string KildeTypeLabel(string? value, string? fallback)
    {
        if (value is not null && KildeTypeNames.TryGetValue(value, out var name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(fallback) ? NotSpecified : fallback;
    }

    /// <summary>
    /// Prose for an identification-level token, falling back to what the API called it.
    /// </summary>
    /// <remarks>
    /// A fallback rather than a throw, for the reason <see cref="KildeTypeLabel"/> has one — the
    /// tokens are Munin's own enum and a new member is a catalogue change, not a bug here. Only
    /// <c>indirectlyIdentifiable</c> appears in the captured payloads this repository holds; the
    /// rest of the table is the literal reading of Munin's spelling, so a token that never
    /// arrives costs nothing and one that does is not renamed into something it is not.
    /// </remarks>
    public string PersonIdentificationLabel(string? value)
    {
        if (value is not null && PersonIdentificationNames.TryGetValue(value, out var name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(value) ? NotSpecified : value;
    }

    /// <summary>
    /// Prose for a kodeverk link's type, falling back to the token the API sent.
    /// </summary>
    /// <remarks>
    /// Two of the three words are the facets' own, deliberately: a helsefaglig kodeverk is the
    /// same thing whether it is being filtered on or read off a variable, and naming it twice
    /// over would be two vocabularies for one catalogue. Case-insensitive because the tokens are
    /// Munin's enum names rather than a contract about capitalisation, and a fallback rather
    /// than a throw for the reason <see cref="KildeTypeLabel"/> has one — a new kind of link is
    /// a catalogue change, not a bug here.
    /// </remarks>
    public string KodeverkTypeLabel(string type) => type switch
    {
        _ when Is(type, "Kildekodeverk") => Kildekodeverk,
        _ when Is(type, "AdministrativtKodeverk") => FacetAdministrativtKodeverk,
        _ when Is(type, "HelsefagligKodeverk") => FacetHelsefagligKodeverk,
        _ => type
    };

    private static bool Is(string value, string token) =>
        string.Equals(value, token, StringComparison.OrdinalIgnoreCase);

    /// <summary>Prose for a datatype code, falling back to the code — same reasoning as above.</summary>
    public string DataTypeLabel(string value) =>
        DataTypeNames.TryGetValue(value, out var name) ? name : value;

    /// <summary>The word for a direction, as the status line and the active button say it.</summary>
    /// <remarks>
    /// A switch with an arm per member rather than "descending, else ascending", for the same
    /// reason <see cref="FieldLabel"/> is one: a member added to <see cref="SortDirection"/>
    /// without a word here would be announced as ascending, and a list announced as ordered the
    /// opposite way to the order it is in is worse than one that fails loudly.
    /// </remarks>
    public string DirectionName(SortDirection direction) => direction switch
    {
        SortDirection.Ascending => Ascending,
        SortDirection.Descending => Descending,
        _ => throw new ArgumentOutOfRangeException(
            nameof(direction), direction, "No name for this sort direction.")
    };

    private static readonly Texts No = new(
        Title: "Variabelutforsker",
        SearchLabel: "Søk i variabler",
        SearchPlaceholder: "Søk etter variabelnavn eller kode",
        SearchButton: "Søk",
        SortBy: "Sorter etter",
        Loading: "Henter variabler …",
        Error: "Kunne ikke hente variabler nå. Prøv igjen om litt.",
        NotSpecified: "Ikke oppgitt",
        SortDefault: "Standard",
        BackToVariables: "← Tilbake til variabler",
        Ongoing: "Pågående",
        FieldKildePath: "Kildesti",
        FieldDataPeriod: "Dataperiode",
        TabDetails: "Detaljer",
        TabData: "Data",
        GroupIdentification: "Identifikasjon",
        GroupPlacement: "Plassering",
        GroupProperties: "Egenskaper",
        ColumnVariable: "Navn",
        Columns: "Kolonner",
        LastColumnHint: "Minst én kolonne må vises.",
        FieldDataType: "Datatype",
        FieldStatus: "Status",
        FieldCode: "Kode",
        FieldSource: "Kilde",
        FieldDataCollection: "Datasamling",
        FieldVariableGroup: "Variabelgruppe",
        FieldPeriod: "Periode",
        FieldDescription: "Beskrivelse",
        FieldKodeverkReference: "Referanse",
        KodeverkUnnamed: "Ukjent navn",
        NoKodeverk: "Ingen kodeverk registrert",
        ShowCodes: "Vis koder",
        HideCodes: "Skjul koder",
        CodesLoading: "Henter koder …",
        CodesError: "Kunne ikke hente kodene nå. Prøv igjen om litt.",
        NoCodes: "Ingen kodeverdier tilgjengelig",
        ColumnCodeValue: "Verdi",
        ColumnCodeName: "Navn",
        ColumnValidFrom: "Gyldig fra",
        ColumnValidTo: "Gyldig til",
        ShowDetails: "Vis detaljer",
        HideDetails: "Skjul detaljer",
        DetailLoading: "Henter detaljer …",
        DetailError: "Kunne ikke hente detaljene nå. Prøv igjen om litt.",
        DetailMissing: "Fant ingen detaljer for denne variabelen.",
        Kildekodeverk: "Kildekodeverk",
        ShowKilde: "Vis datakilde",
        HideKilde: "Skjul datakilde",
        ShowDatasamling: "Vis datasamling",
        HideDatasamling: "Skjul datasamling",
        KildeLoading: "Henter datakilden …",
        DatasamlingLoading: "Henter datasamlingen …",
        KildeError: "Kunne ikke hente datakilden nå. Prøv igjen om litt.",
        DatasamlingError: "Kunne ikke hente datasamlingen nå. Prøv igjen om litt.",
        KildeMissing: "Fant ingen detaljer for denne datakilden.",
        DatasamlingMissing: "Fant ingen detaljer for denne datasamlingen.",
        FieldLegalBasis: "Lovverk",
        FieldDataController: "Dataansvarlig",
        FieldDataProcessor: "Databehandler",
        FieldPersonIdentification: "Grad av personidentifikasjon",
        FieldValidity: "Gyldighet",
        FieldInclusionCriteria: "Inklusjons- og eksklusjonskriterier",
        FieldFrequency: "Frekvens",
        FieldCountingUnit: "Telleenhet",
        FieldVariableCount: "Antall variabler",
        FieldDataCollections: "Antall datasamlinger",
        HeadingMetadata: "Metadata",
        HeadingSourceInformation: "Kildeinformasjon",
        HeadingStatistics: "Statistikk",
        FieldLastUpdated: "Sist oppdatert i Munin",
        FieldTotalVariables: "Totalt antall variabler",
        HeadingDataCollections: "Datasamlinger",
        FieldName: "Navn",
        VariableCountSuffix: "variabler",
        FieldKildeName: "Kildenavn",
        FieldKildeShortName: "Kortnavn",
        FieldVariableGroups: "Variabelgrupper",
        FieldYear: "\u00c5r",
        FieldMinimum: "Minimum",
        FieldMaximum: "Maksimum",
        FieldMean: "Gjennomsnitt",
        FieldStandardDeviation: "Standardavvik",
        StatisticsYearly: "\u00c5rsbasert",
        StatisticsAccumulated: "Akkumulert",
        ShowWholeVariable: "Vis hele variabelen",
        SaveToList: "Lagre i liste",
        RemoveFromList: "Fjern fra liste",
        FirstListName: "Min variabelliste",
        SaveError: "Kunne ikke lagre nå. Prøv igjen om litt.",
        MyListsHeading: "Mine variabellister",
        ChooseList: "Velg liste",
        NewListName: "Navn på ny liste",
        CreateList: "Opprett liste",
        NoListsYet: "Du har ingen variabellister ennå.",
        EmptyList: "Denne listen er tom.",
        RemoveFromThisList: "Fjern",
        ListLoadError: "Kunne ikke hente listen nå. Prøv igjen om litt.",
        VariableNoLongerAvailable: "Variabelen er ikke tilgjengelig lenger",
        HeadingKodeverk: "Kodeverk",
        HeadingVersionHistory: "Versjonshistorikk",
        VersionUnnamed: "Versjon uten navn",
        VersionCurrent: "Gjeldende",
        VersionActive: "Aktiv",
        VersionHistorical: "Historisk",
        FieldValidFrom: "Gyldig fra",
        FieldValidTo: "Gyldig til",
        PersonIdentificationNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["directlyIdentifiable"] = "Direkte identifiserbar",
            ["indirectlyIdentifiable"] = "Indirekte identifiserbar",
            ["pseudonymous"] = "Pseudonymisert",
            ["deIdentified"] = "Avidentifisert",
            ["anonymous"] = "Anonym"
        },
        FiltersTitle: "Filtre",
        ClearFilters: "Fjern alle filtre",
        FilterError: "Kunne ikke oppdatere filtrene nå. Tallene kan være utdaterte.",
        // helsedata's own variable page calls it this, rather than Munin's "Kildetype".
        FacetKildeType: "Type datakilde",
        FacetFilter: "Filter",
        FacetDataType: "Datatype",
        FacetHelsefagligKodeverk: "Helsefaglig kodeverk",
        FacetAdministrativtKodeverk: "Administrativt kodeverk",
        FacetInstrument: "Instrument",
        FacetOther: "Andre filtre",
        HasKildekodeverk: "Har kildekodeverk",
        IncludeHistorical: "Vis historiske",
        NoVariabelgrupper: "Velg en datakilde for å se variabelgrupper.",
        FieldDelkilde: "Delkilde",
        HierarchyTrail: "Valgt hierarki",
        ClearHierarchy: "Fjern hierarkifilteret",
        KildeTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentraltHelseregister"] = "Sentralt helseregister",
            ["nasjonaltMedisinskKvalitetsregister"] = "Nasjonalt medisinsk kvalitetsregister",
            ["annetMedisinskKvalitetsregister"] = "Annet medisinsk kvalitetsregister",
            ["befolkningsbasertHelseundersokelse"] = "Befolkningsbasert helseundersøkelse",
            ["biobank"] = "Biobank",
            ["annenDatakilde"] = "Annen datakilde",
            ["forskningsprosjekt"] = "Forskningsprosjekt",
            ["manueltOpprettet"] = "Manuelt opprettet"
        },
        DataTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Streng",
            ["2"] = "Heltall",
            ["3"] = "Desimaltall",
            ["4"] = "Boolsk",
            ["5"] = "Klokkeslett",
            ["6"] = "Dato",
            ["7"] = "Dato og tid",
            ["8"] = "URI",
            ["9"] = "Base64Binary",
            ["10"] = "Fødselsnummer (11 siffer)"
        },
        Ascending: "stigende",
        Descending: "synkende",
        Pagination: "Paginering",
        SkipToPagination: "Hopp til paginering",
        Previous: "Forrige",
        Next: "Neste",
        PreviousLabel: "Forrige side",
        NextLabel: "Neste side",
        PageOf: (page, totalPages) => $"Side {page} av {totalPages}",
        // The whole sentence, ordering clause included, because the comma and where the clause
        // sits are this language's grammar and not something to fix in C#.
        ResultSummary: (from, to, total, search, filters, field, direction) =>
        {
            var count = total == 1 ? "1 variabel" : $"{total} variabler";
            // One page of a longer list, so say which rows these are rather than captioning
            // rows 26 to 50 as though they were the first 25 of 312.
            var found = from <= 1 && to >= total
                ? $"{count} funnet"
                : $"Viser {from}–{to} av {count} funnet";
            var forSearch = search is null ? "" : $" for «{search}»";
            var narrowed = filters switch
            {
                0 => "",
                1 => ", avgrenset av 1 filter",
                _ => $", avgrenset av {filters} filtre"
            };
            return $"{found}{forSearch}{narrowed}, sortert på {field}, {direction}";
        },
        CrumbMore: (text, others) => $"{text} (+{others})",
        CrumbLabel: text => $"{text} – fjern nivåene under",
        NoResults: (search, filters) =>
        {
            var forSearch = search is null ? "Ingen variabler passet søket" : $"Ingen variabler passet søket «{search}»";
            return filters == 0 ? $"{forSearch}." : $"{forSearch} med filtrene som er valgt.";
        },
        KildeTitle: "Kildeutforsker",
        KildeSearchLabel: "Søk i kilder",
        KildeSearchPlaceholder: "Søk etter navn, kode eller kortnavn",
        KildeListLoading: "Laster kilder …",
        KildeListError: "Kunne ikke laste kilder nå. Prøv igjen om litt.",
        NoKilder: "Ingen kilder er registrert ennå.",
        BackToKilder: "← Tilbake til kildeutforsker",
        StatusActive: "Aktiv",
        StatusPassive: "Passiv",
        ColumnKildetype: "Kildetype",
        ColumnDelkilder: "Delkilder",
        ColumnVariableCount: "Variabler",
        FacetCategory: "Kategori",
        FacetAccessLevel: "Tilgangsnivå",
        ShowFilters: "Vis filtre",
        HideFilters: "Skjul filtre",
        HeadingDelkilderAndDataCollections: "Delkilder og datasamlinger",
        HeadingVariables: "Variabler",
        HeadingAccessCriteria: "Kriterier for tilgang til data",
        HeadingPrices: "Priser",
        BodyAccessCriteria: "Kriteriene for tilgang til data fra denne kilden, og hvordan du søker, "
                        + "er beskrevet på helsedata.no.",
        BodyPrices: "Prisene for utlevering av data fra denne kilden er beskrevet på helsedata.no.",
        KildeVariableCount: count => count == 1
            ? "1 publisert variabel i denne kilden."
            : $"{count} publiserte variabler i denne kilden.",
        KildeCount: count => count == 1 ? "1 kilde" : $"{count} kilder",
        NoKilderMatch: (search, filters) =>
        {
            if (search is null)
            {
                return "Ingen kilder samsvarer med filtrene som er valgt.";
            }

            var forSearch = $"Ingen kilder samsvarer med søket «{search}»";

            return filters == 0 ? $"{forSearch}." : $"{forSearch} og filtrene som er valgt.";
        });

    private static readonly Texts En = new(
        Title: "Variable explorer",
        SearchLabel: "Search variables",
        SearchPlaceholder: "Search by variable name or code",
        SearchButton: "Search",
        SortBy: "Sort by",
        Loading: "Loading variables …",
        Error: "Could not load variables right now. Please try again shortly.",
        NotSpecified: "Not specified",
        SortDefault: "Default",
        BackToVariables: "← Back to variables",
        Ongoing: "Ongoing",
        FieldKildePath: "Source path",
        FieldDataPeriod: "Data period",
        TabDetails: "Details",
        TabData: "Data",
        GroupIdentification: "Identification",
        GroupPlacement: "Placement",
        GroupProperties: "Properties",
        ColumnVariable: "Name",
        Columns: "Columns",
        LastColumnHint: "At least one column has to stay visible.",
        FieldDataType: "Data type",
        FieldStatus: "Status",
        FieldCode: "Code",
        FieldSource: "Source",
        FieldDataCollection: "Data collection",
        FieldVariableGroup: "Variable group",
        FieldPeriod: "Period",
        FieldDescription: "Description",
        FieldKodeverkReference: "Reference",
        KodeverkUnnamed: "Unnamed",
        NoKodeverk: "No code systems registered",
        ShowCodes: "Show codes",
        HideCodes: "Hide codes",
        CodesLoading: "Loading codes …",
        CodesError: "Could not load the codes right now. Please try again shortly.",
        NoCodes: "No code values available",
        ColumnCodeValue: "Value",
        ColumnCodeName: "Name",
        ColumnValidFrom: "Valid from",
        ColumnValidTo: "Valid to",
        ShowDetails: "Show details",
        HideDetails: "Hide details",
        DetailLoading: "Loading details …",
        DetailError: "Could not load the details right now. Please try again shortly.",
        DetailMissing: "No details were found for this variable.",
        Kildekodeverk: "Source code system",
        ShowKilde: "Show data source",
        HideKilde: "Hide data source",
        ShowDatasamling: "Show data collection",
        HideDatasamling: "Hide data collection",
        KildeLoading: "Loading the data source …",
        DatasamlingLoading: "Loading the data collection …",
        KildeError: "Could not load the data source right now. Please try again shortly.",
        DatasamlingError: "Could not load the data collection right now. Please try again shortly.",
        KildeMissing: "No details were found for this data source.",
        DatasamlingMissing: "No details were found for this data collection.",
        FieldLegalBasis: "Legal basis",
        FieldDataController: "Data controller",
        FieldDataProcessor: "Data processor",
        FieldPersonIdentification: "Level of personal identification",
        FieldValidity: "Validity",
        FieldInclusionCriteria: "Inclusion and exclusion criteria",
        FieldFrequency: "Frequency",
        FieldCountingUnit: "Counting unit",
        FieldVariableCount: "Number of variables",
        FieldDataCollections: "Number of data collections",
        HeadingMetadata: "Metadata",
        HeadingSourceInformation: "Source information",
        HeadingStatistics: "Statistics",
        FieldLastUpdated: "Last updated in Munin",
        FieldTotalVariables: "Total number of variables",
        HeadingDataCollections: "Data collections",
        FieldName: "Name",
        VariableCountSuffix: "variables",
        FieldKildeName: "Source name",
        FieldKildeShortName: "Short name",
        FieldVariableGroups: "Variable groups",
        FieldYear: "Year",
        FieldMinimum: "Minimum",
        FieldMaximum: "Maximum",
        FieldMean: "Mean",
        FieldStandardDeviation: "Standard deviation",
        StatisticsYearly: "Yearly",
        StatisticsAccumulated: "Accumulated",
        ShowWholeVariable: "Show the whole variable",
        SaveToList: "Save to list",
        RemoveFromList: "Remove from list",
        FirstListName: "My variable list",
        SaveError: "Could not save just now. Try again shortly.",
        MyListsHeading: "My variable lists",
        ChooseList: "Choose list",
        NewListName: "Name of new list",
        CreateList: "Create list",
        NoListsYet: "You have no variable lists yet.",
        EmptyList: "This list is empty.",
        RemoveFromThisList: "Remove",
        ListLoadError: "Could not fetch the list just now. Try again shortly.",
        VariableNoLongerAvailable: "The variable is no longer available",
        HeadingKodeverk: "Code lists",
        HeadingVersionHistory: "Version history",
        VersionUnnamed: "Version without a name",
        VersionCurrent: "Current",
        VersionActive: "Active",
        VersionHistorical: "Historical",
        FieldValidFrom: "Valid from",
        FieldValidTo: "Valid to",
        PersonIdentificationNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["directlyIdentifiable"] = "Directly identifiable",
            ["indirectlyIdentifiable"] = "Indirectly identifiable",
            ["pseudonymous"] = "Pseudonymised",
            ["deIdentified"] = "De-identified",
            ["anonymous"] = "Anonymous"
        },
        FiltersTitle: "Filters",
        ClearFilters: "Clear all filters",
        FilterError: "Could not refresh the filters right now. The counts may be out of date.",
        FacetKildeType: "Type of data source",
        FacetFilter: "Filter",
        FacetDataType: "Data type",
        FacetHelsefagligKodeverk: "Clinical code system",
        FacetAdministrativtKodeverk: "Administrative code system",
        FacetInstrument: "Instrument",
        FacetOther: "Other filters",
        HasKildekodeverk: "Has source code system",
        IncludeHistorical: "Show historical",
        NoVariabelgrupper: "Select a data source to see variable groups.",
        FieldDelkilde: "Sub-source",
        HierarchyTrail: "Selected hierarchy",
        ClearHierarchy: "Clear the hierarchy filter",
        KildeTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentraltHelseregister"] = "Central health registry",
            ["nasjonaltMedisinskKvalitetsregister"] = "National medical quality registry",
            ["annetMedisinskKvalitetsregister"] = "Other medical quality registry",
            ["befolkningsbasertHelseundersokelse"] = "Population-based health survey",
            ["biobank"] = "Biobank",
            ["annenDatakilde"] = "Other data source",
            ["forskningsprosjekt"] = "Research project",
            ["manueltOpprettet"] = "Manually created"
        },
        DataTypeNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "String",
            ["2"] = "Integer",
            ["3"] = "Decimal",
            ["4"] = "Boolean",
            ["5"] = "Time",
            ["6"] = "Date",
            ["7"] = "Datetime",
            ["8"] = "URI",
            ["9"] = "Base64Binary",
            ["10"] = "National ID (11 digits)"
        },
        Ascending: "ascending",
        Descending: "descending",
        Pagination: "Pagination",
        SkipToPagination: "Skip to pagination",
        Previous: "Previous",
        Next: "Next",
        PreviousLabel: "Previous page",
        NextLabel: "Next page",
        PageOf: (page, totalPages) => $"Page {page} of {totalPages}",
        ResultSummary: (from, to, total, search, filters, field, direction) =>
        {
            var count = total == 1 ? "1 variable" : $"{total} variables";
            var found = from <= 1 && to >= total
                ? $"{count} found"
                : $"Showing {from}–{to} of {count} found";
            var forSearch = search is null ? "" : $" for “{search}”";
            var narrowed = filters switch
            {
                0 => "",
                1 => ", narrowed by 1 filter",
                _ => $", narrowed by {filters} filters"
            };
            return $"{found}{forSearch}{narrowed}, sorted by {field}, {direction}";
        },
        CrumbMore: (text, others) => $"{text} (+{others})",
        CrumbLabel: text => $"{text} – remove the levels below",
        NoResults: (search, filters) =>
        {
            var forSearch = search is null ? "No variables matched your search" : $"No variables matched your search for “{search}”";
            return filters == 0 ? $"{forSearch}." : $"{forSearch} with the filters you have chosen.";
        },
        KildeTitle: "Source explorer",
        KildeSearchLabel: "Search sources",
        KildeSearchPlaceholder: "Search by name, code or short name",
        KildeListLoading: "Loading sources …",
        KildeListError: "Could not load the sources right now. Please try again shortly.",
        NoKilder: "No sources have been registered yet.",
        BackToKilder: "← Back to the source explorer",
        StatusActive: "Active",
        StatusPassive: "Passive",
        ColumnKildetype: "Source type",
        ColumnDelkilder: "Sub-sources",
        ColumnVariableCount: "Variables",
        FacetCategory: "Category",
        FacetAccessLevel: "Access level",
        ShowFilters: "Show filters",
        HideFilters: "Hide filters",
        HeadingDelkilderAndDataCollections: "Sub-sources and data collections",
        HeadingVariables: "Variables",
        HeadingAccessCriteria: "Criteria for access to data",
        HeadingPrices: "Prices",
        BodyAccessCriteria: "The criteria for access to data from this source, and how to apply for it, "
                        + "are described at helsedata.no.",
        BodyPrices: "The prices for having data from this source released are described at helsedata.no.",
        KildeVariableCount: count => count == 1
            ? "1 published variable in this source."
            : $"{count} published variables in this source.",
        KildeCount: count => count == 1 ? "1 source" : $"{count} sources",
        NoKilderMatch: (search, filters) =>
        {
            if (search is null)
            {
                return "No sources match the filters you have chosen.";
            }

            var forSearch = $"No sources match your search for “{search}”";

            return filters == 0 ? $"{forSearch}." : $"{forSearch} and the filters you have chosen.";
        });

    /// <summary>The words for a reader, defaulting to Norwegian for anything that is not English.</summary>
    /// <remarks>
    /// Norwegian rather than a throw for an unrecognised token: the catalogue is Norwegian and the
    /// readers are, so falling back to it leaves a page someone can still use, where a component
    /// that refused to render would take the whole host page down with it.
    /// </remarks>
    public static Texts For(string? language) => ReaderLanguage.IsEnglish(language) ? En : No;
}
