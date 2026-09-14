using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One block of a detail view, wrapped so a contents nav can anchor on it.
/// </summary>
/// <remarks>
/// <para>
/// Emits <c>&lt;section id="…" data-nav-section class="munin-explorer-page__section"&gt;</c> around
/// whatever it is given. The class and helsedata's <c>data-nav-section</c> attribute are written
/// once here rather than at each of the fifteen blocks <see cref="KildeView"/>,
/// <see cref="DatasamlingView"/> and <see cref="VariableView"/> draw between them, so a rename
/// cannot reach two views and miss the third.
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
    /// The element id, which a reader's deep link ends in. A fixed English literal, never derived
    /// from the heading: the headings are bilingual, so a derived id would differ between nb and en.
    /// </summary>
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
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}
