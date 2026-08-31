using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The address bar, for a component that owns some of the query string and none of the rest.
/// </summary>
/// <remarks>
/// No JavaScript ships with this package: <c>history.replaceState</c> is a browser built-in reached
/// through <see cref="IJSRuntime"/>, as <see cref="BrowserDownload"/> reaches <c>Blob</c>.
/// </remarks>
internal sealed class UrlMirror
{
    private readonly IJSRuntime _js;
    private readonly string _path;
    private readonly string _carried;
    private readonly List<(string Name, string Value)> _owned = [];
    private string? _mirrored;

    // owns: whether a decoded parameter name is the component's to read and rewrite. Everything
    // else is carried through untouched, which is the difference between this and a component that
    // rewrites the whole query.
    public UrlMirror(NavigationManager navigation, IJSRuntime js, Func<string, bool> owns)
    {
        _js = js;

        // The circuit's own address is where both halves are readable at once, and it is already
        // absolute: PathBase is in it, where Path alone is relative to the mount point and would
        // send a reader behind a reverse proxy out of the application.
        var address = new Uri(navigation.Uri);
        _path = address.AbsolutePath;

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

    /// <summary>The owned part of the incoming query, for the component's own parser to read.</summary>
    public string Owned =>
        string.Join('&', _owned.Select(pair => Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(pair.Value)));

    /// <summary>The first value the incoming query gave <paramref name="name"/>, decoded.</summary>
    public string? Value(string name) =>
        _owned.Find(pair => string.Equals(pair.Name, name, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } value
            ? value
            : null;

    /// <summary>
    /// Puts <paramref name="query"/> in the address bar beside what the component does not own.
    /// </summary>
    /// <param name="query">The owned keys as a query string with no leading <c>?</c>.</param>
    public async ValueTask MirrorAsync(string query)
    {
        var whole = Join(_carried, query);

        // "?x=1" to set, and the path itself to clear: assigning "" would leave the previous query
        // string in place, so clearing every filter would not clear the URL.
        var url = whole.Length == 0 ? _path : "?" + whole;

        // Without this, every render would call into JS to write the URL it is already showing.
        if (url == _mirrored)
        {
            return;
        }

        _mirrored = url;

        // replaceState, not pushState: opening and closing filters would otherwise fill the history
        // with steps the reader has to walk back through one at a time instead of leaving the site.
        await _js.InvokeVoidAsync("history.replaceState", null, "", url).ConfigureAwait(false);
    }

    private static string Join(string left, string right) =>
        left.Length == 0 ? right : right.Length == 0 ? left : left + "&" + right;

    // The + before the unescape, for VariableFilter.Decode's reasons: a host's query may have been
    // written by an HTML GET form, which spells a space +, and unescaping first turns %2B into one.
    private static string Decode(string token) => Uri.UnescapeDataString(token.Replace('+', ' '));
}
