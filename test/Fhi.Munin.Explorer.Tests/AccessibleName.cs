using AngleSharp.Dom;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What a screen reader would announce a control as, worked out from the markup.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the obvious assertion is the wrong one. "The field has a naming attribute"
/// is satisfied by <c>placeholder</c> and by <c>title</c>, several checking tools accept either as
/// a name, and neither is one: a placeholder is replaced by whatever the reader types, so the
/// field announces as unnamed the moment they start, and <c>title</c> is a tooltip that assistive
/// technology is free to ignore and mobile readers do. A test written against "some attribute is
/// present" passes on exactly the markup this repository shipped as a bug (WCAG 4.1.2, 3.3.2).
/// </para>
/// <para>
/// So the resolution below deliberately stops at the sources that really are names —
/// <c>aria-labelledby</c>, then <c>aria-label</c>, then an associated or wrapping
/// <c>&lt;label&gt;</c>, then the element's own content for a control that is named by it — and
/// has no arm for <c>placeholder</c> or <c>title</c> at all. An empty answer from here is a
/// control that announces as unnamed, whatever attributes it happens to carry.
/// </para>
/// <para>
/// Not the whole accname algorithm: no <c>&lt;fieldset&gt;</c>/<c>&lt;legend&gt;</c>, no
/// <c>alt</c>, no recursion into a labelling element's own labels. Those are not shapes this
/// package emits, and an implementation that grew arms for them would be asserting against itself
/// rather than against the markup.
/// </para>
/// </remarks>
internal static class AccessibleName
{
    /// <summary>The name the control announces as, or an empty string when it has none.</summary>
    public static string Of(IElement element)
    {
        var labelledBy = element.GetAttribute("aria-labelledby");

        if (!string.IsNullOrWhiteSpace(labelledBy))
        {
            var referenced = labelledBy
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => ById(element, id)?.TextContent.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text));

            var joined = string.Join(" ", referenced).Trim();

            if (joined.Length > 0)
            {
                return joined;
            }
        }

        var ariaLabel = element.GetAttribute("aria-label");

        if (!string.IsNullOrWhiteSpace(ariaLabel))
        {
            return ariaLabel.Trim();
        }

        var id = element.GetAttribute("id");

        if (!string.IsNullOrWhiteSpace(id))
        {
            var associated = Tree(element)
                .Where(candidate => candidate.TagName.Equals("LABEL", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(label => string.Equals(
                    label.GetAttribute("for"), id, StringComparison.Ordinal));

            if (associated is not null && associated.TextContent.Trim().Length > 0)
            {
                return associated.TextContent.Trim();
            }
        }

        // A control wrapped in a label is named by it — the shape the kodeverk checkbox uses.
        var wrapping = element.Closest("label");

        if (wrapping is not null && wrapping.TextContent.Trim().Length > 0)
        {
            return wrapping.TextContent.Trim();
        }

        // Content, for the controls that are named by it. A button is; an input never is, which is
        // why this arm cannot rescue the field the guard above is aimed at.
        if (NamedByItsContent(element))
        {
            return element.TextContent.Trim();
        }

        return "";
    }

    /// <summary>
    /// Every element of the tree the control is rendered in.
    /// </summary>
    /// <remarks>
    /// Walking up from the control rather than asking <see cref="INode.Owner"/>, because a bUnit
    /// render is a parsed fragment: its nodes have an owner document and are not attached to it,
    /// so <c>Owner.QuerySelectorAll</c> and <c>Owner.GetElementById</c> both answer nothing and
    /// every <c>&lt;label for&gt;</c> in the component reads as absent. That failure is silent in
    /// exactly the wrong direction — it reports a properly labelled control as unnamed — which is
    /// how it was found.
    /// </remarks>
    private static IEnumerable<IElement> Tree(IElement element)
    {
        INode node = element;

        while (node.Parent is not null)
        {
            node = node.Parent;
        }

        var root = node as IParentNode;

        return root is null ? [] : root.QuerySelectorAll("*");
    }

    private static IElement? ById(IElement element, string id) =>
        Tree(element).FirstOrDefault(candidate =>
            string.Equals(candidate.GetAttribute("id"), id, StringComparison.Ordinal));

    private static bool NamedByItsContent(IElement element) =>
        element.TagName.Equals("BUTTON", StringComparison.OrdinalIgnoreCase)
        || element.TagName.Equals("A", StringComparison.OrdinalIgnoreCase)
        || element.TagName.Equals("SUMMARY", StringComparison.OrdinalIgnoreCase);
}
