using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The address bar, for a component that owns some of the query string and none of the rest.
/// </summary>
/// <remarks>
/// <c>history.replaceState</c> is a browser built-in reached through <see cref="IJSRuntime"/>, as
/// <see cref="BrowserDownload"/> reaches <c>Blob</c> — not an export of the package's own module,
/// so the address bar follows the view whether or not a host serves that file.
/// </remarks>
internal sealed class UrlMirror
{
    private readonly IJSRuntime _js;
    private readonly string _path;
    private readonly string _carried;
    private readonly string? _fragment;
    private readonly List<(string Name, string Value)> _owned = [];
    private string? _mirrored;
    private string? _fragmentOwner;
    private bool _fragmentSpent;

    // The circuit's own address is where both halves are readable at once, and it is already
    // absolute: PathBase is in it, where NavigationManager.Uri's path alone is relative to the
    // mount point and would send a reader behind a reverse proxy out of the application.
    public UrlMirror(NavigationManager navigation, IJSRuntime js, Func<string, bool> owns)
        : this(new Uri(navigation.Uri), js, owns)
    {
    }

    // owns: whether a decoded parameter name is the component's to read and rewrite. Everything
    // else is carried through untouched, which is the difference between this and a component that
    // rewrites the whole query.
    public UrlMirror(Uri address, IJSRuntime js, Func<string, bool> owns)
    {
        _js = js;
        _path = address.AbsolutePath;

        // Kept with its '#', and null for "#" alone: an empty fragment names no section, and writing
        // one back would put a bare hash in the address bar for nothing.
        _fragment = address.Fragment is { Length: > 1 } fragment ? fragment : null;

        var carried = new StringBuilder();

        foreach (var pair in address.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var name = Decode(separator <= 0 ? pair : pair[..separator]);

            if (owns(name))
            {
                _owned.Add((name, separator <= 0 ? "" : Decode(pair[(separator + 1)..])));
            }
            else
            {
                // Re-emitted exactly as it arrived, escaping and all: re-encoding somebody else's
                // parameter is a way to change it.
                carried.Append(carried.Length == 0 ? "" : "&").Append(pair);
            }
        }

        _carried = carried.ToString();
    }

    /// <summary>The address's own path, so a caller can tell this page from another one.</summary>
    public string Path => _path;

    /// <summary>The owned part of the incoming query, for the component's own parser to read.</summary>
    public string Owned =>
        string.Join('&', _owned.Select(pair => Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(pair.Value)));

    /// <summary>
    /// The first value the incoming query gave <paramref name="name"/>, decoded — and null when it
    /// gave none. An empty value is null too: <c>?kilde=</c> names a kilde no better than nothing.
    /// </summary>
    public string? Value(string name) =>
        _owned.Find(pair => string.Equals(pair.Name, name, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } value
            ? value
            : null;

    /// <summary>
    /// Every value the incoming query gave <paramref name="name"/>, decoded and in order, empty ones
    /// included — so a repeated key reads whole, and <c>?columns=</c> is told apart from no key.
    /// </summary>
    /// <remarks>
    /// At most <see cref="MaxValuesPerKey"/> of them: the query is untrusted input, and what is read
    /// here is held for the circuit's life and written back into every link.
    /// </remarks>
    public IReadOnlyList<string> Values(string name) =>
        [.. _owned.Where(pair => string.Equals(pair.Name, name, StringComparison.OrdinalIgnoreCase))
                  .Select(pair => pair.Value)
                  .Take(MaxValuesPerKey)];

    /// <summary>The most values <see cref="Values"/> reads for one key; above any kilde catalogue.</summary>
    public const int MaxValuesPerKey = 500;

    /// <summary>
    /// This page's address carrying <paramref name="query"/> as the owned keys, for an
    /// <c>&lt;a href&gt;</c> the browser resolves on its own.
    /// </summary>
    /// <param name="query">The owned keys as a query string with no leading <c>?</c>.</param>
    /// <remarks>
    /// <para>
    /// Absolute-path rather than relative for <see cref="MirrorAsync"/>'s reason, which a link has
    /// too: a bare <c>?x=1</c> resolves against the document's <c>&lt;base href&gt;</c> and lands
    /// wherever that points rather than back on this page.
    /// </para>
    /// <para>
    /// <b>The argument and the incoming address are the whole of it</b> — nothing here reads what
    /// <see cref="MirrorAsync"/> last wrote. So a caller may build a link from the state it is
    /// about to mirror, during the render that introduces it, and get the address the mirror will
    /// write rather than the one before it. <see cref="DetailToc"/>'s cascade depends on that
    /// ordering: it renders before <c>OnAfterRenderAsync</c> runs.
    /// </para>
    /// <para>
    /// <b>Never a fragment</b>, whatever the incoming address carried — <see cref="MirrorAsync"/> is
    /// the only thing that writes one. A drill-in link is a way out of the view on screen, so an id
    /// naming one of its sections would name nothing in the view the link opens.
    /// </para>
    /// </remarks>
    public string Address(string query)
    {
        var whole = Join(_carried, query);

        return whole.Length == 0 ? _path : _path + "?" + whole;
    }

    /// <summary>
    /// Puts <paramref name="query"/> in the address bar beside what the component does not own, and
    /// behind the section the incoming address named while that is still the view on screen.
    /// </summary>
    /// <param name="query">The owned keys as a query string with no leading <c>?</c>.</param>
    public async ValueTask MirrorAsync(string query)
    {
        var url = Address(query) + Fragment(query);

        // Without this, every render would call into JS to write the URL it is already showing.
        if (url == _mirrored)
        {
            return;
        }

        // replaceState, not pushState: opening and closing filters would otherwise fill the history
        // with steps the reader has to walk back through one at a time instead of leaving the site.
        await _js.InvokeVoidAsync("history.replaceState", null, "", url).ConfigureAwait(false);

        // After the call, not before: a write the browser refused must not read as one it has.
        _mirrored = url;
    }

    /// <summary>
    /// The incoming address's fragment while <paramref name="query"/> is still the state it arrived
    /// with, and nothing once it is not — spent for good at the first different one.
    /// </summary>
    /// <remarks>
    /// A reader who jumped to a section has it in the address bar already, and this is what keeps a
    /// rewrite from taking it back. The owner is the first query mirrored rather than the incoming
    /// one because those are the same state, said by the component rather than by the URL.
    /// </remarks>
    private string Fragment(string query)
    {
        if (_fragment is null || _fragmentSpent)
        {
            return "";
        }

        _fragmentOwner ??= query;

        if (string.Equals(_fragmentOwner, query, StringComparison.Ordinal))
        {
            return _fragment;
        }

        // The section belongs to the view being left, so its id names nothing in the one arriving.
        _fragmentSpent = true;

        return "";
    }

    private static string Join(string left, string right) =>
        left.Length == 0 ? right : right.Length == 0 ? left : left + "&" + right;

    // The + before the unescape, for VariableFilter.Decode's reasons: a host's query may have been
    // written by an HTML GET form, which spells a space +, and unescaping first turns %2B into one.
    private static string Decode(string token) => Uri.UnescapeDataString(token.Replace('+', ' '));
}
