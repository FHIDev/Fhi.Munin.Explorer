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

    protected override void OnParametersSet() => ListState?.SetAuthenticated(IsAuthenticated);
}
