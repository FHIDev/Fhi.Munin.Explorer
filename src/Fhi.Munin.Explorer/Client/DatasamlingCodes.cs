using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// A datasamling's code as a variable link carries it (<c>?variabel=…&amp;datasamling=KREG.SIDKRG</c>,
/// Fhi.Metadata-l9l2n.204), matched among the variable's own datasamlinger as Munin's list import matches one.
/// </summary>
internal static class DatasamlingCodes
{
    /// <summary>Longer than any datasamling code, so a longer value is never read.</summary>
    public const int MaxLength = 100;

    // Munin's bound on the Æ that fold independently before it folds them all one way (ExplorerService.KildeCode.cs).
    private const int MaxIndependentAe = 4;

    /// <summary>Whether <paramref name="value"/> could be a datasamling code: letters, digits, «_», «-» or «.».</summary>
    public static bool IsLinkCode(string? value)
    {
        var trimmed = value?.Trim();

        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxLength
            && trimmed.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.');
    }

    /// <summary>
    /// The one datasamling <paramref name="code"/> names: the stored spelling first, then Å, Ø and Æ as A, O and AE or E.
    /// Null for none, and for two in the same tier, which name neither.
    /// </summary>
    public static Guid? Find(IReadOnlyList<DatasamlingReference> datasamlinger, string? code)
    {
        if (!IsLinkCode(code))
        {
            return null;
        }

        var stored = new HashSet<string>([Canonical(code!)], StringComparer.Ordinal);
        var folded = FoldLetters(code!.Trim()).Select(Canonical).Where(s => !stored.Contains(s)).ToHashSet(StringComparer.Ordinal);

        foreach (var tier in new[] { stored, folded })
        {
            var hits = datasamlinger
                .Where(d => !string.IsNullOrWhiteSpace(d.Code) && tier.Contains(Canonical(d.Code!)))
                .Select(d => d.Id)
                .Distinct()
                .ToList();

            if (hits.Count > 0)
            {
                return hits.Count == 1 ? hits[0] : null;
            }
        }

        return null;
    }

    /// <summary>A code folded as Munin compares catalogue codes: ÆØÅ as AE, OE and AA, then upper case.</summary>
    private static string Canonical(string code) =>
        code.Trim().Replace("Æ", "AE").Replace("æ", "ae").Replace("Ø", "OE").Replace("ø", "oe")
            .Replace("Å", "AA").Replace("å", "aa").ToUpperInvariant();

    private static IEnumerable<string> FoldLetters(string code)
    {
        var plain = code.Replace("Å", "A").Replace("å", "a").Replace("Ø", "O").Replace("ø", "o");
        var parts = plain.Split('Æ', 'æ');
        if (parts.Length - 1 > MaxIndependentAe)
        {
            return [string.Join("AE", parts), string.Join("E", parts)];
        }

        IEnumerable<string> spellings = [parts[0]];
        foreach (var part in parts.Skip(1))
        {
            spellings = spellings.SelectMany(prefix => new[] { prefix + "AE" + part, prefix + "E" + part });
        }

        return spellings;
    }
}
