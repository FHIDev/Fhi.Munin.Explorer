using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The four levels of the catalogue hierarchy, outermost first.
/// </summary>
/// <remarks>
/// The order is the trail's order and the order the members are compared in — a press on one
/// level clears every level greater than it — so the members are not free to be reordered.
/// Kildetype is deliberately not among them: it is a facet of its own in the panel rather than
/// a step on the way to a kilde, and a reader who cleared the trail would not expect the type
/// filter to go with it.
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
/// hangs under every datasamling its variables are in. <c>ShortName</c> is the kilde's
/// <c>kortNavn</c>, null below it; <c>Offered</c> false for a row sent to nest rather than to offer.
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
/// Datasamlinger are split across two lookups rather than keyed into one by whichever parent
/// they hang from: the two id spaces are independent Guids off the wire, and one lookup would
/// read a kilde id that happened to equal a delkilde id as the other level's key and misfile
/// the row with no error anywhere.
/// </remarks>
internal sealed record KildeLevelLookup(
    ILookup<Guid, DelkildeFacet> Delkilder,
    ILookup<Guid, DatasamlingFacet> DatasamlingerByKilde,
    ILookup<Guid, DatasamlingFacet> DatasamlingerByDelkilde);

/// <summary>
/// The filter panel's hierarchy, read off one <c>/api/explorer/filters</c> answer and nothing else.
/// </summary>
/// <remarks>
/// Pure by design: no state, no request, no selection. Counts are the answer's own — the API
/// cross-filters every facet it sends, so a count summed from children would promise a number the
/// rows a tick produces do not match.
/// </remarks>
internal static class FilterHierarchy
{
    /// <summary>
    /// The whole kilde → delkilde → datasamling → variabelgruppe tree the panel narrows by.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="FilterOptions.HierarchyVariabelgrupper"/>, which carries every group
    /// whatever its <see cref="VariabelgruppeFacet.Filter"/> says and is empty against an API
    /// predating it; a kilde the answer does not list draws nothing, the groups under it included.
    /// </remarks>
    internal static IReadOnlyList<HierarchyNode> Build(FilterOptions facets)
    {
        var levels = KildeLevels(facets);
        var grupper = Placements(facets);

        return
        [
            .. facets.Kilder.DistinctBy(kilde => kilde.Id).Select(kilde => Kilde(kilde, levels, grupper))
        ];
    }

    /// <summary>The standalone variabelgruppe facet's own list — the flat checkbox surface, nested.</summary>
    /// <remarks>
    /// It reads no selection and must never be given one: a group opted out of this facet stays a
    /// container while it is ticked, so its chip and the tree remain the way off it and nothing
    /// here can offer it back because the reader chose it. (Fhi.Metadata-fbe3w)
    /// </remarks>
    internal static IReadOnlyList<HierarchyNode> StandaloneVariabelgrupper(FilterOptions facets) =>
        Nest(OnePerId(facets.Variabelgrupper, gruppe => gruppe.Id, gruppe => gruppe.ParentId),
             HierarchyLevel.Variabelgruppe,
             parentPath: "",
             gruppe => gruppe.Id,
             gruppe => gruppe.ParentId,
             (gruppe, path, nested) => new HierarchyNode(
                 path, HierarchyLevel.Variabelgruppe, gruppe.Id, gruppe.Name, null, gruppe.Count, nested,
                 gruppe.IsStandaloneFacetOption));

    /// <summary>Both child levels of the kilde tree, in one pass over the facets.</summary>
    internal static KildeLevelLookup KildeLevels(FilterOptions facets)
    {
        var delkilder = ById(facets.Delkilder, delkilde => delkilde.Id);

        // A delkilde the payload left out — cross-filtered away, or belonging to another kilde — is
        // an absent parent, so its datasamlinger fall back to the kilde rather than disappearing
        // with it.
        bool HangsUnderItsDelkilde(DatasamlingFacet datasamling) =>
            datasamling.DelkildeId is { } parent
            && delkilder.TryGetValue(parent, out var delkilde)
            && delkilde.KildeId == datasamling.KildeId;

        // One entry per id before the split rather than inside each bucket: two copies of one id
        // disagreeing about their delkilde land in different lookups, which no per-bucket
        // de-duplication can see, and the id is then drawn at two paths. (Fhi.Metadata-l9l2n.82)
        var datasamlinger = facets.Datasamlinger.DistinctBy(datasamling => datasamling.Id).ToList();

        return new KildeLevelLookup(
            facets.Delkilder.ToLookup(delkilde => delkilde.KildeId),
            datasamlinger
                .Where(datasamling => !HangsUnderItsDelkilde(datasamling))
                .ToLookup(datasamling => datasamling.KildeId),
            datasamlinger
                .Where(HangsUnderItsDelkilde)
                .ToLookup(datasamling => datasamling.DelkildeId!.Value));
    }

