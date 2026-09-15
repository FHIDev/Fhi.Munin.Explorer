using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The chassis the detail views share: the root, the page chrome, the name block above the fold,
/// the hero fact row under it, the body grid, the contents column and the main column. Four
/// surfaces wear it.
/// </summary>
/// <remarks>
/// <para>
/// Emits the <c>munin-explorer-page</c> names once, for <see cref="KildeView"/>,
/// <see cref="DatasamlingView"/>, <see cref="VariableView"/> and <see cref="VariableListView"/>
/// alike. Three views drawing one page shape under three prefixes is what made every layout rule in
/// Stiler a three-selector compound, and the fourth surface would have made it four.
/// </para>
/// <para>
/// The chrome — <see cref="Trail"/>, <see cref="Eyebrow"/>, <see cref="Actions"/> — is what makes
/// a detail page read as part of helsedata rather than as a component dropped into one. All three
/// draw only when a caller fills them, and the trail's targets are the caller's alone: nothing in
/// this package knows a URL.
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
    /// surface this chassis has nothing to do with. Empty where a view has no older name at all,
    /// which <see cref="VariableListView"/> is: the chassis name then stands alone.
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
    /// The facts the page leads with, drawn as a row between the name block and the body. Empty or
    /// unset draws no row at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six is the shape: Stiler lays this row out as six equal tracks at desktop, three below
    /// 1080px and two below 600px, so five leaves a hole and seven wraps to a row of one. A record
    /// the catalogue has not filled in draws fewer — <see cref="DetailFacts"/> drops a fact with no
    /// value rather than drawing it empty.
    /// </para>
    /// <para>
    /// A summary rather than a relocation. Every fact here is still drawn in the section it belongs
    /// to further down, so a reader who scrolls to the access rule finds it where it always was.
    /// </para>
    /// </remarks>
    [Parameter]
    public IReadOnlyList<DetailFact>? Facts { get; set; }

    /// <summary>
    /// The eyebrow: what kind of thing this page is about — <c>Datakilde</c>, <c>Datasamling</c>,
    /// <c>Variabel</c>. Drawn as a <c>&lt;p&gt;</c>, and nothing at all when it is empty.
    /// </summary>
    /// <remarks>
    /// Never a heading, and not by accident. The page's outline is how a screen reader user moves
    /// through it, and a word above the title rendered as an <c>h*</c> becomes a second title in
    /// that outline — one that names a category rather than the thing on screen.
    /// </remarks>
    [Parameter]
    public string? Eyebrow { get; set; }

    /// <summary>
    /// Where this page sits, outermost step first, the page itself last. Empty or unset draws no
    /// trail at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every step's target is the caller's to supply, because this package has none to give: there
    /// is no router here and helsedata's addresses are not ours. A step whose
    /// <see cref="DetailTrailStep.Href"/> is null is drawn as plain text rather than as a link that
    /// goes nowhere, and a caller with no targets at all passes nothing and gets no trail.
    /// </para>
    /// <para>
    /// The last step is the page the reader is on: <see cref="DetailTrail"/> marks it
    /// <c>aria-current="page"</c> and never draws it as a link, whatever it carries.
    /// </para>
    /// </remarks>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <summary>
    /// The trail's accessible name, in the reader's language. The host page has a breadcrumb of its
    /// own above the mount point, so this landmark is named rather than left anonymous beside it.
    /// </summary>
    [Parameter]
    public string? TrailLabel { get; set; }

    /// <summary>
    /// What the reader can do from this page, gathered into one row above the name block. The row's
    /// element is drawn only when this is set, so a page with no page-level action has no row.
    /// </summary>
    /// <remarks>
    /// For actions that act on the page's own subject. A control that acts on the surface the page
    /// opened inside — the way out of a drill-in, say — belongs to that surface and stays there: an
    /// action row repeating a control still drawn elsewhere is worse than no action row.
    /// </remarks>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>
    /// The contents column, beside the main one. Its element is drawn only when this is set, so a
    /// view that passes nothing draws one column in one track rather than an empty rail beside it.
    /// </summary>
    /// <remarks>
    /// <see cref="DetailToc"/> is what the three drill-in views put here, and they pass null rather
    /// than an empty fragment when they drew no section to link to — an empty one would still draw
    /// the rail. <see cref="VariableListView"/> passes null always: its Kilde filter does the
    /// grouping a contents nav would, so it has no rail at any width.
    /// </remarks>
    [Parameter]
    public RenderFragment? Contents { get; set; }

    /// <summary>The main column: every section the view draws, in the order it draws them.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Anything else the caller writes on this component, splatted onto the root element.
    /// </summary>
    /// <remarks>
    /// <c>data-munin-explorer-version</c> is what this is for: a view mounted on its own is a mount
    /// point a host reads the package version off, and the root is the element it is read from.
    /// A <c>class</c> written here does <em>not</em> displace the root's class list: the splat is
    /// written before <c>class</c> on the element, so <c>munin-explorer-page</c> and
    /// <see cref="ViewRoot"/> stay on the root and a caller's own name is dropped. Pass a view's
    /// own root name through <see cref="ViewRoot"/>, which is the parameter for it.
    /// </remarks>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string RootClasses => Beside("munin-explorer-page", ViewRoot);

    private string MainClasses => Beside("munin-explorer-page__main", ViewMain);

    /// <summary>
    /// The chassis name and the view's own, with no trailing space when a caller passes none —
    /// the class list is what the exact-name tests tokenise, and an empty name is not a name.
    /// </summary>
    private static string Beside(string chassis, string view) =>
        string.IsNullOrWhiteSpace(view) ? chassis : $"{chassis} {view}";
}
