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
            // empty draws every row as unsaved. That is wrong, but it is repaired rather than
            // reported — VariableListState.EnsureActiveListAsync reads membership again on the next
            // ask, so the reader's first press puts the labels right. The alternative, a page-wide
            // alert about a list nobody has touched yet, says nothing they can act on.
        }
    }
}
