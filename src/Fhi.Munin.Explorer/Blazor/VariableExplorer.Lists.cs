using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The bridge between the host's <see cref="IsAuthenticated"/> parameter and the circuit's shared
/// <see cref="VariableListState"/>, so every surface that touches saved lists agrees on who the
/// reader is.
/// </summary>
public partial class VariableExplorer
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    private VariableListState? _listState;

    /// <summary>
    /// Resolved through <c>GetService</c> rather than <c>[Inject]</c>, so a host that renders this
    /// component without calling <c>AddMuninExplorer</c> still gets an explorer — the same
    /// tolerance the package already extends to hosts with no localisation services registered.
    /// </summary>
    private VariableListState? ListState =>
        _listState ??= ServiceProvider.GetService<VariableListState>();

    protected override async Task OnParametersSetAsync()
    {
        // Before the early return below, because the account-link panel is the component's own
        // state rather than the shared list state's, and it has to be dropped either way.
        ResetAccountLinkIfTheReaderChanged();

        if (ListState is null)
        {
            return;
        }

        ListState.SetAuthenticated(IsAuthenticated);

        // Read what is already in the reader's list before the rows draw their buttons. Without it
        // the set is empty on the first render, so a variable saved earlier offers "save" and the
        // press removes it — the label and the action disagreeing about the same variable.
        try
        {
            await ListState.EnsureActiveListAsync();
        }
        catch (Exception)
        {
            // Caught, and nothing said. An exception out of a lifecycle method takes the circuit
            // down with it, which in helsedata's legacy Blazor Server host means the whole CMS page
            // — see the RaiseAsync remarks in VariableExplorer.Querying.cs. The mount fires this
            // read alongside the search and the facet refresh, which is exactly the burst the
            // per-address limiter counts, so a 429 here is an ordinary event rather than a rare one.
            //
            // There is nothing to render: the buttons are drawn from the set, and a set that stayed
            // empty draws every row as unsaved. That is wrong, and the cost is a stale label and
            // nothing more — VariableListState.ToggleSavedAsync decides the direction of a press
            // from the set as it was drawn, so a row that says "save" saves whatever the stored
            // list turns out to hold. The press is also where the refused read is tried again, so
            // it puts the other rows' labels right as it goes. Not here, though: this method runs
            // on every parameter set, so reading again here would send a membership read alongside
            // every search and page turn while the address is already over the limit.
            //
            // Reported nowhere, because a page-wide alert about a list nobody has touched yet says
            // nothing the reader can act on.
        }
    }
}
