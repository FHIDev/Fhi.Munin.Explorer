using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The row of removable chips over a result list, one per active filter, with a clear-all.</summary>
/// <remarks>
/// It writes no state of its own, so the panel and the chips cannot disagree, and it awaits each press so
/// the caller can move focus before the chip it stands on is gone.
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
    /// <param name="receiver">The component a press redraws: an <c>object</c> receiver would never be redrawn.</param>
    /// <param name="heading">The row's own word — "Aktive filtre".</param>
    /// <param name="chips">The ticked values, in the order the panel lists them.</param>
    /// <param name="clearAllLabel">The clear-all's word — the panel's own "Fjern alle filtre".</param>
    /// <param name="clearAll">Clears every value in one write of the same state.</param>
    /// <param name="edit">A second action before the clear-all, as its label and its press; null for none.</param>
    /// <remarks>Nothing at all when <paramref name="chips"/> is empty: a heading over no chips reads as a hidden filter.</remarks>
    internal static RenderFragment For(
        IHandleEvent receiver,
        string heading,
        IReadOnlyList<Chip> chips,
        string clearAllLabel,
        Func<Task> clearAll,
        (string Label, Func<Task> Press)? edit = null) => builder =>
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
        // margin--none is load-bearing: a bare caption paragraph's margins break the row's alignment.
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

            // Not hd-button-square: that form is 2.75rem tall and would burst the 24×24 box Stiler gives it.
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

        if (edit is { } action)
        {
            builder.OpenElement(17, "button");
            builder.AddAttribute(18, "class", "hd-button-square button-square--ghost");
            builder.AddAttribute(19, "type", "button");
            builder.AddAttribute(20, "onclick", EventCallback.Factory.Create(receiver, action.Press));
            builder.AddContent(21, action.Label);
            builder.CloseElement();
        }

        // Last, after the chips, so removing one moves nothing the reader is aiming at — the rule
        // the selection bar follows for the same reason.
        builder.OpenElement(22, "button");
        builder.AddAttribute(23, "class", "hd-button-square button-square--ghost");
        builder.AddAttribute(24, "type", "button");
        builder.AddAttribute(25, "onclick", EventCallback.Factory.Create(receiver, clearAll));
        builder.AddContent(26, clearAllLabel);
        builder.CloseElement();

        builder.CloseElement();
    };
}
