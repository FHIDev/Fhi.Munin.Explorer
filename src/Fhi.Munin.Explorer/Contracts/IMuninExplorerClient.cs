namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Everything the components are allowed to know about fetching data.
/// </summary>
/// <remarks>
/// The RCL depends on this interface and never on <c>HttpClient</c>, configuration or any
/// host type — which is what lets the same component render inside helsedata's Optimizely
/// CMS and inside a standalone Blazor app. The implementation lives in
/// <c>Fhi.Munin.Explorer.Client</c>; a host is free to substitute its own.
/// <para>
/// A request for something that does not exist answers null, or an empty collection, rather than
/// throwing: an id in a URL the user edited is a normal event on a public page, not a fault.
/// Anything else — a 500, a timeout, a network failure — still throws, because that is a fault and
/// the caller has to be able to tell the two apart.
/// </para>
/// <para>
/// One of those throws has a type of its own. The API rate-limits per address, and a refusal on
/// that count comes back as <see cref="MuninExplorerRateLimitedException"/> rather than as the
/// general failure: it is neither a fault nor a "not published", and the only thing that helps is
/// waiting — which is why an implementation must not answer it with null, with an empty
/// collection, with <c>false</c> from one of the writes below, or with a retry of its own. The
/// reasoning is on the exception.
/// </para>
/// <para>
/// The variable-list methods at the bottom follow the same rule in the shape a write can take it:
/// one that names a list the signed-in user does not have answers <c>false</c> rather than throwing,
/// because a list deleted in another tab is the same ordinary event as an edited id. They are the
/// only part of this interface that needs a token, and the only writes that can come back with an
/// <see cref="ArgumentException"/> before anything is sent — see <see cref="MaxVariablesPerBatch"/>.
/// They are not the only methods that can: <see cref="GetKodeverkCodesAsync"/> refuses a path
/// segment it cannot carry the same way.
/// </para>
/// </remarks>
public interface IMuninExplorerClient
{
    /// <summary>Search published variables.</summary>
    /// <remarks>
    /// The server orders with the variable code as a secondary key, so rows sharing a value come
    /// back in the same sequence every time — which is what keeps paging through a sorted result
    /// from showing the same variable twice.
    /// </remarks>
    /// <param name="search">Free-text search. Null or empty returns unfiltered results.</param>
    /// <param name="filter">Facet narrowing on top of the search. Null, or <see cref="VariableFilter.None"/>, narrows nothing.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page.</param>
    /// <param name="sort">Order to sort by. Defaults to the API's own order.</param>
    /// <param name="direction">Direction to sort in.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<Page<VariableSummary>> SearchVariablesAsync(
        string? search,
        VariableFilter? filter = null,
        int page = 1,
        int pageSize = 25,
        SortField sort = SortField.Default,
        SortDirection direction = SortDirection.Ascending,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the filter facets and their counts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counts are cross-filtered, so pass the same <paramref name="search"/> and
    /// <paramref name="filter"/> the variable search used or the numbers will describe a different
    /// selection than the list beside them.
    /// </para>
    /// <para>
    /// A facet does not narrow itself: the API drops the selection made <em>in</em> a facet before
    /// counting that facet, so choosing one kilde leaves the other kilder listed with the counts
    /// they would add. It is the selection in the <em>other</em> facets that moves those numbers,
    /// which is what makes a filter reversible without a second request.
    /// </para>
    /// </remarks>
    /// <param name="search">Same free-text search as <see cref="SearchVariablesAsync"/>.</param>
    /// <param name="filter">Same narrowing as <see cref="SearchVariablesAsync"/>.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    /// <param name="language">
    /// The language to resolve facet display names in, as a two-letter code — <c>"nb"</c> or
    /// <c>"en"</c>. Sent as <c>Accept-Language</c>. The datatype facet's name is resolved server
    /// side from editable master data, so it follows this rather than being mapped by the caller.
    /// Null leaves the header off and takes the API's own default.
    /// <para>
    /// A parameter rather than something configured once on the client, which was considered and
    /// does not work here: the language is a <see cref="Fhi.Munin.Explorer.Contracts"/> consumer's
    /// per-render state, not its per-application state. Two explorers can sit on one page in two
    /// languages, and helsedata serves /no/ and /en/ from one application — a delegating handler or
    /// a culture provider registered in DI cannot tell those apart, and would label one of them
    /// wrongly. It has to travel with the call that asks.
    /// </para>
    /// </param>
    Task<FilterOptions> GetFiltersAsync(
        string? search = null,
        VariableFilter? filter = null,
        string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>List all kilder with summary metadata.</summary>
    /// <remarks>Not paged — the API returns the full list in one array.</remarks>
    /// <param name="search">Case-insensitive substring match on name, code or short name.</param>
    /// <param name="kildeType">Restrict to one kildetype, e.g. <c>sentraltHelseregister</c>.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
        string? search = null,
        string? kildeType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The vocabulary behind the curated properties the kilde list carries: one entry per key its
    /// <see cref="KildeSummary.AdditionalProperties"/> bag can hold. Empty when the API serves none.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="KildeDetail.PropertyMetadata"/>, and for the same reason —
    /// <c>additionalProperties</c> is a bag of stored codes with no dictionary beside it, so a
    /// caller drawing a word for one of those codes needs the vocabulary that defines it. The
    /// detail endpoints ship theirs with the record; the list does not, because the vocabulary is
    /// global rather than per kilde and repeating it on every row would send it some sixty times.
    /// This is the list's half, served as a sibling of it.
    /// <para>
    /// No language parameter, deliberately, and it is the one thing that would look like an
    /// omission: <see cref="GetKilderAsync"/> is fetched language-agnostically and its rows are
    /// rendered to whichever reader is looking, so a caller switching language without refetching
    /// has to be able to switch the words too. That means reading
    /// <see cref="PropertyMetadataEntry.OptionsJson"/>, which carries both labels, rather than
    /// <see cref="PropertyMetadataEntry.Options"/>, which carries the one the request asked for.
    /// </para>
    /// <para>
    /// The one member here with a body, and it answers nothing. This interface is already on the
    /// feed, and a version there cannot be taken back from whoever restored it — so it is a
    /// contract with hosts rather than a seam inside this package, and anything implementing it
    /// instead of consuming <c>MuninExplorerClient</c> stops compiling on upgrade when a member
    /// arrives without a default. Empty is a working answer rather than a placeholder: it is the
    /// state a caller reaches anyway when the endpoint is unreachable, and
    /// <c>KildeExplorer</c> already treats that as labels lost and nothing else — the coded facets
    /// show the catalogue's own tokens. What it costs is that a host which never overrides it gets
    /// CURIEs on two facets silently, which is the price of not breaking the ones that have not
    /// caught up; anything louder would be a page-level failure over a label.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PropertyMetadataEntry>>([]);

    /// <summary>Fetch one kilde with its delkilde/datasamling tree. Null when no such kilde is published.</summary>
    Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the kilde's navigation tree — ids, names and counts only. Null when no such kilde is
    /// published. Prefer this over <see cref="GetKildeAsync"/> when all that is needed is a tree.
    /// </summary>
    Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Fetch one datasamling. Null when no such datasamling is published.</summary>
    Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Fetch one variable with version history, kodeverk and statistics. Null when not published.</summary>
    /// <param name="id">The variable's id.</param>
    /// <param name="includeHistorical">
    /// Include variables whose every version has expired. Off by default, so a search result and a
    /// detail page agree on what exists.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<VariableDetail?> GetVariableAsync(
        Guid id,
        bool includeHistorical = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch every published version of a variable, newest and oldest alike. Empty when the
    /// variable does not exist.
    /// </summary>
    Task<IReadOnlyList<VariableVersion>> GetVariableTimelineAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the code values behind one of a variable's kodeverk links. Null when the catalogue
    /// publishes none for it.
    /// </summary>
    /// <remarks>
    /// Its own call rather than part of <see cref="GetVariableAsync"/>, because a kodeverk can run
    /// to hundreds of codes and most readers never open one. Ask for it when a reader says so.
    /// <para>
    /// Null covers three cases the caller cannot usefully tell apart, and all of which read as "no
    /// codes to show": a <c>HelsefagligKodeverk</c> link, whose values the API does not serve at all
    /// — <see cref="KodeverkLink.HasCodeValues"/> is false on those, so a UI can keep from asking; a
    /// reference the upstream code register does not know; and a variable that is not published.
    /// A fault still throws, the same rule the rest of this interface follows.
    /// </para>
    /// <para>
    /// Both the type and the reference are carried in the request path, so neither may contain a
    /// part that is nothing but dots — <c>..</c> would address a different endpoint on the same
    /// host, and no escaping survives the normalisation that makes it do so. One that does is
    /// refused with an <see cref="ArgumentException"/> before any request is made.
    /// </para>
    /// </remarks>
    /// <param name="variableId">The variable the link hangs off.</param>
    /// <param name="kodeverkType">The link's <see cref="KodeverkLink.KodeverkType"/>, verbatim apart from the dot rule above.</param>
    /// <param name="kodeverkReference">The link's <see cref="KodeverkLink.KodeverkReference"/>, verbatim apart from the dot rule above.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<KodeverkCodes?> GetKodeverkCodesAsync(
        Guid variableId,
        string kodeverkType,
        string kodeverkReference,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ the user's own variable lists
    //
    // Everything above is anonymous and read-only. Everything below is not: the whole of
    // `api/explorer/my/lists` sits behind the API's authenticated explorer policy, so a caller
    // reaches it only when the host has registered an IMuninExplorerTokenProvider *before*
    // AddMuninExplorer — see the remarks on that interface. With the anonymous default in place
    // every one of these answers 401, which arrives here as an HttpRequestException rather than as
    // an empty list, because a host that thinks it wired up sign-in has a fault rather than a user
    // with nothing saved.

    /// <summary>
    /// The largest number of variable ids the API accepts in one batch add or remove.
    /// </summary>
    /// <remarks>
    /// Declared on the contract rather than inside the client because the ceiling belongs to the
    /// API: a host substituting its own <see cref="IMuninExplorerClient"/> is talking to the same
    /// endpoint and meets the same limit. A caller with more ids than this splits them itself —
    /// <c>ids.Chunk(IMuninExplorerClient.MaxVariablesPerBatch)</c> — and calls once per chunk.
    /// <para>
    /// The splitting is left to the caller on purpose. One call either happens or does not; a
    /// split does not, and a client that quietly turned 2500 ids into two requests would leave the
    /// list holding the first 2000 when the second failed, with nothing in the return value to say
    /// so. A caller doing the splitting knows how far it got, which is what it needs to retry or to
    /// tell the user.
    /// </para>
    /// <para>
    /// A <c>static readonly</c> field rather than a <c>const</c>, which reads identically at the
    /// call site and behaves differently across the package boundary: a const literal is copied
    /// into the host's IL when the host compiles, so a host that restored a newer
    /// <c>Fhi.Munin.Explorer</c> after the API raised its ceiling would keep chunking at the old
    /// number while this assembly's own check used the new one. This way the shipped package is
    /// the single answer.
    /// </para>
    /// </remarks>
    static readonly int MaxVariablesPerBatch = 2000;

    /// <summary>The signed-in user's saved variable lists, newest changes and all. Empty when they have none.</summary>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default);

    /// <summary>Create a list for the signed-in user and return it as the API stored it.</summary>
    /// <remarks>
    /// The API trims the name and refuses one that is empty or longer than 200 characters, with a
    /// message in the caller's language. That refusal is a <c>400</c> and therefore throws: it is
    /// something the user must be told about, not an absence to render.
    /// </remarks>
    /// <param name="name">What to call the list.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rename one of the signed-in user's lists. False when they have no list with that id.
    /// </summary>
    /// <remarks>
    /// The name is the only thing a list has to change, which is why this is a rename rather than
    /// an update taking the whole record — the API's own body carries nothing else either.
    /// </remarks>
    /// <param name="id">The list to rename.</param>
    /// <param name="name">What to call it instead.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<bool> RenameMyListAsync(Guid id, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete one of the signed-in user's lists, and everything in it. False when they have no list
    /// with that id.
    /// </summary>
    /// <param name="id">The list to delete.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of what is in a list. Null when the signed-in user has no list with that id.
    /// </summary>
    /// <remarks>
    /// Paged where <see cref="GetMyListsAsync"/> is not, because a list is as long as the user made
    /// it. The API clamps <paramref name="pageSize"/> to at most 1000 and both numbers to at least
    /// 1, and the page that comes back says which size it actually used.
    /// </remarks>
    /// <param name="id">The list to read.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Entries per page. The API's own default is 100 and its ceiling is 1000.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<Page<VariableListItem>?> GetMyListVariablesAsync(
        Guid id,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add variables to one of the signed-in user's lists. False when they have no list with that id.
    /// </summary>
    /// <remarks>
    /// Adding a variable the list already holds is not an error — the API stores each id once, so a
    /// caller need not work out the difference before calling. An empty collection is a legitimate
    /// call and is still sent: the answer says whether the list exists.
    /// </remarks>
    /// <param name="id">The list to add to.</param>
    /// <param name="variableIds">
    /// The variables to add, at most <see cref="MaxVariablesPerBatch"/> of them. More than that is
    /// refused with an <see cref="ArgumentException"/> before any request is made, rather than sent
    /// and answered with a <c>400</c> the caller has to unpack.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<bool> AddVariablesToMyListAsync(
        Guid id,
        IReadOnlyCollection<Guid> variableIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove variables from one of the signed-in user's lists. False when they have no list with
    /// that id.
    /// </summary>
    /// <remarks>
    /// Removing an id the list does not hold is not an error either, for the same reason adding a
    /// duplicate is not. The batch ceiling is the same one, and is enforced here the same way.
    /// </remarks>
    /// <param name="id">The list to remove from.</param>
    /// <param name="variableIds">The variables to remove, at most <see cref="MaxVariablesPerBatch"/> of them.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<bool> RemoveVariablesFromMyListAsync(
        Guid id,
        IReadOnlyCollection<Guid> variableIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the reader's "Ønskede data" annotation against one variable in one of their lists,
    /// or clears it.
    /// </summary>
    /// <remarks>
    /// The one write here that answers with more than "was it yours". The API caps the text at 500
    /// characters and refuses a longer one with a <c>400</c> naming the ceiling, which is a refusal
    /// of something the reader typed rather than a fault — so it comes back as
    /// <see cref="DesiredDataOutcome.Refused"/> with the ceiling attached, not as a throw and not
    /// as a silent success. A caller that cannot draw that distinction leaves the reader typing
    /// into a field that keeps refusing without saying so.
    /// <para>
    /// Clearing and writing are the same call: null, empty, or nothing but whitespace removes the
    /// annotation, which is what the API does with a blank body. The text is trimmed on the way
    /// out because the API trims it on the way in, so the caller is told about the length the API
    /// will actually measure.
    /// </para>
    /// <para>
    /// Carries a default body, like <see cref="ExportListAsync"/> and
    /// <see cref="GetKildePropertyMetadataAsync"/> and for the same reader: this interface is
    /// already on the feed, and a host that implements it rather than consuming
    /// <c>MuninExplorerClient</c> would otherwise stop building on the upgrade. The default refuses
    /// rather than reporting a save that never happened.
    /// </para>
    /// </remarks>
    /// <param name="id">The list the variable is in.</param>
    /// <param name="variableId">The variable to annotate. Spelled <c>variabelId</c> in the route.</param>
    /// <param name="freeText">
    /// What the reader wants from this variable. Null, empty or whitespace clears the annotation.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<DesiredDataResult> SetMyListDesiredDataAsync(
        Guid id,
        Guid variableId,
        string? freeText,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"This {nameof(IMuninExplorerClient)} does not implement {nameof(SetMyListDesiredDataAsync)}. " +
            "Consume MuninExplorerClient, or implement the member.");
    /// <summary>
    /// The reader's chosen variables as a file — xlsx, csv, or a zip when codebooks come too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anonymous, unlike the rest of <c>my/lists</c>: the ids travel in the body, so the endpoint
    /// has no need to know whose list they came from. That is why it lives under
    /// <c>api/explorer/lists</c> rather than <c>my/lists</c>, and why no token is required.
    /// </para>
    /// <para>
    /// The ceiling here is the API's own <c>MaxVariabelCount</c> of 2000, which is not the same
    /// number as <see cref="MaxVariablesPerBatch"/>, and is enforced server-side with a 400 that
    /// names it.
    /// </para>
    /// <para>
    /// Carries a default body, like <see cref="GetKildePropertyMetadataAsync"/> and for the same
    /// reader: a host that implements this contract rather than consuming
    /// <c>MuninExplorerClient</c> would otherwise stop building on the upgrade, and a version
    /// already on the feed cannot be taken back from whoever restored it. The default refuses
    /// rather than answering emptily — an empty file is a worse answer than a clear no.
    /// </para>
    /// </remarks>
    /// <param name="variableIds">The variables to export.</param>
    /// <param name="format">Xlsx or Csv. Csv with codebooks answers with a zip.</param>
    /// <param name="includeKodeverk">Whether to include the codebooks alongside the variables.</param>
    /// <param name="kildeIdFilter">Optional: only the variables belonging to one kilde.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away.</param>
    Task<ExportedList> ExportListAsync(
        IReadOnlyCollection<Guid> variableIds,
        ExportFormat format = ExportFormat.Xlsx,
        bool includeKodeverk = false,
        Guid? kildeIdFilter = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"This {nameof(IMuninExplorerClient)} does not implement {nameof(ExportListAsync)}. " +
            "Consume MuninExplorerClient, or implement the member.");
}