    /// <summary>One entry per id, the copy hanging off a parent that is present winning.</summary>
    /// <remarks>
    /// Naming an id twice drew it twice, so one press ticked both and the row over the results
    /// carried two chips for one filter. Two entries with one id can differ in parent and in name
    /// alike, so keeping the first listed would nest, and label, by payload order. (Fhi.Metadata-l9l2n.82)
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
    /// GroupBy rather than ToDictionary: a payload repeating an id is malformed, but it throws on
    /// the render path and inside the kilde search box's onchange alike, either of which tears the
    /// circuit down over what would otherwise be one oddly drawn row.
    /// </remarks>
    private static Dictionary<Guid, T> ById<T>(IEnumerable<T> entries, Func<T, Guid> id) =>
        entries.GroupBy(id).ToDictionary(group => group.Key, group => group.First());

    /// <summary>One kilde, with the two levels under it and whatever groups hang off the kilde itself.</summary>
    /// <remarks>
    /// Datasamlinger before delkilder, the order the panel's kilde facet already draws them in, and
    /// the groups last: a group no datasamling of the kilde holds is an appendix to the structure
    /// rather than a peer of it, which is how the kilde view's own tree reads it.
    /// </remarks>
    private static HierarchyNode Kilde(KildeFacet kilde, KildeLevelLookup levels, GruppePlacements grupper)
    {
        var path = NodePath("", HierarchyLevel.Kilde, kilde.Id);

        return new(path, HierarchyLevel.Kilde, kilde.Id, kilde.Name, kilde.ShortName, kilde.Count,
        [
            .. Datasamlinger(levels.DatasamlingerByKilde[kilde.Id], path, grupper),
            .. Delkilder(levels.Delkilder[kilde.Id], path, levels, grupper),
            .. Grupper(grupper.ByKilde[kilde.Id], path)
        ]);
    }

    /// <summary>A kilde's delkilder, each nested under the delkilde its own facet names.</summary>
    private static IReadOnlyList<HierarchyNode> Delkilder(
        IEnumerable<DelkildeFacet> delkilder,
        string parentPath,
        KildeLevelLookup levels,
        GruppePlacements grupper) =>
        Nest(OnePerId(delkilder, delkilde => delkilde.Id, delkilde => delkilde.ParentDelkildeId),
             HierarchyLevel.Delkilde,
             parentPath,
             delkilde => delkilde.Id,
             delkilde => delkilde.ParentDelkildeId,
             (delkilde, path, nested) => new HierarchyNode(
                 path, HierarchyLevel.Delkilde, delkilde.Id, delkilde.Name, null, delkilde.Count,
                 [
                     .. Datasamlinger(levels.DatasamlingerByDelkilde[delkilde.Id], path, grupper),
                     .. nested,
                     .. Grupper(grupper.ByDelkilde[delkilde.Id], path)
                 ]));

    /// <summary>Datasamlinger as branches: what hangs under one is the groups placed in it.</summary>
    /// <remarks>
    /// Takes the buckets as they come: <see cref="KildeLevels"/> has already left one entry per id
    /// across both of them, which is where a repeated id has to be settled since the copies can
    /// disagree about which bucket they belong in.
    /// </remarks>
    private static IReadOnlyList<HierarchyNode> Datasamlinger(
        IEnumerable<DatasamlingFacet> datasamlinger, string parentPath, GruppePlacements grupper) =>
    [
        .. datasamlinger.Select(datasamling =>
        {
            var path = NodePath(parentPath, HierarchyLevel.Datasamling, datasamling.Id);

            return new HierarchyNode(
                path, HierarchyLevel.Datasamling, datasamling.Id, datasamling.Name, null, datasamling.Count,
                Grupper(grupper.ByDatasamling[datasamling.Id], path));
        })
    ];

