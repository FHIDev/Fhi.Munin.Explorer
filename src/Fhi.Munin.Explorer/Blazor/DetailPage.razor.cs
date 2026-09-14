using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The chassis the detail views share: the root, the name block above the fold, the body grid, the
/// contents column and the main column.
/// </summary>
/// <remarks>
/// <para>
/// Emits the <c>munin-explorer-page</c> names once, for <see cref="KildeView"/>,
/// <see cref="DatasamlingView"/> and <see cref="VariableView"/> alike. Three views drawing one page
/// shape under three prefixes is what made every layout rule in Stiler a three-selector compound,
/// and a fourth surface would have made it four.
/// </para>
/// <para>
/// Each view's own names are parameters rather than a prefix this component completes, because a
/// name assembled at runtime is invisible to the two checks that reconcile what the package emits
/// against the README inventory and the sample stylesheets — both read literals out of
/// <c>src/</c>. Passing <c>munin-explorer-kilde__main</c> whole keeps the name greppable.
/// </para>
/// <para>
/// The body is the one element that sheds its older name instead of wearing both, and so takes no
/// parameter at all: every published Stiler lays the three views' own body names out as a grid of
/// their own, and an element wearing both names would carry two <c>grid-template-columns</c>
/// declarations from two blocks, with source order rather than either stylesheet deciding it.
/// </para>
/// <para>
/// Public only because a Razor component must be, in the way <see cref="DetailSection"/> and
/// <see cref="KildeHierarchyView"/> are: this is the detail views' own furniture and a host has no
/// reason to mount it. It ships no CSS, like everything else in this package.
/// </para>
/// </remarks>
public sealed partial class DetailPage : ComponentBase
{
    /// <summary>
    /// The view's own root class, worn beside <c>munin-explorer-page</c> rather than replaced by
    /// it. Both are on the element for as long as Stiler styles either.
    /// </summary>
    /// <remarks>
    /// One of these is also the drill-in panel's: <c>munin-explorer-kilde__datasamlinger</c> is
    /// styled under <c>munin-explorer-kilder__expanded</c> in an expanded results row, which is a
    /// surface this chassis has nothing to do with.
    /// </remarks>
    [Parameter, EditorRequired]
    public string ViewRoot { get; set; } = "";

    /// <summary>The view's own main-column class, worn beside <c>munin-explorer-page__main</c>.</summary>
    [Parameter, EditorRequired]
    public string ViewMain { get; set; } = "";

    /// <summary>
    /// The name block: the heading, the identifiers under it, and the description. It sits above
    /// the body rather than inside it, so the grid below holds the columns and nothing else.
    /// </summary>
    [Parameter]
    public RenderFragment? Header { get; set; }

    /// <summary>
    /// The contents column, beside the main one. Its element is drawn only when this is set, so a
    /// view that passes nothing draws one column in one track rather than an empty rail beside it.
    /// </summary>
    /// <remarks>
    /// Nothing in the package sets it yet: the contents nav that fills it is <c>Fhi.Metadata-35w0p.12</c>,
    /// and the column it lands in is declared here so that the nav is markup and a stylesheet rule
    /// rather than a new element as well.
    /// </remarks>
    [Parameter]
    public RenderFragment? Contents { get; set; }

    /// <summary>The main column: every section the view draws, in the order it draws them.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string RootClasses => Beside("munin-explorer-page", ViewRoot);

    private string MainClasses => Beside("munin-explorer-page__main", ViewMain);

    /// <summary>
    /// The chassis name and the view's own, with no trailing space when a caller passes none —
    /// the class list is what the exact-name tests tokenise, and an empty name is not a name.
    /// </summary>
    private static string Beside(string chassis, string view) =>
        string.IsNullOrWhiteSpace(view) ? chassis : $"{chassis} {view}";
}
