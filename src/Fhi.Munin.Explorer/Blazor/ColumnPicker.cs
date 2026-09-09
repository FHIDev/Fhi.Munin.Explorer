using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The column picker both explorers hang above their results: a disclosure holding one checkbox per
/// optional column.
/// </summary>
/// <remarks>
/// <para>
/// helsedata's own shape, read off their compiled stylesheets rather than guessed at. Their
/// variable page hangs this above the results in <c>munin-explorer-header__actions</c> and draws
/// the open list as <c>ul.dropdown-choicepicker</c> with one <c>li.dropdown-choicepicker__item</c>
/// per choice — the list is <c>position: absolute</c> under a trigger whose container is relative,
/// which Stiler now supplies on <c>.munin-explorer__dropdown</c> itself (Fhi.Metadata-f6az7).
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
/// Real checkboxes, as helsedata's own are. The refusal to hide the last visible column is what
/// <c>SetUpdatesAttributeName("checked")</c> below answers, the same way the facet panel answers a
/// filter press it rolls back; the history is on Fhi.Metadata-f6az7.
/// </para>
/// <para>
/// One copy for both explorers: every paragraph above is a borrowed name or a fact about the
/// element, and a second copy is a second place for those to drift from Stiler (Fhi.Metadata-ay3zz).
/// </para>
/// </remarks>
internal static class ColumnPicker
{
    /// <summary>One column, as the picker draws it.</summary>
    /// <remarks>
    /// The label is the word the header above the column uses, so the picker and the column it
    /// turns off are never two names for one thing, and the toggle is the caller's own rule —
    /// visibility and locking are decided there, and drawn here.
    /// </remarks>
    internal readonly record struct Choice(string Label, bool Visible, bool Locked, Action Toggle);

    /// <summary>The picker.</summary>
    /// <param name="receiver">The component whose state a press changes. <c>IHandleEvent</c> and not
    /// <c>object</c>, because that is what <c>EventCallback.InvokeAsync</c> dispatches through — an
    /// <c>object</c> receiver is pressed and never redrawn. The interface is the floor, not a
    /// guarantee: what redraws is <c>ComponentBase</c> handling the event.</param>
    /// <param name="buttonLabel">The trigger's word — "Kolonner".</param>
    /// <param name="choices">The columns, in the order the picker lists them.</param>
    /// <param name="hint">Why the last column refuses, and the id a locked checkbox points at.
    /// Absent when no column can lock.</param>
    /// <remarks>
    /// One parameter and not two, so an id without a sentence — an <c>aria-describedby</c> pointing
    /// at nothing — cannot be written at all. Optional because only the variable explorer can lock
    /// a column: the kilde table draws Navn, Status and Opprettet whatever its picker says.
    /// </remarks>
    internal static RenderFragment For(
        IHandleEvent receiver,
        string buttonLabel,
        IReadOnlyList<Choice> choices,
        (string Id, string Text)? hint = null) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-header");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "munin-explorer-header__actions");

        builder.OpenElement(4, "details");
        // Two of their names rather than one: `dropdown` is the width rule their own actions row
        // applies to the trigger, `munin-explorer__dropdown` the z-index under the open list and,
        // since Fhi.Metadata-f6az7, the `position: relative` this element used to carry inline.
        builder.AddAttribute(5, "class", "dropdown munin-explorer__dropdown");

        // Dressed as their ghost square button, which is what their own trigger is. A <summary>
        // is display: list-item, so a host has to take the disclosure marker off it — two rules,
        // both in the host notes, and both sample hosts carry them.
        builder.OpenElement(6, "summary");
        builder.AddAttribute(7, "class",
            "hd-button-square button-square--ghost munin-explorer-header__actions-button");

        builder.OpenElement(8, "span");
        builder.AddAttribute(9, "class", "icon icon-layout");
        builder.AddAttribute(10, "aria-hidden", "true");
        builder.CloseElement();

        builder.AddContent(11, buttonLabel);

        // Both chevrons are drawn and Stiler hides the one contradicting the open state: nothing
        // here can swap a class, the package shipping no script, and the state is the element's.
        builder.OpenElement(12, "span");
        builder.AddAttribute(13, "class", "icon icon--right icon-keyboard-arrow-down");
        builder.AddAttribute(14, "aria-hidden", "true");
        builder.CloseElement();

        builder.OpenElement(15, "span");
        builder.AddAttribute(16, "class", "icon icon--right icon-keyboard-arrow-up");
        builder.AddAttribute(17, "aria-hidden", "true");
        builder.CloseElement();

        builder.CloseElement();

        builder.OpenElement(18, "ul");
        builder.AddAttribute(19, "class", "dropdown-choicepicker dropdown-choicepicker--right");

        foreach (var choice in choices)
        {
            builder.OpenElement(20, "li");
            builder.AddAttribute(21, "class", "dropdown-choicepicker__item");

            // Stiler's own names: `_choicepicker.scss` overrides `word-break` on
            // `form-control__label` INSIDE this item. Deliberately not `form-control__input`,
            // which hides the input for a drawn replacement this pattern has none of.
            builder.OpenElement(22, "label");
            builder.AddAttribute(23, "class", "form-control");

            builder.OpenElement(24, "input");
            builder.AddAttribute(25, "type", "checkbox");
            builder.AddAttribute(26, "checked", choice.Visible);
            // Inert rather than disabled, the same treatment the pager's buttons and Fjern alle
            // filtre get: `disabled` takes the control out of the tab order, so the one column a
            // reader might want to ask about would be the one they could not reach.
            builder.AddAttribute(27, "aria-disabled", choice.Locked ? "true" : null);
            builder.AddAttribute(28, "aria-describedby", choice.Locked ? hint?.Id : null);
            // The event's own value is ignored: the toggle flips what the caller holds, which is
            // the one state a press and the render after it are certain to agree about.
            builder.AddAttribute(29, "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(receiver, _ => choice.Toggle()));
            // What makes the refusal honest: without it the browser's own tick survives a press the
            // caller declined, because the renders either side are equal and an equal render writes
            // nothing back to the DOM.
            builder.SetUpdatesAttributeName("checked");
            builder.CloseElement();

            builder.OpenElement(30, "span");
            builder.AddAttribute(31, "class", "form-control__label");
            builder.AddContent(32, choice.Label);
            builder.CloseElement();

            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();

        // Why the last one refuses, said once rather than on every checkbox. In the DOM whenever a
        // column CAN lock, so the reference never dangles: one appearing with the attribute that
        // names it would arrive in the same update, which is where a reader loses it.
        if (hint is { } sentence)
        {
            builder.OpenElement(33, "p");
            builder.AddAttribute(34, "class", "screenreader-only");
            builder.AddAttribute(35, "id", sentence.Id);
            builder.AddContent(36, sentence.Text);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    };
}
