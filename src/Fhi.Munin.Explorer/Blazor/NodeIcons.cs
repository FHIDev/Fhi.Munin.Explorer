using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The two class names one icon slot wears: the group's, and each glyph's.</summary>
/// <remarks>Written out per surface, never composed from a prefix: the class-name inventory is
/// reconciled against literals in <c>src/</c> and cannot see a stem finished at runtime.</remarks>
internal sealed record NodeIconClasses(string Group, string Glyph)
{
    /// <summary>The kilde hierarchy's own tree, where the glyphs lead the row.</summary>
    internal static NodeIconClasses Hierarchy { get; } =
        new("munin-explorer-hierarchy__icons", "munin-explorer-hierarchy__icon");

    /// <summary>The facet panels' value rows, where they lead the name. Stiler 0.1.75 and later.</summary>
    internal static NodeIconClasses Facets { get; } =
        new("munin-explorer-filters__icons", "munin-explorer-filters__icon");
}

/// <summary>The decorative icon slot a row writes before its label, in the kilde hierarchy and in
/// the facet panels alike; a host stylesheet decides which side of the label it draws on.</summary>
internal static class NodeIcons
{
    /// <summary>No glyphs at all — what a row draws when the host has turned the icons off.</summary>
    internal static IReadOnlyList<NodeIcon> None => [];

    /// <summary>The glyphs one node shows, in <see cref="DataCategoryIcons.Order"/>.</summary>
    internal static IReadOnlyList<NodeIcon> For(KildeHierarchyNode node) => node.Kind switch
    {
        KildeNodeKind.Delkilde => [DataCategoryIcons.Folder],
        KildeNodeKind.Datasamling => DataCategoryIcons.For(node.Categories),
        _ => []
    };

    /// <summary>
    /// Writes the slot, or nothing at all when there are no glyphs — an empty span would still take
    /// the gap the stylesheet puts between the icons and the name.
    /// </summary>
    internal static void Write(
        RenderTreeBuilder builder, IReadOnlyList<NodeIcon> icons, NodeIconClasses classes)
    {
        if (icons.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", classes.Group);
        // Decorative: a glyph never joins the accessible name of whatever the slot sits in, so the
        // categories reach a reader through SpokenCategories beside it instead.
        builder.AddAttribute(2, "aria-hidden", "true");
        foreach (var icon in icons)
        {
            WriteGlyph(builder, icon, classes.Glyph);
        }
        builder.CloseElement();
    }

    /// <summary>
    /// One glyph and nothing around it: the slot is the caller's, so a legend row can draw the
    /// picture as a direct child of the row, which is what Stiler's rule selects.
    /// </summary>
    internal static void WriteGlyph(RenderTreeBuilder builder, NodeIcon icon, string glyphClass)
    {
        // From 0, as the two writers around it: a sequence borrowed from one caller is safe only
        // where that caller's numbers happen to fit, and this one has two.
        builder.OpenElement(0, "svg");
        builder.SetKey(icon.Key);
        builder.AddAttribute(1, "class", glyphClass);
        builder.AddAttribute(2, "data-node-icon", icon.Key);
        builder.AddAttribute(3, "viewBox", "0 0 24 24");
        // An <svg> with no width or height is 300x150, so a host with no rule for the class
        // above would get one icon the size of a paragraph rather than an undersized one.
        builder.AddAttribute(4, "width", "1em");
        builder.AddAttribute(5, "height", "1em");
        builder.AddAttribute(6, "fill", "none");
        builder.AddAttribute(7, "stroke", "currentColor");
        builder.AddAttribute(8, "stroke-width", "2");
        builder.AddAttribute(9, "stroke-linecap", "round");
        builder.AddAttribute(10, "stroke-linejoin", "round");
        // Both, because focusable="false" is what keeps IE-era engines from making an inline
        // svg a tab stop and aria-hidden is what keeps it off the accessibility tree.
        builder.AddAttribute(11, "focusable", "false");
        builder.AddAttribute(12, "aria-hidden", "true");
        foreach (var shape in icon.Shapes)
        {
            builder.OpenElement(13, shape.Element);
            foreach (var (name, value) in shape.Attributes)
            {
                builder.AddAttribute(14, name, value);
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    /// <summary>
    /// The datakategorier in words, since the glyphs are aria-hidden. Null when there are none, and
    /// null for the folder — the grouping levels' one glyph, which no datasamling ever carries and
    /// which says only what the nesting around the row already says.
    /// </summary>
    internal static string? SpokenCategories(IReadOnlyList<NodeIcon> icons, Texts texts)
    {
        var named = icons
            .Where(icon => icon.Key != DataCategoryIcons.Grouping)
            .Select(icon =>
                texts.DataCategoryNames.TryGetValue(icon.Key, out var name) ? name : icon.Key)
            .ToList();

        return named.Count == 0 ? null : texts.DataCategoryNamed(string.Join(", ", named));
    }

    /// <summary>
    /// Writes those words beside the slot, or nothing when there are none. The separator is a text
    /// node rather than the span's first character: an accessible name is computed per element, so
    /// a space inside the span is trimmed off and the row announces as "Tromsø 1Datakategori: …".
    /// </summary>
    internal static void WriteSpoken(
        RenderTreeBuilder builder, IReadOnlyList<NodeIcon> icons, Texts texts)
    {
        if (SpokenCategories(icons, texts) is not { } categories)
        {
            return;
        }

        builder.AddContent(0, " ");
        builder.OpenElement(1, "span");
        builder.AddAttribute(2, "class", "screenreader-only");
        builder.AddContent(3, categories);
        builder.CloseElement();
    }
}