    /// <summary>The groups placed at one owner, nested among themselves.</summary>
    /// <remarks>
    /// <see cref="VariabelgruppeFacet.ParentId"/> is another group and never the catalogue owner, so
    /// it nests inside a placement and decides nothing about which one: a group whose parent is
    /// placed elsewhere stands as a root here rather than following it out of its own owner.
    /// </remarks>
    private static IReadOnlyList<HierarchyNode> Grupper(
        IEnumerable<VariabelgruppeFacet> placed, string parentPath) =>
        Nest(OnePerId(placed, gruppe => gruppe.Id, gruppe => gruppe.ParentId),
             HierarchyLevel.Variabelgruppe,
             parentPath,
             gruppe => gruppe.Id,
             gruppe => gruppe.ParentId,
             (gruppe, path, nested) => new HierarchyNode(
                 path, HierarchyLevel.Variabelgruppe, gruppe.Id, gruppe.Name, null, gruppe.Count, nested));

    /// <summary>Every variabelgruppe under the ids its own owners name, one list per level.</summary>
    /// <remarks>
    /// Three lookups rather than one keyed by owner, for the reason <see cref="KildeLevelLookup"/>
    /// gives: the three id spaces are independent Guids off the wire, and a single keyed collection
    /// would file a group under whichever level's key its owning id happened to collide with.
    /// </remarks>
    private sealed record GruppePlacements(
        ILookup<Guid, VariabelgruppeFacet> ByKilde,
        ILookup<Guid, VariabelgruppeFacet> ByDelkilde,
        ILookup<Guid, VariabelgruppeFacet> ByDatasamling);

    private static GruppePlacements Placements(FilterOptions facets)
    {
        // Every group the answer carries, whatever its Filter says. The opt-out is the standalone
        // facet's rule alone, so reading it here would take groups off a tree that has to draw
        // them. (Fhi.Metadata-fbe3w)
        var grupper = OnePerId(facets.HierarchyVariabelgrupper, gruppe => gruppe.Id, gruppe => gruppe.ParentId);

        var datasamlinger = ById(facets.Datasamlinger, datasamling => datasamling.Id);
        var delkilder = ById(facets.Delkilder, delkilde => delkilde.Id);

        List<(Guid Owner, VariabelgruppeFacet Gruppe)> byKilde = [];
        List<(Guid Owner, VariabelgruppeFacet Gruppe)> byDelkilde = [];
        List<(Guid Owner, VariabelgruppeFacet Gruppe)> byDatasamling = [];

        foreach (var gruppe in grupper)
        {
            // A payload repeating an owner files the group twice under it, which Grupper's own
            // OnePerId then collapses — so one node per placement, without a second pass here
            // claiming to be what produces it.
            foreach (var owner in gruppe.Owners)
            {
                // The deepest owning id the answer still carries. A datasamling or delkilde the
                // cross-filtering dropped would otherwise take the groups under it off the tree,
                // and the owning kilde is drawn whenever anything under it is.
                if (owner.DatasamlingId is { } datasamlingId
                    && datasamlinger.TryGetValue(datasamlingId, out var datasamling)
                    && datasamling.KildeId == owner.KildeId)
                {
                    byDatasamling.Add((datasamlingId, gruppe));
                }
                else if (owner.DelkildeId is { } delkildeId
                         && delkilder.TryGetValue(delkildeId, out var delkilde)
                         && delkilde.KildeId == owner.KildeId)
                {
                    byDelkilde.Add((delkildeId, gruppe));
                }
                else
                {
                    byKilde.Add((owner.KildeId, gruppe));
                }
            }
        }

        return new GruppePlacements(Lookup(byKilde), Lookup(byDelkilde), Lookup(byDatasamling));

        static ILookup<Guid, VariabelgruppeFacet> Lookup(
            IEnumerable<(Guid Owner, VariabelgruppeFacet Gruppe)> placed) =>
            placed.ToLookup(entry => entry.Owner, entry => entry.Gruppe);
    }

    /// <summary>
    /// Nest one level by the parent id its own entries carry, and turn each into a node.
    /// </summary>
    /// <remarks>
    /// A parent the cross-filtering dropped leaves its children as roots rather than taking them
    /// off the tree, and a parent chain that loops back on itself is seeded by a second pass — both
    /// the rules the panel's own tree follows, and for the reasons stated there.
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
    /// cannot be mistaken for — or compared against — the selection key that word builds.
    /// </remarks>
    private static string NodePath(string parentPath, HierarchyLevel level, Guid id) =>
        parentPath.Length == 0 ? $"{level}:{id}" : $"{parentPath}/{level}:{id}";
}
