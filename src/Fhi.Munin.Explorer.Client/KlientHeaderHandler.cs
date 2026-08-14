using System.Reflection;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Stamps every outgoing request with the header that identifies this client to Munin.
/// </summary>
/// <remarks>
/// Munin's Explorer API is public and anonymous, so a request carries nothing that says who made
/// it. The header is what lets Munin split its observability by consumer — "how much of the
/// Explorer traffic is the embedded component on helsedata.no, and which version of it" — and the
/// same signal is what load will be attributed by if the API is ever rate-limited per consumer.
/// <para>
/// It is a <see cref="DelegatingHandler"/> rather than a default request header on the
/// <c>HttpClient</c> so it applies to every request the pipeline makes, including retries a host
/// adds its own handler for, and so a host that constructs its own client cannot forget it.
/// </para>
/// </remarks>
internal sealed class KlientHeaderHandler : DelegatingHandler
{
    /// <summary>The header Munin's dashboards group by.</summary>
    internal const string Header = "X-Munin-Explorer-Client";

    /// <summary>
    /// Names the kind of consumer, not the package: several packages may ship out of this repo,
    /// but from Munin's side they are one Blazor component embedded in a host.
    /// </summary>
    private const string Klient = "blazor";

    private static readonly string Verdi = $"{Klient}/{LesVersjon()}";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A retried request already carries the header; adding it twice would send two values.
        if (!request.Headers.Contains(Header))
        {
            // Without validation: the value is sanitised below, and a malformed version string
            // must not be the reason a page fails to load its data.
            request.Headers.TryAddWithoutValidation(Header, Verdi);
        }

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>Used when the assembly carries no usable version at all.</summary>
    private const string Ukjent = "ukjent";

    /// <summary>
    /// Reads the assembly's informational version — today that is <c>VersionPrefix</c> from
    /// <c>Directory.Build.props</c>, and at release time whatever the pipeline passes as
    /// <c>-p:Version=</c>.
    /// </summary>
    /// <remarks>
    /// Nothing in this repository stamps a commit sha into the version yet, so every build between
    /// two releases reports the same value. Munin can therefore tell releases apart but not
    /// individual builds; enabling SourceLink or setting <c>SourceRevisionId</c> would change that,
    /// and <see cref="Versjon"/> is written to cope with it when it does.
    /// </remarks>
    private static string LesVersjon()
    {
        var assembly = typeof(KlientHeaderHandler).Assembly;

        return Versjon(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? assembly.GetName().Version?.ToString());
    }

    /// <summary>Reduces a raw assembly version to the part that belongs in the header.</summary>
    /// <remarks>
    /// Anything after a <c>+</c> is semver build metadata — a commit sha, once a build stamps one.
    /// Keeping it would hand Munin a new label value per commit, the cardinality explosion that
    /// makes a dashboard useless, so it is dropped. What remains is reduced to characters that are
    /// safe in a header token, because an odd version string must never be the reason a page fails
    /// to load its data.
    /// </remarks>
    internal static string Versjon(string? raaVersjon)
    {
        if (string.IsNullOrWhiteSpace(raaVersjon))
        {
            return Ukjent;
        }

        var pluss = raaVersjon.IndexOf('+');
        var utenByggemetadata = pluss >= 0 ? raaVersjon[..pluss] : raaVersjon;

        var rene = utenByggemetadata
            .Where(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
            .ToArray();

        return rene.Length == 0 ? Ukjent : new string(rene);
    }
}
