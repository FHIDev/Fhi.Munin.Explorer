using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The instrument page: the questionnaire or scale a variable was collected with, opened in place
/// of the list.
/// </summary>
/// <remarks>
/// A view rather than a route, the same move the whole variable makes and for the same reason —
/// the package has no router, so a page a host can link to is a key in the address bar and a view
/// this component swaps to.
/// </remarks>
public partial class VariableSearch
{
    /// <summary>
    /// The instrument whose page is open, or null when the reader is on the list. Set by the host,
    /// typically from its own URL, the same way <see cref="SelectedVariableId"/> is.
    /// </summary>
    /// <remarks>
    /// Read once, when the component initialises, and owned by the component afterwards. It wins
    /// over <see cref="SelectedVariableId"/> while it is set: a link carrying both was made on the
    /// instrument page, and the variable underneath is what leaving it goes back to.
    /// </remarks>
    [Parameter] public Guid? SelectedInstrumentId { get; set; }

    /// <summary>
    /// Raised when the open instrument changes, so the host can reflect it in its own URL. The
    /// SelectedInstrumentId/SelectedInstrumentIdChanged naming gives the host
    /// <c>@bind-SelectedInstrumentId</c> for free.
    /// </summary>
    /// <remarks>
    /// Supply it from a fully interactive parent: an <see cref="EventCallback"/> serialises to an
    /// empty delegate across a static-SSR boundary and then silently never fires.
    /// </remarks>
    [Parameter] public EventCallback<Guid?> SelectedInstrumentIdChanged { get; set; }

    /// <summary>
    /// An address for this page showing one instrument, by id. Null draws a variable's instruments
    /// as plain text instead of as links.
    /// </summary>
    /// <remarks>
    /// An address rather than a callback because a reader expects a page to be openable in a new
    /// tab, and because only the host knows where this component is mounted. Preserve the search's
    /// other state in it, so leaving the instrument returns the reader to what they came from.
    /// </remarks>
    [Parameter] public Func<Guid, string>? InstrumentHref { get; set; }

    /// <summary>An address for this search narrowed to one instrument's variables, by id.</summary>
    /// <remarks>
    /// Narrowed to that instrument and nothing else: the count the link names is the instrument's
    /// own, so a target carrying the reader's other facets would offer a number the page it opens
    /// does not have. Null draws no such link on the instrument page.
    /// </remarks>
    [Parameter] public Func<Guid, string>? InstrumentVariablesHref { get; set; }

    private Guid? _instrumentId;
    private InstrumentDetail? _instrument;
    private bool _instrumentLoading;

    // Set when the instrument could not be fetched, or when the API publishes no such instrument.
    // Its own field for the reason _detailError is: what failed is this view, and neither the rows
    // behind it nor the variable that linked here are stale because of it.
    private string? _instrumentError;

    // Not under the munin-explorer prefix, exactly as the whole variable's ids are not: these are
    // element ids rather than class names, and the prefix carries an inventory the package owes a
    // stylesheet rule for.
    private string InstrumentRegionId => $"munin-instrument-{_instance}";

    private string InstrumentHeadingId => $"munin-instrument-heading-{_instance}";

    private string InstrumentBusy => _instrumentLoading ? "true" : "false";

    /// <summary>What the instrument view's status line says: that it is loading, or why it is empty.</summary>
    private string? InstrumentStatus => _instrumentLoading ? T.InstrumentLoading : _instrumentError;

    /// <summary>Muted while it is loading, Stiler's infobox when something went wrong.</summary>
    private string InstrumentStatusClass => _instrumentError is null ? "caption" : "infobox infobox--bg-yellow";

    /// <summary>
    /// The heading the region is labelled by while the payload is still on its way or failed to
    /// come.
    /// </summary>
    /// <remarks>
    /// Drawn only then: once the instrument has arrived its own view owns the heading. A landmark
    /// whose label does not exist yet is worse than a plain one, which is why it exists at all.
    /// </remarks>
    private RenderFragment InstrumentHeading => builder =>
    {
        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "headline headline-s margin--bottom");
        builder.AddAttribute(2, "id", InstrumentHeadingId);
        builder.AddContent(3, T.EyebrowInstrument);
        builder.CloseElement();
    };

    /// <summary>A variable's instruments, each linking to its own page where there is one to link to.</summary>
    /// <remarks>
    /// Bare, as <see cref="DatasamlingList"/> beside it is: the panel's own rule styles an unclassed
    /// list, and a name Stiler has never heard of renders as a browser default.
    /// </remarks>
    private RenderFragment InstrumentList(IReadOnlyList<InstrumentReference> instruments) =>
        InstrumentBlock.Write(instruments, Reader, InstrumentHref);

    /// <summary>Open the instrument the host asked for, once the component has its parameters.</summary>
    /// <remarks>
    /// Independent of the result list, unlike <c>OpenInitialSelectionAsync</c>: this view renders
    /// instead of the rows rather than inside one, so whether the id is worth fetching does not
    /// depend on what the search came back with.
    /// </remarks>
    private Task OpenInitialInstrumentAsync() =>
        _instrumentId is { } id ? LoadInstrumentAsync(id) : Task.CompletedTask;

    /// <summary>Leave the instrument and put back whatever the reader was on before it.</summary>
    /// <remarks>
    /// Nothing else is touched: the search, the filters, the page and the open variable were never
    /// torn down, so leaving is a render rather than a fetch. The host is told, so the address stops
    /// naming a page the reader has left.
    /// </remarks>
    private async Task CloseInstrumentAsync()
    {
        // The id is what the view is drawn on, so clearing it is also what disowns a fetch still in
        // flight: its answer lands in fields nothing reads once the id is gone.
        _instrumentId = null;
        _instrument = null;
        _instrumentError = null;
        _instrumentLoading = false;

        await RaiseAsync<Guid?>(SelectedInstrumentIdChanged, null, Log);
    }

    /// <summary>Fetch one instrument into the open view.</summary>
    /// <remarks>
    /// No generation guard, unlike <see cref="LoadDetailAsync"/>: <see cref="SelectedInstrumentId"/>
    /// is read once, so this runs at most once and there is no second call for an abandoned first to
    /// report itself into. Null is "the catalogue does not publish this instrument", not a failure.
    /// </remarks>
    private async Task LoadInstrumentAsync(Guid id)
    {
        _instrument = null;
        _instrumentError = null;
        _instrumentLoading = true;
        StateHasChanged();

        try
        {
            var instrument = await Client.GetInstrumentAsync(id);

            _instrument = instrument;
            _instrumentError = instrument is null ? T.InstrumentMissing : null;
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused instrument {InstrumentId}", id);

            _instrumentError = T.RateLimitError;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not load instrument {InstrumentId}", id);

            _instrumentError = T.InstrumentError;
        }
        finally
        {
            _instrumentLoading = false;
        }
    }
}
