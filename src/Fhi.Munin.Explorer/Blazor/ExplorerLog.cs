using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The host's logger for a component, or none when the host registered no logging.</summary>
/// <remarks>
/// <c>GetService</c> rather than <c>[Inject] ILogger&lt;T&gt;</c>, which throws at render when
/// nothing is registered and would turn a swallowed data error into a dead component. That is what
/// <c>AddMuninExplorer</c>'s <c>AddLogging</c> covers for an ordinary host, and this for the rest.
/// </remarks>
internal static class ExplorerLog
{
    internal static ILogger? For<TComponent>(IServiceProvider? services) =>
        services?.GetService<ILogger<TComponent>>();
}
