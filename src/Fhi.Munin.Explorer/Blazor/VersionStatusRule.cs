namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Reads the API's version status, which arrives as a word in either language.</summary>
internal static class VersionStatusRule
{
    /// <summary>Whether the variable is historical, and so missing from a search that leaves those out.</summary>
    public static bool IsHistorical(string? status) =>
        string.Equals(status, "historical", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "historisk", StringComparison.OrdinalIgnoreCase);
}
