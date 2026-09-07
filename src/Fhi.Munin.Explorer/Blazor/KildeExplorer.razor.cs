using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The kildeutforsker: the kilde list, the drill-in, and a link that reopens the kilde that was on
/// screen when it was copied.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one component a host mounts.</b> <see cref="Language"/> is the whole of what it needs —
/// which is what helsedata's <c>BlazorComponentPage</c> offers — and the name a host reaches for
/// first is therefore the one that works, as it is for <see cref="VariableExplorer"/>.
/// <see cref="KildeSearch"/> stays public underneath for a host that wants to own its own query
/// string or lay the surface out itself.
/// </para>
/// <para>
/// The kildeutforsker's half of what <see cref="VariableExplorer"/> does for the variable side, and
/// much smaller because Kelda carries less: the open kilde is the only part of the view worth
/// linking to, and there are no personal lists to put behind a second tab. A link opens that kilde;
/// closing it puts the reader back on the path they arrived on, <c>PathBase</c> included, rather
/// than on the site root.
/// </para>
/// <para>
/// <b>It reads and writes <c>?kilde=</c> and nothing else.</b> A host's own parameters — and
/// <c>?search=</c>, which Kelda cannot maintain and so must not adopt — are carried through
/// untouched.
/// </para>
/// <para>
/// <b>It must be mounted interactively</b> — <c>render-mode="Server"</c> in a legacy Blazor Server
/// host, <c>@rendermode</c> with <c>prerender: false</c> in a modern one — and it throws on
/// initialisation if it is not. This component is where that mattered first: an
/// <see cref="EventCallback"/> created in a statically rendered parent serialises to an empty
/// delegate, so a host wrapper raising <see cref="KildeSearch.SelectedKildeIdChanged"/> across
/// that boundary was silently dead. The callbacks are created inside this component instead, which
/// is why <see cref="VariableExplorerPath"/> is a path and not a delegate.
/// </para>
/// </remarks>
public sealed partial class KildeExplorer : ComponentBase
{
    /// <summary>The query key this component owns: the id of the kilde the reader has open.</summary>
    public const string QueryKey = "kilde";

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="KildeSearch.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="KildeSearch.ShowAccessAndPrices"/>
    /// <remarks>
    /// Declared here as well as on <see cref="KildeSearch"/>, and forwarded, because this is the
    /// mount a CMS host names: a parameter the mounted type does not declare is dropped before it
    /// is set, silently and with everything still compiling.
    /// </remarks>
    [Parameter] public bool ShowAccessAndPrices { get; set; }

    /// <summary>
    /// Where the host mounted <see cref="VariableSearch"/>, so the chosen kilder can be handed
    /// over to it. Leave it null and the selection column is not offered at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one thing only the host knows, and the reason it is a string: a delegate given to this
    /// component by a statically rendered parent would arrive empty. The handover is a full page
    /// load, because this package has no router and cannot know whether the host has one.
    /// </para>
    /// <para>
    /// <b>Relative to the application, not to the domain.</b> <c>"variabler"</c> and
    /// <c>"/variabler"</c> both mean the same page of your application; the leading slash does not
    /// send the reader to the domain root, because this resolves against
    /// <see cref="NavigationManager.BaseUri"/> so that a host mounted under a path base keeps it. A
    /// full URL is taken as given, for the host whose other explorer is in another application.
    /// </para>
    /// <para>
    /// <b>A CMS host cannot set it, and null is the right answer there.</b> helsedata's
    /// <c>BlazorComponentPage</c> offers a fixed candidate list — <c>Language</c>, <c>SkjemaId</c>,
    /// <c>IsAuthenticated</c> — and drops every name outside it, so this parameter is reachable
    /// only from a host that writes the mount itself. Defaulting it to a guess such as
    /// <c>"variabler"</c> would be worse than leaving it null: the column would be drawn and its
    /// button would land on a page that host may not have. Null draws no column, which is the same
    /// page that mount renders today.
    /// </para>
    /// </remarks>
    [Parameter] public string? VariableExplorerPath { get; set; }

    private Guid? _selectedKildeId;

    private UrlMirror _mirror = default!;

    private EventCallback<IReadOnlyList<Guid>> Handover =>
        VariableExplorerPath is null
            ? default
            : EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, ExploreVariables);

    protected override void OnInitialized()
    {
        InteractiveMount.Require(RendererInfo.IsInteractive, nameof(KildeExplorer));

        _mirror = new UrlMirror(Navigation, JS, key => string.Equals(key, QueryKey, StringComparison.OrdinalIgnoreCase));

        // A kilde id in a URL is whatever a stranger typed. One that does not parse opens the list,
        // and one that parses but names nothing the API publishes opens a view that says so — the
        // component's own documented behaviour, so nothing is validated here.
        if (Guid.TryParse(_mirror.Value(QueryKey), out var parsed))
        {
            _selectedKildeId = parsed;
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        _mirror.MirrorAsync(_selectedKildeId is { } id
            ? QueryKey + "=" + Uri.EscapeDataString(id.ToString())
            : "").AsTask();

    /// <summary>Turn the chosen kilder into the query the variable explorer reads, and go there.</summary>
    /// <remarks>
    /// An empty list is not a selection of none: it is what the component sends when the reader
    /// narrowed nothing, so it lands on the unfiltered variable list. The format is not restated
    /// here — <see cref="VariableFilter.ToQueryString"/> writes what its own <c>Parse</c> reads.
    /// </remarks>
    private void ExploreVariables(IReadOnlyList<Guid> kildeIds)
    {
        var query = new VariableFilter { KildeIds = kildeIds }.ToQueryString();

        // Against the application base rather than the origin. NavigateTo("/variabler") from an app
        // mounted under /optimizely drops the prefix, which is identical locally and sends the
        // reader out of the application behind a reverse proxy — the trap the mirror avoids too.
        var path = (VariableExplorerPath ?? "").TrimStart('/');
        var destination = Navigation.ToAbsoluteUri(query.Length == 0 ? path : path + "?" + query);

        Navigation.NavigateTo(destination.ToString(), forceLoad: true);
    }
}
