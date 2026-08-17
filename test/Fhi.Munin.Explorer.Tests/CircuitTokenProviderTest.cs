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
    private static (IServiceProvider Services, CircuitServicesAccessor Accessor) Krets(
        CircuitServicesAccessor accessor,
        string? token)
    {
        var tjenester = new ServiceCollection();
        tjenester.AddSingleton(accessor);
        tjenester.AddScoped(_ => new BrukerToken { AccessToken = token });

        var scope = tjenester.BuildServiceProvider().CreateScope();
        return (scope.ServiceProvider, accessor);
    }

    /// <summary>
    /// Runs a token fetch the way inbound circuit activity would — through the real handler, so
    /// the tests exercise the shipped code path rather than a re-implementation of it.
    /// </summary>
    private static async Task<string?> IKrets(
        CircuitServicesAccessor accessor,
        IServiceProvider kretsTjenester,
        CircuitTokenProvider provider)
    {
        var handler = new ServicesAccessorCircuitHandler(kretsTjenester, accessor);

        string? token = null;
        await handler.KjorMedKretsensTjenester(async () => token = await provider.HentTokenAsync());
        return token;
    }

    [Fact]
    public async Task HentTokenAsync_NårKalletSkjerIEnKrets_ThenGirKretsensToken()
    {
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);
        var (krets, _) = Krets(accessor, "brukerens-token");

        Assert.Equal("brukerens-token", await IKrets(accessor, krets, provider));
    }

    [Fact]
    public async Task HentTokenAsync_NårDetIkkeErNoenKrets_ThenGirNullIStedetForÅKaste()
    {
        // A background job, a health check, or a plain HTTP request. There is no user to speak
        // for, and that is an ordinary situation — the explorer is anonymous by default.
        var provider = new CircuitTokenProvider(new CircuitServicesAccessor());

        Assert.Null(await provider.HentTokenAsync());
    }

    [Fact]
    public async Task HentTokenAsync_NårBrukerenIkkeErInnlogget_ThenGirNull()
    {
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);
        var (krets, _) = Krets(accessor, null);

        Assert.Null(await IKrets(accessor, krets, provider));
    }

    [Fact]
    public async Task HentTokenAsync_NårToBrukereErPåleneSamtidig_ThenSerIngenDenAndresToken()
    {
        // The property the whole pattern exists for. One provider instance — it is a singleton,
        // because IHttpClientFactory reuses the handler pipeline across callers — answering for
        // two circuits at once.
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);

        var (kretsA, _) = Krets(accessor, "token-A");
        var (kretsB, _) = Krets(accessor, "token-B");

        var a = Task.Run(async () =>
        {
            var svar = new List<string?>();
            for (var i = 0; i < 50; i++)
            {
                svar.Add(await IKrets(accessor, kretsA, provider));
                await Task.Yield();
            }
            return svar;
        });

        var b = Task.Run(async () =>
        {
            var svar = new List<string?>();
            for (var i = 0; i < 50; i++)
            {
                svar.Add(await IKrets(accessor, kretsB, provider));
                await Task.Yield();
            }
            return svar;
        });

        Assert.All(await a, t => Assert.Equal("token-A", t));
        Assert.All(await b, t => Assert.Equal("token-B", t));
    }

    [Fact]
    public async Task HentTokenAsync_EtterAtKretsensArbeidErFerdig_ThenLekkerIkkeTokenetVidere()
    {
        // The reason the handler clears the accessor in a finally. Without that, work running
        // after the circuit's turn — on the same execution context — would still see the last
        // user's services, and the next caller would be handed their token.
        var accessor = new CircuitServicesAccessor();
        var provider = new CircuitTokenProvider(accessor);
        var (krets, _) = Krets(accessor, "brukerens-token");

        await IKrets(accessor, krets, provider);

        Assert.Null(await provider.HentTokenAsync());
    }

    [Fact]
    public async Task KjorMedKretsensTjenester_NårArbeidetKaster_ThenRyddesLikevelOpp()
    {
        // A circuit whose work throws must not leave its services visible to whatever runs
        // next. Without the finally, the exception path is exactly where a token outlives its
        // request — and the exception path is the one nobody exercises by hand.
        var accessor = new CircuitServicesAccessor();
        var tjenester = new ServiceCollection().BuildServiceProvider();
        var handler = new ServicesAccessorCircuitHandler(tjenester, accessor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.KjorMedKretsensTjenester(() => throw new InvalidOperationException("boom")));

        Assert.Null(accessor.Services);
    }
}
