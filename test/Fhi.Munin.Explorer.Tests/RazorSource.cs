using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Component markup as the checks that read source rather than rendered output want it.
/// </summary>
/// <remarks>
/// One copy, for the reason <see cref="Repo"/> is one copy: this stripping lived in two guards, and
/// two copies means the next change to what a Razor comment looks like fixes one of them.
/// </remarks>
internal static class RazorSource
{
    /// <summary>
    /// <paramref name="markup"/> with its Razor comments blanked out.
    /// </summary>
    /// <remarks>
    /// These files explain in prose the rules the guards enforce, so a check a comment can satisfy,
    /// or break, is a check prose can switch off — <c>assert-sample-css-in-step.sh</c> strips for the
    /// same reason. Blanked to length, so a caller indexing the result still lands where it meant to.
    /// </remarks>
    internal static string WithoutComments(string markup) =>
        Regex.Replace(markup, @"@\*.*?\*@", found => new string(' ', found.Length), RegexOptions.Singleline);
}
