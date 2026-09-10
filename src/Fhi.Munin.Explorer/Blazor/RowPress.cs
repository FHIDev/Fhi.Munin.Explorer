using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The pointer gesture on a result row: where it went down, and whether the one that just ended
/// travelled far enough across the row to be a drag-selection rather than a press.
/// </summary>
/// <remarks>
/// One rule over two markups — the variabelutforsker's rows and Kelda's. Written twice they would
/// drift the way the two row renderers did before <see cref="RowCell"/>, and the lifecycle is where
/// that bites: a press recorded in one place and cleared in another swallows a later click.
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
    internal void Pressed(Guid row, MouseEventArgs pressed)
    {
        _wentDown = (row, pressed.ClientX, pressed.ClientY);

        // A new gesture is not the old one's release, and the verdict below belongs to one gesture.
        _dragged = null;
    }

    /// <summary>Settle what the gesture ending on <paramref name="row"/> was, for its own click.</summary>
    /// <remarks>
    /// Only a press this row saw go down is measured, and that is what binds the verdict to one
    /// gesture: a press released anywhere else leaves none, and a press that went down inside the
    /// row always ends in a click the row receives.
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

    /// <summary>Whether the gesture that ended on <paramref name="row"/> was a drag-selection.</summary>
    /// <remarks>
    /// Reading it spends it. A verdict left standing would be read against a later click that has
    /// no gesture behind it — which is how assistive tooling activates a row, and a row that
    /// refused it would be the control-that-does-nothing this guard exists to keep out.
    /// </remarks>
    internal bool Dragged(Guid row)
    {
        var dragged = _dragged == row;
        _dragged = null;

        return dragged;
    }
}
