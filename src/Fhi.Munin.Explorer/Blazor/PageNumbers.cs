using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The numbered page buttons between Forrige and Neste, in
/// <c>munin-explorer-pagination-pages</c> shape.
/// </summary>
/// <remarks>
/// Shared by the result list and the reader's saved lists, which draw one pager over one set of
/// class names. Written twice they would drift the way the two row renderers did before
/// <see cref="RowCell"/>: an accessible name or a boundary fixed in one and not the other.
/// </remarks>
internal static class PageNumbers
{
    /// <summary>How many numbers are on screen around the page in force, at most.</summary>
    private const int WindowSize = 3;

    /// <summary>The pages to draw, in order, with <c>null</c> where the run skips ahead.</summary>
    /// <remarks>
    /// First, last, and three around the page in force — helsedata's own run, "1 2 3 … 100". The
    /// three slide rather than shrink at the ends, so the count does not change under the reader.
    /// A skip standing for one page is drawn as that page: an ellipsis is no narrower, and hides it.
    /// </remarks>
    internal static IReadOnlyList<int?> Window(int page, int totalPages)
    {
        var last = Math.Max(1, totalPages);
        var current = Math.Clamp(page, 1, last);

        var start = Math.Clamp(current - 1, 1, Math.Max(1, last - WindowSize + 1));
        var end = Math.Min(last, start + WindowSize - 1);

        var shown = new SortedSet<int> { 1, last };

        for (var candidate = start; candidate <= end; candidate++)
        {
            shown.Add(candidate);
        }

        var run = new List<int?>();
        var previous = 0;

        foreach (var number in shown)
        {
            if (previous > 0 && number - previous > 1)
            {
                run.Add(number - previous == 2 ? previous + 1 : null);
            }

            run.Add(number);
            previous = number;
        }

        return run;
    }

    /// <summary>Draws the run for <paramref name="page"/> of <paramref name="totalPages"/>.</summary>
    /// <remarks>
    /// The page in force is filled and the rest are ghosts, which is Stiler's own pair: a host with
    /// Stiler alone shows which page it is on, owing this package no stylesheet. It carries no
    /// <c>aria-disabled</c> — <c>aria-current</c> says why it does nothing, and grey would fight the fill.
    /// </remarks>
    internal static RenderFragment Write(
        object receiver,
        int page,
        int totalPages,
        Func<int, Task> goToPage,
        Func<int, string> goToLabel,
        Func<int, string> currentLabel) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-pagination-pages");

        foreach (var number in Window(page, totalPages))
        {
            if (number is not { } target)
            {
                // Decoration, and announced as nothing: a screen reader has the numbers either
                // side and Forrige and Neste, so an ellipsis read out is a page that is not there.
                builder.OpenElement(2, "span");
                builder.AddAttribute(3, "class", "caption margin-right");
                builder.AddAttribute(4, "aria-hidden", "true");
                builder.AddContent(5, "…");
                builder.CloseElement();

                continue;
            }

            var current = target == page;

            builder.OpenElement(6, "button");

            // Keyed by the page rather than by position: the run's length changes as the reader
            // moves through it — a skip opens where numbers were — so positionally the renderer
            // would patch the button under the finger into the page that took its place.
            builder.SetKey(target);

            builder.AddAttribute(7, "class",
                "hd-button-square margin-right "
                + (current ? "button-square--secondary" : "button-square--ghost"));
            builder.AddAttribute(8, "type", "button");
            builder.AddAttribute(9, "aria-current", current ? "page" : null);

            // A bare number names nothing on its own, so each carries the sentence helsedata's own
            // pager gives it. The digit on screen is inside that sentence, so a speech-input user
            // saying what they can see still reaches the button (WCAG 2.5.3).
            builder.AddAttribute(10, "aria-label",
                current ? currentLabel(target) : goToLabel(target));

            builder.AddAttribute(11, "onclick",
                EventCallback.Factory.Create(receiver, () => goToPage(target)));

            builder.AddContent(12, target);
            builder.CloseElement();
        }

        builder.CloseElement();
    };
}
