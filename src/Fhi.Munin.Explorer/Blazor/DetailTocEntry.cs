namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One line of a detail view's contents nav: the section it points at, and the words it points
/// with.
/// </summary>
/// <remarks>
/// <para>
/// The id is the section's own, a fixed English literal that is the same in every language, and
/// the label is that section's heading, in the reader's. Never the other way round: the link is
/// what one reader sends another, so it has to land in the same place whichever language either of
/// them is reading.
/// </para>
/// <para>
/// An entry exists only for a section that actually rendered. The view builds the list and its
/// sections off one predicate each — <see cref="DetailTocBuilder"/> is where that happens — so a
/// link here cannot point at an anchor the view left out.
/// </para>
/// <para>
/// It can still point at another view's anchor, and only one thing stops it: mounting one detail
/// view per document. The ids are global by design, so two views in one document carry each id
/// twice and the browser takes the first. <see cref="DetailSectionIds"/> has the reasoning.
/// </para>
/// </remarks>
/// <param name="Id">The <c>id</c> of the section to scroll to, written without the <c>#</c>.</param>
/// <param name="Label">The section's heading, in the reader's language.</param>
public sealed record DetailTocEntry(string Id, string Label);
