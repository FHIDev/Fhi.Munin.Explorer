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
    /// The sections the placement rows name, built from the curated groups, and the groups no row
    /// names, for the block a view gathers those under.
    /// </summary>
    /// <param name="placements">The page's sections as the API ordered them; empty on an older API.</param>
    /// <param name="groups">Every curated group this payload has, in the order the catalogue gathered them.</param>
    /// <param name="language">The reader's language, which titles the groups and marks the foreign ones.</param>
    /// <param name="completeRecord">The catch-all group's extras, or null on a surface that has none.</param>
    /// <param name="body">
    /// What goes under a section's heading, given the group and its own rows, where the view has
    /// something to add to them. Null draws the group's rows alone.
    /// </param>
    /// <remarks>
    /// Both detail views want exactly this, down to the reservation set seeded with the ids they
    /// write themselves. Written twice, the next change to how a placed group becomes a section
    /// reaches one of them and misses the other (Fhi.Metadata-lr6yh).
    /// </remarks>
    internal static (IReadOnlyList<DetailLayoutSection> Sections, IReadOnlyList<PropertyGroup> Ungrouped) Split(
        IReadOnlyList<SectionPlacement> placements,
        IReadOnlyList<PropertyGroup> groups,
        string? language,
        CompleteRecordExtras? completeRecord = null,
        Func<PropertyGroup, RenderFragment, RenderFragment>? body = null)
    {
        var reader = ReaderLanguage.Of(language);

        HashSet<string> named = new(
            placements.Select(placement => placement.Key).Where(key => !string.IsNullOrEmpty(key)),
            StringComparer.Ordinal);

        HashSet<string> ids = new(DetailSectionIds.Fixed, StringComparer.Ordinal);

        List<DetailLayoutSection> sections = [];
        List<PropertyGroup> ungrouped = [];

        foreach (var group in groups)
        {
            if (group.Key is not { } key || !named.Contains(key))
            {
                ungrouped.Add(group);
                continue;
            }

            var rows = DetailBlocks.GroupBody(group, language, completeRecord);

            sections.Add(new(key, DetailSectionIds.ReserveGroupId(key, ids), group.Name,
                             CatalogueProperties.Foreign(group.NameLanguage, reader),
                             body is null ? rows : body(group, rows)));
        }

        return (sections, ungrouped);
    }

    /// <summary>Whether one of these sections is the one under this key, and so whether it draws.</summary>
    /// <remarks>
    /// Asked of what <see cref="Order"/> is handed rather than of the loop that built it: Order
    /// rearranges its input and drops none of it, so a key here is a key on the page. A fact box
    /// yielding into a section nobody drew is a public field on no surface (Fhi.Metadata-lr6yh).
    /// </remarks>
    internal static bool Draws(IReadOnlyList<DetailLayoutSection> sections, string? key) =>
        key is not null
        && sections.Any(section => string.Equals(section.Key, key, StringComparison.Ordinal));

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
