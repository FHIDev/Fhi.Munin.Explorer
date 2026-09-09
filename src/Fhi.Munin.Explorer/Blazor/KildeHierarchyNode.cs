using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which level of the tree a node came from, which is what decides its icon.</summary>
internal enum KildeNodeKind
{
    Delkilde,
    Datasamling,
    Variabelgruppe
}

internal sealed record KildeHierarchyNode(
    string Key, string Name, int Count, int? Order, KildeNodeKind Kind,
    IReadOnlyList<string> Categories, IReadOnlyList<KildeHierarchyNode> Children)
{
    internal static IReadOnlyList<KildeHierarchyNode> From(KildeHierarchy hierarchy) =>
        Ordered(hierarchy.Delkilder.Select(d => From(d, hierarchy.KildeId.ToString()))
            .Concat(hierarchy.DirectDatasamlinger.Select(d => From(d, hierarchy.KildeId.ToString()))));

    private static KildeHierarchyNode From(HierarchyDelkilde node, string parent)
    {
        var key = $"{parent}/delkilde/{node.Id}";
        // A group's presentationOrder counts a different sequence than a datasamling's — Tromsø4
        // numbers its datasamlinger 1..2 and its groups 537..1189 — so the unassigned ones are
        // ordered among themselves, behind the structure they are an appendix to.
        return new(key, node.Name, node.VariableCount, node.PresentationOrder, KildeNodeKind.Delkilde, [],
        [
            .. Ordered(node.Children.Select(d => From(d, key))
                .Concat(node.Datasamlinger.Select(d => From(d, key)))),
            .. Ordered(node.UnassignedVariabelgrupper.Select(g => From(g, key)))
        ]);
    }

    private static KildeHierarchyNode From(HierarchyDatasamling node, string parent)
    {
        var key = $"{parent}/datasamling/{node.Id}";
        return new(key, node.Name, node.VariableCount, node.PresentationOrder, KildeNodeKind.Datasamling,
            node.Categories, Ordered(node.Variabelgrupper.Select(g => From(g, key))));
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
