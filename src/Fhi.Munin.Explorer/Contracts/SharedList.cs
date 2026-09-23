namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// A variable list somebody shared as a code, as <c>GET /api/explorer/lists/share/{code}</c>
/// stored it: a snapshot of names and variables, not a list the reader owns.
/// </summary>
/// <remarks>
/// The snapshot is written by whichever frontend shared it — this package or Runa — and read
/// tolerantly: an item without a readable <c>variabelId</c> is dropped, a repeated one keeps its
/// first occurrence, and a display field that is missing or unreadable is null. The display fields
/// are the sharer's copy at the time of sharing, so they may be older than the catalogue.
/// </remarks>
/// <param name="Name">The name the sharer gave the list, trimmed. Empty when the snapshot has none.</param>
/// <param name="Items">The variables, in the order the snapshot holds them, each id once.</param>
public sealed record SharedList(string Name, IReadOnlyList<VariableListItem> Items)
{
    /// <summary>How many characters a share code has. The API mints them from A–Z and 0–9.</summary>
    public const int CodeLength = 6;

    /// <summary>
    /// The code upper-cased, or <see langword="null"/> when it is not six ASCII letters or digits
    /// — the only codes the API can have minted.
    /// </summary>
    public static string? NormalizeCode(string? code)
    {
        var trimmed = code?.Trim();

        return trimmed is { Length: CodeLength } && trimmed.All(char.IsAsciiLetterOrDigit)
            ? trimmed.ToUpperInvariant()
            : null;
    }
}
