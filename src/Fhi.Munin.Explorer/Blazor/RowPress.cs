using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The pointer gesture on a result row: where it went down, and whether the click that ended it was
/// a reader selecting text rather than pressing the row.
/// </summary>
/// <remarks>
/// One rule over two markups — the variabelutforsker's rows and Kelda's — and over the row and the
/// controls inside it, which have to answer it the same way or a drag across a name acts on the row
/// it was copied from. Written out at each of those places it would drift the way the two row
/// renderers did before <see cref="RowCell"/>.
/// </remarks>
internal sealed class RowPress
{
    /// <summary>CSS pixels the pointer may travel between press and release and still be a press.</summary>
    /// <remarks>
    /// Under a character's width, so selecting even one letter of a code reads as the drag it is;
    /// above a shaky hand, so an ordinary click still opens what the row opens.
    /// </remarks>
    private const double Slack = 4;

    private (Guid Row, double X, double Y)? _wentDown;

    private Guid? _dragged;

    /// <summary>Take note of a press going down inside <paramref name="row"/>.</summary>
    /// <remarks>
    /// Recorded for the controls in the row as well as the space between them: a reader dragging
    /// from the name across the row is selecting text, and the browser lands that gesture's click
    /// on the row rather than on the control it began in.
    /// </remarks>
    internal void Pressed(Guid row, MouseEventArgs pressed) =>
        _wentDown = (row, pressed.ClientX, pressed.ClientY);

    /// <summary>Settle whether the gesture ending on <paramref name="row"/> travelled across it.</summary>
    /// <remarks>
    /// Only a press this row saw go down is measured, and the record of it is spent here either
    /// way: a gesture is over once the pointer comes up, and one left standing is what the release
    /// after it — a selection begun off the list and let go over a row — would be measured against.
    /// </remarks>
    internal void Released(Guid row, MouseEventArgs released)
    {
        var wentDown = _wentDown;
        _wentDown = null;

        _dragged = wentDown is { } start
            && start.Row == row
            && (Math.Abs(released.ClientX - start.X) > Slack
                || Math.Abs(released.ClientY - start.Y) > Slack)
            ? row
            : null;
    }

    /// <summary>
    /// Whether <paramref name="clicked"/> on <paramref name="row"/> was a reader selecting text
    /// rather than pressing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All three gestures in one place, because the row and every control inside it that stops the
    /// click has to read them alike: distance misses the two that stand still, since a double-click
    /// takes a word and a shift-click extends the selection to it.
    /// </para>
    /// <para>
    /// A click reporting no count is one no pointer gesture produced — how the keyboard and
    /// assistive tooling activate a row — so it is a press, and no verdict is read against it. That
    /// is also what makes a verdict nobody asks for harmless: a gesture that ends inside one of
    /// those controls lands its click there, where it stops, so no click of the row's is left to
    /// read the verdict its release found. (Fhi.Metadata-l9l2n.81)
    /// </para>
    /// </remarks>
    internal bool WasSelection(Guid row, MouseEventArgs clicked) =>
        clicked.Detail > 0 && (_dragged == row || clicked.Detail > 1 || clicked.ShiftKey);
}
