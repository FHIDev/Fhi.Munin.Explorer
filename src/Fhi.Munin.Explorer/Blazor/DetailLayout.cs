using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One section a detail page draws: where it is anchored, what it is headed with, and what is
/// under it.
/// </summary>
/// <param name="Key">
/// What a placement row addresses it by — a catalogue group key, or one of
/// <see cref="SectionKeys"/>. Null for a section the placement data cannot name, which is drawn
/// after the ones it can.
/// </param>
/// <param name="Id">The <c>id</c> a reader's deep link ends in — see <see cref="DetailSectionIds"/>.</param>
/// <param name="Heading">The section's heading and nav label, in the reader's language.</param>
/// <param name="HeadingLanguage">A <c>lang</c> where the heading is the catalogue's word rather than the reader's.</param>
/// <param name="Body">Everything under the heading. Built only for a section that has something to draw.</param>
internal sealed record DetailLayoutSection(
    string? Key,
    string Id,
    string Heading,
    string? HeadingLanguage,
    RenderFragment Body);

/// <summary>
/// A detail page's sections in the order the catalogue puts them, both kinds in one pass.
/// </summary>
/// <remarks>
/// The alternative — property groups as one block, then a hand-ordered run of the view's own —
/// cannot draw the mockups at all: the kilde page's datasamlinger sit between two property
/// sections, and every later reordering would be a release of this package rather than an edit a
/// curator makes (Fhi.Metadata-35w0p.22).
/// <para>
/// A view keeps its own fallback order for whatever the placement data does not name, so a section
/// is never dropped for want of a placement row and an API predating the collection draws the page
/// exactly as it drew before.
/// </para>
/// </remarks>
internal static class DetailLayout
{
    /// <summary>
    /// The sections to draw, placed ones first in the catalogue's order and the rest after.
    /// </summary>
    /// <param name="placements">The page's sections as the API ordered them; empty on an older API.</param>
    /// <param name="groups">Sections built from curated properties, in the order the catalogue gathered them.</param>
    /// <param name="blocks">The view's own sections, in the order the view declares them.</param>
    /// <remarks>
    /// A placement naming neither draws nothing: a section whose every property is empty on this
    /// payload has no rows, and a built-in one this view does not draw is another page's.
    /// <para>
    /// The two pools are kept apart on <see cref="SectionPlacement.IsBuiltIn"/> rather than merged
    /// and matched by key alone. The kinds share one namespace, so a lookup across both would let a
    /// curated group named <c>statistikk</c> swallow this package's block of that name — and the
    /// block, having no row of its own in the payload, would then be drawn nowhere.
    /// </para>
    /// <para>
    /// A section counts as placed only once it has been found, so a row whose flag sends it to the
    /// pool the other kind is in falls to the unplaced tail rather than off the page: the split
    /// above is what keeps two kinds apart, and this is what keeps a wrong flag from deleting a
    /// section and every field in it.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<DetailLayoutSection> Order(
        IReadOnlyList<SectionPlacement> placements,
        IReadOnlyList<DetailLayoutSection> groups,
        IReadOnlyList<DetailLayoutSection> blocks)
    {
        List<DetailLayoutSection> ordered = [];

        // Two sets rather than one: the first is which rows have been read, the second which
        // sections they actually found, and only the second decides what the tail leaves out.
        HashSet<string> addressed = new(StringComparer.Ordinal);
        HashSet<string> placed = new(StringComparer.Ordinal);

        foreach (var placement in placements)
        {
            if (string.IsNullOrEmpty(placement.Key) || !addressed.Add(placement.Key))
            {
                continue;
            }

            if (Find(placement.IsBuiltIn ? blocks : groups, placement.Key) is { } section)
            {
                ordered.Add(section);
                placed.Add(placement.Key);
            }
        }

        ordered.AddRange(Unplaced(groups, placed));
        ordered.AddRange(Unplaced(blocks, placed));

        return ordered;
    }

    private static DetailLayoutSection? Find(IReadOnlyList<DetailLayoutSection> sections, string key) =>
        sections.FirstOrDefault(section => string.Equals(section.Key, key, StringComparison.Ordinal));

    /// <summary>Whatever no placement row named, in the order its own pool was given in.</summary>
    private static IEnumerable<DetailLayoutSection> Unplaced(
        IReadOnlyList<DetailLayoutSection> sections, IReadOnlySet<string> placed) =>
        sections.Where(section => section.Key is null || !placed.Contains(section.Key));
}
