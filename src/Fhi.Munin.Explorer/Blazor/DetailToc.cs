using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The contents nav of a detail view: one link per section the view drew, in the order it drew
/// them.
/// </summary>
/// <remarks>
/// <para>
/// Fragment links and no script at all, so the scrolling is the host's to do and
/// <c>scroll-margin-top</c> on <see cref="DetailSection"/>'s wrapper is what puts the heading clear
/// of a sticky site header. Whether the jump carries keyboard FOCUS as well as the viewport depends
/// on the host: a browser making the fragment jump itself moves focus to the target, and a Blazor
/// <c>Router</c> intercepting a same-page press scrolls without it. This component's half is that
/// the target can take focus at all — <see cref="DetailSection"/> writes <c>tabindex="-1"</c> for
/// it. The one thing that would need script is the highlight following the reader down the page,
/// and it is deliberately not here: the nav is the whole navigational benefit and it ships now.
/// The package does ship a JavaScript module since Fhi.Metadata-35w0p.14, and this nav uses none
/// of it — the scrolling is still the browser's.
/// </para>
/// <para>
/// Each href carries this page's own path and query in front of the <c>#</c>. A bare <c>#id</c> is
/// resolved against the document's <c>&lt;base href&gt;</c> rather than against the page being
/// read, and helsedata's Optimizely host sets that to <c>/</c>, so every link left the page for the
/// site root instead of scrolling. The query goes with the path because a browser treats a fragment
/// jump as same-document only when the path and the query both match: <c>/MuninKelda/#metadata</c>
/// pressed on <c>/MuninKelda/?kilde=…</c> is a fresh load of that page with no kilde open, which
/// loses the reader's place by a quieter route than the site root does.
/// </para>
/// <para>
/// A host mounting a detail view inside <see cref="VariableExplorer"/> or
/// <see cref="KildeExplorer"/> gets that address from the wrapper, which is the only side that
/// knows what it last wrote. Mounted under anything else the links are built from the circuit's own
/// address, so a host owning its own query string has to move it through
/// <see cref="NavigationManager"/>: an address bar moved by <c>history.replaceState</c> alone is
/// one Blazor is never told about, and the links would keep naming the address the reader arrived
/// on.
/// </para>
/// <para>
/// So no <c>form-menu__list__item--active</c> is emitted either. With nothing tracking the scroll
/// position, any item marked active is permanently wrong everywhere else on the page, and a
/// highlight that lies is worse than no highlight.
/// </para>
/// <para>
/// The list wears helsedata's own <c>form-menu__list</c> names rather than anything under this
/// package's prefix: they are global, unscoped classes in <c>Fhi.Helsedata.Stiler</c> and reach its
/// compiled stylesheet, so the nav takes that site's link colour and padding for nothing and this
/// package adds no rule for them. <c>form-menu__list__item</c> carries no rule of its own anywhere
/// — only the active modifiers do, and neither is emitted here.
/// </para>
/// <para>
/// Goes in <see cref="DetailPage.Contents"/>, which draws the column around it. This component
/// emits no wrapper of its own, and nothing when it is given no entries — see
/// <see cref="Column"/> for the reason that matters.
/// </para>
/// </remarks>
public sealed class DetailToc : ComponentBase, IDisposable
{
    /// <summary>
    /// The name <see cref="VariableExplorer"/> and <see cref="KildeExplorer"/> cascade their
    /// mirrored address under, and this component reads it back by.
    /// </summary>
    internal const string PageAddressName = "MuninExplorerPageAddress";

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    // The address bar as the wrapper that owns the query last wrote it. Cascaded rather than read
    // from NavigationManager, whose Uri is the address the circuit STARTED on: UrlMirror writes
    // with history.replaceState, which moves the browser without telling Blazor.
    [CascadingParameter(Name = PageAddressName)]
    private string? PageAddress { get; set; }

    /// <summary>
    /// The sections to link to, in document order. Every one must be a section that rendered: a
    /// link to an <c>id</c> the document does not carry is a control that does nothing.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<DetailTocEntry> Entries { get; set; } = [];

    /// <summary>
    /// The nav's accessible name, in the reader's language — <c>Innhold</c>, or <c>Contents</c>.
    /// </summary>
    /// <remarks>
    /// A landmark among the host page's own, so it is named rather than left for a screen reader to
    /// announce as one navigation of several.
    /// </remarks>
    [Parameter, EditorRequired]
    public string Label { get; set; } = "";

    /// <summary>
    /// This nav as the fragment <see cref="DetailPage.Contents"/> takes, or null when there is
    /// nothing to link to.
    /// </summary>
    /// <remarks>
    /// Null and an empty fragment are different answers there: the chassis draws the contents
    /// column for any fragment at all, so a view with no sections would get an empty rail beside
    /// its content rather than no rail.
    /// </remarks>
    internal static RenderFragment? Column(IReadOnlyList<DetailTocEntry> entries, string label) =>
        entries.Count == 0
            ? null
            : builder =>
            {
                builder.OpenComponent<DetailToc>(0);
                builder.AddComponentParameter(1, nameof(Entries), entries);
                builder.AddComponentParameter(2, nameof(Label), label);
                builder.CloseComponent();
            };

    /// <inheritdoc />
    protected override void OnInitialized() => Navigation.LocationChanged += Moved;

    /// <inheritdoc />
    public void Dispose() => Navigation.LocationChanged -= Moved;

    // A host that owns the query can rewrite it without anything above this component re-rendering,
    // and the hrefs would then keep naming the address the reader arrived on. InvokeAsync because a
    // LocationChanged raised off the renderer's dispatcher throws, which tears the circuit down.
    private void Moved(object? sender, LocationChangedEventArgs e) => _ = InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Entries.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "aria-label", Label);

        builder.OpenElement(2, "h2");
        builder.AddContent(3, Label);
        builder.CloseElement();

        builder.OpenElement(4, "ul");
        builder.AddAttribute(5, "class", "form-menu__list");

        // Rooted-absolute, the shape UrlMirror.Address writes, so the fallback and the cascade agree.
        // It carries the host's path base whatever the document's <base href> says, because a rooted
        // href resolves against that base's ORIGIN and never against its path.
        var page = PageAddress is { Length: > 0 } given ? given : new Uri(Navigation.Uri).PathAndQuery;

        var seq = 10;

        foreach (var entry in Entries)
        {
            builder.OpenElement(seq, "li");
            builder.AddAttribute(seq + 1, "class", "form-menu__list__item");

            builder.OpenElement(seq + 2, "a");
            builder.AddAttribute(seq + 3, "href", $"{page}#{entry.Id}");
            builder.AddContent(seq + 4, entry.Label);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
