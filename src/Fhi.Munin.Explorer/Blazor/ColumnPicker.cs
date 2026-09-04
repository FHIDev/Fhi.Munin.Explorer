using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The column picker both explorers hang above their results: a disclosure holding one toggle per
/// optional column.
/// </summary>
/// <remarks>
/// <para>
/// helsedata's own shape, read off their compiled stylesheets rather than guessed at. Their
/// variable page hangs this above the results in <c>munin-explorer-header__actions</c> and draws
/// the open list as <c>ul.dropdown-choicepicker</c> with one <c>li.dropdown-choicepicker__item</c>
/// per choice — the list is <c>position: absolute</c> under a trigger whose container is relative,
/// which is what the inline style below supplies, exactly as their own React does inline.
/// </para>
/// <para>
/// It is deliberately NOT <c>sortable-dropdown</c>, which the bead behind the first of these
/// pointed at. That name is their MOBILE sort control — <c>.sortable-dropdown { display: none }</c>
/// site-wide, revived only under <c>max-width: 1280px</c> — so wearing it would have hidden the
/// picker on every desktop, silently, which is the failure mode this repository keeps
/// rediscovering.
/// </para>
/// <para>
/// A <c>&lt;details&gt;</c> rather than a button and a panel, for the reason the filter facets are:
/// their dropdown opens, closes on Escape and closes on an outside click from React state, and this
/// package ships no script. The element does the first two natively and costs nothing; what is lost
/// is the outside click, which leaves the list open rather than broken.
/// </para>
/// <para>
/// It wears two names, not one, and both are borrowed from Stiler: <c>munin-explorer__dropdown</c>
/// is the z-index that lifts the open list over the rows below it
/// (<c>.munin-explorer__dropdown { z-index: 99 }</c>), and the bare <c>dropdown</c> is what widens
/// the trigger to its row — <c>variables.css</c> carries
/// <c>.munin-explorer-header__actions .dropdown { width: 100% }</c>, unconditionally, beside the
/// <c>dropdown-choicepicker</c> rule that puts the open list 36px down. Both were read back off the
/// compiled stylesheet; neither is ours.
/// </para>
/// <para>
/// Choosing <c>&lt;details&gt;</c> costs a host outside helsedata's estate two rules the element
/// itself makes necessary: a <c>&lt;summary&gt;</c> is <c>display: list-item</c>, so without
/// <c>list-style: none</c> and <c>::-webkit-details-marker { display: none }</c> the trigger draws
/// a browser disclosure triangle beside "Kolonner" that their own button does not have. Both sample
/// hosts carry them, and the host notes say so — helsedata's own control is a button, so nothing in
/// their <c>variables.css</c> has a reason to suppress a marker here. The filter panel's
/// <c>&lt;details&gt;</c> is not a precedent: its summary is not dressed as a button, so its marker
/// is wanted.
/// </para>
/// <para>
/// Toggle buttons rather than checkboxes, which is what the facet values already are. Their own
/// items are a visually-hidden <c>checkbox__input</c> with a label drawing the box, and that
/// pattern needs the DOM's checked state and the component's to agree — a refusal to hide the last
/// column would leave the browser showing a box the component believes is still ticked. A button
/// carries no state of its own, so <c>aria-pressed</c> is the whole truth.
/// </para>
/// <para>
/// One copy for both explorers rather than two, because every paragraph above is either a borrowed
/// name or a decision about the element itself, and a second copy is a second place for them to
/// drift from Stiler. What differs between the callers is which columns there are and whether one
/// can lock, and both arrive as arguments (Fhi.Metadata-ay3zz).
/// </para>
/// </remarks>
internal static class ColumnPicker
{
    /// <summary>One column, as the picker draws it.</summary>
    /// <param name="Label">The word the header above the column uses, so the picker and the column
    /// it turns off are never two names for one thing.</param>
    /// <param name="Visible">Whether the column is on screen.</param>
    /// <param name="Locked">Whether it refuses to be turned off.</param>
    /// <param name="Toggle">What a press does. The caller owns the rule; this only draws it.</param>
    internal readonly record struct Choice(string Label, bool Visible, bool Locked, Action Toggle);

