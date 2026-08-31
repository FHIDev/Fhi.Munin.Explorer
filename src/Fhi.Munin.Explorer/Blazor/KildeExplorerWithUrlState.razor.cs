using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// <see cref="KildeExplorer"/> with the open kilde in the host's address bar, and the handover to
/// <see cref="VariableExplorer"/> wired up.
/// </summary>
/// <remarks>
/// <para>
/// The kildeutforsker's half of what <see cref="VariableExplorerWithUrlState"/> does for the
/// variable explorer, and much smaller because Kelda carries less: the open kilde is the only part
/// of the view worth linking to. A link opens that kilde; closing it puts the reader back on the
/// path they arrived on, <c>PathBase</c> included, rather than on the site root.
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
/// delegate, so a host wrapper raising <see cref="KildeExplorer.SelectedKildeIdChanged"/> across
/// that boundary was silently dead. The callbacks are created inside this component instead, which
/// is why <see cref="VariableExplorerPath"/> is a path and not a delegate.
/// </para>
/// </remarks>
public partial class KildeExplorerWithUrlState : ComponentBase
{
    /// <summary>The query key this component owns: the id of the kilde the reader has open.</summary>
    public const string QueryKey = "kilde";

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="KildeExplorer.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <summary>
    /// Where the host mounted <see cref="VariableExplorer"/>, so the chosen kilder can be handed
    /// over to it. Leave it null and the selection column is not offered at all.
    /// </summary>
    /// <remarks>
    /// The one thing only the host knows, and the reason it is a string: a delegate given to this
    /// component by a statically rendered parent would arrive empty. The handover is a full page
    /// load, because this package has no router and cannot know whether the host has one.
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
        InteractiveMount.Require(RendererInfo.IsInteractive, nameof(KildeExplorerWithUrlState));

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

        Navigation.NavigateTo(
            query.Length == 0 ? VariableExplorerPath! : VariableExplorerPath + "?" + query,
            forceLoad: true);
    }
}
