using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The four levels of the catalogue hierarchy, outermost first.</summary>
/// <remarks>
/// Declaration order is the trail's order and the order the members are compared in, so they are
/// not free to be reordered. Kildetype is a facet of its own rather than a step towards a kilde.
/// </remarks>
internal enum HierarchyLevel
{
    Kilde,
    Delkilde,
    Datasamling,
    Variabelgruppe
}

/// <summary>One node of the catalogue hierarchy, and whatever hangs under it.</summary>
/// <remarks>
/// <c>Path</c> is where the node is drawn and <c>Id</c> what ticking it selects, since one group
/// hangs under every datasamling its variables are in; <c>Offered</c> false nests without offering.
/// </remarks>
internal sealed record HierarchyNode(
    string Path,
    HierarchyLevel Level,
    Guid Id,
    string Name,
    string? ShortName,
    int Count,
    IReadOnlyList<HierarchyNode> Children,
    bool Offered = true);

/// <summary>The delkilder and datasamlinger of the kilde facet, keyed by what each hangs under.</summary>
/// <remarks>
/// Two datasamling lookups rather than one keyed by parent: the id spaces are independent Guids off
/// the wire, so a kilde id equal to a delkilde id would misfile the row with no error anywhere.
/// </remarks>
internal sealed record KildeLevelLookup(
    ILookup<Guid, DelkildeFacet> Delkilder,
    ILookup<Guid, DatasamlingFacet> DatasamlingerByKilde,
    ILookup<Guid, DatasamlingFacet> DatasamlingerByDelkilde);

/// <summary>The filter panel's hierarchy, read off one <c>/api/explorer/filters</c> answer.</summary>
/// <remarks>
/// Pure by design: no state, no request, no selection. Counts are the answer's own — the API
/// cross-filters every facet, so a summed count would promise a number a tick does not produce.
/// </remarks>
internal static class FilterHierarchy
{
    /// <summary>The whole kilde → delkilde → datasamling → variabelgruppe tree the panel narrows by.</summary>
    /// <remarks>
    /// Built from <see cref="FilterOptions.HierarchyVariabelgrupper"/>, which carries every group
    /// whatever its <see cref="VariabelgruppeFacet.Filter"/> says and is empty against an older API.
    /// </remarks>
    internal static IReadOnlyList<HierarchyNode> Build(FilterOptions facets)
    {
        var levels = KildeLevels(facets);
        var placements = Placements(facets);

        return
        [
            .. facets.Kilder.DistinctBy(kilde => kilde.Id).Select(kilde => Kilde(kilde, levels, placements))
        ];
    }

    /// <summary>The standalone variabelgruppe facet's own list — the flat checkbox surface, nested.</summary>
    /// <remarks>
    /// It reads no selection and must never be given one: a group opted out of this facet stays a
    /// container while ticked, so its chip and the tree remain the way off it. (Fhi.Metadata-fbe3w)
    /// </remarks>
    internal static IReadOnlyList<HierarchyNode> StandaloneVariabelgrupper(FilterOptions facets) =>
        Nest(OnePerId(facets.Variabelgrupper,
                      variabelgruppe => variabelgruppe.Id,
                      variabelgruppe => variabelgruppe.ParentId),
             HierarchyLevel.Variabelgruppe,
             parentPath: "",
             variabelgruppe => variabelgruppe.Id,
             variabelgruppe => variabelgruppe.ParentId,
             (variabelgruppe, path, nested) => new HierarchyNode(
                 path, HierarchyLevel.Variabelgruppe, variabelgruppe.Id, variabelgruppe.Name, null,
                 variabelgruppe.Count, nested, variabelgruppe.IsStandaloneFacetOption));

