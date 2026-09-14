using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A detail page's breadcrumb: where the thing on screen sits, one step per level, the page itself
/// last.
/// </summary>
/// <remarks>
/// <para>
/// Wears helsedata's own <c>breadcrumbs</c> names rather than anything under this package's prefix,
/// on the same terms as <see cref="DetailToc"/>'s <c>form-menu__list</c>: they are global, unscoped
/// classes in <c>Fhi.Helsedata.Stiler</c>, so the trail takes that site's type, colour and dividers
/// for nothing and this package invents no name to have a rule written for.
/// </para>
/// <para>
/// The list is an <c>&lt;ol&gt;</c> because the steps are ordered — the WAI-ARIA pattern, and the
/// one thing a breadcrumb tells a screen reader that a row of links does not. A host whose rule for
/// <c>breadcrumbs__list</c> is written against a <c>&lt;ul&gt;</c> element rather than the class
/// draws a numbered list, which still reads correctly.
/// </para>
/// <para>
/// A step with no <c>Href</c> is plain text. This package owns no addresses, so the surface above
/// supplies every target and may have none to give; a step drawn as a link that goes nowhere is
/// worse than a step drawn as words. The last step is the current page and is plain text always.
/// </para>
/// </remarks>
public sealed class DetailTrail : ComponentBase
{
    /// <summary>
    /// The steps, outermost first, the current page last. Nothing renders for an empty list: one
    /// step is the page itself and says nothing a reader could act on.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<DetailTrailStep> Steps { get; set; } = [];

    /// <summary>
    /// The nav's accessible name, in the reader's language — <c>Brødsmulesti</c>, or
    /// <c>Breadcrumb</c>.
    /// </summary>
    /// <remarks>
    /// A landmark among the host page's own, and the host page has a breadcrumb of its own above
    /// the mount point on every helsedata article. Two anonymous navigations are two navigations;
    /// a named one is the one the reader was looking for.
    /// </remarks>
    [Parameter, EditorRequired]
    public string Label { get; set; } = "";

    /// <summary>
    /// <paramref name="above"/> with the page's own name appended as the last step, or null when
    /// the caller supplied no steps.
    /// </summary>
    /// <remarks>
    /// The three detail views' shared rule in one place, so a change to it cannot half-apply. Null
    /// rather than a one-step list, because a trail whose only step is the page names nowhere the
    /// reader could go; and the last step is built with no target, which this class drops anyway.
    /// </remarks>
    /// <param name="above">The steps the surface above supplied, outermost first.</param>
    /// <param name="named">What the page shows for its subject, and whether that is still Norwegian.</param>
    /// <param name="reader">The reader's language, against which the label is marked or left alone.</param>
    internal static IReadOnlyList<DetailTrailStep>? Append(
        IReadOnlyList<DetailTrailStep>? above, (string Text, bool Norwegian) named, string reader)
    {
        if (above is not { Count: > 0 } steps)
        {
            return null;
        }

        return [.. steps, new DetailTrailStep(named.Text, null, CatalogueProperties.Foreign(named.Norwegian, reader))];
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Steps.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "class", "breadcrumbs");
        // Left off rather than written empty when a caller passed no name: an `aria-label=""`
        // is a landmark claiming a name it does not have.
        builder.AddAttribute(2, "aria-label", string.IsNullOrWhiteSpace(Label) ? null : Label);

        builder.OpenElement(3, "ol");
        builder.AddAttribute(4, "class", "breadcrumbs__list");

        var seq = 10;

        for (var index = 0; index < Steps.Count; index++)
        {
            var step = Steps[index];
            var current = index == Steps.Count - 1;

            builder.OpenElement(seq, "li");
            builder.AddAttribute(seq + 1, "class",
                current ? "breadcrumbs__list-item breadcrumbs__last-crumb" : "breadcrumbs__list-item");

            // Null on every step but the last, so the attribute is left out rather than spelled
            // "false" — the treatment the hierarchy trail's own aria-current gets.
            builder.AddAttribute(seq + 2, "aria-current", current ? "page" : null);

            if (index > 0)
            {
                // Empty and hidden: what goes in it is the host stylesheet's, and a separator a
                // screen reader reads aloud is punctuation between two names it already parted.
                builder.OpenElement(seq + 3, "span");
                builder.AddAttribute(seq + 4, "class", "breadcrumbs__divider");
                builder.AddAttribute(seq + 5, "aria-hidden", "true");
                builder.CloseElement();
            }

            if (!current && step.Href is { } href)
            {
                builder.OpenElement(seq + 6, "a");
                builder.AddAttribute(seq + 7, "href", href);
                Words(builder, seq + 8, step);
                builder.CloseElement();
            }
            else
            {
                Words(builder, seq + 8, step);
            }

            builder.CloseElement();
            seq += 20;
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// The step's label, wrapped in a <c>lang</c> span when the catalogue's language is not the
    /// reader's.
    /// </summary>
    /// <remarks>
    /// The span goes around the words and never on the <c>&lt;a&gt;</c>: an accessible name is
    /// announced in the computed language of the element that owns it, and a langed link would
    /// switch voice for the whole control rather than for the name inside it.
    /// </remarks>
    private static void Words(RenderTreeBuilder builder, int seq, DetailTrailStep step)
    {
        if (step.Lang is null)
        {
            builder.AddContent(seq, step.Label);
            return;
        }

        builder.OpenElement(seq + 1, "span");
        builder.AddAttribute(seq + 2, "lang", step.Lang);
        builder.AddContent(seq + 3, step.Label);
        builder.CloseElement();
    }
}
