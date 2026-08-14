namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Everything the components are allowed to know about fetching data.
/// </summary>
/// <remarks>
/// The RCL depends on this interface and never on <c>HttpClient</c>, configuration or any
/// host type — which is what lets the same component render inside helsedata's Optimizely
/// CMS and inside a standalone Blazor app. The implementation lives in
/// <c>Fhi.Munin.Explorer.Client</c>; a host is free to substitute its own.
/// </remarks>
public interface IMuninExplorerClient
{
    /// <summary>Search published variables.</summary>
    /// <param name="sok">Free-text search. Null or empty returns unfiltered results.</param>
    /// <param name="side">1-based page number.</param>
    /// <param name="sideStorrelse">Rows per page.</param>
    Task<Side<VariabelSammendrag>> SokVariablerAsync(
        string? sok,
        int side = 1,
        int sideStorrelse = 25,
        CancellationToken cancellationToken = default);
}
