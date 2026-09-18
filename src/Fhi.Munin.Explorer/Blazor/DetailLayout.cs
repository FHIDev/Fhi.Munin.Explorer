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
/// A view keeps its own fallback order for whatever the placement data does not name, so a section
/// is never dropped for want of a placement row and an API predating the collection draws the page
/// exactly as it drew before (Fhi.Metadata-35w0p.22).
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
    /// The kinds are kept apart on <see cref="SectionPlacement.IsBuiltIn"/> and so is the
    /// bookkeeping: they share one key namespace, so one set across both would take a block out of
    /// its own fallback the moment a curated group of that name is placed.
    /// </remarks>
    internal static IReadOnlyList<DetailLayoutSection> Order(
        IReadOnlyList<SectionPlacement> placements,
        IReadOnlyList<DetailLayoutSection> groups,
        IReadOnlyList<DetailLayoutSection> blocks)
    {
        List<DetailLayoutSection> ordered = [];

        // Two sets rather than one: the first is which rows have been read, the second which
        // sections they actually found, and only the second decides what the tail leaves out. A
        // row sent to the pool the other kind is in therefore falls to the tail rather than away.
        HashSet<(string Key, bool BuiltIn)> addressed = [];
        HashSet<(string Key, bool BuiltIn)> placed = [];

        foreach (var placement in placements)
        {
            if (string.IsNullOrEmpty(placement.Key) || !addressed.Add((placement.Key, placement.IsBuiltIn)))
            {
                continue;
            }

            if (Find(placement.IsBuiltIn ? blocks : groups, placement.Key) is { } section)
            {
                ordered.Add(section);
                placed.Add((placement.Key, placement.IsBuiltIn));
            }
        }

        ordered.AddRange(Unplaced(groups, placed, builtIn: false));
        ordered.AddRange(Unplaced(blocks, placed, builtIn: true));

        return ordered;
    }

    private static DetailLayoutSection? Find(IReadOnlyList<DetailLayoutSection> sections, string key) =>
        sections.FirstOrDefault(section => string.Equals(section.Key, key, StringComparison.Ordinal));

    /// <summary>Whatever no placement row named, in the order its own pool was given in.</summary>
    private static IEnumerable<DetailLayoutSection> Unplaced(
        IReadOnlyList<DetailLayoutSection> sections,
        IReadOnlySet<(string Key, bool BuiltIn)> placed,
        bool builtIn) =>
        sections.Where(section => section.Key is null || !placed.Contains((section.Key, builtIn)));
}
