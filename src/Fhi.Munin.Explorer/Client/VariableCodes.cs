namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// A variable's code as a link carries it (<c>?variabel=V_MFR.KJONN</c>, Fhi.Metadata-l9l2n.182): the same in every
/// environment, where an id is not. Munin resolves it in any case and with the reader's ÆØÅ.
/// </summary>
internal static class VariableCodes
{
    /// <summary>Munin's own bound on a variable code (Fhi.Metadata-9huq2.27), so a longer value is never sent.</summary>
    public const int MaxLength = 255;

    /// <summary>Whether <paramref name="value"/> could be a variable code: letters, digits, «_», «-» or «.», not too long.</summary>
    public static bool IsLinkCode(string? value)
    {
        var trimmed = value?.Trim();

        // A letter or digit at least: "." and ".." are path segments, not codes.
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxLength && trimmed.Any(char.IsLetterOrDigit)
            && trimmed.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.');
    }
}
