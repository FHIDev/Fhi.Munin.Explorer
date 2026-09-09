using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The decorative icon slot a hierarchy row draws in front of its label: a folder for the grouping
/// levels, one glyph per datakategori for a datasamling, and nothing for a variabelgruppe.
/// </summary>
internal static class NodeIcons
{
    /// <summary>No glyphs at all — what a row draws when the host has turned the icons off.</summary>
    internal static IReadOnlyList<NodeIcon> None => [];

    /// <summary>The glyphs one node shows, in <see cref="DatakategoriIcons.Order"/>.</summary>
    internal static IReadOnlyList<NodeIcon> For(KildeHierarchyNode node) => node.Kind switch
    {
        KildeNodeKind.Delkilde => [DatakategoriIcons.Folder],
        KildeNodeKind.Datasamling => DatakategoriIcons.For(node.Categories),
        _ => []
    };

    /// <summary>
    /// Writes the slot, or nothing at all when there are no glyphs — an empty span would still take
    /// the gap the stylesheet puts between the icons and the name.
    /// </summary>
    internal static void Write(RenderTreeBuilder builder, IReadOnlyList<NodeIcon> icons)
    {
        if (icons.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "munin-explorer-hierarchy__icons");
        // Decorative: the row's accessible name stays its label, and the categories these stand for
        // are said in words beside them rather than twice.
        builder.AddAttribute(2, "aria-hidden", "true");
        foreach (var icon in icons)
        {
            builder.OpenElement(3, "svg");
            builder.SetKey(icon.Key);
            builder.AddAttribute(4, "class", "munin-explorer-hierarchy__icon");
            builder.AddAttribute(5, "data-node-icon", icon.Key);
            builder.AddAttribute(6, "viewBox", "0 0 24 24");
            // An <svg> with no width or height is 300x150, so a host with no rule for the class
            // above would get one icon the size of a paragraph rather than an undersized one.
            builder.AddAttribute(7, "width", "1em");
            builder.AddAttribute(8, "height", "1em");
            builder.AddAttribute(9, "fill", "none");
            builder.AddAttribute(10, "stroke", "currentColor");
            builder.AddAttribute(11, "stroke-width", "2");
            builder.AddAttribute(12, "stroke-linecap", "round");
            builder.AddAttribute(13, "stroke-linejoin", "round");
            // Both, because focusable="false" is what keeps IE-era engines from making an inline
            // svg a tab stop and aria-hidden is what keeps it off the accessibility tree.
            builder.AddAttribute(14, "focusable", "false");
            builder.AddAttribute(15, "aria-hidden", "true");
            foreach (var shape in icon.Shapes)
            {
                builder.OpenElement(16, shape.Element);
                foreach (var (name, value) in shape.Attributes)
                {
                    builder.AddAttribute(17, name, value);
                }
                builder.CloseElement();
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    /// <summary>
    /// The glyphs in words, for the reader who cannot see them. Null for a folder, which says only
    /// what the nesting around it already says, and null when there are no glyphs.
    /// </summary>
    internal static string? SpokenCategories(
        KildeNodeKind kind, IReadOnlyList<NodeIcon> icons, Texts texts)
    {
        if (kind != KildeNodeKind.Datasamling || icons.Count == 0)
        {
            return null;
        }

        var named = icons.Select(icon =>
            texts.DatakategoriNames.TryGetValue(icon.Key, out var name) ? name : icon.Key);

        return texts.DatakategoriNamed(string.Join(", ", named));
    }
}
