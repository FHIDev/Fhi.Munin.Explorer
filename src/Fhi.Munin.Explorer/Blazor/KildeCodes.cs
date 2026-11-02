namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A kilde's code as a link carries it (Fhi.Metadata-9huq2.12): <c>?kilde=dar</c> for K_DAR, the same in every environment,
/// where an id is not. The API resolves it with or without «K_», in any case and with the reader's ÆØÅ.
/// </summary>
internal static class KildeCodes
{
    /// <summary>Longer than any kilde code, so a longer value is never sent.</summary>
    public const int MaxLength = 100;

    private const string Prefix = "K_";

    /// <summary>The short form a link writes: the code without «K_», lower-cased; null for a blank code.</summary>
    public static string? ShortForm(string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var bare = trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > Prefix.Length
            ? trimmed[Prefix.Length..]
            : trimmed;

        return bare.ToLowerInvariant();
    }

    /// <summary>Whether <paramref name="value"/> could be a kilde code: letters, digits, «_» or «-», not too long.</summary>
    public static bool IsLinkCode(string? value)
    {
        var trimmed = value?.Trim();

        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxLength
            && trimmed.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');
    }
}
