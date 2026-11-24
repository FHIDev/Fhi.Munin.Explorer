using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Catalogue text drawn through <see cref="CatalogueMarkdown"/>, formatted where the host turns
/// <see cref="Client.MuninExplorerFeatures.CatalogueFormatting"/> on, inside an optional element.
/// </summary>
internal sealed class CatalogueText : ComponentBase
{
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter] public string? Text { get; set; }

    /// <summary>The element around the text, or none; a <c>p</c> or <c>span</c> becomes a <c>div</c> where a list is drawn.</summary>
    [Parameter] public string? Element { get; set; }

    [Parameter] public string? Class { get; set; }

    [Parameter] public string? Lang { get; set; }

    /// <summary>The text as a fragment, for the static builders that draw it.</summary>
    internal static RenderFragment Of(string? text, string? element = null, string? cssClass = null, string? lang = null) =>
        builder =>
        {
            builder.OpenComponent<CatalogueText>(0);
            builder.AddComponentParameter(1, nameof(Text), text);
            builder.AddComponentParameter(2, nameof(Element), element);
            builder.AddComponentParameter(3, nameof(Class), cssClass);
            builder.AddComponentParameter(4, nameof(Lang), lang);
            builder.CloseComponent();
        };

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var formatted = Services.GetService<CatalogueFormattingSwitch>()?.On == true;

        if (Element is null)
        {
            builder.AddContent(0, CatalogueMarkdown.Render(Text, formatted));
            return;
        }

        builder.OpenElement(1, CatalogueMarkdown.DrawsList(Text, formatted) ? "div" : Element);
        builder.AddAttribute(2, "class", Class);
        builder.AddAttribute(3, "lang", Lang);
        builder.AddContent(4, CatalogueMarkdown.Render(Text, formatted));
        builder.CloseElement();
    }
}
