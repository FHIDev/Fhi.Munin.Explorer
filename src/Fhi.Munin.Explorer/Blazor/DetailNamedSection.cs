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
/// <see cref="Id"/> joins the fixed ids the view writes itself — <c>metadata</c>, <c>source</c>,
/// <c>statistics</c> and the rest — so it must be none of those, and the same in every language.
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
