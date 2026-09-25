using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which level of the tree a node came from, which is what decides its icon.</summary>
internal enum KildeNodeKind
{
    Delkilde,
    Datasamling,
    Variabelgruppe
}

// DatasamlingId is last and defaulted because only a datasamling has a page to open. Order is a
// variabelgruppe's presentationOrder and null on the other kinds, which Siblings ranks by displayOrder.
internal sealed record KildeHierarchyNode(
    string Key, string Name, int Count, int? Order, KildeNodeKind Kind,
    IReadOnlyList<string> Categories, IReadOnlyList<KildeHierarchyNode> Children,
    Guid? DatasamlingId = null)
{
    internal static IReadOnlyList<KildeHierarchyNode> From(KildeHierarchy hierarchy) =>
        Siblings(hierarchy.Delkilder, hierarchy.DirectDatasamlinger, hierarchy.KildeId.ToString());

    private static KildeHierarchyNode From(HierarchyDelkilde node, string parent)
    {
        var key = $"{parent}/delkilde/{node.Id}";
        // Unassigned variabelgrupper keep their own presentationOrder, behind the displayOrder-ranked
        // structure they are an appendix to.
        return new(key, node.Name, node.VariableCount, null, KildeNodeKind.Delkilde, [],
        [
            .. Siblings(node.Children, node.Datasamlinger, key),
            .. Ordered(node.UnassignedVariabelgrupper.Select(g => From(g, key)))
        ]);
    }

    /// <summary>One parent's delkilder and datasamlinger on the API's shared displayOrder, never on
    /// presentationOrder, which numbers each kind apart. (Fhi.Metadata-fuzw0)</summary>
    private static IReadOnlyList<KildeHierarchyNode> Siblings(
        IEnumerable<HierarchyDelkilde> delkilder, IEnumerable<HierarchyDatasamling> datasamlinger, string parent) =>
    [
        .. SiblingOrder.Merge(
                delkilder.Select(d => (Rank: d.DisplayOrder, d.Id, Node: From(d, parent))),
                datasamlinger.Select(d => (Rank: d.DisplayOrder, d.Id, Node: From(d, parent))),
                sibling => sibling.Rank, sibling => sibling.Node.Name, sibling => sibling.Id)
            .Select(sibling => sibling.Node)
    ];

    private static KildeHierarchyNode From(HierarchyDatasamling node, string parent)
    {
        var key = $"{parent}/datasamling/{node.Id}";
        return new(key, node.Name, node.VariableCount, null, KildeNodeKind.Datasamling,
            node.Categories, Ordered(node.Variabelgrupper.Select(g => From(g, key))), node.Id);
    }

    private static KildeHierarchyNode From(HierarchyVariabelgruppe node, string parent)
    {
        // One group can occur under several owners; browser disclosure state belongs to a position.
        var key = $"{parent}/variabelgruppe/{node.Id}";
        return new(key, node.Name, node.VariableCount, node.PresentationOrder, KildeNodeKind.Variabelgruppe, [],
            Ordered(node.ChildVariabelgrupper.Select(g => From(g, key))));
    }

    private static IReadOnlyList<KildeHierarchyNode> Ordered(IEnumerable<KildeHierarchyNode> nodes) =>
        [.. nodes.OrderBy(n => n.Order ?? int.MaxValue)
            .ThenBy(n => n.Name, CatalogueProperties.CatalogueOrder)
            .ThenBy(n => n.Key, StringComparer.Ordinal)];
}
