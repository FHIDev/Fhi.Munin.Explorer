namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The codes of the kilder <see cref="KildeSearch"/> has loaded, so <see cref="KildeExplorer"/> can write a kilde's code
/// into the address bar instead of its id (Fhi.Metadata-9huq2.12).
/// </summary>
internal sealed class KildeCodeBook
{
    internal const string CascadeName = "MuninExplorerKildeCodes";

    private readonly Dictionary<Guid, string> _codes = [];

    /// <summary>Raised when a kilde's code becomes known or changes.</summary>
    public event Action<Guid>? Learned;

    public string? For(Guid id) => _codes.GetValueOrDefault(id);

    /// <summary>
    /// The short form the address may carry for <paramref name="id"/>, or null when it would not read back: unknown, not a
    /// link code, or shared with another kilde this book knows (K_X beside X).
    /// </summary>
    public string? ShortFormFor(Guid id)
    {
        var shortForm = KildeCodes.ShortForm(_codes.GetValueOrDefault(id));

        return shortForm is not null && KildeCodes.IsLinkCode(shortForm)
               && !_codes.Any(other => other.Key != id && KildeCodes.ShortForm(other.Value) == shortForm)
            ? shortForm
            : null;
    }

    public void Learn(Guid id, string? code)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(code) || _codes.GetValueOrDefault(id) == code)
        {
            return;
        }

        _codes[id] = code;
        Learned?.Invoke(id);
    }
}
