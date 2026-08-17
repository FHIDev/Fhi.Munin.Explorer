using Microsoft.AspNetCore.Components.Server.Circuits;

namespace LegacyHost.Authentication;

/// <summary>
/// Makes the current circuit's service provider reachable from code that is not itself
/// circuit-scoped — which is the only way a singleton can answer a question about "the
/// current user" in Blazor Server.
/// </summary>
/// <remarks>
/// <para>
/// This is the pattern documented for accessing server-side Blazor services from a different
/// DI scope, and it exists here because the obvious alternatives are wrong:
/// </para>
/// <para>
/// <b><c>IHttpContextAccessor</c> does not work.</b> Circuit activity arrives over a WebSocket,
/// not an HTTP request, so <c>HttpContext</c> is null for everything after the connection is
/// established. A token handler written against it does not throw — it quietly finds no token
/// and calls anonymously, which looks like "the API forgot who I am" rather than like a bug in
/// the host.
/// </para>
/// <para>
/// <b>Capturing the user in a field does not work either.</b> <c>IHttpClientFactory</c> builds
/// the message-handler pipeline in its own scope and reuses it across every caller for about
/// two minutes, so whatever a handler captures at construction is shared by everyone who calls
/// afterwards. That is how one person's token ends up on another person's request.
/// </para>
/// </remarks>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> Current = new();

    /// <summary>
    /// The services of the circuit whose work is currently executing, or <c>null</c> when the
    /// caller is not inside circuit activity at all — a background job, or a plain HTTP request.
    /// Callers must treat null as an ordinary answer.
    /// </summary>
    public IServiceProvider? Services
    {
        get => Current.Value;
        internal set => Current.Value = value;
    }
}

/// <summary>
/// Sets <see cref="CircuitServicesAccessor.Services"/> around each piece of inbound circuit
/// activity, and clears it again afterwards.
/// </summary>
/// <remarks>
/// <para>
/// What actually keeps one circuit's services from being visible to the next is the runtime, not
/// the <c>finally</c> below. An <c>async</c> method runs against a copy of the
/// <c>ExecutionContext</c>, so an <see cref="AsyncLocal{T}"/> written inside
/// <see cref="KjorMedKretsensTjenester"/> is already invisible to its caller once the call
/// returns — deleting the <c>finally</c> does not make a single test in this repository fail.
/// </para>
/// <para>
/// It is kept anyway, as the cheap half of a belt and braces. The guarantee above holds because
/// this method is <c>async</c>; a later edit that made it synchronous would lose the automatic
/// restore silently, and then the explicit clear is the only thing standing between one user's
/// token and the next caller. Isolation that depends on a method keeping its <c>async</c>
/// modifier is worth one line of insurance.
/// </para>
/// <para>
/// The isolation that <em>is</em> load-bearing here is <see cref="AsyncLocal{T}"/> itself rather
/// than a plain field: work forked from two circuits runs on genuinely independent contexts, and
/// that is the property the concurrency test in <c>CircuitTokenProviderTest</c> fails without.
/// </para>
/// </remarks>
public sealed class ServicesAccessorCircuitHandler(
    IServiceProvider services,
    CircuitServicesAccessor accessor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        context => KjorMedKretsensTjenester(() => next(context));

    /// <summary>
    /// Runs <paramref name="arbeid"/> with this circuit's services visible to
    /// <see cref="CircuitServicesAccessor"/>, and clears them again afterwards — including when
    /// the work throws.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CreateInboundActivityHandler"/> so the cleanup can be tested.
    /// <c>CircuitInboundActivityContext</c> is constructed by the framework and cannot be made
    /// in a test, and "the token does not outlive the request it belongs to" is too important
    /// to leave resting on a reading of the code.
    /// </remarks>
    public async Task KjorMedKretsensTjenester(Func<Task> arbeid)
    {
        accessor.Services = services;
        try
        {
            await arbeid();
        }
        finally
        {
            accessor.Services = null;
        }
    }
}
