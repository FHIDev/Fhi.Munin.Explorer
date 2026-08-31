using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One column of a variable row, in <c>munin-explorer-dataitem-main__column</c> shape.
/// </summary>
/// <remarks>
/// Shared by the search results and the reader's saved lists, which draw the same columns of the
/// same catalogue. Written twice they looked alike and could stop being alike without anything
/// failing — an attribute or a rule for an empty value added to one is not inherited by the other.
/// </remarks>
internal static class RowCell
{
    /// <summary>
    /// Sequence numbers one cell consumes. A caller drawing several must space them at least
    /// this far apart — see the sequence note in <see cref="Write"/>.
    /// </summary>
    internal const int Slots = 14;

    /// <summary>
    /// Draws one cell.
    /// </summary>
    /// <remarks>
    /// The field name is not shown in the cell — the column header names it. It is still emitted
    /// for assistive technology, because a screen reader moving down a column has no header to
    /// glance up at.
    /// <para>
    /// <paramref name="catalogue"/> says whose words the value is. Nearly always the catalogue's,
    /// which are Norwegian whatever the reader's language is, so they are marked <c>lang="no"</c>.
    /// A column the component composes itself — the dataperiode — is in the reader's language
    /// already and is left unmarked, exactly like the "Ikke oppgitt" beside it, so it inherits the
    /// host page's language rather than claiming a language it is not in.
    /// </para>
    /// </remarks>
    internal static void Write(
        RenderTreeBuilder builder,
        int seq,
        string label,
        string? value,
        string? key,
        string notSpecified,
        string? tooltip = null,
        bool catalogue = true)
    {
        // Sequence numbers ascend without gaps or repeats through every path below. Blazor uses
        // them positionally to diff one render against the next, so a number that goes backwards
        // makes the renderer compare the wrong nodes — an earlier version emitted seq+15 before
        // seq+2 and would have diffed the label span against the value span.
        builder.OpenElement(seq, "div");
        builder.AddAttribute(seq + 1, "class",
            key is null
                ? "munin-explorer-dataitem-main__column"
                : $"munin-explorer-dataitem-main__column munin-explorer-dataitem-main__{key}");

        // The cell, which is what this element has always been called in the comments here and is
        // now what it is. Without the role the value and the header above it were two unrelated
        // runs of text, so nothing said which column a value belonged to (WCAG 1.3.1).
        builder.AddAttribute(seq + 2, "role", "cell");

        // The full value as a tooltip on the CELL, because a cell can be clipped — the code column
        // truncates rather than wraps, since a broken identifier is neither readable nor copyable.
        // A column may show a shorter form than the value it holds: kilde shows the short name.
        var hoverText = string.IsNullOrWhiteSpace(tooltip) ? value : tooltip;

        if (!string.IsNullOrWhiteSpace(hoverText))
        {
            builder.AddAttribute(seq + 3, "title", hoverText);
        }

        // The field name, for assistive technology only. The column header names it on screen, so
        // showing it in every cell as well would undo what the header is for — but a screen reader
        // moving down a column has no header to glance up at, so the name has to travel with the
        // value or "Inklusjon" means nothing.
        //
        // NOT an aria-label on the value: aria-label REPLACES the text it labels, so a reader would
        // hear the field name instead of the value. screenreader-only is Stiler's own class for
        // this, 16 rules in the site-wide stylesheet.
        builder.OpenElement(seq + 4, "span");
        builder.AddAttribute(seq + 5, "class", "screenreader-only");
        builder.AddContent(seq + 6, $"{label}: ");
        builder.CloseElement();

        builder.OpenElement(seq + 7, "span");
        builder.AddAttribute(seq + 8, "class", "munin-explorer-dataitem-main__column__text");

        if (string.IsNullOrWhiteSpace(value))
        {
            builder.AddContent(seq + 9, notSpecified);
        }
        else if (catalogue)
        {
            // The label follows Language; the value does not. Munin's metadata is Norwegian
            // whatever language the surrounding UI is in, and an English speech synthesiser
            // reading Norwegian variable names is unintelligible (WCAG 3.1.2).
            builder.OpenElement(seq + 10, "span");
            builder.AddAttribute(seq + 11, "lang", "no");
            builder.AddContent(seq + 12, value);
            builder.CloseElement();
        }
        else
        {
            // The component's own words, in the reader's language. Unmarked, so it inherits the
            // host page's language the same way every other string this component composes does.
            builder.AddContent(seq + 13, value);
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
