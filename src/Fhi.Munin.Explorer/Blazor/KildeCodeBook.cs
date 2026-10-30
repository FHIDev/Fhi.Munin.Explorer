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
