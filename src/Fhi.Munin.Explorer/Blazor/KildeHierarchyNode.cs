using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which level of the tree a node came from, which is what decides its icon.</summary>
internal enum KildeNodeKind
{
    Delkilde,
    Datasamling,
    Variabelgruppe
}

// DatasamlingId is last and defaulted because only one of the three kinds has one: a delkilde and
// a variabelgruppe have no page to open, so the node that can be routed to is the node that carries
// an id rather than one that carries a flag beside it.
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
        // A group's presentationOrder counts a different sequence than a datasamling's — Tromsø4
        // numbers its datasamlinger 1..2 and its groups 537..1189 — so the unassigned ones are
        // ordered among themselves, behind the structure they are an appendix to.
        return new(key, node.Name, node.VariableCount, node.PresentationOrder, KildeNodeKind.Delkilde, [],
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
        return new(key, node.Name, node.VariableCount, node.PresentationOrder, KildeNodeKind.Datasamling,
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
