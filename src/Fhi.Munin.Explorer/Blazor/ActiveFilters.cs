using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The row of removable chips over a result list: one per ticked facet value, with a control that
/// clears the lot.
/// </summary>
/// <remarks>
/// <para>
/// What it is for: a panel can be folded away, scrolled past or simply long, so the only thing on
/// screen saying the list is narrowed was a clause in the count line. The chips say which values,
/// and give each one a way back that is beside the results rather than back inside the panel.
/// </para>
/// <para>
/// It writes no state of its own. Every chip's remove and the clear-all are the caller's handlers
/// over the caller's own selection, so the checkboxes in the panel and the chips over the table can
/// never disagree about which filters the list obeys — which they would the moment a chip row grew
/// a collection of its own to draw from.
/// </para>
/// <para>
/// Three names under this package's prefix: <c>munin-explorer-filters__active</c> is the row,
/// <c>munin-explorer-filters__chip</c> the capsule and <c>munin-explorer-filters__chip-remove</c>
/// the close control inside it. Their rules are <c>Fhi.Metadata-l9l2n.51</c>'s, on
/// <c>Fhi.Helsedata.Stiler</c> PR 39206 and not in 0.1.42, the version pinned here. The other
/// two elements wear Stiler's own names rather than new ones — the heading is a
/// <c>caption margin--none</c> paragraph, where <c>margin--none</c> is load-bearing because a bare
/// <c>caption</c> paragraph carries block margins that break the row's alignment, and the clear-all
/// is an <c>hd-button-square button-square--ghost</c> — Stiler's own name for a ghost button, the
/// one the facet panel's fold toggle already wears, so the control is new and the name is not.
/// </para>
/// <para>
/// The capsule is a <c>&lt;span&gt;</c> holding a <c>&lt;button&gt;</c>, not a button itself: one
/// control per thing that can be pressed, and the only thing that can be pressed here is the
/// removal. It carries no <c>aria-pressed</c> for the same reason — a chip is not a toggle showing
/// a state, it is the state, and it is gone once the value is. The close control must not wear
/// <c>hd-button-square</c>: that form is 2.75rem tall and would burst the 24×24 box Stiler gives it.
/// </para>
/// <para>
/// A pressed chip takes itself off the page, so the caller is handed the press rather than told
/// about it afterwards: <see cref="Chip.Remove"/> and the clear-all are both awaited, which is what
/// lets a caller move focus somewhere that still exists before the render that removes the control
/// the reader is standing on. Without that the focus lands on <c>&lt;body&gt;</c> and the next Tab
/// starts at the top of the host's page.
/// </para>
/// <para>
/// Written for either explorer though only the kildeutforsker draws it today, the bargain
/// <see cref="ColumnPicker"/> makes: the markup is a row of labels and two borrowed class names,
/// and a second copy would be a second place for those to drift from Stiler. The two panels'
/// selection state does not match and does not have to — Kelda holds a dictionary of ticked values
/// and the variable explorer a <c>VariableFilter</c> whose removals refetch — because what differs
/// is the projection into <see cref="Chip"/>, which each caller writes, and not the row.
/// </para>
/// </remarks>
internal static class ActiveFilters
{
    /// <summary>One active filter, as the row draws it.</summary>
    /// <param name="Text">The value as the panel's own checkbox shows it, cut to the same length.</param>
    /// <param name="RemoveLabel">
    /// The remove control's whole accessible name. It names the value, because a row of controls
    /// all announcing "Fjern" is a row a screen reader cannot tell apart.
    /// </param>
    /// <param name="Title">The whole value where <paramref name="Text"/> was cut, and nothing where it was not.</param>
    /// <param name="Language">
    /// The language <paramref name="Text"/> is written in, or null where it is the reader's own or
    /// belongs to no language at all. Null means "do not mark this".
    /// </param>
    /// <param name="Remove">
    /// Clears this one value, through whatever state the panel's own checkbox writes. Awaited, so a
    /// caller can hand focus on before the chip is gone.
    /// </param>
    internal readonly record struct Chip(
        string Text, string RemoveLabel, string? Title, string? Language, Func<Task> Remove);

    /// <summary>The row.</summary>
    /// <param name="receiver">The component whose state a press changes. <c>IHandleEvent</c> and not
    /// <c>object</c>, because that is what <c>EventCallback.InvokeAsync</c> dispatches through — an
    /// <c>object</c> receiver is pressed and never redrawn.</param>
    /// <param name="heading">The row's own word — "Aktive filtre".</param>
    /// <param name="chips">The ticked values, in the order the panel lists them.</param>
    /// <param name="clearAllLabel">The clear-all's word — the panel's own "Fjern alle filtre".</param>
    /// <param name="clearAll">Clears every value in one write of the same state.</param>
    /// <remarks>
    /// Nothing at all when <paramref name="chips"/> is empty, heading and clear-all included: a row
    /// saying "Aktive filtre" over no chips, beside a button that would clear nothing, is furniture
    /// that reads as a filter the reader cannot see.
    /// </remarks>
    internal static RenderFragment For(
        IHandleEvent receiver,
        string heading,
        IReadOnlyList<Chip> chips,
        string clearAllLabel,
        Func<Task> clearAll) => builder =>
    {
        if (chips.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-filters__active");

        // A heading rather than a landmark or a list: the row is one line of a component that is
        // already a section of somebody else's page, and its own count line says the same fact in
        // words one element away.
        builder.OpenElement(2, "p");
        builder.AddAttribute(3, "class", "caption margin--none");
        builder.AddContent(4, heading);
        builder.CloseElement();

        foreach (var chip in chips)
        {
            // Deliberately unkeyed. The obvious key is the value, and two facets can label a value
            // the same word — which is a duplicate key, and the renderer throws on one. Nothing
            // rests on the identity here: focus has left the row before the chip goes.
            builder.OpenElement(5, "span");
            builder.AddAttribute(6, "class", "munin-explorer-filters__chip");

            // An element of its own around the value, so lang covers the catalogue's words and
            // stops before the button, whose accessible name is this package's prose: on the
            // capsule it inherits, and an English page then speaks that name in Norwegian.
            builder.OpenElement(7, "span");
            builder.AddAttribute(8, "lang", chip.Language);
            builder.AddAttribute(9, "title", chip.Title);
            builder.AddContent(10, chip.Text);
            builder.CloseElement();

            builder.OpenElement(11, "button");
            builder.AddAttribute(12, "class", "munin-explorer-filters__chip-remove");
            builder.AddAttribute(13, "type", "button");
            // The glyph is not a name, so the name is written down. It replaces the × rather than
            // adding to it. (Fhi.Metadata-ag4n7)
            builder.AddAttribute(14, "aria-label", chip.RemoveLabel);
            builder.AddAttribute(15, "onclick", EventCallback.Factory.Create(receiver, chip.Remove));
            builder.AddContent(16, "×");
            builder.CloseElement();

            builder.CloseElement();
        }

        // Last, after the chips, so removing one moves nothing the reader is aiming at — the rule
        // the selection bar follows for the same reason.
        builder.OpenElement(17, "button");
        builder.AddAttribute(18, "class", "hd-button-square button-square--ghost");
        builder.AddAttribute(19, "type", "button");
        builder.AddAttribute(20, "onclick", EventCallback.Factory.Create(receiver, clearAll));
        builder.AddContent(21, clearAllLabel);
        builder.CloseElement();

        builder.CloseElement();
    };
}
