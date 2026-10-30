namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The codes of the kilder <see cref="KildeSearch"/> has loaded, and which short forms the API has confirmed open them, so
/// <see cref="KildeExplorer"/> writes a code into the address only when the link it makes works (Fhi.Metadata-9huq2.12).
/// </summary>
internal sealed class KildeCodeBook
{
    internal const string CascadeName = "MuninExplorerKildeCodes";

    private readonly Dictionary<Guid, string> _codes = [];
    private readonly Dictionary<Guid, string> _verified = [];
    private readonly HashSet<(Guid, string)> _asked = [];

    /// <summary>Raised when a kilde's code becomes known or changes.</summary>
    public event Action<Guid>? Learned;

    public void Learn(Guid id, string? code)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(code) || _codes.GetValueOrDefault(id) == code)
        {
            return;
        }

        _codes[id] = code;
        Learned?.Invoke(id);
    }

    /// <summary>Record that the API resolved <paramref name="linkCode"/> to <paramref name="id"/>.</summary>
    public void Verify(Guid id, string linkCode)
    {
        if (id != Guid.Empty && KildeCodes.IsLinkCode(linkCode))
        {
            _verified[id] = linkCode.Trim();
        }
    }

    /// <summary>What the address may carry for <paramref name="id"/>: a code the API has resolved to it, else null.</summary>
    public string? ShortFormFor(Guid id) => _verified.GetValueOrDefault(id);

    /// <summary>
    /// The short form of <paramref name="id"/>'s learned code when it is a link code, not yet the one verified and not
    /// asked before; marks it asked. Null when there is nothing to ask.
    /// </summary>
    public string? NextToVerify(Guid id)
    {
        var shortForm = KildeCodes.ShortForm(_codes.GetValueOrDefault(id));
        if (shortForm is null || !KildeCodes.IsLinkCode(shortForm) || _verified.GetValueOrDefault(id) == shortForm)
        {
            return null;
        }

        return _asked.Add((id, shortForm)) ? shortForm : null;
    }
}
