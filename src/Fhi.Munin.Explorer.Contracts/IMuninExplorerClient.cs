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
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page.</param>
    /// <param name="sort">Order to sort by. Defaults to the API's own order.</param>
    /// <param name="direction">Direction to sort in.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<Page<VariableSummary>> SearchVariablesAsync(
        string? search,
        int page = 1,
        int pageSize = 25,
        SortField sort = SortField.Default,
        SortDirection direction = SortDirection.Ascending,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the filter facets and their counts.
    /// </summary>
    /// <remarks>
    /// The counts are cross-filtered, so pass the same narrowing the variable search used or the
    /// numbers will describe a different selection than the list beside them.
    /// </remarks>
    /// <param name="search">Same free-text search as <see cref="SearchVariablesAsync"/>.</param>
    /// <param name="kildeType">Restrict counts to one kildetype, e.g. <c>sentraltHelseregister</c>.</param>
    /// <param name="cancellationToken">Cancelled when the caller goes away — in a Blazor host, when the component is disposed.</param>
    Task<FilterOptions> GetFiltersAsync(
        string? search = null,
        string? kildeType = null,
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
