using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The column picker above both explorers' results: a list menu of one checkbox per optional column.</summary>
/// <remarks>
/// A list menu, not Stiler's <c>&lt;details&gt;</c> dropdown, so it opens as the others do (Fhi.Metadata-mhqx0.8).
/// </remarks>
internal static class ColumnPicker
{
    /// <summary>One column, as the picker draws it.</summary>
    /// <remarks>
    /// The label is the word the header above the column uses, so the picker and the column it
    /// turns off are never two names for one thing, and the toggle is the caller's own rule —
    /// visibility and locking are decided there, and drawn here.
    /// </remarks>
    internal readonly record struct Choice(string Label, bool Visible, bool Locked, Func<Task> Toggle);

    /// <summary>The picker.</summary>
    /// <param name="receiver">The component whose state a press changes. <c>IHandleEvent</c> and not
    /// <c>object</c>, because that is what <c>EventCallback.InvokeAsync</c> dispatches through — an
    /// <c>object</c> receiver is pressed and never redrawn. The interface is the floor, not a
    /// guarantee: what redraws is <c>ComponentBase</c> handling the event.</param>
    /// <param name="fold">Whether the picker is open, its ids and its keys, held by the component.</param>
    /// <param name="buttonLabel">The trigger's word — "Kolonner".</param>
    /// <param name="closeLabel">The word on the «Lukk» row a phone's bottom sheet shows.</param>
    /// <param name="choices">The columns, in the order the picker lists them.</param>
    /// <param name="before">A control drawn in the same row ahead of the picker, or none.</param>
    /// <param name="hint">Why the last column refuses, and the id a locked checkbox points at.
    /// Absent when no column can lock.</param>
    /// <remarks>
    /// One parameter and not two, so an id without a sentence — an <c>aria-describedby</c> pointing
    /// at nothing — cannot be written at all. Optional because only the variable explorer can lock
    /// a column: the kilde table draws Navn, Status and Opprettet whatever its picker says.
    /// </remarks>
    internal static RenderFragment For(
        IHandleEvent receiver,
        ColumnsFold fold,
        string buttonLabel,
        string closeLabel,
        IReadOnlyList<Choice> choices,
        (string Id, string Text)? hint = null,
        RenderFragment? before = null) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-header");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "munin-explorer-header__actions");

        // Another control of the same row, before the picker: «Last ned utvalg» stands beside «Kolonner».
        builder.AddContent(4, before);

        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "class", "munin-explorer-list-menu");
        builder.AddAttribute(7, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(receiver, fold.CloseOnEscapeAsync));

        // Dressed as their ghost square button, with the chevron for the state the component holds.
        builder.OpenElement(8, "button");
        builder.AddAttribute(9, "class", "hd-button-square button-square--ghost munin-explorer-header__actions-button");
        builder.AddAttribute(10, "type", "button");
        builder.AddAttribute(11, "id", fold.ToggleId);
        builder.AddAttribute(12, "aria-expanded", fold.Open ? "true" : "false");
        builder.AddAttribute(13, "aria-controls", fold.PanelId);
        builder.AddAttribute(14, "onclick", EventCallback.Factory.Create<MouseEventArgs>(receiver, fold.Press));
        builder.AddElementReferenceCapture(15, element => fold.Toggle = element);
        builder.OpenElement(16, "span");
        builder.AddAttribute(17, "class", "icon icon-layout");
        builder.AddAttribute(18, "aria-hidden", "true");
        builder.CloseElement();
        builder.AddContent(19, buttonLabel);
        builder.OpenElement(20, "span");
        builder.AddAttribute(21, "class", fold.Open ? "icon icon--right icon-keyboard-arrow-up" : "icon icon--right icon-keyboard-arrow-down");
        builder.AddAttribute(22, "aria-hidden", "true");
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(23, "div");
        builder.AddAttribute(24, "class", "munin-explorer-list-menu__panel munin-explorer-list-menu__panel--rows");
        builder.AddAttribute(25, "id", fold.PanelId);
        builder.AddAttribute(26, "role", "group");
        builder.AddAttribute(27, "aria-labelledby", fold.ToggleId);
        builder.AddAttribute(28, "hidden", !fold.Open);

        foreach (var choice in choices)
        {
            builder.OpenElement(29, "label");
            builder.AddAttribute(30, "class", "munin-explorer-list-menu__check");

            builder.OpenElement(31, "input");
            builder.AddAttribute(32, "type", "checkbox");
            builder.AddAttribute(33, "checked", choice.Visible);
            // Inert rather than disabled: `disabled` takes the control out of the tab order, so the one column
            // a reader might want to ask about would be the one they could not reach.
            builder.AddAttribute(34, "aria-disabled", choice.Locked ? "true" : null);
            builder.AddAttribute(35, "aria-describedby", choice.Locked ? hint?.Id : null);
            // The toggle flips what the caller holds; the browser's own tick is written back after a refused press.
            builder.AddAttribute(36, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(receiver, _ => choice.Toggle()));
            builder.SetUpdatesAttributeName("checked");
            builder.CloseElement();

            builder.AddContent(37, " ");
            builder.AddContent(38, choice.Label);
            builder.CloseElement();
        }

        // Why the last one refuses, said once rather than on every checkbox (and in the DOM whenever one can lock).
        if (hint is { } sentence)
        {
            builder.OpenElement(39, "p");
            builder.AddAttribute(40, "class", "screenreader-only");
            builder.AddAttribute(41, "id", sentence.Id);
            builder.AddContent(42, sentence.Text);
            builder.CloseElement();
        }

        // Shown only while the panel is a phone's bottom sheet; explorer-interop.js shows it and acts on it.
        builder.OpenElement(43, "button");
        builder.AddAttribute(44, "class", "munin-explorer-list-menu__close");
        builder.AddAttribute(45, "type", "button");
        builder.AddAttribute(46, "hidden", true);
        builder.AddAttribute(47, "style", "display: none");
        builder.AddContent(48, closeLabel);
        builder.CloseElement();

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    };
}
