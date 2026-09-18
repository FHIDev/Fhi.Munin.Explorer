namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One line of a detail view's contents nav: the section it points at, and the words it points
/// with.
/// </summary>
/// <remarks>
/// <para>
/// The id is the section's own, a literal or a catalogue key and the same in every language, and
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
/// <para>
/// <see cref="Language"/> is the heading's own <c>lang</c> where the section is headed in the
/// catalogue's word rather than the reader's — a placed property section on an English page, whose
/// group the curator has translated into no English. The link's text is that heading byte for
/// byte, so it has to be marked wherever the heading is: unmarked, a screen reader announces the
/// same words in the heading's voice and in English phonetics in the nav (WCAG 3.1.2).
/// </para>
/// </remarks>
/// <param name="Id">The <c>id</c> of the section to scroll to, written without the <c>#</c>.</param>
/// <param name="Label">The section's heading, in the reader's language.</param>
/// <param name="Language">A <c>lang</c> for the label where it is foreign to the reader; null where it is not.</param>
public sealed record DetailTocEntry(string Id, string Label, string? Language = null);
