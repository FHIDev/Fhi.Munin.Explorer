using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A section handed to a detail view to draw among its own, and to list in its contents nav.
/// </summary>
/// <remarks>
/// <para>
/// The view draws the <c>&lt;section&gt;</c>, the heading and the nav entry, all off this one
/// value, so an entry cannot outlive its section or a section go unlisted. Pass a section only when
/// it has something to show: the view has no way to tell an empty <see cref="Body"/> from a full one.
/// </para>
/// <para>
/// Give <see cref="Id"/> one no other element on the page has, and none the views write:
/// <c>metadata</c>, <c>criteria</c>, <c>source</c>, <c>statistics</c>, <c>datacollections</c>,
/// <c>versions</c>, <c>dataperiod</c>, <c>datatype</c>, <c>variablegroups</c>, <c>instruments</c>,
/// <c>validity</c> — and nothing starting
/// <c>section-</c>, which the detail views finish with the catalogue's own section keys, so that set
/// is Munin's to extend rather than a list this package can close. A repeated id puts two elements
/// on the page under it, and the nav's second link lands on the first. Keep it the same in every
/// language, since a reader shares the link.
/// </para>
/// </remarks>
/// <param name="Id">The section's <c>id</c>, which the nav links to, written without the <c>#</c>.</param>
/// <param name="Heading">The section's heading and nav label, in the reader's language.</param>
/// <param name="Body">Everything under the heading.</param>
public sealed record DetailNamedSection(string Id, string Heading, RenderFragment Body)
{
    /// <summary>The line the contents nav draws for this section.</summary>
    internal DetailTocEntry Entry => new(Id, Heading);
}
