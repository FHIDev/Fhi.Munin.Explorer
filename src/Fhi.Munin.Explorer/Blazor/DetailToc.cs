using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The contents nav of a detail view: one link per section the view drew, in the order it drew
/// them.
/// </summary>
/// <remarks>
/// <para>
/// Plain <c>#fragment</c> links and no script at all. The browser does the scrolling, and
/// <c>scroll-margin-top</c> on <see cref="DetailSection"/>'s wrapper is what puts the heading clear
/// of a sticky site header. The one thing that would need script is the highlight following the
/// reader down the page, and it is deliberately not here: the nav is the whole navigational
/// benefit, it ships now, and this package still ships zero JavaScript.
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
public sealed class DetailToc : ComponentBase
{
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
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Entries.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "aria-label", Label);

        builder.OpenElement(2, "ul");
        builder.AddAttribute(3, "class", "form-menu__list");

        var seq = 10;

        foreach (var entry in Entries)
        {
            builder.OpenElement(seq, "li");
            builder.AddAttribute(seq + 1, "class", "form-menu__list__item");

            builder.OpenElement(seq + 2, "a");
            builder.AddAttribute(seq + 3, "href", $"#{entry.Id}");
            builder.AddContent(seq + 4, entry.Label);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