    /// <summary>The picker.</summary>
    /// <param name="receiver">The component whose state a press changes, so Blazor re-renders it.</param>
    /// <param name="buttonLabel">The trigger's word — "Kolonner".</param>
    /// <param name="choices">The columns, in the order the picker lists them.</param>
    /// <param name="hintId">Where a locked column points. Null when no column can lock.</param>
    /// <param name="hint">Why the last one refuses. Null when no column can lock.</param>
    /// <remarks>
    /// The hint is optional because only one caller can lock a column. The variable explorer's
    /// seven are every column its rows carry, so hiding all of them would leave rows of nothing but
    /// names; the kilde table draws Navn, Status and Opprettet whatever the picker says, so there is
    /// nothing there for a lock to prevent — and a hint no button points at is a paragraph a screen
    /// reader would meet for no reason.
    /// </remarks>
    internal static RenderFragment For(
        object receiver,
        string buttonLabel,
        IReadOnlyList<Choice> choices,
        string? hintId = null,
        string? hint = null) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-header");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "munin-explorer-header__actions");

        builder.OpenElement(4, "details");
        // Two of their names rather than one: `dropdown` is the width rule their own actions row
        // applies to the trigger, `munin-explorer__dropdown` the z-index under the open list.
        // Both are in the host contract — see the remarks above.
        builder.AddAttribute(5, "class", "dropdown munin-explorer__dropdown");
        // Their own inline style, not a stylesheet: the list below is absolutely positioned and
        // anchors to the nearest positioned ancestor, and helsedata's React sets exactly this on
        // the same element. Without it an open list would hang off whatever happens to be
        // positioned further up the host's page.
        builder.AddAttribute(6, "style", "position:relative");

        // Dressed as their ghost square button, which is what their own trigger is. A <summary>
        // is display: list-item, so a host has to take the disclosure marker off it — two rules,
        // both in the host notes, and both sample hosts carry them.
        builder.OpenElement(7, "summary");
        builder.AddAttribute(8, "class",
            "hd-button-square button-square--ghost munin-explorer-header__actions-button");
        builder.AddContent(9, buttonLabel);
        builder.CloseElement();

        builder.OpenElement(10, "ul");
        builder.AddAttribute(11, "class", "dropdown-choicepicker dropdown-choicepicker--right");

        foreach (var choice in choices)
        {
            builder.OpenElement(12, "li");
            builder.AddAttribute(13, "class", "dropdown-choicepicker__item");

            builder.OpenElement(14, "button");
            builder.AddAttribute(15, "class", "hd-button-reset");
            builder.AddAttribute(16, "type", "button");
            builder.AddAttribute(17, "aria-pressed", choice.Visible ? "true" : "false");
            // Inert rather than disabled, the same treatment the pager's buttons and Fjern alle
            // filtre get: `disabled` takes the control out of the tab order, so the one column a
            // reader might want to ask about would be the one they could not reach. The caller's
            // own toggle is what makes the refusal true.
            builder.AddAttribute(18, "aria-disabled", choice.Locked ? "true" : null);
            builder.AddAttribute(19, "aria-describedby", choice.Locked ? hintId : null);
            builder.AddAttribute(20, "onclick", EventCallback.Factory.Create(receiver, choice.Toggle));

            // The label as the button's own text, with no element and so no class name around it.
            // An earlier draft wrapped it in a span wearing `form-control__label`, which is a name
            // nothing else in this component uses and which could not be read back off Stiler's
            // compiled stylesheet — the one thing AGENTS.md says must never be guessed at, because
            // a name Stiler has never heard of renders as a raw browser default. The wrapper bought
            // nothing either: the item is a flex row and the button an inline-flex box, so a bare
            // text node is centred by the rules already on them.
            builder.AddContent(21, choice.Label);

            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();

        // Why the last one refuses, said once and pointed at rather than repeated on every button.
        // Always in the DOM while a column CAN lock, so the reference is never dangling:
        // aria-describedby resolves against hidden text, and a paragraph that appeared only when a
        // column locked would be one more node arriving in the same update as the attribute naming
        // it.
        if (hintId is not null && hint is not null)
        {
            builder.OpenElement(22, "p");
            builder.AddAttribute(23, "class", "screenreader-only");
            builder.AddAttribute(24, "id", hintId);
            builder.AddContent(25, hint);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    };
}
