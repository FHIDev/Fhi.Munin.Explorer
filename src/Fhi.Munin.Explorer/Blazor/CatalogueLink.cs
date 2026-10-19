using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A link the catalogue supplied. One to another host opens in a new window and says so to a screen
/// reader; the icon is the host's own rule for <c>a[target="_blank"]</c> (Fhi.Metadata-c8lgk, WCAG 3.2.5).
/// </summary>
internal sealed class CatalogueLink : ComponentBase
{
    /// <summary>The cascade that carries the reader's language to links built far from any parameter.</summary>
    internal const string ReaderName = "Fhi.Munin.Explorer:reader";

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [CascadingParameter(Name = ReaderName)] private string? Reader { get; set; }

    [Parameter, EditorRequired] public string Href { get; set; } = "";

    /// <summary>Null leaves the attribute out, for a scheme that is not the web's.</summary>
    [Parameter] public string? Rel { get; set; } = "noopener noreferrer";

    [Parameter] public string? Style { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>An absolute web address on another host than the page; a relative one is the page's own.</summary>
    internal static bool IsExternal(string href, string pageAddress) =>
        Uri.TryCreate(href, UriKind.Absolute, out var target)
        && (target.Scheme == Uri.UriSchemeHttps || target.Scheme == Uri.UriSchemeHttp)
        && Uri.TryCreate(pageAddress, UriKind.Absolute, out var page)
        && !string.Equals(target.Host, page.Host, StringComparison.OrdinalIgnoreCase);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var external = IsExternal(Href, Navigation.Uri);

        builder.OpenElement(0, "a");
        builder.AddAttribute(1, "href", Href);
        builder.AddAttribute(2, "target", external ? "_blank" : null);
        builder.AddAttribute(3, "rel", Rel);
        builder.AddAttribute(4, "style", Style);
        builder.AddContent(5, ChildContent);

        if (external)
        {
            builder.OpenElement(6, "span");
            builder.AddAttribute(7, "class", "screenreader-only");
            builder.AddContent(8, " " + Texts.For(Reader).OpensInNewWindow);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
