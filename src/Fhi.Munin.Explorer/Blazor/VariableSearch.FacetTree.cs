using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

// The facet panel's value lists: nested checkboxes, the disclosures that open and shut their branches, and which are open.
public partial class VariableSearch
{
    /// <summary>A facet's values as a nested list of checkboxes, keyed so a reorder never moves a box.</summary>
    /// <remarks>
    /// <c>lang</c> sits on the label, never the <c>&lt;li&gt;</c>, which would pass it to children. A shut
    /// branch's children are absent, not hidden, so none is tabbable. (Fhi.Metadata-j0a2h, Fhi.Metadata-adog5)
    /// </remarks>
    private RenderFragment FacetList(IReadOnlyList<FacetValue> values, string? id = null) => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "id", id);

        foreach (var value in values)
        {
            var branch = value.Children.Count > 0;
            var open = branch && IsBranchOpen(value.Key);

            builder.OpenElement(2, "li");
            builder.SetKey(value.Key);

            if (branch)
            {
                builder.AddAttribute(3, "class", "munin-explorer-filters__branch");
                Disclosure(builder, value, open);
            }

            // Held in a local so the null check below is one the compiler can carry into the branch.
            var toggle = value.Toggle;

            if (toggle is null)
            {
                // A container names a variabelgruppe in the catalogue's own Norwegian and the chip
                // for that group is marked, so this row has to be too. A span with no class is
                // somewhere to hang the marking that costs no rule in Stiler.
                builder.OpenElement(20, "span");
                builder.AddAttribute(21, "lang", value.Language);
                builder.AddContent(22, value.Label);
                builder.CloseElement();

                // Outside the span above rather than inside it, which is where the summary this
                // replaced held it: the marking is the catalogue's language, and this figure is
                // ours. A group's SIZE, so `__groupcount` and never `__chosen`. (Fhi.Metadata-l9l2n.104)
                if (value is { GroupHeading: true, Count: { } members })
                {
                    builder.AddContent(23, " ");
                    builder.OpenElement(24, "span");
                    builder.AddAttribute(25, "class", "munin-explorer-filters__groupcount");
                    builder.AddContent(26, members.ToString(CultureInfo.CurrentCulture));
                    builder.CloseElement();
                }
            }
            else
            {
                builder.OpenElement(30, "label");
                builder.OpenElement(31, "input");
                builder.AddAttribute(32, "type", "checkbox");
                builder.AddAttribute(33, "checked", value.Selected);

                // The half-tick itself is a DOM property with no attribute, so markup carries this and
                // explorer-interop.js's markMixed sets it after render. (Fhi.Metadata-cjezd)
                var mixed = !value.Selected && value.ChosenBelow > 0;
                if (mixed)
                {
                    builder.AddAttribute(34, "data-mixed", "true");
                }

                // The event's own value is ignored: the toggle flips what the filter holds, which
                // is the one state a press and the render after it are certain to agree about.
                builder.AddAttribute(35, "onchange",
                                     EventCallback.Factory.Create<ChangeEventArgs>(this, _ => toggle()));

                // What a plain onchange does not do and this panel needs: a press that ApplyFilterAsync
                // drops mid-fetch writes no state, so the renders either side are equal and
                // the browser's own tick stays on over a filter that is off until `checked` is forced.
                builder.SetUpdatesAttributeName("checked");

                builder.CloseElement();

                // Both or neither, as KildeHierarchyView does it: the words are the glyphs said
                // aloud, so keeping them would leave the switch doing nothing for the reader who
                // pressed it. The badge below is a fact rather than a picture and is outside both.
                var icons = _showNodeIcons ? value.Icons : null;
                if (icons is { Count: > 0 })
                {
                    builder.AddContent(36, (RenderFragment)(nested =>
                        NodeIcons.Write(nested, icons, NodeIconClasses.Facets)));
                }

                // The marking sits on the name, not on the label around it: the label also carries
                // this package's own prose below, which is the reader's language and not the
                // catalogue's and must not be pronounced as Norwegian. (WCAG 3.1.2)
                builder.OpenElement(37, "span");
                builder.AddAttribute(38, "lang", value.Language);
                builder.AddContent(39, value.Label);
                builder.CloseElement();

                // The half-tick in words, inside the label so it is part of the checkbox's name.
                if (mixed)
                {
                    builder.AddContent(40, " ");
                    builder.OpenElement(41, "span");
                    builder.AddAttribute(42, "class", "screenreader-only");
                    builder.AddContent(43, T.ChosenBelow(value.ChosenBelow));
                    builder.CloseElement();
                }

                // Keep the spoken categories after the name even though the decorative icons lead it.
                if (icons is { Count: > 0 } && value.SpeakIcons)
                {
                    builder.AddContent(44, (RenderFragment)(nested =>
                        NodeIcons.WriteSpoken(nested, icons, T)));
                }

                // Real text rather than a rule on the row, so the badge is part of the checkbox's
                // accessible name: what it says is a fact about the kilde and not decoration, and a
                // mark only a stylesheet drew would reach a sighted reader and nobody else.
                if (value.Badge is { } badge)
                {
                    builder.AddContent(45, " ");
                    builder.OpenElement(46, "span");
                    builder.AddAttribute(47, "class", "munin-explorer-filters__badge");
                    builder.AddContent(48, badge);
                    builder.CloseElement();
                }

                // The space is a text node of the label, not the span's first character, so a rule
                // dressing the count dresses the number and not the gap. The name keeps the space
                // either way (Fhi.Metadata-47lha).
                if (value.Count is { } count)
                {
                    builder.AddContent(49, " ");
                    builder.OpenElement(50, "span");
                    builder.AddAttribute(51, "class", "munin-explorer-filters__count");
                    builder.AddContent(52, $"({count})");
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            if (open)
            {
                builder.AddContent(60, FacetList(value.Children, BranchId(value.Key)));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>The control that opens and shuts one branch of a facet tree.</summary>
    /// <remarks>
    /// Beside the checkbox, never around it, since expanding must not filter. <c>aria-controls</c> only
    /// while the list is drawn: a shut branch renders no children for it to name.
    /// </remarks>
    private void Disclosure(RenderTreeBuilder builder, FacetValue value, bool open)
    {
        builder.OpenElement(4, "button");
        builder.AddAttribute(5, "type", "button");
        builder.AddAttribute(6, "class", "munin-explorer-filters__disclosure");
        builder.AddAttribute(7, "aria-expanded", open ? "true" : "false");
        builder.AddAttribute(8, "aria-controls", open ? BranchId(value.Key) : null);
        var nameId = BranchId(value.Key) + "-name";
        builder.AddAttribute(9, "aria-labelledby", $"{nameId}-action {nameId}");

        var key = value.Key;

        builder.AddAttribute(10, "onclick",
                             EventCallback.Factory.Create<MouseEventArgs>(this, e => ToggleBranchFromControl(key, e)));

        // Text rather than a rule, so a host with no stylesheet still sees which way the branch
        // points — and aria-hidden, so the name is the label alone: an arrow is not the visible
        // word WCAG 2.5.3 asks a name to contain.
        builder.OpenElement(11, "span");
        builder.AddAttribute(12, "aria-hidden", "true");
        builder.AddContent(13, open ? "▾" : "▸");
        builder.CloseElement();

        // Keep catalogue words in their own language without applying it to the UI action.
        builder.OpenElement(14, "span");
        builder.AddAttribute(15, "class", "screenreader-only");
        builder.OpenElement(16, "span");
        builder.AddAttribute(17, "id", nameId + "-action");
        builder.AddAttribute(18, "lang", ReaderLanguage.Of(Language));
        builder.AddContent(19, open ? T.CollapseBranch : T.ExpandBranch);
        builder.CloseElement();
        builder.AddContent(20, " ");
        builder.OpenElement(21, "span");
        builder.AddAttribute(22, "id", nameId);
        builder.AddAttribute(23, "lang", value.Language ?? ReaderLanguage.Of(Language));
        builder.AddContent(24, value.Label);
        builder.CloseElement();
        builder.CloseElement();

        builder.CloseElement();
    }

    /// <summary>Which branches of the facet trees are open, by the value key of each.</summary>
    /// <remarks>
    /// Never cleared when a filters answer arrives, so a branch the reader opened is still open
    /// after ticking something inside it — the keys are the payload's own ids rather than positions.
    /// </remarks>
    private readonly HashSet<string> _expandedBranches = [];

    /// <summary>Whether the branch at <paramref name="key"/> is drawn open. Every branch starts shut.</summary>
    private bool IsBranchOpen(string key) => _expandedBranches.Contains(key);

    /// <summary>The id of the list one branch discloses, unique to this mount and to that branch.</summary>
    /// <remarks>Every segment is valid in a CSS selector, so a test or a host can write <c>#id</c>.</remarks>
    private string BranchId(string key) => $"munin-explorer-branch-{_instance}-{IdPart(key)}";

    /// <summary>One facet value key as an id can spell it, and no two keys alike.</summary>
    /// <remarks>
    /// A space in an API kildetype would split <c>aria-controls</c> into two ids. Escaped, not replaced,
    /// so keys differing only in punctuation stay apart.
    /// </remarks>
    private static string IdPart(string key)
    {
        var id = new StringBuilder(key.Length);

        foreach (var character in key)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '-')
            {
                id.Append(character);
            }
            else
            {
                id.Append(CultureInfo.InvariantCulture, $"_{(int)character:x4}");
            }
        }

        return id.ToString();
    }

    /// <summary>Open or shut one branch, and nothing else.</summary>
    /// <remarks>
    /// Leaves <see cref="_filter"/> alone, so ticks inside survive a reopen. Double- and shift-clicks are
    /// ignored; a click with no count is the keyboard's and goes through. (Fhi.Metadata-zel47)
    /// </remarks>
    private void ToggleBranchFromControl(string key, MouseEventArgs released)
    {
        if (RowPress.WasSelectionStandingStill(released))
        {
            return;
        }

        if (!_expandedBranches.Add(key))
        {
            _expandedBranches.Remove(key);
        }
    }

    /// <summary>Open every branch of every facet tree, or shut all of them.</summary>
    /// <remarks>
    /// Walked off <see cref="FacetGroups"/> rather than remembered as a flag, so a branch the
    /// reader shuts after Utvid alle stays shut the way a facet does.
    /// </remarks>
    private void FoldAllBranches(bool open)
    {
        _expandedBranches.Clear();

        if (!open)
        {
            return;
        }

        foreach (var group in FacetGroups)
        {
            OpenBranches(group.Values);
        }
    }

    /// <summary>Open every branch under <paramref name="values"/>, and everything under those.</summary>
    /// <remarks>
    /// Descends only into a key it had not already opened, so a payload whose nodes name each other
    /// as parent terminates here: a stack overflow is process-fatal and cannot be caught.
    /// </remarks>
    private void OpenBranches(IReadOnlyList<FacetValue> values)
    {
        foreach (var value in values)
        {
            if (value.Children.Count > 0 && _expandedBranches.Add(value.Key))
            {
                OpenBranches(value.Children);
            }
        }
    }

    private bool _chosenBranchesOpened;

    /// <summary>Open the branches standing between the top of each facet tree (Kilde, Variabelgruppe, Filter) and what the address chose.</summary>
    /// <remarks>
    /// Arriving from the Kildeutforsker with a kilde chosen left it under a shut kildetype heading, out of sight
    /// (#6756). Once per mount (VariableExplorer remounts on each arrival), so a branch the reader shuts stays shut.
    /// </remarks>
    private void OpenBranchesToChosenOnce()
    {
        if (_chosenBranchesOpened || _facets is not { } facets)
        {
            return;
        }

        _chosenBranchesOpened = true;
        // Every hierarchical facet the same way, so a filter or a variabelgruppe chosen on a shared link is in
        // sight as a kilde is (Fhi.Metadata-l9l2n.159); every other branch stays shut.
        HashSet<string> walked = [];
        OpenChosenBranches(KildeGroup(facets).Values, walked);
        OpenChosenBranches(VariabelgruppeGroup(facets).Values, walked);
        OpenChosenBranches(SavedFilterGroup(facets).Values, walked);
    }

    private void OpenChosenBranches(IReadOnlyList<FacetValue> values, HashSet<string> walked)
    {
        foreach (var value in values)
        {
            if (value.ChosenBelow > 0 && value.Children.Count > 0 && walked.Add(value.Key))
            {
                _expandedBranches.Add(value.Key);
                OpenChosenBranches(value.Children, walked);
            }
        }
    }
}
