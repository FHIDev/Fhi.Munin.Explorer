using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Which rows are open, as the host's address carries it, and where on the page the reader was when
/// they left it — so Back from the variable explorer lands on the row they followed the link from.
/// (Fhi.Metadata-95j8b)
/// </summary>
public sealed partial class KildeSearch : IAsyncDisposable
{
    /// <summary>
    /// The kilder whose datasamlinger are open when the list opens. Set by the host, typically from
    /// its own URL; the component owns which rows are open afterwards.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, as <see cref="TickedKildeIds"/> is. An id the list does not
    /// hold, or one with no datasamlinger to open, is dropped without a word. Each row opened this
    /// way is a fetch, so at most twenty open by themselves. Once the rows have loaded the page is
    /// scrolled back to where it was when the reader left it, where the browser kept that, and to
    /// the first open row otherwise — which needs the package's browser module; without it the rows
    /// open and the page stays where the browser put it.
    /// </remarks>
    [Parameter] public IReadOnlyList<Guid>? ExpandedKildeIds { get; set; }

    /// <summary>
    /// Raised whenever the reader opens or closes a row, with every open row's kilde id in the
    /// list's own catalogue order. Gives a host <c>@bind-ExpandedKildeIds</c>.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyList<Guid>> ExpandedKildeIdsChanged { get; set; }

    private IReadOnlyList<Guid> _seededExpanded = [];

    // Set once the seeded rows have been asked for, so the scroll is put back over the rows'
    // contents rather than over a table still waiting for them.
    private bool _expandedSeeded;
    private bool _scrollPending;
    private bool _scrollRemembered;
    private bool _disposed;
    private ExplorerInterop? _interop;

    private void SeedExpanded()
    {
        _seededExpanded = [.. (ExpandedKildeIds ?? []).Distinct()];
        _scrollPending = _seededExpanded.Count > 0;
    }

    /// <summary>Open the rows the host named, and fetch what they draw.</summary>
    private async Task OpenExpandedRowsAsync()
    {
        HashSet<Guid> named = [.. _seededExpanded];

        var rows = _kilder.Where(kilde => named.Contains(kilde.Id) && CanExpand(kilde))
                          .Select(kilde => kilde.Id)
                          .Take(MaxMarkedRowsOpened)
                          .ToList();

        foreach (var kilde in rows)
        {
            _expanded.Add(kilde);

            if (!_datasamlinger.ContainsKey(kilde) && !_datasamlingerLoading.Contains(kilde))
            {
                await LoadDatasamlingerAsync(kilde);
            }
        }

        _expandedSeeded = true;
        _scrollPending &= rows.Count > 0;

        if (_scrollPending)
        {
            StateHasChanged();
        }
    }

    // In the list's order rather than the set's, so two readers opening the same rows share a link.
    private Task RaiseExpandedAsync() =>
        RaiseAsync(ExpandedKildeIdsChanged,
                   (IReadOnlyList<Guid>)[.. _kilder.Select(kilde => kilde.Id).Where(_expanded.Contains)],
                   Log);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var restore = _scrollPending && _expandedSeeded && _datasamlingerLoading.Count == 0;
        var remember = !_scrollRemembered && _expanded.Count > 0;

        if (_disposed || (!restore && !remember) || (_interop is null && Services.GetService<IJSRuntime>() is null))
        {
            return;
        }

        if (restore)
        {
            _scrollPending = false;
        }

        var interop = _interop ??= new ExplorerInterop(Services.GetRequiredService<IJSRuntime>());

        if (await interop.TryLoadAsync())
        {
            try
            {
                if (remember)
                {
                    _scrollRemembered = true;
                    await interop.RememberScrollAsync(TitleId);
                }

                // Only over the list: a kilde opened from the address has taken the list's place.
                if (restore && _selectedId is null)
                {
                    var first = Visible.FirstOrDefault(kilde => IsExpanded(kilde.Id));
                    await interop.RestoreScrollAsync(first is null ? null : PanelId(first.Id));
                }
            }
            // A fault inside the module costs the scroll position and nothing the reader can read.
            catch (JSException ex)
            {
                Log?.LogWarning(ex, "could not keep the kilde list's scroll position");
            }
        }

        // Disposal can land during the awaits above, which the renderer does not wait for.
        if (_disposed)
        {
            await interop.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;

        return _interop?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
