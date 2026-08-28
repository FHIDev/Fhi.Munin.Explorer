using LegacyHost.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The reference pattern a Blazor Server host uses to call Munin as its signed-in user.
/// </summary>
/// <remarks>
/// Worth testing rather than just documenting: the failure this pattern exists to prevent is
/// one user's token being sent on another user's request, and neither that nor its absence is
/// visible by reading the code. The naive versions — <c>IHttpContextAccessor</c>, or a handler
/// that captures the user once — both compile, both work for a single user in development, and
/// both fail only once two people are online at the same time.
/// </remarks>
public class CircuitTokenProviderTest
{
    /// <summary>Builds a circuit-like scope holding one user's token.</summary>
    private static (IServiceProvider Services, CircuitServicesAccessor Accessor) Circuit(
        CircuitServicesAccessor accessor,
        string? token)
    {
        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        services.AddScoped(_ => new UserToken { AccessToken = token });

        var scope = services.BuildServiceProvider().CreateScope();
        return (scope.ServiceProvider, accessor);
    }

    /// <summary>
    /// Runs a token fetch the way inbound circuit activity would — through the real handler, so
    /// the tests exercise the shipped code path rather than a re-implementation of it.
    /// </summary>
    private static async Task<string?> InCircuit(
        CircuitServicesAccessor accessor,
        IServiceProvider circuitServices,
        CircuitTokenProvider provider)
    {
        var handler = new ServicesAccessorCircuitHandler(circuitServices, accessor);

        string? token = null;
        await handler.RunWithCircuitServicesAsync(async () => token = await provider.GetTokenAsync());
        return token;
    }

    [Fact]
    public async Task GetTokenAsync_WhenTheCallHappensInsideACircuit_ThenTheCircuitsTokenIsReturned()
    {
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);
        var (circuit, _) = Circuit(accessor, "the-users-token");

        Assert.Equal("the-users-token", await InCircuit(accessor, circuit, provider));
    }

    [Fact]
    public async Task GetTokenAsync_WhenThereIsNoCircuit_ThenNullIsReturnedRatherThanThrowing()
    {
        // A background job, a health check, or a plain HTTP request. There is no user to speak
        // for, and that is an ordinary situation — the explorer is anonymous by default.
        var provider = new CircuitTokenProvider(new CircuitServicesAccessor());

        Assert.Null(await provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_WhenTheUserIsNotSignedIn_ThenNullIsReturned()
    {
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);
        var (circuit, _) = Circuit(accessor, null);

        Assert.Null(await InCircuit(accessor, circuit, provider));
    }

    [Fact]
    public async Task GetTokenAsync_WhenTwoUsersAreSignedInAtOnce_ThenNeitherSeesTheOthersToken()
    {
        // The property the whole pattern exists for. One provider instance — it is a singleton,
        // because IHttpClientFactory reuses the handler pipeline across callers — answering for
        // two circuits at once.
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);

        var (circuitA, _) = Circuit(accessor, "token-A");
        var (circuitB, _) = Circuit(accessor, "token-B");

        var a = Task.Run(async () =>
        {
            var answers = new List<string?>();
            for (var i = 0; i < 50; i++)
            {
                answers.Add(await InCircuit(accessor, circuitA, provider));
                await Task.Yield();
            }
            return answers;
        });

        var b = Task.Run(async () =>
        {
            var answers = new List<string?>();
            for (var i = 0; i < 50; i++)
            {
                answers.Add(await InCircuit(accessor, circuitB, provider));
                await Task.Yield();
            }
            return answers;
        });

        Assert.All(await a, t => Assert.Equal("token-A", t));
        Assert.All(await b, t => Assert.Equal("token-B", t));
    }

    [Fact]
    public async Task GetTokenAsync_AfterTheCircuitsWorkIsDone_ThenTheTokenDoesNotLeakOnwards()
    {
        // Pins the behaviour, but be honest about what enforces it: the runtime, not this code.
        // RunWithCircuitServicesAsync is async, so it runs against a copy of the ExecutionContext
        // and its AsyncLocal write is already invisible here. Deleting the handler's finally
        // does not make this test fail — see the note in CircuitServicesAccessor.
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);
        var (circuit, _) = Circuit(accessor, "the-users-token");

        await InCircuit(accessor, circuit, provider);

        Assert.Null(await provider.GetTokenAsync());
    }

    [Fact]
    public async Task RunWithCircuitServicesAsync_WhenTheWorkThrows_ThenItStillCleansUp()
    {
        // Same caveat as the test above: the ExecutionContext copy would restore this even with
        // no finally at all, so this documents the exception path rather than guarding it. Kept
        // because a future edit could make the method synchronous, and then it would guard it.
        var accessor = new CircuitServicesAccessor();
        var services = new ServiceCollection().BuildServiceProvider();
        var handler = new ServicesAccessorCircuitHandler(services, accessor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.RunWithCircuitServicesAsync(() => throw new InvalidOperationException("boom")));

        Assert.Null(accessor.Services);
    }
}