    /// <summary>Both child levels of the kilde tree, in one pass over the facets.</summary>
    internal static KildeLevelLookup KildeLevels(FilterOptions facets)
    {
        // One entry per id before either level is keyed: copies of one id can name different
        // parents — a delkilde two kilder, a datasamling two delkilder — so they land in different
        // buckets, which no per-bucket collapse can see, and the id is drawn twice. (Fhi.Metadata-l9l2n.82)
        var listedDelkilder = OnePerId(
            facets.Delkilder, delkilde => delkilde.Id, delkilde => delkilde.ParentDelkildeId);
        var listedDatasamlinger = facets.Datasamlinger.DistinctBy(datasamling => datasamling.Id).ToList();

        var delkilder = ById(listedDelkilder, delkilde => delkilde.Id);

        // A delkilde the payload left out — cross-filtered away, or belonging to another kilde — is
        // an absent parent, so its datasamlinger fall back to the kilde rather than disappearing
        // with it.
        bool HangsUnderItsDelkilde(DatasamlingFacet datasamling) =>
            datasamling.DelkildeId is { } parent
            && delkilder.TryGetValue(parent, out var delkilde)
            && delkilde.KildeId == datasamling.KildeId;

        return new KildeLevelLookup(
            listedDelkilder.ToLookup(delkilde => delkilde.KildeId),
            listedDatasamlinger
                .Where(datasamling => !HangsUnderItsDelkilde(datasamling))
                .ToLookup(datasamling => datasamling.KildeId),
            listedDatasamlinger
                .Where(HangsUnderItsDelkilde)
                .ToLookup(datasamling => datasamling.DelkildeId!.Value));
    }

    /// <summary>One entry per id, the copy hanging off a parent that is present winning.</summary>
    /// <remarks>
    /// Two entries with one id can differ in parent and in name alike, so keeping the first listed
    /// would nest, and label, by payload order. (Fhi.Metadata-l9l2n.82)
    /// </remarks>
    internal static IReadOnlyList<T> OnePerId<T>(IEnumerable<T> entries, Func<T, Guid> id, Func<T, Guid?> parentId)
    {
        var listed = entries.ToList();
        var known = listed.Select(id).ToHashSet();

        return [.. listed.GroupBy(id).Select(copies => copies.FirstOrDefault(Parented) ?? copies.First())];

        bool Parented(T entry) => parentId(entry) is { } parent && known.Contains(parent);
    }

    /// <summary>The entries keyed by id, the first listed copy of a repeated one winning.</summary>
    /// <remarks>
    /// GroupBy rather than ToDictionary: a repeated id is malformed, but throwing on the render path
    /// tears the circuit down over what would otherwise be one oddly drawn row.
    /// </remarks>
    private static Dictionary<Guid, T> ById<T>(IEnumerable<T> entries, Func<T, Guid> id) =>
        entries.GroupBy(id).ToDictionary(group => group.Key, group => group.First());

    /// <summary>One kilde, with the two levels under it and whatever groups hang off the kilde itself.</summary>
    /// <remarks>
    /// Datasamlinger before delkilder, the order the panel's kilde facet already draws them in, and
    /// the groups last: a group no datasamling of the kilde holds is an appendix to the structure.
    /// </remarks>
    private static HierarchyNode Kilde(KildeFacet kilde, KildeLevelLookup levels, VariabelgruppePlacements placements)
    {
        var path = NodePath("", HierarchyLevel.Kilde, kilde.Id);

        return new(path, HierarchyLevel.Kilde, kilde.Id, kilde.Name, kilde.ShortName, kilde.Count,
        [
            .. Datasamlinger(levels.DatasamlingerByKilde[kilde.Id], path, placements),
            .. Delkilder(levels.Delkilder[kilde.Id], path, levels, placements),
            .. Variabelgrupper(placements.ByKilde[kilde.Id], path)
        ]);
    }

    /// <summary>A kilde's delkilder, each nested under the delkilde its own facet names.</summary>
    /// <remarks>
    /// Takes the buckets as they come: <see cref="KildeLevels"/> settles repeated ids across all of
    /// them at once, which is the only place it can be done since copies can disagree about a parent.
    /// </remarks>
    private static IReadOnlyList<HierarchyNode> Delkilder(
        IEnumerable<DelkildeFacet> delkilder,
        string parentPath,
        KildeLevelLookup levels,
        VariabelgruppePlacements placements) =>
        Nest([.. delkilder],
             HierarchyLevel.Delkilde,
             parentPath,
             delkilde => delkilde.Id,
             delkilde => delkilde.ParentDelkildeId,
             (delkilde, path, nested) => new HierarchyNode(
                 path, HierarchyLevel.Delkilde, delkilde.Id, delkilde.Name, null, delkilde.Count,
                 [
                     .. Datasamlinger(levels.DatasamlingerByDelkilde[delkilde.Id], path, placements),
                     .. nested,
                     .. Variabelgrupper(placements.ByDelkilde[delkilde.Id], path)
                 ]));

