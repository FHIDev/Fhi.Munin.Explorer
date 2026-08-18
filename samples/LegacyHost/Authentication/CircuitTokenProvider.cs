using Fhi.Munin.Explorer.Contracts;

namespace LegacyHost.Authentication;

/// <summary>
/// Holds the access token for one signed-in user, for the lifetime of one circuit.
/// </summary>
/// <remarks>
/// Registered scoped, so in Blazor Server there is exactly one of these per circuit — which is
/// to say, per user session. A real host fills this in when the circuit starts, from whatever it
/// already uses to sign people in; on helsedata.no that is the ID-porten access token their
/// OIDC handler saved with <c>SaveTokens</c>.
/// </remarks>
public sealed class UserToken
{
    public string? AccessToken { get; set; }
}

/// <summary>
/// Supplies the current circuit's access token to the Munin explorer client.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton, because the client's handler pipeline is built once and reused —
/// so this type must hold no user state of its own. Everything about "who is calling" is read
/// per call, through the circuit's own services, and nothing is cached between calls.
/// </para>
/// <para>
/// Returning <c>null</c> is a normal answer, not a failure. Calls made outside any circuit have
/// no user to speak for, and a signed-out visitor browsing public metadata is the common case —
/// the explorer is anonymous by default and stays useful that way.
/// </para>
/// </remarks>
public sealed class CircuitTokenProvider(CircuitServicesAccessor circuitServices)
    : IMuninExplorerTokenProvider
{
    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        // Resolved per call, never held. The circuit this runs for is decided by the caller's
        // execution context, so caching anything here would answer a later call with an earlier
        // user's token.
        var token = circuitServices.Services?.GetService<UserToken>()?.AccessToken;

        return Task.FromResult(string.IsNullOrWhiteSpace(token) ? null : token);
    }
}
