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
}