    /// <summary>Datasamlinger as branches: what hangs under one is the groups placed in it.</summary>
    /// <remarks>Takes the buckets as they come, for the reason <see cref="Delkilder"/> gives.</remarks>
    private static IReadOnlyList<HierarchyNode> Datasamlinger(
        IEnumerable<DatasamlingFacet> datasamlinger, string parentPath, VariabelgruppePlacements placements) =>
    [
        .. datasamlinger.Select(datasamling =>
        {
            var path = NodePath(parentPath, HierarchyLevel.Datasamling, datasamling.Id);

            return new HierarchyNode(
                path, HierarchyLevel.Datasamling, datasamling.Id, datasamling.Name, null, datasamling.Count,
                Variabelgrupper(placements.ByDatasamling[datasamling.Id], path));
        })
    ];

    /// <summary>The groups placed at one owner, nested among themselves.</summary>
    /// <remarks>
    /// <see cref="VariabelgruppeFacet.ParentId"/> is another group and never the catalogue owner: a
    /// group whose parent is placed elsewhere stands as a root here rather than following it out.
    /// </remarks>
    private static IReadOnlyList<HierarchyNode> Variabelgrupper(
        IEnumerable<VariabelgruppeFacet> placed, string parentPath) =>
        Nest(OnePerId(placed, variabelgruppe => variabelgruppe.Id, variabelgruppe => variabelgruppe.ParentId),
             HierarchyLevel.Variabelgruppe,
             parentPath,
             variabelgruppe => variabelgruppe.Id,
             variabelgruppe => variabelgruppe.ParentId,
             (variabelgruppe, path, nested) => new HierarchyNode(
                 path, HierarchyLevel.Variabelgruppe, variabelgruppe.Id, variabelgruppe.Name, null,
                 variabelgruppe.Count, nested));

    /// <summary>Every variabelgruppe under the ids its own owners name, one list per level.</summary>
    /// <remarks>
    /// Three lookups rather than one keyed by owner, for the reason <see cref="KildeLevelLookup"/>
    /// gives: the id spaces are independent Guids and one collection would file by a collision.
    /// </remarks>
    private sealed record VariabelgruppePlacements(
        ILookup<Guid, VariabelgruppeFacet> ByKilde,
        ILookup<Guid, VariabelgruppeFacet> ByDelkilde,
        ILookup<Guid, VariabelgruppeFacet> ByDatasamling);

