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
    /// hold, or one with no datasamlinger to open, is dropped without a word, and
    /// <see cref="ExpandedKildeIdsChanged"/> is raised without it. Each row opened this way is a
    /// fetch, so at most twenty open by themselves, the rows <see cref="MarkedDatasamlinger"/>
    /// opens counted among them. Once the rows have loaded the page is scrolled back to where it
    /// was when the reader left it, and to the first open row otherwise — which needs the package's
    /// browser module; without it the rows open and the page stays where the browser put it.
    /// <para>
    /// That position is the one thing the package writes to Web Storage: the page's vertical
    /// offset, per tab in <c>sessionStorage</c> under <c>Fhi.Munin.Explorer:scroll:</c> followed
    /// by the page's path and query, written when the reader follows a link out of the list or
    /// leaves the page and removed when it is read back. Nothing about the reader is in it.
    /// </para>
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

    /// <summary>Open every row the address marked or named open, and fetch what those rows draw.</summary>
    /// <remarks>
    /// One call per row, not per mark (the address can name several under one kilde), and none for a
    /// row in hand. <see cref="RowsToOpen"/> says which rows, and caps an untrusted query.
    /// </remarks>
    private async Task OpenSeededRowsAsync()
    {
        var rows = RowsToOpen();

        foreach (var kilde in rows)
        {
            _expanded.Add(kilde);

            if (!_datasamlinger.ContainsKey(kilde) && !_datasamlingerLoading.Contains(kilde))
            {
                await LoadDatasamlingerAsync(kilde);
            }
        }

        _expandedSeeded = true;
        _scrollPending &= rows.Any(_seededExpanded.Contains);

        // An id that opened nothing would otherwise be written back into every later link.
        if (!_seededExpanded.All(_expanded.Contains))
        {
            await RaiseExpandedAsync();
        }

        if (_marked.Count > 0 || _scrollPending)
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
        await ReadFilterFoldAsync();

        var restore = _scrollPending && _expandedSeeded && _datasamlingerLoading.Count == 0;
        var remember = !_scrollRemembered && _expanded.Count > 0;

        if (_disposed || (!restore && !remember) || (_interop is null && Services.GetService<IJSRuntime>() is null))
        {
            return;
        }

        // Both before the first await: renders overlap here, and each would otherwise ask again.
        _scrollPending &= !restore;
        _scrollRemembered |= remember;

        var interop = _interop ??= new ExplorerInterop(Services.GetRequiredService<IJSRuntime>());

        if (await interop.TryLoadAsync())
        {
            try
            {
                if (remember)
                {
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
        else
        {
            // Not there yet, perhaps: a later render asks again, as TryLoadAsync wants.
            _scrollRemembered &= !remember;
        }

        // Disposal can land during the awaits above, which the renderer does not wait for.
        if (_disposed)
        {
            await ReleaseAsync(interop);
        }
    }

    // The wide screen's kept fold, read once the module is up (Fhi.Metadata-amp8c).
    private async Task ReadFilterFoldAsync()
    {
        if (_disposed || Facets is not { Count: > 0 } || (_interop is null && Services.GetService<IJSRuntime>() is null))
        {
            return;
        }

        var interop = _interop ??= new ExplorerInterop(Services.GetRequiredService<IJSRuntime>());

        if (await interop.TryLoadAsync() && await _filterFold.ReadOnceAsync(interop))
        {
            StateHasChanged();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_interop is { } interop)
        {
            await ReleaseAsync(interop);
        }
    }

    // The module keeps a listener on the window, and through it this list's whole table.
    private async Task ReleaseAsync(ExplorerInterop interop)
    {
        try
        {
            if (_scrollRemembered)
            {
                await interop.ForgetScrollAsync(TitleId);
            }
        }
        catch (JSException ex)
        {
            Log?.LogWarning(ex, "could not let go of the kilde list's scroll position");
        }

        await interop.DisposeAsync();
    }
}
