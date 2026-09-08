using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer;

/// <summary>The host's logger for a component, or none when the host registered no logging.</summary>
/// <remarks>
/// <c>GetService</c> rather than <c>[Inject] ILogger&lt;T&gt;</c>, which throws at render when
/// nothing is registered and would turn a swallowed data error into a dead component. That is what
/// <c>AddMuninExplorer</c>'s <c>AddLogging</c> covers for an ordinary host, and this for the rest.
/// <para>
/// What comes back is wrapped. Every call site is the first statement of a <c>catch</c> whose whole
/// purpose is that nothing escapes it, and <c>Logger&lt;T&gt;.Log</c> does not swallow a provider's
/// failure — it rethrows it as an <see cref="AggregateException"/>. A host whose sink throws would
/// otherwise lose the page that the catch was written to save, and skip the sentence on screen too.
/// </para>
/// </remarks>
internal static class ExplorerLog
{
    internal static ILogger? For<TComponent>(IServiceProvider? services)
    {
        try
        {
            return Guard(services?.GetService<ILogger<TComponent>>());
        }
        catch (Exception)
        {
            // Resolving is host code as well: ILoggerFactory.CreateLogger runs here, lazily, inside
            // the catch. This file is the one place in src/ that may swallow, because an exception
            // thrown by logging has nowhere left to be written down — see the guard test.
            return null;
        }
    }

    /// <summary>An already-resolved logger, wrapped the same way — for a constructor-injected one.</summary>
    internal static ILogger? Guard(ILogger? logger) =>
        logger is null or Guarded ? logger : new Guarded(logger);

    private sealed class Guarded(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            try
            {
                return inner.BeginScope(state);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            try
            {
                return inner.IsEnabled(logLevel);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
            catch (Exception)
            {
                // The sink itself is what failed, so there is nowhere to report it to.
            }
        }
    }
}