    private static VariabelgruppePlacements Placements(FilterOptions facets)
    {
        // Every group the answer carries, whatever its Filter says. The opt-out is the standalone
        // facet's rule alone, so reading it here would take groups off a tree that has to draw
        // them. (Fhi.Metadata-fbe3w)
        var variabelgrupper = OnePerId(facets.HierarchyVariabelgrupper,
                                       variabelgruppe => variabelgruppe.Id,
                                       variabelgruppe => variabelgruppe.ParentId);

        var datasamlinger = ById(facets.Datasamlinger, datasamling => datasamling.Id);
        var delkilder = ById(facets.Delkilder, delkilde => delkilde.Id);

        List<(Guid Owner, VariabelgruppeFacet Variabelgruppe)> byKilde = [];
        List<(Guid Owner, VariabelgruppeFacet Variabelgruppe)> byDelkilde = [];
        List<(Guid Owner, VariabelgruppeFacet Variabelgruppe)> byDatasamling = [];

        foreach (var variabelgruppe in variabelgrupper)
        {
            // A payload repeating an owner files the group twice under it, which Variabelgrupper's
            // own OnePerId then collapses — so one node per placement, without a second pass here
            // claiming to be what produces it.
            foreach (var owner in variabelgruppe.Owners)
            {
                // The deepest owning id the answer still carries. A datasamling or delkilde the
                // cross-filtering dropped would otherwise take the groups under it off the tree,
                // and the owning kilde is drawn whenever anything under it is.
                if (owner.DatasamlingId is { } datasamlingId
                    && datasamlinger.TryGetValue(datasamlingId, out var datasamling)
                    && datasamling.KildeId == owner.KildeId)
                {
                    byDatasamling.Add((datasamlingId, variabelgruppe));
                }
                else if (owner.DelkildeId is { } delkildeId
                         && delkilder.TryGetValue(delkildeId, out var delkilde)
                         && delkilde.KildeId == owner.KildeId)
                {
                    byDelkilde.Add((delkildeId, variabelgruppe));
                }
                else
                {
                    byKilde.Add((owner.KildeId, variabelgruppe));
                }
            }
        }

        return new VariabelgruppePlacements(Lookup(byKilde), Lookup(byDelkilde), Lookup(byDatasamling));

        static ILookup<Guid, VariabelgruppeFacet> Lookup(
            IEnumerable<(Guid Owner, VariabelgruppeFacet Variabelgruppe)> placed) =>
            placed.ToLookup(entry => entry.Owner, entry => entry.Variabelgruppe);
    }

    /// <summary>Nest one level by the parent id its own entries carry, and turn each into a node.</summary>
    /// <remarks>
    /// A parent the cross-filtering dropped leaves its children as roots rather than taking them off
    /// the tree, and a chain looping back on itself is seeded by a second pass — the panel's rules.
    /// </remarks>
    private static IReadOnlyList<HierarchyNode> Nest<T>(
        IReadOnlyList<T> all,
        HierarchyLevel level,
        string parentPath,
        Func<T, Guid> id,
        Func<T, Guid?> parentId,
        Func<T, string, IReadOnlyList<HierarchyNode>, HierarchyNode> build)
    {
        if (all.Count == 0)
        {
            return [];
        }

        var known = all.Select(id).ToHashSet();

        var byParent = all
            .Where(entry => parentId(entry) is { } parent && known.Contains(parent))
            .ToLookup(entry => parentId(entry)!.Value);

        HashSet<Guid> placed = [];
        List<HierarchyNode> roots = [];

        // Real roots first, then whatever they could not reach: every member of a cycle has its
        // parent present, so none of them is a root, and dropping them would take a filter off the
        // panel with no error anywhere.
        AddRoots(entry => !Parented(entry));
        AddRoots(_ => true);

        return roots;

        bool Parented(T entry) => parentId(entry) is { } parent && known.Contains(parent);

        void AddRoots(Func<T, bool> isRoot)
        {
            // A foreach rather than a query, because `placed` is a set the body mutates: building a
            // node places its whole subtree, so a cycle's other member is already drawn by the time
            // the second pass reaches it and must not be built as a root of its own as well.
            foreach (var entry in all)
            {
                if (isRoot(entry) && placed.Add(id(entry)))
                {
                    roots.Add(Node(entry, parentPath));
                }
            }
        }

        HierarchyNode Node(T entry, string ownerPath)
        {
            var path = NodePath(ownerPath, level, id(entry));

            List<HierarchyNode> nested = [];

            foreach (var child in byParent[id(entry)])
            {
                if (placed.Add(id(child)))
                {
                    nested.Add(Node(child, path));
                }
            }

            return build(entry, path, nested);
        }
    }

    /// <summary>Where a node is drawn: its ancestors' path, then its own level and id.</summary>
    /// <remarks>
    /// The level is the enum's own name rather than the panel's Norwegian facet word, so a path
    /// cannot be mistaken for the selection key that word builds.
    /// </remarks>
    private static string NodePath(string parentPath, HierarchyLevel level, Guid id) =>
        parentPath.Length == 0 ? $"{level}:{id}" : $"{parentPath}/{level}:{id}";
}
