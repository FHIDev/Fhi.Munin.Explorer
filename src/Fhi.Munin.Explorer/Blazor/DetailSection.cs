using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One block of a detail view, wrapped so a contents nav can anchor on it.
/// </summary>
/// <remarks>
/// <para>
/// Emits <c>&lt;section id="…" data-nav-section class="munin-explorer-page__section" tabindex="-1"&gt;</c>
/// around whatever it is given. The class, helsedata's <c>data-nav-section</c> attribute and the
/// negative <c>tabindex</c> that lets a fragment jump land focus here are written once rather than
/// at every block <see cref="KildeView"/>, <see cref="DatasamlingView"/>,
/// <see cref="VariableView"/> and <see cref="InstrumentView"/> draw between them, so a rename
/// cannot reach some of the views and miss the rest. No count: the views are added to, and a
/// number here is a claim about other files that nothing would fail on.
/// </para>
/// <para>
/// Public only because a Razor component must be, in the way <see cref="KildeHierarchyView"/> and
/// <see cref="VariableListFilters"/> are: this is the detail views' own furniture and a host has no
/// reason to mount it. It draws nothing of its own — no padding, no border, no margin — so an
/// empty one is invisible rather than a blank box.
/// </para>
/// </remarks>
public sealed class DetailSection : ComponentBase
{
    /// <summary>
    /// The element id, which a reader's deep link ends in. A fixed English literal under the
    /// <c>munin-explorer-</c> prefix, or one derived from the catalogue's own section key under
    /// <c>munin-explorer-section-</c> — never from the heading, which is bilingual and would resolve
    /// for a reader in one language and nobody in the other. A key stripped of what a fragment
    /// cannot carry, and numbered where two strip alike.
    /// </summary>
    /// <remarks>
    /// The prefix keeps it clear of the host page's own ids. Nothing per-instance goes in it, so
    /// the id is the same for every reader and a page mounts one detail view. Two of them write these
    /// ids twice, and the second view's contents nav then scrolls the reader into the first view's
    /// sections, because a browser resolves a fragment to the first element that matches.
    /// </remarks>
    [Parameter, EditorRequired]
    public string Id { get; set; } = "";

    /// <summary>
    /// The heading and everything under it. Render the section only when this has content — a
    /// section holding a heading and nothing else is the empty block the views suppress.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.AddAttribute(1, "id", Id);
        builder.AddAttribute(2, "data-nav-section", true);
        builder.AddAttribute(3, "class", "munin-explorer-page__section");

        // A fragment jump moves focus only to a focusable target, so without this the reader is
        // scrolled to the section and their next Tab carries on from the nav they just left —
        // WCAG 2.4.3. Negative, so the section itself never enters the tab order.
        builder.AddAttribute(4, "tabindex", "-1");
        builder.AddContent(5, ChildContent);
        builder.CloseElement();
    }
}
