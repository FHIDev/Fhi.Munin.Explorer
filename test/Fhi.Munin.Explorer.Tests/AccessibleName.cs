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
    /// <summary>
    /// HTML's labelable elements — the only ones a wrapping <c>&lt;label&gt;</c> can name.
    /// </summary>
    /// <remarks>
    /// A hidden input is not one of them, and neither is anything outside this list: a
    /// <c>&lt;span&gt;</c>, an <c>&lt;a&gt;</c> or a <c>&lt;div role="button"&gt;</c> sitting
    /// inside a label takes no name from it however close the words are on screen.
    /// </remarks>
    private const string LabelableSelector =
        "button, input:not([type=hidden]), meter, output, progress, select, textarea";

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

        // A control wrapped in a label is named by it — the shape the kodeverk checkbox uses, and
        // the list picker. Narrowly, though: HTML names (a) a labelable element from (b) the label
        // whose FIRST labelable descendant it is. Two shapes a browser leaves unnamed would
        // otherwise resolve to a name here — anything not labelable dropped inside a label, and a
        // second control under a label whose first one is something else, as in
        // `<label><input type="checkbox"/> Inkluder kodeverk <input type="text"/></label>`, where
        // the text field really does announce as unnamed. Both would be a silent pass on exactly
        // the defect this helper exists to catch, which is the failure direction that matters:
        // the file's promise is that an empty answer means an unnamed control.
        var wrapping = element.Matches(LabelableSelector) ? element.Closest("label") : null;

        if (wrapping is not null && NothingLabelableComesFirst(element))
        {
            var text = TextExcept(wrapping, element);

            if (text.Length > 0)
            {
                return text;
            }
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
    /// Whether this control is the first labelable element under the label that wraps it.
    /// </summary>
    /// <remarks>
    /// Walked backwards and upwards from the control rather than asked of the label as
    /// "is <c>QuerySelector</c>'s answer this element", because bUnit hands out wrapper objects
    /// that re-find their element after a render: the wrapper and the element it stands for are
    /// never the same reference, so an identity comparison answers no for a control that really is
    /// the first one. Nothing here compares nodes — a previous sibling holding a labelable element
    /// is what disqualifies it, and that is a question about the sibling alone.
    /// </remarks>
    private static bool NothingLabelableComesFirst(IElement element)
    {
        var node = element;

        while (node is not null && !node.TagName.Equals("LABEL", StringComparison.OrdinalIgnoreCase))
        {
            for (var before = node.PreviousElementSibling;
                 before is not null;
                 before = before.PreviousElementSibling)
            {
                if (before.Matches(LabelableSelector) || before.QuerySelector(LabelableSelector) is not null)
                {
                    return false;
                }
            }

            node = node.ParentElement;
        }

        // Off the top of the tree without meeting a label, which the caller has already ruled out.
        return node is not null;
    }

    /// <summary>
    /// The label's own words, with the control it names left out of them.
    /// </summary>
    /// <remarks>
    /// <c>TextContent</c> alone is wrong for a label that wraps its control, which is the shape
    /// this component uses twice. The list picker is a <c>&lt;select&gt;</c> inside its label, and
    /// the select's text content is every option in it — so the whole answer would be "Velg liste
    /// Liste A Liste B Liste C" for a control that announces as "Velg liste". accname skips the
    /// element being named when it walks the label, and so does this: the traversal is what the
    /// name is built from, not the label's flattened text.
    /// </remarks>
    private static string TextExcept(IElement label, IElement named)
    {
        var words = label.Descendants<IText>()
            .Where(text => !named.Contains(text))
            .Select(text => text.Data);

        // Runs of whitespace collapse to one, the way a screen reader flattens them — otherwise
        // the newlines and indentation Razor writes around the control land in the middle of the
        // name and no equality assertion can be written against it.
        return string.Join(" ", string.Concat(words)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
