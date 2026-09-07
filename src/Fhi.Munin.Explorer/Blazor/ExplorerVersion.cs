using System.Reflection;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The version of this package, as every root element carries it in
/// <c>data-munin-explorer-version</c>.
/// </summary>
/// <remarks>
/// Read off the assembly at load time rather than written down anywhere: this package ships no
/// static web assets and no endpoint, so the DOM is the only place a deployed version can be read
/// from — and a literal that drifts is worse than the silence it replaces. (Fhi.Metadata-sqbei)
/// </remarks>
internal static class ExplorerVersion
{
    /// <summary>
    /// The assembly's <see cref="AssemblyInformationalVersionAttribute"/> — the release version
    /// with its prerelease suffix, and the commit behind the <c>+</c> where the build recorded one.
    /// </summary>
    /// <remarks>
    /// Falls back to the assembly version, which is always present but drops the suffix, so
    /// alpha.7 and alpha.8 would read alike. Never empty: an attribute that is there and blank
    /// reads as "no version", which is the one answer this must not give.
    /// </remarks>
    internal static string Current { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(ExplorerVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informational;
    }
}
